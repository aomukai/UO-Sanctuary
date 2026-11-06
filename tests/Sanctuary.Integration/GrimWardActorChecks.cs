// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class GrimWardActorChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrimWardActorChecks));
    private sealed record Saved(uint Owner,uint Corpse,uint Loot,int Mana,int Life,GrimWardActor[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"grim-ward-actor-fixtures.json");
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {
            bool again=reload;string command=reload?"grim-ward-actor-reload-check":"grim-ward-actor-check";
            ConsoleInputHandler.RegisterCommand(command,"Ward original corpse/child/clock/context/owner/save",_=>Core.LoopContext.Post(()=>
            {
                try{Log.Information("{Name}: passed",command);FixtureNetwork.Connected(command+"-result",network=>
                {
                    try{if(again){Reload(network);}else{Check(network);}Log.Information("{Result}: passed",command+"-result");}
                    catch(Exception e){Log.Error(e,"{Result}: failed",command+"-result");}
                });}
                catch(Exception e){Log.Error(e,"{Name}: failed",command);}
            }));
        }
    }
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int rank,int hard)=>new(SkillMechanics.For(CombatAbility.GrimWard),k=>k=="Grim Ward"?rank:255,k=>k=="Find Potion"?hard:0,99);
    private static GrimWardActor State(PlayerMobile p)=>GrimWardActors.Snapshot().Single(s=>s.Owner==p.Serial.Value);
    private static void Invalid(GrimWardActor s)
    {
        var before=GrimWardActors.Snapshot();bool refused=false;
        try{GrimWardActors.Restore([s]);}catch(InvalidDataException){refused=true;}
        Require(refused&&before.SequenceEqual(GrimWardActors.Snapshot()),"Malformed Ward state replaced current actors");
    }
    private static void Check(NetState network)
    {
        var p=DoubleSwingChecks.Owner(3,ability:CombatAbility.GrimWard);var account=FixtureNetwork.Authenticate(network,p,"WardActor");
        var corpse=FindPotionChecks.Body(p);var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
        target.SetResistance(ResistanceType.Physical,100);var loot=corpse.Items.Single();
        var start=TimedEffects.Now;int mana=NecromancerEffects.ManaBalance256(p),life=p.Hits;
        try
        {
            Require(GrimWardActors.Target(p,corpse),"Real soft corpse refused");
            var s=GrimWardActors.CreatePrepared(p,corpse,Values(3,20),start);
            Require(s is{StartClass:252,BodyClass:253,StartFrames:8,Phase:WardPhase.Start,Rank:3,FindPotion:20,LastPulse:-1}
                &&CorpseSkillRules.NoSelect(corpse)&&loot.Parent==corpse&&corpse.CanLoot(p,null),"Startup context/size/reservation/retained loot mismatch");
            uint original=s.Marker;
            Require(GrimWardActors.CreatePrepared(p,corpse,Values(3,20),start)==null,"Used corpse created another Ward");
            GrimWardActors.Tick(start.AddMilliseconds(7*40));Require(State(p).Marker==original&&NecromancerEffects.Curse(target)==null,"Ward emitted during startup");
            GrimWardActors.Tick(start.AddMilliseconds(8*40));s=State(p);
            var curse=NecromancerEffects.Curse(target);
            Require(s is{Phase:WardPhase.Body,LastPulse:0}&&s.Marker!=original&&World.FindItem((Serial)original)==null
                &&curse?.Source==s.Marker&&curse.PhysicalDelta==-24&&curse.Ward.FullPhysical==-120
                &&curse.Terror.Override==p.Serial.Value,"First child/pulse,owned hard synergy,immunity reduction or initial fear source missing");
            var first=curse;GrimWardActors.Tick(start.AddMilliseconds(8*40));Require(NecromancerEffects.Curse(target)==first,"Duplicate tick replayed a pulse");
            // Actual owner movement and equipment changes cannot move or rewrite
            // the captured Ward. The target is not initially fighting its caster.
            p.MoveToWorld(new(p.X+12,p.Y,p.Z),p.Map);var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");
            NecromancerEffects.Profiles.Clear(target);GrimWardActors.Tick(start.AddMilliseconds(14*40));
            curse=NecromancerEffects.Curse(target);Require(curse is{Rank:3,PhysicalDelta:-24}&&curse.Source==s.Marker&&State(p).Origin==s.Origin,"Pulse followed owner or re-read gear ranks");
            NecromancerEffects.Profiles.Clear(target);var map=p.Map;var point=p.Location;
            p.MoveToWorld(point,map==Map.Trammel?Map.Felucca:Map.Trammel);GrimWardActors.Tick(start.AddMilliseconds(20*40));
            Require(State(p).LastPulse==12&&World.FindItem((Serial)s.Marker).Map==map&&NecromancerEffects.Curse(target)==null,"Foreign facet Ward travelled or kept fighting");
            p.MoveToWorld(point,map);GrimWardActors.Tick(start.AddMilliseconds(20*40));Require(NecromancerEffects.Curse(target)==null,"Facet return replayed skipped combat");
            network.Mobile=null;p.NetState=null;GrimWardActors.Tick(start.AddMilliseconds(26*40));
            Require(NecromancerEffects.Curse(target)==null&&State(p).LastPulse==18,"Disconnected owner kept combat or paused clock");
            network.Mobile=p;p.NetState=network;GrimWardActors.Tick(start.AddMilliseconds(26*40));Require(NecromancerEffects.Curse(target)==null,"Reconnect replayed skipped combat");
            GrimWardActors.Tick(start.AddMilliseconds(32*40));Require(NecromancerEffects.Curse(target)?.Rank==3&&State(p).LastPulse==24,"Next new pulse did not resume");
            NecromancerEffects.Profiles.Clear(target);GrimWardActors.Tick(start.AddMilliseconds((8+996)*40));
            Require(State(p).LastPulse==996&&NecromancerEffects.Curse(target)!=null,"Last body pulse missing");
            NecromancerEffects.Profiles.Clear(target);GrimWardActors.Tick(start.AddMilliseconds((8+1000)*40));
            Require(State(p).Phase==WardPhase.End&&NecromancerEffects.Curse(target)==null&&World.FindItem((Serial)s.Marker)!=null,"Body expiry kept pulses or skipped stop presentation");
            GrimWardActors.Tick(start.AddMilliseconds((8+1009)*40));Require(!GrimWardActors.Snapshot().Any(x=>x.Owner==p.Serial.Value)&&World.FindItem((Serial)s.Marker)==null,"Expired marker survived");
            Require(NecromancerEffects.ManaBalance256(p)==mana&&p.Hits==life&&loot.Parent==corpse,"Prepared controller paid mana,healed or consumed original loot");
            p.AddToBackpack(helm);p.MoveToWorld(new(point.X-12,point.Y,point.Z),map);
            // Preserve original world actors with explicit inspection-only UTC
            // holds. Natural epoch boundaries above used the normal durations.
            var body=FindPotionChecks.Body(p);DoubleSwingChecks.Ready(p);
            Require(ClassCombat.BeginCorpse(p,CombatAbility.GrimWard,body.Serial,body.Location,Core.TickCount)
                &&NecromancerEffects.ManaBalance256(p)==mana-1024&&!CorpseSkillRules.NoSelect(body),"Paid saved learned start");
            long effect=ClassCombat.For(p).EffectAt;
            Require(ClassCombat.ResolveAction(p,effect)&&!ClassCombat.ResolveAction(p,effect),"Saved learned release/duplicate");
            s=State(p);Require(s.Rank==3&&s.FindPotion==1,"Saved learned release did not snapshot real ranks");
            GrimWardActors.Tick(s.BornUtc.AddMilliseconds(8*40));s=State(p);mana-=1024;
            GrimWardActors.Restore(GrimWardActors.Snapshot().Select(x=>x.Owner==p.Serial.Value?x with{BornUtc=TimedEffects.Now.AddHours(1)}:x).ToArray());
            s=State(p);Invalid(s with{Rank=0});Invalid(s with{LastPulse=7});Invalid(s with{Marker=p.Serial.Value});Invalid(s with{StartFrames=5});
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,corpse.Serial.Value,loot.Serial.Value,mana,life,[s],0)));
            Log.Information("Ward actor:real corpse,start-to-new-child,first/last pulse,duplicate/foreign-facet/offline suppression,captured rank/hard synergy,immunity/initial threat,expiry/loot and malformed save checks pass;inspection UTC hold explicit;learned cast still separate");
        }
        finally{NecromancerEffects.Profiles.Clear(target);target.Delete();p.Internalize();}
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        FixtureNetwork.Authenticate(network,p,"WardReload");
        Require(GrimWardActors.Snapshot().Where(x=>x.Owner==s.Owner).SequenceEqual(s.Actors),"Original Ward payload changed on reload");
        Require(World.FindItem((Serial)s.Corpse) is Corpse{Deleted:false} corpse&&CorpseSkillRules.NoSelect(corpse)
            &&World.FindItem((Serial)s.Loot)?.Parent==corpse,"Original used corpse/loot reservation lost");
        Require(p.Hits==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Reload changed owner wounds/resources");
        foreach(var a in s.Actors){Require(World.FindItem((Serial)a.Marker) is{Deleted:false} marker&&marker.Map.MapID==a.Map&&marker.Location==a.Origin,"Original saved body marker changed");}
        p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
