// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record OakBodyState(uint Owner,OakBirth Birth,MonsterDifficulty Difficulty,DateTime NextRecoveryUtc)
{
    public string Profile{get;init;}="source-birth-no-movement-v1";
    public OakWorldState Controller{get;init;}
}

// Saved vulnerable spirit and explicit source motion/recovery/aura consumers.
// The base keeps its Oak serialization identity; mapped spirit subclasses
// reuse the same saved body and motion without native skeleton combat.
[SerializationGenerator(0)]
public partial class DruidOakBody:SkeletonGuardian,ISourceLifeMaximum
{
    [SerializableField(0,setter:"private")]private string _saved="{}";
    private OakBodyState _prepared;
    internal bool Loaded{get;set;}
    internal OakBodyState Prepared
    {
        get
        {
            var s=_prepared??(_saved=="{}"?null:_prepared=JsonSerializer.Deserialize<OakBodyState>(_saved));
            return s?.Controller is{} c?s with{Controller=DruidOakWorld.Copy(c)}:s;
        }
    }
    internal string SavedText=>_saved;
    internal virtual CombatAbility SpiritAbility=>CombatAbility.OakSage;
    internal virtual void ScheduleAura(DateTime at)=>DruidOakAuras.Schedule(this,at);
    internal virtual void AdvanceAura(DateTime at)=>DruidOakAuras.Advance(this,at);
    internal virtual void StopAura()=>DruidOakAuras.Stop(this);
    public override DiabloClass SummonClass=>DiabloClass.Druid;
    public override bool Permitted(PlayerMobile owner)=>ClassCreation.Profiles.Histories(owner.Serial)
        .Any(x=>x.Profile.Class==DiabloClass.Druid && x.Ranks.GetValueOrDefault("druid."+SourceMonsterId)>0);
    public override bool CanRegenStam=>false;
    public override bool UsesNativeSummonTimer=>false;
    public override bool BleedImmune=>false;
    public override Poison PoisonImmune=>null;
    public override bool IsDispellable=>false;
    internal override string SourceMonsterId=>DruidSpiritStats.Key(SpiritAbility);
    internal override int SourceRegeneration256=>Prepared?.Birth.Regeneration256??DruidSpiritStats.Spawn(SpiritAbility,SummonDifficulty).Regeneration256;
    public override long SourceLife256=>Prepared?.Birth.Life256??DruidSpiritStats.Spawn(SpiritAbility,SummonDifficulty).Stats.Life256;
    public int MaximumLife256
    {
        get
        {
            long basis=SourceLife256+((long)HitsMaxSeed-CombatProfile.Life)*256;
            return (int)Math.Clamp(basis+ClassResources.Percentage(basis,BattleOrders.LifePercent(this)),256,65000*256);
        }
    }
    public override MonsterProfile CombatProfile=>Prepared is{} s
        ?new(s.Birth.Level,(s.Birth.Life256+255)/256,s.Birth.Attack,s.Birth.Defense,0,0,1000,400,0,0,0,0,0)
        :new(1,(int)(SourceLife256/256),0,DruidSpiritStats.Spawn(SpiritAbility,SummonDifficulty).Stats.Defense,0,0,1000,400,0,0,0,0,0);
    public override (int Min,int Max) SkillWeaponDamage=>(0,0);
    public DruidOakBody()
    {
        Name="Oak Sage [placeholder]";Body=0x3A;BaseSoundID=-1;CantWalk=true;ControlSlots=0;
        AIObject?.AITimer.Stop();SetHits(60);SetDamage(0,0);
    }
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromSeconds(1);
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    internal static void Validate(OakBodyState state,CombatAbility ability=CombatAbility.OakSage)
    {
        try
        {
            if(state?.Birth is not{Rank:>=1 and <=255,Level:>=1 and <=99}
                || !((Serial)state.Owner).IsMobile || state.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
                || state.NextRecoveryUtc.Kind!=DateTimeKind.Utc || state.NextRecoveryUtc>DateTime.MaxValue.AddMilliseconds(-40)
                || state.Profile is not("source-birth-no-movement-v1" or "source-birth-and-totem-motion-v2")
                || (state.Controller==null)!=(state.Profile=="source-birth-no-movement-v1")
                || DruidSpiritStats.Rescale(state.Birth,state.Difficulty,ability)!=state.Birth)
            {throw new InvalidDataException("Invalid prepared Oak Sage state");}
            if(state.Controller!=null)
            {DruidOakWorld.Validate(state.Controller);if(state.Controller.NextUtc!=state.NextRecoveryUtc){throw new InvalidDataException("Oak clocks disagree");}}
        }
        catch(Exception e) when(e is ArgumentException or NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unsupported Oak Sage birth snapshot",e);}
    }
    internal void RestorePrepared(OakBodyState state)
    {Validate(state,SpiritAbility);var copy=state.Controller is{} c?state with{Controller=DruidOakWorld.Copy(c)}:state;string json=JsonSerializer.Serialize(copy);_prepared=copy;_saved=json;}
    internal static DruidOakBody CreatePrepared(PlayerMobile owner,Point3D point,DateTime at,CombatAbility ability=CombatAbility.OakSage)
    {
        if(at.Kind!=DateTimeKind.Utc || at>DateTime.MaxValue.AddMilliseconds(-40) || owner?.Map==null || owner.Map==Map.Internal
            || !owner.Map.CanSpawnMobile(point))
        {throw new ArgumentException("Oak Sage needs a legal prepared ground point and UTC clock");}
        var birth=DruidSpiritStats.Capture(owner,ability);
        var state=new OakBodyState(owner.Serial.Value,birth,SummonDifficulties.For(owner.Map),at.AddMilliseconds(40));
        Validate(state,ability);DruidOakBody body=ability switch{CombatAbility.OakSage=>new DruidOakBody(),CombatAbility.HeartOfWolverine=>new DruidWolverineBody(),CombatAbility.SpiritOfBarbs=>new DruidBarbsBody(),_=>throw new NotSupportedException("Unmapped spirit body")};
        try
        {
            body.RestorePrepared(state);body.Initialize(owner,birth.Rank,0);body.SetResistances();
            if(!body.SetControlMaster(owner)){throw new InvalidOperationException("Oak Sage control master refused");}
            BattleCommand.Track(body);PersistentMinions.Profiles.Track(body);body.MoveToWorld(point,owner.Map);
            body.AIObject?.AITimer.Stop();SkillHealth.SetBalance256(body,body.MaximumLife256);DruidOaks.Track(body);body.ScheduleAura(at);return body;
        }
        catch{body.Delete();throw;}
    }
    private void SetResistances()
    {
        int F(string key)=>NecromancerSummons.Source(SourceMonsterId,key,SummonDifficulty);
        SetResistance(ResistanceType.Physical,F("ResDm"));SetResistance(ResistanceType.Fire,F("ResFi"));
        SetResistance(ResistanceType.Cold,F("ResCo"));SetResistance(ResistanceType.Energy,F("ResLi"));
        // Preserve source immunity totals rather than substituting skeleton's
        // PoisonImmune. maxpoisonresist is retained in the birth snapshot;
        // the existing NPC mitigation route treats resistance>=100as immune.
        SetResistance(ResistanceType.Poison,checked(F("ResPo")+Prepared.Birth.PoisonResistance));
    }
    internal override void RefreshDifficulty()
    {
        if(Prepared is{} state){RestorePrepared(state with{Difficulty=SummonDifficulty,Birth=DruidSpiritStats.Rescale(state.Birth,SummonDifficulty,SpiritAbility)});}
        base.RefreshDifficulty();SetResistances();
    }
    internal void Advance(DateTime at,bool actions)
    {
        if(Prepared?.Controller!=null){DruidOakWorld.Advance(this,at,actions);return;}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Oak clock must be UTC");}
        if(Deleted || Prepared is not{} state || state.NextRecoveryUtc>at){return;}
        if(ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner || owner.Serial.Value!=state.Owner){Delete();return;}
        long frames=(at-state.NextRecoveryUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;
        var next=state.NextRecoveryUtc.AddTicks(checked(frames*TimeSpan.FromMilliseconds(40).Ticks));
        if(!PersistentMinions.Active(owner,this) || !Permitted(owner))
        {PersistentMinions.ParkClass(owner,DiabloClass.Druid);RestorePrepared(Prepared with{NextRecoveryUtc=next});return;}
        if(!actions || owner.Map==null || owner.Map==Map.Internal)
        {PersistentMinions.Suspend(owner);RestorePrepared(Prepared with{NextRecoveryUtc=next});return;}
        if(Map==Map.Internal){PersistentMinions.Resume(owner);}
        if(Map!=Map.Internal && Alive)
        {
            if(!Poisoned && TimedEffects.CanRegenerateHits(this) && !SkillHealth.Full(this))
            {SkillHealth.Heal(this,(int)Math.Min(int.MaxValue,frames*BattleOrders.Regeneration256(this)));}
            AdvanceAura(at);
        }
        RestorePrepared(Prepared with{NextRecoveryUtc=next});AIObject?.AITimer.Stop();
    }
    [AfterDeserialization]private void AfterOakLoad()
    {AIObject?.AITimer.Stop();_prepared=JsonSerializer.Deserialize<OakBodyState>(_saved);Validate(_prepared,SpiritAbility);RestorePrepared(_prepared);Loaded=true;DruidOaks.Track(this);}
    public override void SuspendActions()
    {
        base.SuspendActions();StopAura();
        if(Prepared is{Controller:{} c} s){RestorePrepared(s with{Controller=DruidOakWorld.Park(c)});}
    }
    public override void OnAfterDelete(){DruidOaks.Forget(this);StopAura();base.OnAfterDelete();}
}

public static class DruidOaks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidOaks));
    private static readonly System.Collections.Generic.HashSet<DruidOakBody> Bodies=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static void Track(DruidOakBody body)=>Bodies.Add(body);
    internal static void Forget(DruidOakBody body)=>Bodies.Remove(body);
    internal static void Tick(DateTime at,bool? actions=null)
    {
        foreach(var body in Bodies.ToArray())
        {
            if(body.Deleted){Bodies.Remove(body);continue;}
            bool loaded=body.Loaded;body.Loaded=false;
            try{body.Advance(at,!loaded&&(actions??(body.ControlMaster?.NetState is{Running:true})));}
            catch(Exception e){DruidOakWorld.Fault(body,e.Message);Log.Error(e,"Spirit controller refused unsupported context for {Name}/{Serial}",body.Name,body.Serial);}
        }
        DruidOakAuras.Expire(at);DruidWolverineAuras.Expire(at);
    }
}
