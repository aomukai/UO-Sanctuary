// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class SentryEmergenceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SentryEmergenceChecks));
    private sealed record Saved(uint Owner,uint Actor,uint Target,int Map,Point3D Origin,SentryBodyState State,int Life256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sentry-emergence-fixture.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("sentry-emergence-check",Check);Register("sentry-emergence-reload-check",Reload);Register("sentry-emergence-live-check",Live);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Saved Sentry S1 phase, original AI deadline and offline aging; player input gated",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sentry-emergence-source-probe.json")));int cases=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();int end=AssassinSentryEmergence.EndDelay(N(0));
            Require(N(3)==1&&end==N(4)&&N(5)==1&&N(7)==0
                &&AssassinSentryEmergence.NeutralThink(100+end,N(1)==0?0:100+N(1),N(2)!=0)-100==N(6),"Complete source S1 scheduler/end/neutral case "+cases);cases++;
        }
        Require(cases==40,"Incomplete S1 source fixture");
        int queries=0,dispatches=0;var c=AssassinSentryController.StartEmerging();
        for(int frame=1;frame<=28;frame++)
        {
            c=AssassinSentryController.Advance(c,true,true,false,true,true,()=>5,()=>{queries++;return new(1,10);},()=>true,()=>true,_=>dispatches++);
            if(frame<15){Require(c.Mode==8&&c.EmergenceEnd==15&&c.NextThink==25&&queries==0,"Emergence ran AI before its original deadline");}
            if(frame is >=15 and <25){Require(c.Mode==1&&c.NextThink==25&&queries==0,"S1 end restarted or advanced the AI clock");}
            if(frame==25){Require(c.Mode==14&&c.Thinks==1&&c.Attempts==1,"First source think was not birth+25");}
        }
        Require(dispatches==1&&queries==1&&c.Dispatches==1,"Emergence shifted or duplicated the birth+28 release");
        c=AssassinSentryController.StartEmerging();
        for(int frame=1;frame<25;frame++)
        {c=AssassinSentryController.Inactive(c,1);Require(c.Frame==frame&&c.NextThink==25&&c.Mode==(frame<15?8:1)&&c.Thinks==0,"Partial offline aging lost birth deadline");}
        var skipped=AssassinSentryController.Inactive(AssassinSentryController.StartEmerging(),100);
        Require(skipped is{Frame:100,Mode:1,NextThink:101,Thinks:0,Attempts:0,EmergenceEnd:0,Ai.RemainingShots:-1},"Long offline emergence replayed AI");
        var old=JsonSerializer.SerializeToNode(AssassinSentryController.Start()).AsObject();old.Remove("EmergenceEnd");old.Remove("EmergenceProfile");
        var restored=JsonSerializer.Deserialize<SentryControlState>(old.ToJsonString());AssassinSentryController.Validate(restored);
        Require(restored.EmergenceEnd==0&&restored.EmergenceProfile==null&&restored.Mode==1,"Old neutral save became a new S1 phase");
        Log.Information("Sentry emergence:40 complete source scheduler/neutral cases, no-action S1, end15/AI25/release28, partial/long offline clocks and old-save defaults pass");
    }
    private static BladeMissileTarget Target(PlayerMobile p)
    {
        var t=new BladeMissileTarget{NoKillAwards=true,Frozen=true,CantWalk=true};t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();t.SetHits(2000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Energy,0);return t;
    }
    private static AssassinSentryBody Create(PlayerMobile p,DateTime at)=>AssassinSentryBody.CreateEmerging(p,new(p.X+2,p.Y,p.Z),at,SourceRandom.InitLow(31));
    private static void Frames(AssassinSentryBody b,int count,bool actions)
    {for(int i=0;i<count;i++){b.AdvanceController(b.Prepared.NextFrameUtc,actions);}}
    private static void Cleanup(AssassinSentryBody b)
    {
        var all=AssassinSentryBolts.Snapshot();AssassinSentryBolts.Restore(all with{Bolts=all.Bolts.Where(x=>x.Actor!=b.Serial.Value).ToArray()});b.Delete();
    }
    private static void Persist(PlayerMobile p,BladeMissileTarget target)
    {
        target.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);target.AIObject?.AITimer.Stop();
        target.Hits=target.HitsMax;SkillHealth.Clear(target);var b=Create(p,DateTime.UtcNow.AddDays(1));Frames(b,7,false);SkillHealth.SetBalance256(b,12850);
        Require(b.Prepared.Control is{Mode:8,Frame:7,EmergenceEnd:15,NextThink:25}&&b.Prepared.Control.EmergenceProfile==AssassinSentryEmergence.Profile,"Actual pending S1 phase");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,target.Serial.Value,p.Map.MapID,p.Location,b.Prepared,SkillHealth.Balance256(b))));
    }
    private static void Check()
    {
        Differential();var p=BladeBodyChecks.Fresh(6360,40);p.Name="SentryEmergenceFixture";var target=Target(p);int mana=NecromancerEffects.ManaBalance256(p);
        var b=Create(p,DateTime.UtcNow.AddDays(1));ulong seed=b.Prepared.Seed;
        Frames(b,14,false);Require(b.Prepared.Control is{Mode:8,Frame:14,NextThink:25}&&b.Prepared.Seed==seed,"Offline S1 froze or consumed RNG");
        Frames(b,10,false);Require(b.Prepared.Control is{Mode:1,Frame:24,NextThink:25,Thinks:0,Ai.RemainingShots:-1},"Offline phase end fired early or replayed shots");
        Frames(b,4,true);Require(b.Prepared.Control is{Frame:28,Dispatches:1,Ai.RemainingShots:4},"Return before initial AI changed first actual volley");Cleanup(b);
        var list=Enumerable.Range(0,6).Select(_=>Create(p,DateTime.UtcNow.AddDays(1))).ToArray();
        Require(list[0].Deleted&&AssassinTrapRoster.For(p).Select(x=>x.Actor).SequenceEqual(list.Skip(1).Select(x=>x.Serial.Value)),"S1 actors did not share FIFO cap5");
        foreach(var body in list.Skip(1)){Cleanup(body);}
        b=Create(p,DateTime.UtcNow.AddDays(1));Frames(b,7,false);SkillHealth.SetBalance256(b,12850);var before=b.Prepared;var facets=SummonDifficulties.Facets();var map=p.Map;var point=p.Location;
        try
        {
            SummonDifficulties.SetFacets([new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);p.MoveToWorld(new(6000,2600,0),Map.Felucca);SummonDifficulties.Sync(p);
            Require(b.Map==map&&b.Location==before.Position&&b.Prepared.Creation.Difficulty==MonsterDifficulty.Hell
                &&JsonSerializer.Serialize(b.Prepared.Control)==JsonSerializer.Serialize(before.Control)&&b.Prepared.NextFrameUtc==before.NextFrameUtc
                &&SkillHealth.Balance256(b)==12850,"S1 facet scaling changed the phase/clock/cast facet/injury");
        }
        finally{p.MoveToWorld(point,map);SummonDifficulties.SetFacets(facets);SummonDifficulties.Sync(p);Cleanup(b);}
        Require(NecromancerEffects.ManaBalance256(p)==mana,"Actor preparation charged player input mana");Persist(p,target);
        Require(!AssassinCombat.Implemented(CombatAbility.ChargedBoltSentry),"S1 phase enabled unfinished player casting");
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var b=(AssassinSentryBody)World.FindMobile((Serial)saved.Actor);var target=(BladeMissileTarget)World.FindMobile((Serial)saved.Target);
        Require(JsonSerializer.Serialize(b.Prepared)==JsonSerializer.Serialize(saved.State)&&SkillHealth.Balance256(b)==saved.Life256,"Actual S1 world reload changed phase/profile/deadline/context/life");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);Frames(b,7,true);Require(b.Prepared.Control.Mode==8,"Restored S1 ended before source frame15");
        Frames(b,1,true);Require(b.Prepared.Control is{Mode:1,Frame:15,NextThink:25},"Restored S1 did not retain original AI25");
        Frames(b,13,true);Require(b.Prepared.Control is{Frame:28,Dispatches:1},"Restored S1 first volley was shifted or duplicated");
        int health=SkillHealth.Balance256(target);var ids=AssassinSentryBolts.Snapshot().Bolts.Where(x=>x.Actor==b.Serial.Value).Select(x=>x.Id).ToArray();
        Require(ids.Length>0,"Restored S1 produced no actual bolts");
        // Seeded charged bolts can all miss a small stationary target. Move
        // this receiver onto an emitted waypoint to verify real contact,
        // without changing projectile aim, motion, damage or its saved packet.
        var first=AssassinSentryBolts.State(ids[0]);var intercept=first.Path[1];
        target.MoveToWorld(new(first.OriginX+intercept.X,first.OriginY+intercept.Y,first.Z),b.Map);
        // These manually advanced day-ahead fixture clocks can overlap the
        // retained flights of other tests. Scope this inspection to its actor;
        // the connected tests exercise the unmodified shared natural clock.
        var all=AssassinSentryBolts.Snapshot();var foreign=all.Bolts.Where(x=>x.Actor!=b.Serial.Value).ToArray();
        AssassinSentryBolts.Restore(all with{Bolts=all.Bolts.Where(x=>x.Actor==b.Serial.Value).ToArray()});
        try
        {
            for(int i=0;i<30;i++)
            {var live=ids.Select(AssassinSentryBolts.State).Where(x=>x!=null).ToArray();if(live.Length==0){break;}AssassinSentryBolts.TickFrame(live.Min(x=>x.NextUtc),true);}
        }
        finally{var own=AssassinSentryBolts.Snapshot();AssassinSentryBolts.Restore(own with{Bolts=own.Bolts.Concat(foreign).ToArray()});}
        string detail=JsonSerializer.Serialize(ids.Select(AssassinSentryBolts.State).Where(x=>x!=null).Select(x=>new{x.Id,x.X,x.Y,x.Frame,x.Fault,x.Minimum256,x.Maximum256}));
        Require(SkillHealth.Balance256(target)<health,$"Resumed S1-to-controller volley caused no real damage: target={target.Serial} selected={b.Prepared.Control.Target} targetPose={target.Location}/{target.Map} actorPose={b.Location}/{b.Map} cached={b.Prepared.CachedAim} hp={health}->{SkillHealth.Balance256(target)} resist={target.EnergyResistance} active={detail}");Cleanup(b);Persist(p,target);
    }
    private static void Live()
    {
        var p=BladeBodyChecks.Fresh(6400,40);p.Name="SentryEmergenceLive";var target=Target(p);var net=NetState.Instances.Single(x=>x.Mobile==null&&IPAddress.IsLoopback(x.Address));
        net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();
        var b=Create(p,TimedEffects.Now);int health=SkillHealth.Balance256(target);
        var offline=BladeBodyChecks.Fresh(6440,40);var other=Target(offline);var held=Create(offline,TimedEffects.Now);ulong seed=held.Prepared.Seed;int otherHealth=SkillHealth.Balance256(other);
        Require(b.Prepared.Control.Mode==8&&held.Prepared.Control.Mode==8,"Live birth skipped S1");
        Timer.StartTimer(TimeSpan.FromMilliseconds(1500),()=>
        {
            try
            {
                Require(b.Prepared.Control.Dispatches==1&&SkillHealth.Balance256(target)<health,"Natural S1-to-AI25/release28 added delay or caused no damage");
                Require(offline.NetState==null&&held.Prepared.Control is{Mode:1,EmergenceEnd:0,Thinks:0,Attempts:0,Ai.RemainingShots:-1}
                    &&held.Prepared.Control.Frame>=15&&held.Prepared.Seed==seed&&SkillHealth.Balance256(other)==otherHealth,"Natural offline S1 froze or fought/replayed RNG");
                Log.Information("sentry-emergence-live-result: passed");
            }
            catch(Exception e){Log.Error(e,"sentry-emergence-live-result: failed");}
            finally
            {
                // NetState explicitly permits immediate Dispose for fixture cleanup.
#pragma warning disable CS0618
                net.Dispose();
#pragma warning restore CS0618
                Cleanup(b);Cleanup(held);target.Delete();other.Delete();p.Delete();offline.Delete();
            }
        });
    }
}
