// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SigilFamilyChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SigilFamilyChecks));
    private sealed record Entry(uint Owner,SigilActor Actor,SigilOwnerState State);
    private sealed record Saved(Entry[] Entries,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sigil-family-fixtures.json");
    private static readonly CombatAbility[] Abilities=[CombatAbility.SigilLethargy,CombatAbility.SigilRancor,CombatAbility.SigilDeath];
    private static readonly int[][] Missiles=[[699,686,702],[700,687,703],[701,688,704]];
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static SkillValues Values(CombatAbility ability,int rank)
    {var row=SkillMechanics.For(ability);return new(row,k=>k==row.SourceKey?rank:0,_=>0,99);}
    private static SigilActor Actor(uint marker)=>SigilActors.Snapshot().Actors.Single(a=>a.Marker==marker);
    private static SigilOwnerState State(PlayerMobile owner)=>SigilActors.Snapshot().Owners.Single(o=>o.Owner==owner.Serial.Value);
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {
            string name=reload?"sigil-family-reload-check":"sigil-family-check";bool again=reload;
            ConsoleInputHandler.RegisterCommand(name,"Shared Sigil bodies,recipient isolation,migration and original saves",_=>Core.LoopContext.Post(()=>
            {
                Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",network=>
                {try{if(again){Reload(network);}else{Check(network);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});
            }));
        }
    }
    private static void Check(NetState network)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(p,99);
        FixtureSpace.Corridor(p,7100,24,3520);FixtureNetwork.Authenticate(network,p,"SigilFamily");
        var point=new Point3D(p.X+3,p.Y,p.Z);var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(point,p.Map);
        var at=TimedEffects.Now;SigilActor previous=null;int pulses=0;CombatAbility last=CombatAbility.None;
        void Pulse(SigilActor actor,PlayerMobile owner,DateTime time){if(owner==p){pulses++;last=actor.Ability;}}
        SigilActors.Pulse+=Pulse;
        try
        {
            int mana=NecromancerEffects.ManaBalance256(p),hp=p.Hits,step=0;
            for(int tier=0;tier<3;tier++)for(int family=0;family<3;family++)
            {
                int rank=new[]{1,10,20}[tier];var ability=Abilities[family];var time=at.AddMilliseconds(step++*40);
                SigilLethargy.Clear(target);var actor=SigilActors.CreatePrepared(p,Values(ability,rank),point,time);
                Require(actor!=null&&actor.Ability==ability&&actor.Missile==Missiles[family][tier]&&actor.Frames==100+rank*25
                    &&SigilActors.Values(actor).Ability==ability&&SigilActors.Values(actor).Rank==rank
                    &&SigilActors.Values(actor).Eval("aurarangecalc")==new[]{4,6,8}[tier]
                    &&SigilActors.RecipientFunction(ability)==(family==1?61:30),"Sigil family body/rank/recipient identity differs");
                Require(World.FindItem((Serial)actor.Marker)is{Movable:false} item&&item.Name.Contains("placeholder")
                    &&item.Name.StartsWith(SkillMechanics.For(ability).Name),"Sigil has another skill's marker label");
                if(previous!=null){Require(Actor(previous.Marker).Retiring,"Different Sigil did not retire previous shared-slot marker");}
                // Retiring actors may have one terminal pulse;drain it before
                // testing the new marker's isolation from Lethargy's subscriber.
                SigilActors.Tick(time);SigilLethargy.Clear(target);int before=pulses;
                SigilActors.Tick(actor.BornUtc.AddMilliseconds(240));
                Require(pulses==before+1&&last==ability,"Family do14pulse failed or dispatched another skill");
                Require((SigilLethargy.State(target)!=null)==(family==0),"Rancor/Death inherited Lethargy's recipient effects");
                if(previous!=null){Require(World.FindItem((Serial)previous.Marker)==null,"Retired cross-family marker survived driver tick");}
                Require(State(p).Stats.Single(s=>s.Stat==354&&s.Layer==1).Value==unchecked((int)actor.Marker),"Family created a separate roster slot");
                previous=actor;
            }
            Require(NecromancerEffects.ManaBalance256(p)==mana&&p.Hits==hp,"Prepared family changed resources");
            Require(WarlockCombat.Handles(CombatAbility.SigilRancor)&&!WarlockCombat.Handles(CombatAbility.SigilDeath)
                &&WarlockCombat.Implemented(CombatAbility.SigilRancor)&&!WarlockCombat.Implemented(CombatAbility.SigilDeath),"Approved Rancor or gated Death admission changed");
            var beforeSave=SigilActors.Snapshot();bool rejected=false;
            try{SigilActors.Restore(beforeSave with{Actors=beforeSave.Actors.Select(a=>a.Marker==previous.Marker?a with{Ability=CombatAbility.SigilRancor}:a).ToArray()});}
            catch(InvalidDataException){rejected=true;}
            Require(rejected&&JsonSerializer.Serialize(SigilActors.Snapshot())==JsonSerializer.Serialize(beforeSave),"Mismatched saved family corrupted live roster");
        }
        finally{SigilActors.Pulse-=Pulse;SigilActors.Clear(p);SigilLethargy.Clear(target);target.Delete();}
        network.Mobile=null;p.NetState=null;
        var entries=new Entry[3];
        for(int family=0;family<3;family++)
        {
            var owner=family==0?p:ClassSkillChecks.Player(DiabloClass.Warlock);owner.Female=family==2;
            if(family!=0){ClassCreation.Profiles.AdvanceLevel(owner,99);FixtureSpace.Corridor(owner,6900+family*40,12,3520);}
            FixtureNetwork.Authenticate(network,owner,"SigilFamilySave");
            var actor=SigilActors.CreatePrepared(owner,Values(Abilities[family],200),new(owner.X+3,owner.Y,owner.Z),TimedEffects.Now);
            Require(actor!=null&&actor.Frames==5100,"Normal saved family marker refused");entries[family]=new(owner.Serial.Value,actor,State(owner));
            network.Mobile=null;owner.NetState=null;
        }
        // Existing version0JSON has no Ability property. Constructor default
        // must preserve Lethargy rather than zero/another skill;new states stay typed.
        var original=SigilActors.Snapshot();var json=JsonNode.Parse(JsonSerializer.Serialize(original));
        foreach(var node in json["Actors"].AsArray())
        {if(node["Marker"].GetValue<uint>()==entries[0].Actor.Marker){node.AsObject().Remove("Ability");}}
        SigilActors.Restore(JsonSerializer.Deserialize<SigilActorSave>(json.ToJsonString()));
        Require(Actor(entries[0].Actor.Marker)==entries[0].Actor&&JsonSerializer.Serialize(SigilActors.Snapshot())==JsonSerializer.Serialize(original),"Version0Lethargy migration changed original context");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(entries,0)));
        Log.Information("Sigil families:all9owned bodies,shared cross-skill replacement,do14recipient identity,no accidental Lethargy effects,free prepared resources,closed unfinished handlers,atomic family mismatch refusal and old JSON migration pass;3normal204second original markers saved without artificial lifetime holds.");
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2&&saved.Entries.Length==3,"Missing family save generation");
        foreach(var entry in saved.Entries)
        {
            var owner=World.FindMobile((Serial)entry.Owner)as PlayerMobile;var actor=Actor(entry.Actor.Marker);var state=State(owner);
            Require(actor with{LastPulse=entry.Actor.LastPulse}==entry.Actor&&actor.Ability==entry.Actor.Ability
                &&World.FindItem((Serial)actor.Marker)is{Deleted:false} item&&item.Location==actor.Origin&&item.Map.MapID==actor.Map
                &&state.EpochUtc==entry.State.EpochUtc&&state.EventUtc==entry.State.EventUtc&&state.Expiry==entry.State.Expiry
                &&state.Flags==entry.State.Flags&&state.Stats.SequenceEqual(entry.State.Stats),"Original family/marker/source context changed after reload");
            FixtureNetwork.Authenticate(network,owner,"SigilFamilyReload");int pulses=0;
            void Pulse(SigilActor s,PlayerMobile p,DateTime at){if(s.Marker==actor.Marker){Require(s.Ability==entry.Actor.Ability,"Reload pulse changed family");pulses++;}}
            SigilActors.Pulse+=Pulse;
            try{var now=TimedEffects.Now;var nextPulse=actor.BornUtc.AddMilliseconds((Math.Max(0,actor.LastPulse)+6)*40L);if(now<nextPulse){now=nextPulse;}SigilActors.Tick(now);int first=pulses;Require(first==1,"Original family marker did not resume its pulse");SigilActors.Tick(now);Require(pulses==first,"Family reload replayed a pulse");}
            finally{SigilActors.Pulse-=Pulse;network.Mobile=null;owner.NetState=null;}
        }
        // A successful resumed Rancor pulse advances its saved application cursor.
        // Preserve that exact post-pulse actor for the next reload comparison.
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1,Entries=saved.Entries.Select(e=>e with{Actor=Actor(e.Actor.Marker)}).ToArray()}));
    }
}
