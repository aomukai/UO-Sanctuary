// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record BoundDemonState(uint Actor,uint Owner,string RuntimeType,string Monster,BindDemonSuppliers Birth,long Order,
    MonsterDifficulty Difficulty,MonsterNativeCombat Normal,int BaseMaximum256,int? SavedLife256=null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EngorgeEffect Engorge{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public BoundDemonAuraGrant Aura{get;init;}
}
public sealed class BoundDemonProfiles:GenericPersistence
{
    private BoundDemonState[] _snapshot;
    public BoundDemonProfiles():base("SanctuaryBoundDemons",251){}
    public override void Serialize(){_snapshot=BoundDemonHosts.SaveSnapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown bound demon host version");}BoundDemonHosts.Restore(JsonSerializer.Deserialize<BoundDemonState[]>(reader.ReadString()));}
    public override void PostDeserialize()=>BoundDemonHosts.AfterLoad();
}

// Prepared live-body host only. Successful binding is supplied explicitly;
// no invented capture chance/channel or automatic learned admission occurs.
internal static class BoundDemonHosts
{
    private static ImmutableDictionary<uint,BoundDemonState> States=ImmutableDictionary<uint,BoundDemonState>.Empty;
    public static void Configure(){_=new BoundDemonProfiles();PersistentMinions.RegisterBehavior(Behavior);}
    public static void Initialize()
    {
        var target=ClassCombatHooks.TargetAllowed;
        ClassCombatHooks.TargetAllowed=(actor,to)=>actor is BaseCreature body&&State(body)!=null
            ?CanAct(body)&&body.ControlMaster is PlayerMobile owner&&ClassCombat.IsHostile(owner,to)&&target?.Invoke(actor,to)!=false:target?.Invoke(actor,to)!=false;
        var hits=ClassGearHooks.CanRegenerateHits;var mana=ClassGearHooks.CanRegenerateMana;var stamina=ClassGearHooks.CanRegenerateStamina;
        bool Recovery(Mobile actor)=>actor is not BaseCreature body||State(body)==null||CanAct(body);
        ClassGearHooks.CanRegenerateHits=actor=>Recovery(actor)&&hits?.Invoke(actor)!=false;
        ClassGearHooks.CanRegenerateMana=actor=>Recovery(actor)&&mana?.Invoke(actor)!=false;
        ClassGearHooks.CanRegenerateStamina=actor=>Recovery(actor)&&stamina?.Invoke(actor)!=false;
    }
    internal static BoundDemonState State(BaseCreature actor)=>actor!=null&&States.TryGetValue(actor.Serial.Value,out var state)
        &&(World.Loading||actor is{Deleted:false}&&actor.ControlMaster?.Serial.Value==state.Owner&&actor.GetType().FullName==state.RuntimeType
            &&BattleCommand.PetOrder(actor)==state.Order)?state:null;
    internal static bool Active(Mobile actor)=>actor is BaseCreature body&&State(body)!=null;
    private static BindDemonSuppliers Stats(Mobile actor)=>actor is BaseCreature body?State(body)?.Birth:null;
    internal static int Armor(Mobile actor)=>Stats(actor)?.Armor??0;
    internal static int Attack(Mobile actor)=>Stats(actor)?.AttackBonus??0;
    internal static int DamagePercent(Mobile actor)=>checked((Stats(actor)?.DamagePercent??0)+BoundDemonUpgrades.DamagePercent(actor));
    internal static int FlatDamage(Mobile actor)=>Stats(actor)?.FlatDamage??0;
    internal static int AttackSpeed(Mobile actor)=>Stats(actor)?.AttackSpeed??0;
    internal static int Velocity(Mobile actor)=>checked((Stats(actor)?.VelocityPercent??0)+BoundDemonUpgrades.Velocity(actor));
    internal static int Resistance(Mobile actor,string element)=>element=="physical"?Stats(actor)?.PhysicalResist??0
        :element is"fire"or"cold"or"ltng"or"pois"?Stats(actor)?.SummonResist??0:0;
    internal static MonsterDifficulty? Difficulty(BaseCreature actor)=>State(actor)?.Difficulty;
    internal static int? Maximum256(BaseCreature actor)
    {
        if(State(actor)is not{} state){return null;}
        long result=state.BaseMaximum256+ClassResources.Percentage(state.BaseMaximum256,BattleOrders.LifePercent(actor));
        if(result is<1 or>int.MaxValue-255){throw new NotSupportedException("Buffed bound demon life exceeds source range");}return(int)result;
    }
    private static bool Trained(PlayerMobile owner)=>ClassCreation.Profiles.TryGet(owner.Serial,out var p)&&p.Class==DiabloClass.Warlock
        ?ClassCreation.Profiles.GetRank(owner.Serial,"warlock.binddemon")>0
        :ClassCreation.Profiles.Histories(owner.Serial).Any(h=>h.Profile.Class==DiabloClass.Warlock&&h.Ranks.TryGetValue("warlock.binddemon",out var rank)&&rank>0);
    private sealed class NativeBehavior(BaseCreature actor):IPersistentMinion
    {
        public DiabloClass SummonClass=>DiabloClass.Warlock;
        public bool Permitted(PlayerMobile owner)=>State(actor)is{} row&&row.Owner==owner.Serial.Value&&Trained(owner);
        public void SuspendActions()
        {BoundDemonAuras.Stop(actor,State(actor)?.Order??BattleCommand.PetOrder(actor));EngorgeEffects.ForgetNative(actor.Serial.Value);actor.AIObject?.AITimer.Stop();if(actor.Spell is Server.Spells.Spell spell){spell.Disturb(Server.Spells.DisturbType.Kill);}actor.Blessed=true;}
        public void ResumeActions(){if(CanAct(actor)){actor.Blessed=false;actor.AIObject?.AITimer.Start();}}
    }
    private static IPersistentMinion Behavior(BaseCreature actor)=>State(actor)!=null?new NativeBehavior(actor):null;
    internal static bool CanAct(BaseCreature body)=>body is{Alive:true,Deleted:false}&&State(body)is{} state
        &&body.ControlMaster is PlayerMobile{Alive:true,Deleted:false,NetState:{Running:true}} owner&&owner.Serial.Value==state.Owner
        &&body.Map==owner.Map&&body.Map!=Map.Internal&&PersistentMinions.Active(owner,body)&&Behavior(body).Permitted(owner)&&!MinionSafeAreas.ShouldPark(owner,body);
    internal static BaseCreature[] For(PlayerMobile owner)=>States.Values.Where(s=>s.Owner==owner.Serial.Value)
        .Select(s=>World.FindMobile((Serial)s.Actor)as BaseCreature).Where(b=>b is{Alive:true,Deleted:false}&&b.ControlMaster==owner&&State(b)!=null)
        .OrderBy(BattleCommand.PetOrder).ToArray();
    private static CreatureVariant Profile(string type,string monster,MonsterDifficulty difficulty)
    {
        if(!CreatureProfiles.TryVariant(type,difficulty,out var profile)||profile.Rewards.Reference!=monster||!profile.Rewards.Enabled
            ||difficulty!=MonsterDifficulty.Normal&&profile is not{Enabled:true,ApplyStats:true,Life:>0})
        {throw new NotSupportedException("No qualified bound demon difficulty projection: "+type+"/"+difficulty);}
        return profile;
    }
    private static int BaseMaximum(string type,string monster,MonsterNativeCombat normal,MonsterDifficulty difficulty)
    {
        var row=BindDemonPrepared.Monster(monster);var profile=Profile(type,monster,difficulty);
        int life=difficulty==MonsterDifficulty.Normal?normal.Life256??throw new NotSupportedException("Missing original Normal life baseline")
            :checked(profile.Life.Value*256);
        // Explicit prepared monpet context:current unbuffed body maximum,
        // one player. This does not establish the protected binder's context.
        long result=SkillFormula.Evaluate(row.BoundFormula,(name,arg,property)=>name=="pcl5"?life/256
            :name=="stat"&&arg=="monster_playercount"&&property=="accr"?1:throw new NotSupportedException("Unmapped bound-life source context"));
        if(result is<1 or>int.MaxValue/256){throw new NotSupportedException("Unsupported bound maximum life");}return checked((int)result*256);
    }
    private static void ApplyBody(BaseCreature body,BoundDemonState state)
    {
        var profile=Profile(state.RuntimeType,state.Monster,state.Difficulty);
        MonsterVariantBaselines.Apply(body,state.Normal,profile);
        BoundDemonUpgrades.ApplyNative(body,state.Birth);
        if(body.DamageMin<0||body.DamageMax<body.DamageMin){throw new NotSupportedException("Bound body has no native damage endpoints");}
        // Keep the original typed base roll intact. Native positive weapon
        // packets add the captured flat amount to physical exactly once.
        body.HitsMaxSeed=(state.BaseMaximum256+255)/256;
    }
    private static void Validate(BoundDemonState state)
    {
        if(state==null||!((Serial)state.Actor).IsMobile||!((Serial)state.Owner).IsMobile||state.Actor==state.Owner||state.Order<=0
            ||state.Normal?.Actor!=state.Actor||state.Normal.Life256 is not>0||state.Normal.Minimum<0||state.Normal.Maximum<state.Normal.Minimum
            ||string.IsNullOrWhiteSpace(state.RuntimeType)||!Enum.IsDefined(state.Difficulty)||state.SavedLife256 is<0)
        {throw new InvalidDataException("Invalid original bound demon identity/baseline");}
        BindDemonPrepared.Validate(state.Birth);
        EngorgeEffects.Validate(state.Engorge,state.Owner);BoundDemonAuras.Validate(state.Aura,state.Birth);
        if(state.Birth.InheritedPhysical5!=0||state.Birth.InheritedPhysical7!=0)
        {throw new NotSupportedException("Bound inherited physical sunder writer remains unqualified");}
        if(state.BaseMaximum256!=BaseMaximum(state.RuntimeType,state.Monster,state.Normal,state.Difficulty))
        {throw new InvalidDataException("Bound life projection differs from its original context");}
    }
    internal static bool BindPrepared(PlayerMobile owner,BaseCreature body,out string issue)
    {
        issue="The living creature is not an eligible original demon.";
        if(owner is not{Alive:true,Deleted:false,NetState:{Running:true},Map:not null}||owner.Map==Map.Internal||!Trained(owner)||MinionSafeAreas.Contains(owner)
            ||body is not{Alive:true,Deleted:false,Controlled:false,Summoned:false,Blessed:false}||body is BaseVendor or SkeletonGuardian or EncounterCreature
            ||body.ControlMaster!=null||body.SummonMaster!=null||PersistentMinions.Behavior(body)!=null||body.Guild!=null
            ||body.Map!=owner.Map||!owner.InRange(body,19)||!owner.InLOS(body)||!ClassCombat.IsHostile(owner,body)||!NamedCombat.Demon(body))
        {return false;}
        var spawner=body.Spawner;
        if(ReviveWorldControllers.Issue(spawner)is{} controller){issue=controller;return false;}
        int[] channels=[body.PhysicalDamage,body.FireDamage,body.ColdDamage,body.PoisonDamage,body.EnergyDamage];
        if(body.ChaosDamage!=0||body.DirectDamage!=0||channels.Any(n=>n is<0 or>100)||channels.Sum()!=100)
        {issue="Unsupported native damage channels require their own binding adapter.";return false;}
        BindDemonSuppliers birth;MonsterNativeCombat normal;string monster;BoundDemonAuraGrant aura;
        try
        {
            birth=BindDemonPrepared.Capture(owner);monster=CreatureProfiles.For(body).Rewards.Reference;
            if(birth.InheritedPhysical5!=0||birth.InheritedPhysical7!=0)
            {throw new NotSupportedException("Original inherited sunder needs a qualified binding adapter");}
            if(BindDemonPrepared.Monster(monster).IndexScope!="MonsterClass"){throw new NotSupportedException("Named encounter binding needs its own original-host adapter");}
            int? unbuffed=BattleOrders.LifePercent(body)!=0&&MonsterVariants.SpawnDifficulty(body)==MonsterDifficulty.Normal
                ?BattleOrders.NativeLifeBase256(body):null;
            MonsterVariantBaselines.RememberNormal(body,unbuffed);normal=MonsterVariantBaselines.Find(body)
                ??throw new NotSupportedException("Original Normal combat baseline is unavailable");
            if(normal.Minimum<0||normal.Maximum<normal.Minimum)
            {throw new NotSupportedException("Original body damage endpoints need a qualified weapon adapter");}
            foreach(var d in Enum.GetValues<MonsterDifficulty>()){_=BaseMaximum(body.GetType().FullName,monster,normal,d);}
            aura=BoundDemonAuras.CapturePrepared(body,birth,TimedEffects.Now);
        }
        catch(Exception e)when(e is NotSupportedException or InvalidDataException or InvalidOperationException or ArgumentException)
        {issue=e.Message;return false;}
        var resources=BattleOrders.Capture(body);int slots=body.ControlSlots;
        body.AIObject?.AITimer.Stop();body.ControlSlots=0;
        if(!body.SetControlMaster(owner)){body.ControlSlots=slots;body.AIObject?.AITimer.Start();issue="Native owner assignment failed.";return false;}
        BattleCommand.Track(body);var difficulty=SummonDifficulties.For(owner.Map);
        var state=new BoundDemonState(body.Serial.Value,owner.Serial.Value,body.GetType().FullName,monster,birth,BattleCommand.PetOrder(body),difficulty,normal,
            BaseMaximum(body.GetType().FullName,monster,normal,difficulty)){Aura=aura};Validate(state);
        States=States.Add(state.Actor,state);Behavior(body).SuspendActions();spawner?.Remove(body);body.Spawner=null;body.NoKillAwards=true;body.Tamable=false;
        body.Combatant=body.FocusMob=body.ControlTarget=null;body.Home=body.Location;body.HomeMap=body.Map;body.CurrentWayPoint=null;
        ApplyBody(body,state);BattleOrders.Change(body,resources,preserveSummonLifeRatio:true);PersistentMinions.Profiles.Track(body);
        foreach(var prior in For(owner).Where(b=>b!=body))
        {BoundDemonAuras.Stop(prior,State(prior).Order);prior.Delete();States=States.Remove(prior.Serial.Value);EngorgeEffects.ForgetNative(prior.Serial.Value);}
        WarlockGoatmanWorld.Enforce(owner);body.IssueOrder(OrderType.Guard,null);Behavior(body)?.ResumeActions();BoundDemonAuras.Tick(body,TimedEffects.Now);issue=null;return true;
    }
    internal static void Sync(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false,Map:not null}||owner.Map==Map.Internal||World.Loading){return;}
        var difficulty=SummonDifficulties.For(owner.Map);
        foreach(var body in For(owner))
        {
            var old=State(body);if(body.Map!=Map.Internal&&body.Map!=owner.Map){PersistentMinions.Suspend(body);}
            if(old.Difficulty==difficulty){continue;}
            var resources=BattleOrders.Capture(body);var state=old with{Difficulty=difficulty,BaseMaximum256=BaseMaximum(old.RuntimeType,old.Monster,old.Normal,difficulty)};
            Validate(state);States=States.SetItem(state.Actor,state);ApplyBody(body,state);BattleOrders.Change(body,resources,preserveSummonLifeRatio:true);
        }
    }
    internal static void StoreEngorge(BaseCreature body,EngorgeEffect effect)
    {
        var state=State(body)??throw new NotSupportedException("No original bound Engorge host");
        EngorgeEffects.Validate(effect,state.Owner);States=States.SetItem(state.Actor,state with{Engorge=effect});
    }
    internal static void RetirePrepared(BaseCreature body)
    {
        var state=State(body)??throw new NotSupportedException("No original bound body to retire");
        Behavior(body).SuspendActions();body.Delete();States=States.Remove(state.Actor);EngorgeEffects.ForgetNative(state.Actor);
    }
    internal static void Tick()
    {
        foreach(var old in States.Values.ToArray())
        {
            var body=World.FindMobile((Serial)old.Actor)as BaseCreature;
            if(body is not{Alive:true,Deleted:false}){EngorgeEffects.ForgetNative(old.Actor);BoundDemonAuras.Forget(old.Actor);States=States.Remove(old.Actor);continue;}
            if(State(body)==null)
            {if(body.GetType().FullName==old.RuntimeType&&BattleCommand.PetOrder(body)==old.Order){body.Delete();}EngorgeEffects.ForgetNative(old.Actor);BoundDemonAuras.Forget(old.Actor);States=States.Remove(old.Actor);continue;}
            if(body.ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner){body.Delete();EngorgeEffects.ForgetNative(old.Actor);BoundDemonAuras.Forget(old.Actor);States=States.Remove(old.Actor);continue;}
            Sync(owner);
            if(!CanAct(body)&&body.Map!=Map.Internal){PersistentMinions.Suspend(body);}
            EngorgeEffects.RecoverNative(body,TimedEffects.Now);BoundDemonAuras.Tick(body,TimedEffects.Now);
        }
    }
    internal static BoundDemonState[] SaveSnapshot()=>States.Values.Where(s=>World.FindMobile((Serial)s.Actor)is BaseCreature{Alive:true,Deleted:false} body&&State(body)==s)
        .OrderBy(s=>s.Order).Select(s=>s with{SavedLife256=SkillHealth.Balance256(World.FindMobile((Serial)s.Actor))}).ToArray();
    internal static void Restore(BoundDemonState[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing bound demon hosts");}
        var next=ImmutableDictionary<uint,BoundDemonState>.Empty;var owners=new System.Collections.Generic.HashSet<uint>();var orders=new System.Collections.Generic.HashSet<long>();
        foreach(var state in saved){Validate(state);if(next.ContainsKey(state.Actor)||!owners.Add(state.Owner)||!orders.Add(state.Order)){throw new InvalidDataException("Duplicate bound actor/owner/order");}next=next.Add(state.Actor,state);}
        foreach(var old in States.Values){EngorgeEffects.ForgetNative(old.Actor);if(World.FindMobile((Serial)old.Actor)is BaseCreature body){BoundDemonAuras.Stop(body,old.Order);}else{BoundDemonAuras.Forget(old.Actor);}}States=next;
    }
    internal static void AfterLoad()
    {
        Tick();foreach(var state in States.Values.ToArray())
        {
            if(World.FindMobile((Serial)state.Actor)is not BaseCreature body||State(body)==null){throw new InvalidDataException("Bound demon lost its original type/owner/order");}
            ApplyBody(body,state);if(state.SavedLife256 is{} life){SkillHealth.SetBalance256(body,life);States=States.SetItem(state.Actor,state with{SavedLife256=null});}
            PersistentMinions.Restore(body);
        }
    }
}
