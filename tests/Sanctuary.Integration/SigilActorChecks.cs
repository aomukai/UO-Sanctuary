// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SigilActorChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SigilActorChecks));
    private sealed record Saved(uint Owner,SigilActor Actor,SigilOwnerState State,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sigil-actor-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {
            string name=reload?"sigil-actor-reload-check":"sigil-actor-check";bool again=reload;
            ConsoleInputHandler.RegisterCommand(name,"Original Sigil marker/replacement/UTC/owner/saves",_=>Core.LoopContext.Post(()=>
            {
                Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",network=>
                {try{if(again){Reload(network);}else{Check(network);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});
            }));
        }
    }
    private static SkillValues Values(int rank)=>new(SkillMechanics.For(CombatAbility.SigilLethargy),k=>k=="Sigil Lethargy"?rank:0,_=>0,99);
    private static SigilActor Actor(uint marker)=>SigilActors.Snapshot().Actors.Single(a=>a.Marker==marker);
    private static SigilOwnerState State(PlayerMobile owner)=>SigilActors.Snapshot().Owners.Single(a=>a.Owner==owner.Serial.Value);
    private static void Invalid(SigilActorSave saved)
    {
        string before=JsonSerializer.Serialize(SigilActors.Snapshot());bool rejected=false;
        try{SigilActors.Restore(saved);}catch(InvalidDataException){rejected=true;}
        Require(rejected&&JsonSerializer.Serialize(SigilActors.Snapshot())==before,"Malformed Sigil save replaced current state");
    }
    private static void Check(NetState network)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"warlock.sigillethargy",1);
        FixtureSpace.Corridor(p,6760,24,3520);FixtureNetwork.Authenticate(network,p,"SigilActor");
        var map=p.Map;var origin=p.Location;var point=new Point3D(p.X+3,p.Y,p.Z);var start=TimedEffects.Now;
        int count=0;uint last=0;int rank=0;Point3D pulsePoint=default;
        void Pulse(SigilActor s,PlayerMobile owner,DateTime at){if(owner!=p){return;}count++;last=s.Marker;rank=s.Rank;pulsePoint=s.Origin;}
        SigilActors.Pulse+=Pulse;
        try
        {
            NecromancerEffects.SetMana256(p,100*256);int mana=NecromancerEffects.ManaBalance256(p),hp=p.Hits;
            var occupied=PoisonDaggerChecks.Target(p);occupied.MoveToWorld(point,map);
            SigilActor s;try{s=SigilActors.CreatePrepared(p,Values(1),point,start);}
            finally{occupied.Delete();}
            Require(s is{Rank:1,Missile:699,Frames:125,LastPulse:-1}&&World.FindItem((Serial)s.Marker)is{Movable:false} marker
                &&marker.Name.Contains("placeholder")&&marker.Location==point&&State(p).Stats.Any(v=>v.Stat==354&&unchecked((uint)v.Value)==s.Marker),"Real rank1marker/label/source roster missing");
            SigilActors.Tick(start);Require(count==1&&last==s.Marker&&rank==1&&Actor(s.Marker).LastPulse==0,"First do14pulse missing");
            SigilActors.Tick(start.AddMilliseconds(5*40));Require(count==1,"Early periodic pulse");
            SigilActors.Tick(start.AddMilliseconds(6*40));Require(count==2&&Actor(s.Marker).LastPulse==6,"Six-frame pulse missing");
            var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");p.MoveToWorld(new(p.X+10,p.Y,p.Z),map);
            SigilActors.Tick(start.AddMilliseconds(12*40));Require(count==3&&rank==1&&pulsePoint==point&&Actor(s.Marker).Rank==1,"Marker followed owner or recaptured gear rank");
            var moved=p.Location;p.MoveToWorld(moved,Map.Felucca);SigilActors.Tick(start.AddMilliseconds(18*40));
            Require(count==3&&Actor(s.Marker).LastPulse==18&&World.FindItem((Serial)s.Marker).Map==map,"Facet move transported marker or kept combat");
            p.MoveToWorld(moved,map);SigilActors.Tick(start.AddMilliseconds(18*40));Require(count==3,"Facet return replayed skipped pulse");
            network.Mobile=null;p.NetState=null;SigilActors.Tick(start.AddMilliseconds(24*40));Require(count==3&&Actor(s.Marker).LastPulse==24,"Offline marker fought or paused");
            network.Mobile=p;p.NetState=network;SigilActors.Tick(start.AddMilliseconds(24*40));Require(count==3,"Reconnect replayed skipped pulse");
            SigilActors.Tick(start.AddMilliseconds(30*40));Require(count==4,"Next new pulse did not resume");
            SigilActors.Tick(start.AddMilliseconds(125*40));Require(!SigilActors.Snapshot().Actors.Any(a=>a.Marker==s.Marker)&&World.FindItem((Serial)s.Marker)==null&&!SigilActors.Snapshot().Owners.Any(a=>a.Owner==p.Serial.Value),"Natural marker/state timer did not expire");
            p.MoveToWorld(origin,map);p.AddToBackpack(helm);
            var next=start.AddSeconds(6);var large=SigilActors.CreatePrepared(p,Values(20),point,next);var end=State(p).EventUtc;
            Require(large is{Rank:20,Missile:702,Frames:600},"Large tier/range mismatch");
            var small=SigilActors.CreatePrepared(p,Values(1),new(point.X+2,point.Y,point.Z),next.AddMilliseconds(41));
            Require(small!=null&&small.BornUtc==next.AddMilliseconds(40)&&Actor(large.Marker).Retiring&&World.FindItem((Serial)large.Marker)!=null&&State(p).EventUtc==end
                &&State(p).Stats.Single(v=>v.Stat==354&&v.Layer==1).Value==unchecked((int)small.Marker),"Replacement immediately deleted old marker,failed slot reuse or shortened owner timer");
            SigilActors.Tick(next.AddMilliseconds(41));Require(World.FindItem((Serial)large.Marker)==null&&World.FindItem((Serial)small.Marker)!=null,"Lifetime-zero marker did not retire on next driver tick");
            var before=JsonSerializer.Serialize(SigilActors.Snapshot());Require(SigilActors.CreatePrepared(p,Values(1),new(-1,-1,0),next.AddMilliseconds(80))==null&&JsonSerializer.Serialize(SigilActors.Snapshot())==before,"Bad placement mutated old roster");
            World.FindItem((Serial)small.Marker).Delete();var recovered=SigilActors.CreatePrepared(p,Values(10),point,next.AddMilliseconds(120));
            Require(recovered is{Missile:686,Frames:350}&&State(p).Stats.Single(v=>v.Stat==355&&v.Layer==0).Value==1,"Missing old item did not reuse its slot or select medium tier");
            var extended=SigilActors.CreatePrepared(p,Values(30),new(point.X+4,point.Y,point.Z),next.AddMilliseconds(161));
            Require(extended!=null&&State(p).EventUtc==extended.BornUtc.AddMilliseconds(extended.Frames*40L),"Between-frame cast left owner timer earlier than marker expiry");
            Require(NecromancerEffects.ManaBalance256(p)==mana&&p.Hits==hp,"Prepared marker allocated resource payment or healing");
            var current=SigilActors.Snapshot();Invalid(current with{Actors=current.Actors.Select(a=>a.Marker==recovered.Marker?a with{Frames=1}:a).ToArray()});
            Invalid(current with{Owners=current.Owners.Select(o=>o.Owner==p.Serial.Value?o with{Flags=128}:o).ToArray()});
            SigilActors.Clear(p);p.MoveToWorld(origin,map);
            var saved=SigilActors.CreatePrepared(p,Values(200),point,TimedEffects.Now);
            Require(saved is{Rank:200,Frames:5100,Missile:702}&&!WarlockCombat.Implemented(CombatAbility.SigilLethargy),"Normal saved marker or public gate mismatch");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,saved,State(p),0)));
            var witch=ClassSkillChecks.Player(DiabloClass.Warlock);witch.Female=true;ClassCreation.Profiles.AdvanceLevel(witch,99);
            try{FixtureSpace.Corridor(witch,6800,8,3520);Require(SigilActors.CreatePrepared(witch,Values(1),new(witch.X+2,witch.Y,witch.Z),TimedEffects.Now)!=null,"Witch prepared marker refused");}
            finally{SigilActors.Clear(witch);witch.Delete();}
            Log.Information("Sigil actor:real marker and source roster,all three Lethargy tiers,do14periodic identity,gear/position snapshot,facet/offline no replay,lifetime-zero retirement,longer owner timer,missing item repair,failed placement,atomic malformed restore and resource preservation pass;normal204second marker saved for actual reloads. No recipient status or learned cast admitted.");
        }
        finally{SigilActors.Pulse-=Pulse;}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;
        Require(p!=null&&saved.Generation<2,"Saved Sigil owner/generation missing");FixtureNetwork.Authenticate(network,p,"SigilReload");
        var actor=Actor(saved.Actor.Marker);var state=State(p);
        Require(actor.Marker==saved.Actor.Marker&&actor.BornUtc==saved.Actor.BornUtc&&actor.Origin==saved.Actor.Origin&&actor.Frames==5100&&actor.Rank==200
            &&World.FindItem((Serial)actor.Marker)is{Deleted:false}item&&item.Map.MapID==actor.Map&&item.Location==actor.Origin
            &&state.EpochUtc==saved.State.EpochUtc&&state.EventUtc==saved.State.EventUtc&&state.Expiry==saved.State.Expiry&&state.Flags==saved.State.Flags
            &&state.Stats.SequenceEqual(saved.State.Stats),"Saved original marker identity/context/clock/roster changed");
        var map=Map.Maps[actor.Map];p.MoveToWorld(new(actor.Origin.X-3,actor.Origin.Y,actor.Origin.Z),map);int pulses=0;
        void Pulse(SigilActor s,PlayerMobile owner,DateTime at){if(s.Marker==actor.Marker){pulses++;}}
        SigilActors.Pulse+=Pulse;
        try{var now=TimedEffects.Now;SigilActors.Tick(now);int first=pulses;SigilActors.Tick(now);Require(pulses==first,"Reload duplicate time replayed pulse");}
        finally{SigilActors.Pulse-=Pulse;}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Actor=Actor(actor.Marker),Generation=saved.Generation+1}));
    }
}
