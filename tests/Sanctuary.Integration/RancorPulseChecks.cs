// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class RancorPulseChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(RancorPulseChecks));
    private sealed record Saved(int Generation,uint Owner,uint Target,SigilActor Actor,NecromancerState State,int Life256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"rancor-pulse-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {
            string name=reload?"rancor-pulse-reload-check":"rancor-pulse-check";bool again=reload;
            ConsoleInputHandler.RegisterCommand(name,"Complete prepared Rancor pulse candidate",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",network=>
            {try{Check(network,again);Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
    }
    private static SkillValues Values(int rank)=>new(SkillMechanics.For(CombatAbility.SigilRancor),_=>rank,_=>0,99);
    private static SigilActor Actor(uint marker)=>SigilActors.Snapshot().Actors.Single(a=>a.Marker==marker);
    private static int Seed(int roll)
    {for(int n=0;n<10000;n++){ulong s=SourceRandom.InitLow(n);if(SourceRandom.Limited(ref s,100)==roll){return n;}}throw new InvalidOperationException("Chance seed");}
    private static DateTime Next(SigilActor actor)
    {int current=checked((int)((TimedEffects.Now-actor.BornUtc).TotalMilliseconds/40));return actor.BornUtc.AddMilliseconds(40L*Math.Max(actor.LastPulse+6,current/6*6+6));}
    private static CleaveActionTarget Enemy(PlayerMobile p,int dx,int dy=0,int dz=0)
    {var t=new CleaveActionTarget{NoKillAwards=true,Frozen=true,CantWalk=true};t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z+dz),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);SkillHealth.SetBalance256(t,t.HitsMax*256);return t;}
    private static void Detach(NetState network,PlayerMobile p){network.Mobile=null;p.NetState=null;}
    private static void Exercise(NetState network)
    {
        var p=CleaveActionChecks.Fresh();FixtureNetwork.Authenticate(network,p,"RancorPulseExercise");
        var a=Enemy(p,3);var b=Enemy(p,4);var outside=Enemy(p,5);var high=Enemy(p,3,1,20);var plain=Enemy(p,7);var ally=Enemy(p,3,-1);ally.ControlMaster=p;ally.Controlled=true;
        RancorPulseResult result=default;int seeds=0,pulses=0;
        void Pulse(SigilActor actor,PlayerMobile owner,DateTime at)
        {
            if(actor.Owner!=p.Serial.Value){return;}pulses++;
            result=SigilRancorPulseProposal.PulsePrepared(actor,owner,at,()=>
            {
                Require(!SigilRancorPulseProposal.PulsePrepared(actor,owner,at).Claimed,"Nested pulse was not claimed before callbacks");
                return Seed(seeds++==0?74:75);
            });
        }
        SigilActors.Pulse+=Pulse;
        try
        {
            var actor=SigilActors.CreatePrepared(p,Values(1),new(p.X+2,p.Y,p.Z),TimedEffects.Now);Require(actor!=null,"Rancor marker creation");
            var at=actor.BornUtc;SigilActors.Tick(at);
            Require(result is{Claimed:true,Recipients:2,Successes:1}&&seeds==2&&SourceMind.Neutral(a)&&!SourceMind.Neutral(b)
                &&SigilRancorPrimary.State(a)?.DamagePercent==50&&SigilRancorPrimary.State(b)?.AttackRate==5
                &&SigilRancorPrimary.State(outside)==null&&SigilRancorPrimary.State(high)==null&&SigilRancorPrimary.State(ally)==null,
                "Boundary75chance/radius/height/ally filtering or primary-on-failure failed");
            var original=NecromancerEffects.Profiles.Find(a);var second=NecromancerEffects.Profiles.Find(b);
            var primaryUntil=TimedEffects.Now.AddSeconds(10);Require(SigilRancorPrimary.ApplyPrepared(p,plain,Values(1),primaryUntil,true),"Primary-only expiry setup");plain.Combatant=a;plain.FocusMob=a;
            Require(SigilRancorPulseProposal.ExpirePrepared(plain,primaryUntil)&&plain.Combatant==a&&plain.FocusMob==a,"Primary-only expiry interrupted unrelated combat");
            Require(SigilRancorPrimary.ApplyPrepared(p,plain,Values(1),primaryUntil,true)&&SigilRancorMind.ApplyPrepared(p,plain,primaryUntil,SourceRandom.InitLow(0),true),"Ownership expiry setup");
            plain.ControlMaster=p;plain.Controlled=true;
            Require(SigilRancorPulseProposal.ExpirePrepared(plain,primaryUntil)&&plain.Combatant==a&&plain.FocusMob==a,"Expiry reset a newly owned creature's controller");plain.Controlled=false;plain.ControlMaster=null;
            SigilActors.Tick(at);Require(pulses==1&&!SigilRancorPulseProposal.PulsePrepared(Actor(actor.Marker),p,at).Claimed&&NecromancerEffects.Profiles.Find(a)==original,"Duplicate pulse advanced RNG");
            ulong expected=original.RancorMind.Seed;uint roll=SourceRandom.Limited(ref expected,100);at=Next(Actor(actor.Marker));SigilActors.Tick(at);
            var refreshed=NecromancerEffects.Profiles.Find(a);
            Require(refreshed.RancorMind.Seed==expected&&refreshed.RancorPrimary.Creator==original.RancorPrimary.Creator&&refreshed.RancorPrimary.UntilUtc==at.AddMilliseconds(480)
                &&(roll<75?refreshed.RancorMind.UntilUtc==at.AddMilliseconds(480):refreshed.RancorMind.UntilUtc==original.RancorMind.UntilUtc),"Equal refresh/failure rewrote creator,stream or lease");
            var strong=refreshed with{RancorPrimary=refreshed.RancorPrimary with{Rank=20,DamagePercent=145,AttackRate=24}};NecromancerEffects.Profiles.Store(a,strong);
            at=Next(Actor(actor.Marker));SigilActors.Tick(at);Require(NecromancerEffects.Profiles.Find(a)==strong,"Weaker pulse changed stronger state or spent its RNG");

            // A new larger marker uses the shared one-slot replacement and
            // its own rank, not later owner gear. No paid cast is claimed here.
            var larger=SigilActors.CreatePrepared(p,Values(20),new(p.X+2,p.Y,p.Z),at.AddMilliseconds(40));Require(larger!=null&&Actor(actor.Marker).Retiring,"Shared Sigil replacement failed");
            SigilActors.Tick(larger.BornUtc);Require(World.FindItem((Serial)actor.Marker)==null&&SigilRancorPrimary.State(outside)?.Rank==20,"Terminal retirement or large-radius acquisition failed");
            var saved=SigilActors.Snapshot();string encoded=JsonSerializer.Serialize(saved);bool refused=false;
            try{SigilActors.Restore(saved with{Actors=saved.Actors.Select(s=>s.Marker==larger.Marker?s with{RancorPulse=int.MaxValue}:s).ToArray()});}catch(InvalidDataException){refused=true;}
            Require(refused&&JsonSerializer.Serialize(SigilActors.Snapshot())==encoded,"Malformed pulse cursor changed live roster");
            var before=NecromancerEffects.Profiles.Find(a);Detach(network,p);at=Next(Actor(larger.Marker));SigilActors.Tick(at);
            Require(NecromancerEffects.Profiles.Find(a)==before,"Offline marker replayed recipient effects");
            FixtureNetwork.Authenticate(network,p,"RancorPulseReturn");p.MoveToWorld(p.Location,Map.Felucca);at=Next(Actor(larger.Marker));SigilActors.Tick(at);
            Require(NecromancerEffects.Profiles.Find(a)==before&&World.FindItem((Serial)larger.Marker)?.Map==Map.Trammel,"Facet separation followed owner or kept pulsing");p.MoveToWorld(p.Location,Map.Trammel);
            at=Next(Actor(larger.Marker));SigilActors.Tick(at);Require(NecromancerEffects.Profiles.Find(a).RancorMind.Seed!=before.RancorMind.Seed,"Return did not resume one new pulse");
            var final=NecromancerEffects.Profiles.Find(a);var curse=new NecromancerCurse(CombatAbility.Attract,p.Serial.Value,1,at.AddSeconds(30),p.X,p.Y);
            var ordinary=SourceMind.Attract(a,final,b,750,at);NecromancerEffects.Profiles.Store(a,final with{Curse=curse,Mind=ordinary});
            Require(SigilRancorPulseProposal.ExpirePrepared(a,at.AddMilliseconds(480))&&NecromancerEffects.Curse(a)==curse&&NecromancerEffects.Profiles.Find(a).Mind==ordinary,"Expiry removed protected ordinary AI");
            Require(!SigilRancorPulseProposal.ExpirePrepared(a,at.AddMilliseconds(480)),"Expiry cleanup replayed");
            p.Kill();SigilActors.Tick(at.AddMilliseconds(520));Require(!SigilActors.Snapshot().Actors.Any(s=>s.Owner==p.Serial.Value),"Owner death retained marker");
            Require(!SigilRancorPulseProposal.Approved&&!WarlockCombat.Implemented(CombatAbility.SigilRancor),"Candidate enabled automatic learned Rancor");
        }
        finally
        {SigilActors.Pulse-=Pulse;Detach(network,p);SigilActors.Clear(p);foreach(var t in new[]{a,b,outside,high,plain,ally}){NecromancerEffects.Profiles.Clear(t);t.Delete();}p.Delete();}
        Log.Information("Rancor pulse candidate:claimed once,74/75boundary,primary on failure,rank/radius refresh,shared roster,offline/facet/death and independent expiry pass;automatic input gated.");
    }
    private static void Check(NetState network,bool reload)
    {
        Exercise(network);PlayerMobile p;CleaveActionTarget target;SigilActor actor;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=World.FindMobile((Serial)s.Owner)as PlayerMobile;target=World.FindMobile((Serial)s.Target)as CleaveActionTarget;actor=Actor(s.Actor.Marker);
            Require(p!=null&&target!=null&&actor.BornUtc==s.Actor.BornUtc&&actor.Frames==s.Actor.Frames&&actor.RancorPulse==s.Actor.RancorPulse
                &&NecromancerEffects.Profiles.Find(target)==s.State&&SkillHealth.Balance256(target)==s.Life256,"Original marker/cursor/recipient RNG/wounds changed");
        }
        else
        {
            p=CleaveActionChecks.Fresh();target=Enemy(p,3);actor=SigilActors.CreatePrepared(p,Values(200),new(p.X+2,p.Y,p.Z),TimedEffects.Now);
            SkillHealth.SetBalance256(target,target.HitsMax*256-129);
        }
        FixtureNetwork.Authenticate(network,p,"RancorPulseSaved");var old=NecromancerEffects.Profiles.Find(target)?.RancorMind;ulong expected=old?.Seed??SourceRandom.InitLow(Seed(0));SourceRandom.Limited(ref expected,100);
        int calls=0;void Pulse(SigilActor a,PlayerMobile owner,DateTime at){if(a.Marker==actor.Marker){var r=SigilRancorPulseProposal.PulsePrepared(a,owner,at,()=>Seed(0));Require(r is{Claimed:true,Recipients:1},"Saved pulse recipient set");calls++;}}
        SigilActors.Pulse+=Pulse;
        try{SigilActors.Tick(Next(actor));Require(calls==1&&NecromancerEffects.Profiles.Find(target).RancorMind.Seed==expected,"Original stream did not continue exactly one pulse");}
        finally{SigilActors.Pulse-=Pulse;}
        var state=NecromancerEffects.Profiles.Find(target);state=state with{RancorPrimary=state.RancorPrimary with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)},RancorMind=state.RancorMind with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}};
        NecromancerEffects.Profiles.Store(target,state);actor=Actor(actor.Marker);Detach(network,p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(generation,p.Serial.Value,target.Serial.Value,actor,state,SkillHealth.Balance256(target))));
    }
}
