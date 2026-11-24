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

// UO has one master slot. Keep the player as control master and record the
// actual raising caster separately; never replace either with a proxy actor.
public sealed record RaisedMinionState(uint Actor,uint Caster,uint Owner,DiabloClass Class,MonsterDifficulty Difficulty,int? SavedLife256=null);
public sealed class RaisedMinionProfiles:GenericPersistence
{
    public RaisedMinionProfiles():base("SanctuaryRaisedMinions",256){}
    public override void Serialize(IGenericWriter writer)
    {writer.Write(0);writer.Write(JsonSerializer.Serialize(RaisedMinions.SaveSnapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown raised minion ownership version");}
        RaisedMinions.Restore(JsonSerializer.Deserialize<RaisedMinionState[]>(reader.ReadString()));
    }
    public override void PostDeserialize()=>RaisedMinions.AfterLoad();
}
public static class RaisedMinions
{
    private static Dictionary<uint,RaisedMinionState> States=new();
    public static RaisedMinionProfiles Profiles{get;private set;}
    public static void Configure(){Profiles=new();PersistentMinions.RegisterBehavior(Behavior);}
    internal static RaisedMinionState State(BaseCreature body)=>body==null?null:States.GetValueOrDefault(body.Serial.Value);
    internal static RaisedMinionState[] Snapshot()=>States.Values.OrderBy(x=>x.Actor).ToArray();
    internal static RaisedMinionState[] SaveSnapshot()=>Snapshot().Where(row=>World.FindMobile((Serial)row.Actor)is GraveyardUndead{Alive:true,Deleted:false})
        .Select(row=>row with{SavedLife256=SkillHealth.Balance256((Mobile)World.FindMobile((Serial)row.Actor))}).ToArray();
    internal static void Restore(RaisedMinionState[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);var states=new Dictionary<uint,RaisedMinionState>();
        foreach(var row in rows)
        {
            if(row==null||row.Actor==0||row.Caster==0||row.Owner==0||row.Actor==row.Caster||row.Caster==row.Owner||row.Actor==row.Owner
                ||!((Serial)row.Actor).IsMobile||!((Serial)row.Caster).IsMobile||!((Serial)row.Owner).IsMobile
                ||!Enum.IsDefined(row.Class)||!Enum.IsDefined(row.Difficulty)||row.SavedLife256 is <=0||!states.TryAdd(row.Actor,row))
            {throw new InvalidDataException("Invalid raised minion ownership graph");}
        }
        if(states.Values.Any(row=>states.ContainsKey(row.Caster))){throw new InvalidDataException("Raised helpers cannot recursively summon helpers");}
        States=states;
    }
    private static BaseCreature Caster(RaisedMinionState row)=>World.FindMobile((Serial)row.Caster) is BaseCreature{Alive:true,Deleted:false} caster
        &&caster.ControlMaster?.Serial.Value==row.Owner&&PersistentMinions.Behavior(caster) is{} behavior&&behavior.SummonClass==row.Class?caster:null;
    internal static bool CanRaise(GraveyardUndead caster)
    {
        if(!caster.Controlled&&!caster.Summoned){return true;}
        return State(caster)==null&&caster.ControlMaster is PlayerMobile{Alive:true,Deleted:false,NetState:not null} owner
            &&PersistentMinions.Behavior(caster) is{} behavior&&PersistentMinions.Active(owner,caster)
            &&PersistentMinions.Permitted(owner,caster,behavior.SummonClass)&&caster.Map==owner.Map&&caster.Map!=Map.Internal
            &&!MinionSafeAreas.ShouldPark(owner,caster);
    }
    internal static bool CorpseAllowed(GraveyardUndead caster,Corpse corpse)=>!caster.Controlled&&!caster.Summoned
        ||caster.ControlMaster is PlayerMobile owner&&CorpseSkillRules.DireWolfUsableAt(owner,corpse,caster,8);
    internal static bool Attach(GraveyardUndead caster,GraveyardUndead body)
    {
        if(!caster.Controlled&&!caster.Summoned){return true;}
        if(!CanRaise(caster)||body is not{Alive:true,Deleted:false,Controlled:false,Summoned:false}||body.Map!=Map.Internal){return false;}
        var owner=(PlayerMobile)caster.ControlMaster;var kind=PersistentMinions.Behavior(caster).SummonClass;
        MonsterVariantBaselines.RememberNormal(body);
        var row=new RaisedMinionState(body.Serial.Value,caster.Serial.Value,owner.Serial.Value,kind,MonsterVariants.Difficulty(body));
        States.Add(row.Actor,row);body.ControlSlots=0;
        if(!body.SetControlMaster(owner)){States.Remove(row.Actor);return false;}
        body.NoKillAwards=true;body.Tamable=false;PersistentMinions.Profiles.Track(body);return true;
    }
    private sealed class BehaviorAdapter(GraveyardUndead body,RaisedMinionState row):IPersistentMinion
    {
        public DiabloClass SummonClass=>row.Class;
        public bool CanActivate=>Caster(row)!=null;
        public bool Permitted(PlayerMobile owner)=>row.Owner==owner.Serial.Value&&Caster(row) is{} caster
            &&PersistentMinions.Permitted(owner,caster,row.Class);
        public void SuspendActions(){body.StopCombatActions();body.AIObject?.AITimer.Stop();body.Blessed=true;}
        public void ResumeActions(){body.Blessed=false;body.AIObject?.AITimer.Start();}
    }
    private static IPersistentMinion Behavior(BaseCreature body)=>body is GraveyardUndead undead&&State(body) is{} row?new BehaviorAdapter(undead,row):null;
    internal static void RemoveFor(BaseCreature caster)
    {
        foreach(var row in Snapshot().Where(row=>row.Caster==caster.Serial.Value))
        {States.Remove(row.Actor);if(World.FindMobile((Serial)row.Actor)is GraveyardUndead{Deleted:false} body){body.Delete();}}
        States.Remove(caster.Serial.Value);
    }
    internal static void Sweep()
    {
        foreach(var row in Snapshot())
        {
            if(World.FindMobile((Serial)row.Actor)is not GraveyardUndead{Alive:true,Deleted:false} body){States.Remove(row.Actor);continue;}
            if(Caster(row)==null||body.ControlMaster?.Serial.Value!=row.Owner||World.FindMobile((Serial)row.Owner)is not PlayerMobile{Alive:true,Deleted:false})
            {body.Delete();States.Remove(row.Actor);}
        }
    }
    internal static void AfterLoad()
    {
        Sweep();foreach(var row in Snapshot())
        {
            if(World.FindMobile((Serial)row.Actor)is not GraveyardUndead body){throw new InvalidDataException("Raised minion lost its original actor");}
            ApplyDifficulty(body,row);if(row.SavedLife256 is{} life){SkillHealth.SetBalance256(body,life);States[row.Actor]=row with{SavedLife256=null};}
            PersistentMinions.Restore(body);
        }
    }
    private static void ApplyDifficulty(GraveyardUndead body,RaisedMinionState row)
    {
        var basis=MonsterVariantBaselines.Find(body)??throw new InvalidDataException("Raised minion lost its Normal baseline");
        var profile=CreatureProfiles.Variant(body,row.Difficulty);
        if(!profile.Enabled||row.Difficulty!=MonsterDifficulty.Normal&&!profile.ApplyStats){throw new NotSupportedException("Raised minion has no supported difficulty variant");}
        MonsterVariantBaselines.Apply(body,basis,profile);
        body.HitsMaxSeed=profile.ApplyStats&&profile.Life is{} life?life:checked((basis.Life256.Value+255)/256);
    }
    internal static void Sync(PlayerMobile owner)
    {
        var difficulty=SummonDifficulties.For(owner);bool resume=false;
        foreach(var old in Snapshot().Where(row=>row.Owner==owner.Serial.Value))
        {
            if(World.FindMobile((Serial)old.Actor)is not GraveyardUndead{Alive:true,Deleted:false} body||body.ControlMaster!=owner){continue;}
            if(body.Map!=Map.Internal&&body.Map!=owner.Map){PersistentMinions.Suspend(body);resume=true;}
            if(old.Difficulty==difficulty){continue;}
            int life=SkillHealth.Balance256(body),maximum=SkillHealth.Maximum256(body);
            var next=old with{Difficulty=difficulty,SavedLife256=null};States[old.Actor]=next;ApplyDifficulty(body,next);
            SkillHealth.SetBalance256(body,SummonDifficulties.Life256(life,maximum,SkillHealth.Maximum256(body)));
        }
        if(resume&&owner.NetState!=null){PersistentMinions.Resume(owner);}
    }
}
