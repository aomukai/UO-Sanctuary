// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record DecoyStats(int Rank,int Life256,int Level,int Defense,int Resistance,int Regeneration256,int DurationFrames);
public static class AmazonDecoys
{
    // Keep the component entry point while sharing the literal source operation
    // with weapon missile conversion. Current executable order remains acceptance.
    internal static int Percentage(int value,int percent,int divisor)
        =>SourcePercentage.Calculate(value,percent,divisor);
    internal static int Life256(int maximum256,int percent,int bonus)
    {int initial=Percentage(maximum256,percent,100);return initial+Percentage(initial,bonus,100);}
    internal static int Level(int casterLevel,int rank)=>Math.Clamp(rank+3*casterLevel/4,1,casterLevel);
    internal static int Regeneration256(int maximum256)=>(NecromancerSummons.Source("dopplezon","DamageRegen")*(maximum256>>8))>>4;
    internal static DecoyStats Values(PlayerMobile owner,SkillValues values)
    {
        int level=Level(ClassStats.Level(owner),values.Rank);
        int life=Life256((int)ClassResources.Parts(owner,0).Maximum256,(int)values.Eval("calc3"),(int)values.Eval("calc1"));
        return new(values.Rank,life,level,
            NecromancerSummons.Source("dopplezon","AC")+NecromancerSummons.Source("level-"+level,"L-AC"),
            (int)values.Eval("aurastatcalc1"),
            Regeneration256(life),
            (int)values.Eval("calc2"));
    }
    // The old spawn-search implementation is a stub. Supported casts require
    // a free cell; no unverified fallback radius or ordering is invented.
    internal static bool Destination(PlayerMobile owner,Serial selected,Point3D point,out Point3D aim)
    {
        aim=point;
        if(selected!=Serial.Zero)
        {
            var target=World.FindEntity(selected);
            if(target==null || target.Deleted || target.Map!=owner.Map){return false;}
            aim=target.Location;
        }
        return owner.Map!=null && owner.Map!=Map.Internal && aim.X>=0 && aim.Y>=0 && aim.X<owner.Map.Width && aim.Y<owner.Map.Height
            && owner.InRange(aim,CombatProtocol.TeethRange) && Math.Abs(aim.Z-owner.Z)<16 && owner.InLOS(aim)
            && SpellHelper.CheckTown(owner.Location,owner) && SpellHelper.CheckTown(aim,owner) && owner.Map.CanSpawnMobile(aim);
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
        =>SkillCombat.Begin(owner,CombatAbility.Decoy,selected,point,now,(p,ability,id,where)=>Destination(p,id,where,out _));
    public static bool Release(PlayerMobile owner)
    {
        var action=ClassCombat.For(owner);
        if(!AmazonCombat.Available(owner,CombatAbility.Decoy) || !Destination(owner,action.PendingTarget,action.PendingPoint,out var point)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Decoy);
        // Supported SC casts retain the shared native recheck when mana is lost
        // during windup. The older ignored-consume edge is not a modern oracle.
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        var pet=Create(owner,point,values);if(pet==null){return false;}
        bool paid=NecromancerEffects.Pay(owner,values.Mana256);
        if(!paid){pet.Delete();return false;}
        owner.PlaySound(0x1E5); // Identified placeholder for amazon_valkyrie_cast.
        return true;
    }
    public static void Configure()
    {
        EventSink.Disconnected+=Cancel;
        EventSink.Logout+=Cancel;
    }
    public static void Cancel(Mobile owner)
    {if(owner!=null){foreach(var pet in For(owner)){pet.Delete();}}}
    private static readonly HashSet<AmazonDecoy> _active=new();
    internal static void Track(AmazonDecoy pet)=>_active.Add(pet);
    internal static void Forget(AmazonDecoy pet)=>_active.Remove(pet);
    internal static AmazonDecoy[] For(Mobile owner)
    {
        var result=new List<AmazonDecoy>();foreach(var pet in _active)
        {if(!pet.Deleted && pet.SummonMaster==owner){result.Add(pet);}}return result.ToArray();
    }
    // Allocation itself never debits mana; the successful no-start do-event
    // pays afterward through Release.
    internal static AmazonDecoy Create(PlayerMobile owner,Point3D point,SkillValues values)
    {
        if(owner is not{Alive:true,Deleted:false} || !ClassCombat.Enabled(owner) || owner.Map==null || owner.Map==Map.Internal
            || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Amazon
            || !owner.Map.CanSpawnMobile(point) || values.Rank is <1 or >255){return null;}
        var stats=Values(owner,values);
        // Existing native hit maxima are whole and bounded65000. Do not silently
        // clamp a source maximum outside that representable range.
        if(stats.Life256<256 || stats.Life256>65000*256){return null;}
        var pet=new AmazonDecoy();pet.Prepare(owner,stats);pet.MoveToWorld(point,owner.Map);
        foreach(var previous in For(owner)){if(previous!=pet){previous.Delete();}}
        Track(pet);pet.Start();pet.FixedEffect(0x376A,10,16,0x481,0);return pet;
    }
}

[SerializationGenerator(0)]
public partial class AmazonDecoy:BaseCreature
{
    [SerializableField(0,setter:"private")] private string _stats="{}";
    private DecoyStats _snapshot;
    [SerializableField(1,setter:"private")] private DateTime _expiresUtc;
    private TimerExecutionToken _timer;
    private long _lastFrame;
    public DecoyStats Snapshot=>_snapshot??=JsonSerializer.Deserialize<DecoyStats>(_stats);
    internal int Maximum256=>Snapshot==null?base.HitsMax*256:(int)Math.Clamp((long)Snapshot.Life256+ClassResources.Percentage(Snapshot.Life256,BattleOrders.LifePercent(this)),256,65000L*256);
    public override int HitsMax=>World.Loading?65000:(Maximum256+255)/256;
    public override bool Commandable=>false;
    public override bool IsBondable=>false;
    public override bool IsDispellable=>false;
    public override bool CanRegenHits=>false;
    public override bool CanRegenMana=>false;
    public override bool DeleteCorpseOnDeath=>true;
    public override bool IsEnemy(Mobile other)=>false;
    public override bool CanBeHarmful(Mobile other,bool message,bool ignoreOurBlessedness)=>false;
    [Constructible] public AmazonDecoy():base(AIType.AI_Melee,FightMode.None,0)
    {
        Name="Decoy [placeholder]";Body=0x191;Hue=0x481;CantWalk=true;Summoned=true;ControlSlots=0;NoKillAwards=true;
        SetStr(1);SetDex(0);SetInt(0);SetMana(0);SetStam(0);SetDamage(0,0);AIObject?.AITimer.Stop();
    }
    internal void Prepare(PlayerMobile owner,DecoyStats stats)
    {
        _snapshot=stats;_stats=JsonSerializer.Serialize(stats);SummonMaster=owner;_expiresUtc=TimedEffects.Now.AddMilliseconds(stats.DurationFrames*40L);SummonEnd=_expiresUtc;
        Body=owner.Female?0x191:0x190; // Identified native silhouette; final Decoy appearance pending.
        SetHits((stats.Life256+255)/256);SkillHealth.SetBalance256(this,stats.Life256);
        SetResistance(ResistanceType.Physical,0);SetResistance(ResistanceType.Fire,stats.Resistance);
        SetResistance(ResistanceType.Energy,stats.Resistance);SetResistance(ResistanceType.Cold,stats.Resistance);SetResistance(ResistanceType.Poison,stats.Resistance);
    }
    internal void Pause()=>_timer.Cancel();
    internal void HoldUntil(DateTime until){_expiresUtc=until;SummonEnd=until;}
    internal void Start()
    {_timer.Cancel();_lastFrame=Core.TickCount;Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Tick,out _timer);}
    private void Tick()=>Advance(TimedEffects.Now,Core.TickCount);
    internal void Advance(DateTime at,long now)
    {
        if(Deleted){_timer.Cancel();return;}
        if(!Alive || _snapshot==null || at>=_expiresUtc || SummonMaster is not PlayerMobile{Alive:true,Deleted:false} owner
            || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Amazon
            || SkillMechanics.Rank(owner,CombatAbility.Decoy)<=0 || owner.Map!=Map.Internal && owner.Map!=Map)
        {Delete();return;}
        // A temporary decoy retains its fixed location and native timed-summon
        // lifecycle. It is not a follower/commandable persistent pet.
        AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;
        long frames=Math.Max(0,now-_lastFrame)/40;_lastFrame+=frames*40;
        if(frames>0 && TimedEffects.CanRegenerateHits(this))
        {SkillHealth.Heal(this,(int)Math.Min(int.MaxValue,frames*AmazonDecoys.Regeneration256(Maximum256)));}
    }
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    public override void GenerateLoot(bool spawning){}
    public override bool OnBeforeDeath(){Delete();return false;}
    public override void OnDelete(){_timer.Cancel();AmazonDecoys.Forget(this);base.OnDelete();}
    [AfterDeserialization] private void AfterLoad()
    {
        AIObject?.AITimer.Stop();_snapshot=JsonSerializer.Deserialize<DecoyStats>(_stats);
        if(_snapshot is not{Rank:>=1 and <=255,Life256:>=256 and <=16640000,Level:>=1 and <=99,Resistance:>=0 and <=85,DurationFrames:>0})
        {Timer.StartTimer(TimeSpan.Zero,Delete);return;}
        AmazonDecoys.Track(this);Start();
    }
}
