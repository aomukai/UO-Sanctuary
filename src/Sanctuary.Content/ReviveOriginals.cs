// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Logging;
namespace Sanctuary;

public sealed record ReviveOriginalState(uint Corpse,uint Actor,string RuntimeType,bool Frozen,bool CantWalk,bool Hidden,bool Blessed,string BeforeDeathJson=null,string BeforeDeathIssue=null);
public sealed class ReviveOriginalProfiles:GenericPersistence
{
    public ReviveOriginalProfiles():base("SanctuaryReviveOriginals",222){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(ReviveOriginals.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown retained original creature version");}
        ReviveOriginals.Restore(JsonSerializer.Deserialize<ReviveOriginalState[]>(reader.ReadString())??throw new InvalidDataException("Missing retained original creatures"));
    }
    public override void PostDeserialize()=>ReviveOriginals.Prune();
}
// Retain the actual native actor, not a constructor or a replacement summon.
// Automatic death retention is enabled with the learned Revive handler.
// Optional accepted-death data retains resources and item effects separately
// from corpse loot. It does not attach stats/procs or activate revived AI.
public static class ReviveOriginals
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveOriginals));
    private sealed record Registry(Dictionary<uint,ReviveOriginalState> Corpses,Dictionary<uint,uint> Actors);
    private static Registry _registry=new(new(),new());
    public static ReviveOriginalProfiles Profiles{get;private set;}
    public static void Configure()
    {
        Profiles=new();
        var retained=ClassLootHooks.IsRetainedOriginal;
        ClassLootHooks.IsRetainedOriginal=actor=>IsRetained(actor)||retained?.Invoke(actor)==true;
        var capture=ClassLootHooks.RetainOriginal;
        ClassLootHooks.RetainOriginal=(actor,corpse)=>NecromancerCombat.Implemented(CombatAbility.Revive)&&Capture(actor,corpse)||capture?.Invoke(actor,corpse)==true;
        var before=ClassLootHooks.BeforeDeath;
        ClassLootHooks.BeforeDeath=actor=>{before?.Invoke(actor);if(NecromancerCombat.Implemented(CombatAbility.Revive)){ReviveBeforeDeath.Capture(actor);}};
    }
    public static void Initialize()
    {
        var deleted=CorpseRecoveryHooks.Deleted;
        CorpseRecoveryHooks.Deleted=corpse=>{Remove(corpse.Serial.Value);deleted?.Invoke(corpse);};
        Timer.StartTimer(TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1),Prune);
    }
    internal static bool SupportedDeathPath(Type type)
    {
        var declaring=type.GetMethod(nameof(BaseCreature.OnDeath),[typeof(Container)])?.DeclaringType;
        return declaring==typeof(BaseCreature)||declaring==typeof(GraveyardUndead);
    }
    internal static bool IsRetained(BaseCreature actor)=>actor!=null&&_registry.Actors.ContainsKey(actor.Serial.Value);
    internal static ReviveOriginalState State(Corpse corpse)=>corpse==null?null:_registry.Corpses.GetValueOrDefault(corpse.Serial.Value);
    internal static ReviveOriginalState[] Snapshot()=>_registry.Corpses.Values.OrderBy(r=>r.Corpse).ToArray();
    internal static void Restore(ReviveOriginalState[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var corpses=new Dictionary<uint,ReviveOriginalState>();var actors=new Dictionary<uint,uint>();
        foreach(var row in rows)
        {
            if(row==null||!((Serial)row.Corpse).IsItem||!((Serial)row.Actor).IsMobile||row.Actor==0
                ||string.IsNullOrWhiteSpace(row.RuntimeType)||!corpses.TryAdd(row.Corpse,row)||!actors.TryAdd(row.Actor,row.Corpse))
            {throw new InvalidDataException("Invalid or duplicate retained original creature");}
            if(row.BeforeDeathIssue!=null&&(string.IsNullOrWhiteSpace(row.BeforeDeathIssue)||row.BeforeDeathJson!=null)){throw new InvalidDataException("Conflicting or empty before-death capture issue");}
            if(row.BeforeDeathJson!=null){ReviveBeforeDeath.Validate(row.BeforeDeathJson,row.RuntimeType);}
        }
        _registry=new(corpses,actors);
    }
    internal static BaseCreature Find(Corpse corpse)
    {
        if(corpse is not{Deleted:false}||!_registry.Corpses.TryGetValue(corpse.Serial.Value,out var row)){return null;}
        var actor=World.FindMobile((Serial)row.Actor) as BaseCreature;
        if(actor is not{Deleted:false}||actor.GetType().FullName!=row.RuntimeType){return null;}
        var history=CorpseSkillRules.Profiles.Find(corpse);
        if(history?.BaseLife?.OriginalMonsterSerial!=row.Actor||history.ReviveCreature?.RuntimeType!=row.RuntimeType||corpse.Owner!=actor)
        {throw new InvalidDataException("Retained native creature disagrees with its original corpse");}
        return actor;
    }
    internal static ReviveOriginalState DetachForActivation(Corpse corpse,BaseCreature expected)
    {
        if(expected==null||Find(corpse)!=expected||expected.Map!=Map.Internal||expected.Alive)
        {throw new InvalidOperationException("Original actor is not in inactive corpse storage");}
        var row=_registry.Corpses[corpse.Serial.Value];
        _registry.Corpses.Remove(row.Corpse);_registry.Actors.Remove(row.Actor);return row;
    }
    internal static void ReattachAfterFailedActivation(Corpse corpse,BaseCreature actor,ReviveOriginalState row)
    {
        if(corpse is not{Deleted:false}||actor is not{Deleted:false}||corpse.Serial.Value!=row.Corpse||actor.Serial.Value!=row.Actor
            ||actor.GetType().FullName!=row.RuntimeType||_registry.Corpses.ContainsKey(row.Corpse)||_registry.Actors.ContainsKey(row.Actor))
        {throw new InvalidDataException("Cannot restore the original inactive lease");}
        _registry.Corpses.Add(row.Corpse,row);_registry.Actors.Add(row.Actor,row.Corpse);Park(actor);
    }
    internal static bool Capture(BaseCreature actor,Container container)
    {
        var pending=ReviveBeforeDeath.Take(actor);string before=pending.Json;
        if(actor is null||actor.Deleted||container is not Corpse{Deleted:false} corpse||actor.Controlled||actor.Summoned
            ||actor is BaseVendor or GraveyardUndead{Raised:true}||!SupportedDeathPath(actor.GetType())||_registry.Corpses.ContainsKey(corpse.Serial.Value)||IsRetained(actor))
        {return false;}
        var row=CorpseSkillRules.Profiles.Find(corpse);
        if(row is not{Owned:false,Shattered:false,NoSelect:false,Selectable:true}||row.BaseLife?.OriginalMonsterSerial!=actor.Serial.Value
            ||row.ReviveCreature?.RuntimeType!=actor.GetType().FullName||GenericPetSearchTables.SourceId(row.Source) is not{} cls
            ||ReviveSpecies.ForClass(cls)!=true||!SourceAiEligibility.CanSwitch(cls,SourceAiEligibility.CurrentMonster))
        {return false;}
        if(ReviveWorldControllers.Issue(actor.Spawner) is{} controllerIssue)
        {Log.Warning("{Issue}",controllerIssue);return false;}
        // Match ordinary death's roster cleanup while retaining the actual
        // actor. The spawner must not count, respawn/delete or own this corpse.
        ReviveWorldControllers.DetachDeath(actor);
        var original=before==null?null:ReviveBeforeDeath.Decode(before);
        var saved=new ReviveOriginalState(corpse.Serial.Value,actor.Serial.Value,actor.GetType().FullName,original?.Frozen??actor.Frozen,original?.CantWalk??actor.CantWalk,original?.Hidden??actor.Hidden,original?.Blessed??actor.Blessed,before,pending.Issue);
        _registry.Corpses.Add(saved.Corpse,saved);_registry.Actors.Add(saved.Actor,saved.Corpse);
        Park(actor);return true;
    }
    private static void Park(BaseCreature actor)
    {
        if(actor is GraveyardUndead undead){undead.StopCombatActions();}
        actor.Combatant=null;actor.Warmode=false;actor.Frozen=actor.CantWalk=actor.Hidden=actor.Blessed=true;
        actor.AIObject?.AITimer.Stop();actor.Internalize();
    }
    private static void Remove(uint corpse)
    {
        if(!_registry.Corpses.Remove(corpse,out var row)){return;}
        _registry.Actors.Remove(row.Actor);
        if(World.FindMobile((Serial)row.Actor) is BaseCreature{Deleted:false} actor)
        {
            if(actor.GetType().FullName!=row.RuntimeType){throw new InvalidDataException("Retained creature identity collision during cleanup");}
            actor.Delete();
        }
    }
    internal static void Prune()
    {
        ReviveBeforeDeath.PrunePending();
        foreach(var row in Snapshot())
        {
            if(World.FindItem((Serial)row.Corpse) is not Corpse{Deleted:false} corpse){Remove(row.Corpse);continue;}
            // A different corpse consumer, or a legacy metadata replacement,
            // can retire the dead-unit projection while leaving loot intact.
            if(CorpseSkillRules.NoSelect(corpse)||corpse.SanctuaryConsumed||corpse.Animated)
            {Remove(row.Corpse);continue;}
            BaseCreature actor;
            try{actor=Find(corpse);}
            catch(InvalidDataException e)
            {
                // Only retire our own inactive original. Never conceal an
                // identity/type collision or delete a living/owned actor.
                if(World.FindMobile((Serial)row.Actor) is not BaseCreature{Alive:false,Deleted:false,ControlMaster:null} inactive
                    ||inactive.Map!=Map.Internal||inactive.GetType().FullName!=row.RuntimeType){throw;}
                Log.Warning("Retiring inactive Revive original {Actor} for unusable corpse {Corpse}: {Reason}",row.Actor,row.Corpse,e.Message);
                Remove(row.Corpse);continue;
            }
            if(actor==null){_registry.Corpses.Remove(row.Corpse);_registry.Actors.Remove(row.Actor);continue;}
            Park(actor);
        }
    }
}
