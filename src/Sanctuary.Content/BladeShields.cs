// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record BladeShieldState(uint Owner,long Id,int Rank,DateTime StartedUtc,DateTime UntilUtc,DateTime? NextPulseUtc=null);
public sealed record BladeShieldSave(BladeShieldState[] States,long NextId);
public sealed class BladeShieldProfiles:GenericPersistence
{
    private BladeShieldSave _snapshot;
    public BladeShieldProfiles():base("SanctuaryBladeShield",157){}
    public override void Serialize(){_snapshot=BladeShields.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Blade Shield version");}BladeShields.Restore(JsonSerializer.Deserialize<BladeShieldSave>(reader.ReadString()));}
    public override void PostDeserialize()=>BladeShields.Prune(TimedEffects.Now);
}

public static class BladeShields
{
    private sealed record Cast(long Id,long EffectAt,Map Map,Point3D Origin,Serial Weapon);
    private static readonly Dictionary<uint,BladeShieldState> States=new();
    private static readonly Dictionary<uint,Cast> Casts=new();
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeShields));
    private static long _nextId;
    internal static Func<int,int> SkillRoll=Utility.Random;
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<int,int> WeaponRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    internal static Random WearRoll=Random.Shared;
    public static void Configure()
    {
        _=new BladeShieldProfiles();
        EventSink.Disconnected+=Cancel;EventSink.Logout+=Cancel;
        EventSink.Connected+=owner=>{if(owner is PlayerMobile player){Resume(player,TimedEffects.Now);}};
    }
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static bool Eligible(Mobile owner)=>owner is PlayerMobile{Alive:true,Deleted:false}
        && ClassCreation.Profiles.TryGet(owner.Serial,out var profile) && profile.Class==DiabloClass.Assassin;
    internal static BladeShieldState State(Mobile owner)=>Eligible(owner) && States.TryGetValue(owner.Serial.Value,out var state)
        && state.UntilUtc>TimedEffects.Now?state:null;
    internal static ActionTiming Timing(int fcr)
    {
        // Supplementary AISC composites:17 frames/speed256/event1 at9.
        // SC ignores UseAttackRate. Reuse the established ordinary cast repeat
        // minus-one adapter; current executable frame acceptance is separate.
        int speed=CombatTiming.FrameAdvance(new(AttackFamily.Unarmed,0),fcr,0,0);
        int end=Math.Max(1,(17*256+speed-1)/speed-1);
        return new(end,Math.Min(end,(9*256+speed-1)/speed));
    }
    internal static BladeShieldState Start(PlayerMobile owner,SkillValues values,DateTime at)
    {
        if(!Eligible(owner) || values.Ability!=CombatAbility.BladeShield || values.Rank is <1 or >255
            || at.Kind!=DateTimeKind.Utc || !NecromancerEffects.CanPay(owner,values.Mana256))
        {throw new InvalidOperationException("Invalid Blade Shield start context");}
        var state=new BladeShieldState(owner.Serial.Value,checked(++_nextId),values.Rank,at,
            at.AddMilliseconds(checked(values.Eval("auralencalc")*40)));
        // Complete start28 replaces its own state regardless of the old rank.
        // Generic start consumes mana afterwards and deletes old periodic work.
        States[owner.Serial.Value]=state;
        if(!NecromancerEffects.Pay(owner,values.Mana256)){throw new InvalidOperationException("Blade Shield start mana changed synchronously");}
        return state;
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!SkillCombat.Begin(owner,CombatAbility.BladeShield,Serial.Zero,owner.Location,now,
            (p,_,_,_)=>p.Map!=null && p.Map!=Map.Internal,timing:Timing(CombatTiming.Fcr(owner)))){return false;}
        var action=ClassCombat.For(owner);var state=Start(owner,SkillMechanics.Values(owner,CombatAbility.BladeShield),TimedEffects.TimeAt(now));
        Casts[owner.Serial.Value]=new(state.Id,action.EffectAt,owner.Map,owner.Location,action.PendingWeapon);
        owner.FixedEffect(0x373A,10,15,0x481,0);owner.PlaySound(0x1E5); // Identified blade-shield cast/attachment/audio placeholders.
        return true;
    }
    public static bool Release(PlayerMobile owner,long now)
    {
        if(!Casts.Remove(owner.Serial.Value,out var cast) || !Eligible(owner) || State(owner) is not{} state
            || state.Id!=cast.Id || now<cast.EffectAt || owner.Map!=cast.Map || owner.Location!=cast.Origin
            || (CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=cast.Weapon){return false;}
        return Activate(owner,state.Id,TimedEffects.TimeAt(now));
    }
    internal static bool Activate(PlayerMobile owner,long id,DateTime at)
    {
        if(!Eligible(owner) || !States.TryGetValue(owner.Serial.Value,out var state) || state.Id!=id || at<state.StartedUtc || state.UntilUtc<=at){return false;}
        int initialRank=SkillMechanics.Rank(owner,CombatAbility.BladeShield);
        if(initialRank<=0){return false;}
        Pulse(owner,initialRank); // Initial used-skill rank; recurring events keep the state-list cast rank.
        if(States.TryGetValue(owner.Serial.Value,out var current) && current.Id==id)
        {States[owner.Serial.Value]=current with{NextPulseUtc=at.AddMilliseconds(1000)};}
        return true;
    }
    internal static int RollSkill(SkillValues values)
    {
        int min=values.PhysicalDamage256(false),max=values.PhysicalDamage256(true),span=Math.Max(0,max-min);
        return min+(span>0?Math.Clamp(SkillRoll(span),0,span-1):0);
    }
    internal static int Pulse(PlayerMobile owner,int rank)
    {
        if(!Eligible(owner) || owner.Map==null || owner.Map==Map.Internal || SpellHelper.IsTown(owner.Location,owner)){return 0;}
        var values=SkillMechanics.Values(owner,CombatAbility.BladeShield,rank);
        int skill=RollSkill(values),hits=0;double radius=SkillGeometry.Tiles((int)values.Eval("aurarangecalc"));
        // Current table radius and observed beyond-contact hits are retained.
        // The old GetResultFlags extra melee reach gate has not been measured
        // in the current build; exact outer hit geometry remains acceptance work.
        foreach(var target in SkillCombat.Targets(owner,owner.Location,radius).OfType<BaseCreature>())
        {
            if(SpellHelper.IsTown(target.Location,target)){continue;}
            if(!Eligible(owner)){break;}
            try
            {
            var weapon=CombatTiming.Equipped(owner);using var melee=weapon==null?null:WeaponHands.UseMelee(owner,weapon);
            owner.DoHarmful(target,true);
            if(ClassStats.CheckHit(owner,target,0,HitRoll,notifyMelee:false)!=true){continue;}
            if(!SourceBlocking.TryBlock(target,true,out bool blocked))
            {Log.Warning("Blade Shield skipped unsupported block supplier: {Target}",target.Serial);continue;}
            if(blocked){continue;}
            var damage=BladeShieldDamage.Capture(owner,target,skill,WeaponRoll,Effects);
            var parts=SkillCombat.MitigatePrepared(owner,target,damage with{Poison=0},out int absorbed);
            // Current ResultFlags32 suppresses attacked-in-melee, weapon
            // damage and item kill events. Direct damage, poison/chill, ordinary
            // leech, target-AC fill and item durability are independent lanes.
            int defense=ClassGear.Bonus(owner,"dmg-ac");
            if(defense<0){TimedEffects.ReduceDefense(target,-defense,TimedEffects.Now);}
            var elements=BowLaunch.WeaponElements(owner,weapon);
            WeaponLeech.Apply(owner,target,NamedElements.To256(parts.Physical),elements);
            if(absorbed>0){SkillHealth.Heal(target,absorbed);}
            int loss;
            using(CombatItemEvents.Suppress(owner,target))
            {loss=SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total));CombatDamage.Finish(target,owner);}
            CreatureHitRecovery.Apply(target,owner,loss); // Pinned hit class3 uses divisor16, independent of weapon family.
            if(target is{Alive:true,Deleted:false})
            {
                if(damage.Cold>0){GearEffects.ApplyChill(target,Core.TickCount,damage.ColdMs,owner);}
                if(damage.SourcePoison is{Rate256:>0} poison){poison.Commit(owner,target,TimedEffects.Now);}
            }
            AmazonJab.Wear(weapon,WearRoll);hits++;
            if(target is{Alive:true,Deleted:false}){target.FixedEffect(0x37B9,5,5,0x481,0);} // Identified blade-shield contact placeholder.
            }
            catch(NotSupportedException e){Log.Warning("Blade Shield skipped unsupported damage supplier: {Target} {Reason}",target.Serial,e.Message);}
        }
        return hits;
    }
    internal static void Tick(DateTime at)
    {
        Prune(at);
        foreach(var state in States.Values.Where(x=>x.NextPulseUtc<=at).OrderBy(x=>x.Owner).ToArray())
        {
            if(World.FindMobile((Serial)state.Owner) is PlayerMobile learned && SkillMechanics.Rank(learned,CombatAbility.BladeShield)<=0)
            {States[state.Owner]=state with{NextPulseUtc=null};continue;}
            if(World.FindMobile((Serial)state.Owner) is not PlayerMobile owner || owner.NetState is not{Running:true}
                || owner.Map==null || owner.Map==Map.Internal)
            {
                // No replayed offline damage. Keep the original expiry and
                // resume the next interval from the next connected update.
                States[state.Owner]=state with{NextPulseUtc=at.AddMilliseconds(1000)};continue;
            }
            Pulse(owner,state.Rank);
            if(States.TryGetValue(state.Owner,out var current) && current.Id==state.Id)
            {States[state.Owner]=current with{NextPulseUtc=at.AddMilliseconds(1000)};}
        }
    }
    private static void Resume(PlayerMobile owner,DateTime at)
    {if(State(owner) is{NextPulseUtc:not null} state){States[owner.Serial.Value]=state with{NextPulseUtc=at.AddMilliseconds(1000)};}}
    internal static void Cancel(Mobile owner){if(owner!=null){Casts.Remove(owner.Serial.Value);}}
    public static void Clear(Mobile owner)
    {if(owner!=null){States.Remove(owner.Serial.Value);Cancel(owner);}}
    internal static void Prune(DateTime at)
    {
        foreach(var state in States.Values.ToArray())
        {if(state.UntilUtc<=at || !Eligible(World.FindMobile((Serial)state.Owner))){States.Remove(state.Owner);Casts.Remove(state.Owner);}}
    }
    internal static BladeShieldSave Snapshot(){Prune(TimedEffects.Now);return new(States.Values.OrderBy(x=>x.Owner).ToArray(),_nextId);}
    internal static void Restore(BladeShieldSave saved)
    {
        if(saved?.States==null || saved.NextId<0){throw new InvalidDataException("Missing Blade Shield states");}
        States.Clear();Casts.Clear();_nextId=saved.NextId;var ids=new HashSet<long>();
        foreach(var state in saved.States)
        {
            if(!((Serial)state.Owner).IsMobile || state.Id<1 || state.Id>_nextId || !ids.Add(state.Id) || state.Rank is <1 or >255
                || state.StartedUtc.Kind!=DateTimeKind.Utc || state.UntilUtc.Kind!=DateTimeKind.Utc
                || state.UntilUtc-state.StartedUtc!=TimeSpan.FromSeconds(120+12*(state.Rank-1))
                || state.NextPulseUtc is{} next && (next.Kind!=DateTimeKind.Utc || next<state.StartedUtc)
                || !States.TryAdd(state.Owner,state)){throw new InvalidDataException("Invalid Blade Shield state");}
        }
    }
}
