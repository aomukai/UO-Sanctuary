// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

// Optional source HP is distinct from the legacy integral native setup. It
// does not substitute for complete source passive/stat/event/AI composition.
public sealed record ReviveNativeSetup(int Rank,int MaximumLife,int Life,int Mana,int Stamina,ReviveLifePlan LifePlan=null,ReviveCombatPlan CombatPlan=null,int? RegenerationRate=null);
public sealed record ReviveNativeState(uint Actor,uint Owner,ReviveOriginalState Original,ReviveNativeSetup Setup,long Order,MonsterDifficulty Difficulty,int? SavedLife256=null);
public sealed class ReviveNativeProfiles:GenericPersistence
{
    public ReviveNativeProfiles():base("SanctuaryReviveNativeActivation",223){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(ReviveNativeActivation.SaveSnapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown prepared Revive activation version");}
        ReviveNativeActivation.Restore(JsonSerializer.Deserialize<ReviveNativeState[]>(reader.ReadString())??throw new InvalidDataException("Missing prepared Revive activations"));
    }
    public override void PostDeserialize()=>ReviveNativeActivation.AfterLoad();
}
// Original-actor adapter shared by prepared integration checks and the learned
// cast. Caller validation and optional source plans remain explicit.
public static class ReviveNativeActivation
{
    private static Dictionary<uint,ReviveNativeState> States=new();
    private static long NextOrder;
    public static ReviveNativeProfiles Profiles{get;private set;}
    public static void Configure()
    {
        Profiles=new();PersistentMinions.RegisterBehavior(Behavior);
    }
    public static void Initialize()
    {
        var allowed=ClassCombatHooks.TargetAllowed;
        ClassCombatHooks.TargetAllowed=(actor,target)=>actor is not BaseCreature creature||State(creature)==null
            ?allowed?.Invoke(actor,target)!=false:CanAct(creature)&&target?.Map==creature.Map&&!ClassCombat.Friendly(creature,target)&&allowed?.Invoke(actor,target)!=false;
        Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Tick);
    }
    internal static ReviveNativeState State(BaseCreature actor)=>actor==null?null:States.GetValueOrDefault(actor.Serial.Value);
    internal static ReviveNativeState[] Snapshot()=>States.Values.OrderBy(s=>s.Order).ToArray();
    internal static ReviveNativeState[] SaveSnapshot()=>Snapshot().Select(row=>row.Setup.LifePlan!=null&&World.FindMobile((Serial)row.Actor) is BaseCreature{Alive:true,Deleted:false} actor
        ?row with{SavedLife256=SkillHealth.Balance256(actor)}:row).ToArray();
    private static bool ValidSetup(ReviveNativeSetup setup)
        =>setup is{Rank:>0 and <=255,MaximumLife:>0,Life:>0,Mana:>=0,Stamina:>=0}&&setup.Life<=setup.MaximumLife
            &&(setup.LifePlan!=null?setup.MaximumLife==(setup.LifePlan.Maximum256+255L)/256&&setup.Life==setup.MaximumLife:setup.MaximumLife<=65000);
    private static void Validate(ReviveNativeState row)
    {
        if(row is null||row.Actor==0||!((Serial)row.Actor).IsMobile||row.Owner==0||!((Serial)row.Owner).IsMobile
            ||row.Original==null||!((Serial)row.Original.Corpse).IsItem||row.Original.Actor!=row.Actor||row.Original.BeforeDeathJson==null||row.Original.BeforeDeathIssue!=null
            ||!ValidSetup(row.Setup)||row.Order<=0||!Enum.IsDefined(row.Difficulty)||row.SavedLife256 is <=0)
        {throw new InvalidDataException("Invalid prepared native Revive identity/setup");}
        if(row.Setup.LifePlan is{} plan){ReviveLife.Validate(plan);}
        else if(row.SavedLife256!=null){throw new InvalidDataException("Integral-only Revive cannot contain source life");}
        ReviveCombat.Validate(row.Setup);
        ReviveRegeneration.Validate(row.Setup);
        ReviveBeforeDeath.Validate(row.Original.BeforeDeathJson,row.Original.RuntimeType);
    }
    internal static void Restore(ReviveNativeState[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);var states=new Dictionary<uint,ReviveNativeState>();var orders=new HashSet<long>();var corpses=new HashSet<uint>();long next=0;
        foreach(var row in rows)
        {Validate(row);if(!states.TryAdd(row.Actor,row)||!orders.Add(row.Order)||!corpses.Add(row.Original.Corpse)){throw new InvalidDataException("Duplicate prepared native Revive");}next=Math.Max(next,row.Order);}
        States=states;NextOrder=next;
    }
    private static bool Trained(PlayerMobile owner)
        =>ClassCreation.Profiles.TryGet(owner.Serial,out var current)&&current.Class==DiabloClass.Necromancer
            ?ClassCreation.Profiles.GetRank(owner.Serial,"necromancer.revive")>0
            :ClassCreation.Profiles.Histories(owner.Serial).Any(h=>h.Profile.Class==DiabloClass.Necromancer&&h.Ranks.GetValueOrDefault("necromancer.revive")>0);
    private sealed class NativeBehavior(BaseCreature actor):IPersistentMinion
    {
        public DiabloClass SummonClass=>DiabloClass.Necromancer;
        public bool Permitted(PlayerMobile owner)=>State(actor) is{} row&&row.Owner==owner.Serial.Value&&Trained(owner)
            &&(owner.Map==null||owner.Map==Map.Internal||SummonDifficulties.For(owner.Map)==row.Difficulty);
        public void SuspendActions(){if(actor is GraveyardUndead undead){undead.StopCombatActions();}actor.AIObject?.AITimer.Stop();actor.Blessed=true;}
        public void ResumeActions(){if(CanAct(actor)){actor.Blessed=false;actor.AIObject?.AITimer.Start();}}
    }
    private static IPersistentMinion Behavior(BaseCreature actor)=>State(actor)!=null?new NativeBehavior(actor):null;
    private static bool CanAct(BaseCreature actor)=>actor is{Alive:true,Deleted:false}&&actor.ControlMaster is PlayerMobile{Alive:true,Deleted:false,NetState:not null} owner
        &&owner.Map==actor.Map&&actor.Map!=Map.Internal&&PersistentMinions.Active(owner,actor)&&Behavior(actor).Permitted(owner);
    internal static bool CommitPrepared(PlayerMobile owner,Corpse corpse,Point3D destination,ReviveAiStatus status,ReviveNativeSetup setup,out BaseCreature actor,out string issue)
    {
        actor=null;issue="The original corpse is not ready for native activation.";
        if(owner is not{Alive:true,Deleted:false,Map:not null}||owner.Map==Map.Internal||!Trained(owner)
            ||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Necromancer
            ||!ValidSetup(setup)
            ||setup.Rank!=SkillMechanics.Rank(owner,CombatAbility.Revive)||owner.Followers>owner.FollowersMax
            ||!CorpseSkillRules.Usable(owner,corpse,SkeletonPets.Range)){return false;}
        var original=ReviveOriginals.Find(corpse);
        var saved=ReviveOriginals.Snapshot().SingleOrDefault(r=>r.Corpse==corpse.Serial.Value);
        if(setup.LifePlan is{} plan)
        {
            ReviveLife.Validate(plan);var sourceHistory=CorpseSkillRules.Profiles.Find(corpse);
            if(sourceHistory?.Source!=plan.Source||sourceHistory.BaseLife is not{} life||life.Level!=plan.OriginalLevel||life.Difficulty!=plan.Difficulty||life.Seed!=plan.Seed
                ||plan.CasterLevel!=ClassStats.Level(owner)||plan.MasteryRank!=SkillMechanics.Rank(owner,CombatAbility.SkeletonMastery))
            {issue="Source life context changed before native activation.";return false;}
        }
        ReviveCombat.Validate(setup);
        if(setup.CombatPlan!=null&&ReviveCombat.Capture(owner,setup.LifePlan,setup.Rank)!=setup.CombatPlan)
        {issue="Source combat context changed before native activation.";return false;}
        if(original==null||saved?.BeforeDeathJson==null||saved.BeforeDeathIssue!=null||original.Spawner!=null||original.Guild!=null)
        {issue="Original state or native spawn/guild transfer is unsupported.";return false;}
        if(!SourceCorpseIdentity.For(corpse).AllowsRevivedAi(status)){issue="The source revived-AI gate refuses this corpse.";return false;}
        if(destination.X<0||destination.Y<0||destination.X>=owner.Map.Width||destination.Y>=owner.Map.Height
            ||!owner.Map.CanSpawnMobile(destination)||!owner.InLOS(destination)||Math.Abs(destination.Z-corpse.Z)>=16)
        {issue="Native destination is blocked; source placement/failure composition remains separate.";return false;}
        if(setup.Mana>original.ManaMax||setup.Stamina>original.StamMax){issue="Explicit native resources exceed the original pools.";return false;}
        var row=new ReviveNativeState(original.Serial.Value,owner.Serial.Value,saved,setup,checked(NextOrder+1),SummonDifficulties.For(owner.Map));Validate(row);
        int slots=original.ControlSlots,maximum=original.HitsMaxSeed;bool noRewards=original.NoKillAwards,tamable=original.Tamable;bool detached=false;
        var home=original.Home;var homeMap=original.HomeMap;var waypoint=original.CurrentWayPoint;
        try
        {
            // Bind while still inactive, then detach before any corpse cleanup.
            States.Add(row.Actor,row);original.ControlSlots=0;
            if(!original.SetControlMaster(owner)){States.Remove(row.Actor);original.ControlSlots=slots;issue="Native owner binding failed.";return false;}
            original.NoKillAwards=true;original.Tamable=false;original.HitsMaxSeed=setup.MaximumLife;
            ReviveOriginals.DetachForActivation(corpse,original);detached=true;
            original.Blessed=original.Hidden=original.Frozen=original.CantWalk=false;
            original.Hits=setup.Life;original.Mana=setup.Mana;original.Stam=setup.Stamina;
            if(setup.LifePlan!=null){SkillHealth.SetBalance256(original,SkillHealth.Maximum256(original));}
            original.Combatant=null;original.FocusMob=null;original.ControlTarget=null;original.MoveToWorld(destination,owner.Map);
            if(!CorpseSkillRules.Reserve(corpse)){throw new InvalidOperationException("Original corpse reservation changed during native activation");}
        }
        catch
        {
            States.Remove(row.Actor);original.AIObject?.AITimer.Stop();original.SetControlMaster(null);original.ControlSlots=slots;
            original.Home=home;original.HomeMap=homeMap;original.CurrentWayPoint=waypoint;
            original.HitsMaxSeed=maximum;original.NoKillAwards=noRewards;original.Tamable=tamable;original.Hits=original.Mana=original.Stam=0;
            SkillHealth.Clear(original);
            if(detached){ReviveOriginals.ReattachAfterFailedActivation(corpse,original,saved);}
            throw;
        }
        var history=CorpseSkillRules.Profiles.Find(corpse);CorpseSkillRules.Profiles.Store(history with{UnitReactivated=true});
        NextOrder=row.Order;BattleCommand.Track(original);PersistentMinions.Profiles.Track(original);
        original.IssueOrder(OrderType.Guard,null);NecromancerCombat.Consume(corpse);
        var owned=Snapshot().Where(s=>s.Owner==row.Owner&&World.FindMobile((Serial)s.Actor) is{Alive:true,Deleted:false}).OrderBy(s=>s.Order).ToArray();
        foreach(var old in owned.Take(Math.Max(0,owned.Length-setup.Rank)))
        {if(World.FindMobile((Serial)old.Actor) is BaseCreature oldActor){oldActor.Delete();}States.Remove(old.Actor);}
        if(owner.NetState==null){PersistentMinions.Suspend(owner);}else{Behavior(original).ResumeActions();}
        actor=original;issue=null;return true;
    }
    internal static void AfterLoad()
    {
        Tick();foreach(var row in Snapshot())
        {
            if(World.FindMobile((Serial)row.Actor) is not BaseCreature actor||actor.GetType().FullName!=row.Original.RuntimeType||actor.ControlMaster?.Serial.Value!=row.Owner)
            {throw new InvalidDataException("Prepared Revive lost its original native actor/owner");}
            if(row.SavedLife256 is{} life)
            {SkillHealth.SetBalance256(actor,life);States[row.Actor]=row with{SavedLife256=null};}
            PersistentMinions.Restore(actor);
        }
    }
    private static void Tick()
    {
        foreach(var row in Snapshot())
        {
            var actor=World.FindMobile((Serial)row.Actor) as BaseCreature;
            if(actor is not{Alive:true,Deleted:false}){States.Remove(row.Actor);continue;}
            if(actor.ControlMaster?.Serial.Value!=row.Owner||World.FindMobile((Serial)row.Owner) is not PlayerMobile{Alive:true,Deleted:false} owner)
            {actor.Delete();States.Remove(row.Actor);continue;}
            if(!CanAct(actor)&&actor.Map!=Map.Internal){PersistentMinions.Suspend(owner);}
            ReviveRegeneration.Recover(actor,CanAct(actor),Core.TickCount);
        }
    }
}
