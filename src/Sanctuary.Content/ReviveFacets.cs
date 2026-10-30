// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Live projection is separate from the immutable release and corpse history.
public sealed record ReviveFacetPlan(string Definition,ReviveLifePlan Life,ReviveCombatPlan Combat);
internal static class ReviveFacets
{
    private static readonly Dictionary<uint,string> Issues=new();
    internal static string Issue(BaseCreature actor)=>Issues.GetValueOrDefault(actor.Serial.Value);
    internal static MonsterDifficulty? Difficulty(BaseCreature actor)=>ReviveNativeActivation.State(actor) is{FacetPlan:not null} row?row.Difficulty:null;
    private static (CreatureVariant Profile,int Level) Target(string definition,string source,MonsterDifficulty difficulty)
    {
        if(!CreatureProfiles.TryVariant(definition,difficulty,out var row)||!row.Enabled
            ||difficulty!=MonsterDifficulty.Normal&&!row.ApplyStats
            ||row.Rewards.Reference!=source)
        {throw new NotSupportedException("No approved combat variant for this Revive in "+difficulty+".");}
        int? level=row.ApplyStats?row.Level:null;level??=row.Rewards.Enabled?row.Rewards.Level:null;
        if(level is not >0){throw new NotSupportedException("The Revive target variant has no supported level.");}
        return(row,level.Value);
    }
    private static ReviveFacetPlan Calculate(ReviveNativeState state,string definition,MonsterDifficulty difficulty)
    {
        var birth=state.Setup.LifePlan;
        if(birth==null||state.Setup.CombatPlan==null)
        {throw new NotSupportedException("This older Revive has no source life/combat plan for facet rescaling.");}
        var target=Target(definition,birth.Source,difficulty);
        var corpse=new CorpseBaseSnapshot(state.Actor,target.Level,difficulty,CorpseBaseSnapshots.Base256(birth.Source,difficulty,target.Level),birth.Seed);
        var life=ReviveLife.Calculate(birth.Source,corpse,birth.CasterLevel,birth.MasteryRank);
        return new(definition,life,ReviveCombat.Calculate(life,state.Setup.Rank,_=>0));
    }
    internal static void Validate(ReviveNativeState state)
    {
        if(state.FacetPlan is not{} plan){return;}
        try
        {
            if(string.IsNullOrWhiteSpace(plan.Definition)||Calculate(state,plan.Definition,state.Difficulty)!=plan)
            {throw new InvalidDataException("Revive facet projection does not reproduce its birth context and target variant");}
        }
        catch(Exception error)when(error is NotSupportedException or OverflowException)
        {throw new InvalidDataException("Invalid Revive facet projection",error);}
    }
    internal static void RestoreActor(BaseCreature actor,ReviveNativeState state)
    {
        if(state.FacetPlan is not{} plan){return;}
        if(actor is EncounterCreature||CreatureProfiles.Definition(actor).Type!=plan.Definition
            ||MonsterVariantBaselines.Find(actor) is not{} basis)
        {throw new InvalidDataException("Saved Revive facet projection lost its native baseline or definition");}
        MonsterVariantBaselines.Apply(actor,basis,Target(plan.Definition,plan.Life.Source,state.Difficulty).Profile);
        actor.HitsMaxSeed=(plan.Life.Maximum256+255)/256;
    }
    internal static void Sync(PlayerMobile owner)
    {
        var difficulty=SummonDifficulties.For(owner.Map);bool resume=false;
        foreach(var old in ReviveNativeActivation.Snapshot().Where(x=>x.Owner==owner.Serial.Value))
        {
            if(World.FindMobile((Serial)old.Actor) is not BaseCreature{Alive:true,Deleted:false} actor||actor.ControlMaster!=owner){continue;}
            if(old.Difficulty==difficulty)
            {
                bool unblocked=Issues.Remove(old.Actor);
                if(owner.NetState!=null&&PersistentMinions.Active(owner,actor)
                    &&(unblocked||actor.Map!=Map.Internal&&actor.Map!=owner.Map))
                {PersistentMinions.Suspend(actor);resume=true;}
                continue;
            }
            try
            {
                if(actor is EncounterCreature){throw new NotSupportedException("Encounter Revive facet projection is not implemented.");}
                var plan=Calculate(old,CreatureProfiles.Definition(actor).Type,difficulty);
                MonsterVariantBaselines.RememberNormal(actor);
                var basis=MonsterVariantBaselines.Find(actor)??throw new NotSupportedException("This older difficulty variant has no saved native Normal combat baseline.");
                var next=old with{Difficulty=difficulty,FacetPlan=plan,SavedLife256=null};Validate(next);
                int life=SkillHealth.Balance256(actor),maximum=SkillHealth.Maximum256(actor),nativeMaximum=actor.HitsMaxSeed;
                var before=MonsterVariantBaselines.Capture(actor);
                PersistentMinions.Suspend(actor);
                try
                {
                    ReviveNativeActivation.Store(next);
                    RestoreActor(actor,next);
                    SkillHealth.SetBalance256(actor,SummonDifficulties.Life256(life,maximum,SkillHealth.Maximum256(actor)));
                    ReviveRegeneration.Recover(actor,false,Core.TickCount);
                }
                catch
                {
                    ReviveNativeActivation.Store(old);actor.HitsMaxSeed=nativeMaximum;
                    MonsterVariantBaselines.Apply(actor,before);
                    SkillHealth.SetBalance256(actor,life);throw;
                }
                Issues.Remove(actor.Serial.Value);resume=true;
            }
            catch(Exception error)when(error is NotSupportedException or OverflowException)
            {
                PersistentMinions.Suspend(actor);
                if(Issues.GetValueOrDefault(actor.Serial.Value)!=error.Message)
                {Issues[actor.Serial.Value]=error.Message;owner.SendMessage(0x22,"Revive remains parked: "+error.Message);}
            }
        }
        if(resume&&owner.NetState!=null){PersistentMinions.Resume(owner);}
        foreach(uint id in Issues.Keys.ToArray()){if(World.FindMobile((Serial)id) is not BaseCreature{Deleted:false}){Issues.Remove(id);}}
    }
}
