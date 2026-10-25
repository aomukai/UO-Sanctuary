// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class SentryAimChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SentryAimChecks));
    private sealed record Saved(uint Owner,uint Actor,uint FormerTarget,int Map,Point3D Origin,SentryBodyState State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sentry-aim-fixture.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("sentry-aim-check",Check);Register("sentry-aim-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Sentry cached path aim, moving/deleted target and actual pending-cast restoration",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Point2D Cell(Mobile m)=>new(m.X*2,m.Y*2);
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sentry-aim-source-probe.json")));int cases=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var input=row.GetProperty("input");int N(int i)=>input[i].GetInt32();
            Point2D old=new(N(3)==0?0:88,N(3)==0?0:89),target=new(N(1),N(2));
            var cached=AssassinSentryAim.Begin(old,target);var expected=row.GetProperty("cached");
            Require(cached==new Point2D(expected[0].GetInt32(),expected[1].GetInt32()),"Complete source stationary target cache "+cases);
            Point2D? present=N(4) switch{1=>new(115,109),3 or 4=>null,_=>target};
            var resolved=AssassinSentryAim.Resolve(cached,present);expected=row.GetProperty("resolved");
            Require(resolved==new Point2D(expected[0].GetInt32(),expected[1].GetInt32())
                &&(resolved.X!=0&&resolved.Y!=0)==(row.GetProperty("success").GetInt32()!=0),"Complete source current/fallback aim "+cases);
            cases++;
        }
        Require(cases==100,"Incomplete Sentry source aim fixture");
        bool refused=false;try{AssassinSentryAim.Resolve(null,null);}catch(NotSupportedException){refused=true;}
        Require(refused&&AssassinSentryAim.Resolve(null,new(123,125))==new Point2D(123,125),"Historical missing cache invented a fallback or prevented live target use");
        Log.Information("Sentry aim:100 complete stationary source initializer/registry/getter cases match; moving, dead-present, missing, self and zero-coordinate transitions pass");
    }
    private static BladeMissileTarget Target(PlayerMobile p)
    {
        var target=new BladeMissileTarget{NoKillAwards=true};target.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);target.AIObject?.AITimer.Stop();return target;
    }
    private static AssassinSentryBody Pending(PlayerMobile p,Mobile target)
    {
        var b=AssassinSentryBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),DateTime.UtcNow.AddDays(1),SourceRandom.InitLow(29));
        Require(b.Prepared.CachedAim==new Point2D(0,0),"Fresh dynamic path did not start with zero cache");
        var select=AssassinSentryActors.SelectTarget;AssassinSentryActors.SelectTarget=_=>new(target.Serial.Value,10);
        try{for(int i=0;i<25;i++){b.AdvanceController(b.Prepared.NextFrameUtc,true);}}
        finally{AssassinSentryActors.SelectTarget=select;}
        Require(b.Prepared.Control is{Mode:14,Dispatches:0}&&b.Prepared.CachedAim==Cell(target),"Actual attack start did not capture its stationary path aim");return b;
    }
    private static void Finish(AssassinSentryBody b,Point2D aim)
    {
        while(b.Prepared.Control.Dispatches==0){b.AdvanceController(b.Prepared.NextFrameUtc,true);Require(b.Prepared.Control.Frame<=28,"Pending aim did not dispatch on its source frame");}
        var bolts=AssassinSentryBolts.Snapshot().Bolts.Where(x=>x.Actor==b.Serial.Value).OrderBy(x=>x.Id).ToArray();
        Require(bolts.Length==AssassinSentryCreation.Bolts(b.Prepared.Creation),"Cached-aim dispatch lost individual missiles");
        int heading=ChargedBolts.Heading(aim.X-b.X*2,aim.Y-b.Y*2);
        for(int i=0;i<bolts.Length;i++)
        {Require(bolts[i].Path.SequenceEqual(ChargedBolts.Path(ChargedBolts.SeedSubtile(aim.X,i),heading,77)),"Actual missile factory did not consume the resolved aim");}
    }
    private static void Cleanup(AssassinSentryBody b)
    {
        var all=AssassinSentryBolts.Snapshot();AssassinSentryBolts.Restore(all with{Bolts=all.Bolts.Where(x=>x.Actor!=b.Serial.Value).ToArray()});b.Delete();
    }
    private static void Persist(PlayerMobile p)
    {
        var target=Target(p);var b=Pending(p,target);target.Delete();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,target.Serial.Value,p.Map.MapID,p.Location,b.Prepared)));
    }
    private static void Check()
    {
        Differential();var p=BladeBodyChecks.Fresh(6320,40);p.Name="SentryAimFixture";
        var target=Target(p);var b=Pending(p,target);Point2D captured=Cell(target);
        var legacy=JsonNode.Parse(JsonSerializer.Serialize(b.Prepared)).AsObject();legacy.Remove("CachedAim");
        Require(JsonSerializer.Deserialize<SentryBodyState>(legacy.ToJsonString()).CachedAim==null,"Historical save lost unknown-cache distinction");
        target.MoveToWorld(new(target.X+2,target.Y+2,target.Z),target.Map);Finish(b,Cell(target));
        Require(b.Prepared.CachedAim==captured,"Live getter rewrote the path cache during the windup");Cleanup(b);target.Delete();
        target=Target(p);b=Pending(p,target);captured=Cell(target);target.MoveToWorld(new(target.X+2,target.Y+2,target.Z),target.Map);target.Delete();Finish(b,captured);Cleanup(b);
        var empty=AssassinSentryBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),DateTime.UtcNow.AddDays(1),SourceRandom.InitLow(1));
        AssassinSentryBolts.Dispatch(empty,0);Require(!AssassinSentryBolts.Snapshot().Bolts.Any(x=>x.Actor==empty.Serial.Value),"Zero fallback launched invented origin missiles");Cleanup(empty);
        Persist(p);Require(!AssassinCombat.Implemented(CombatAbility.ChargedBoltSentry),"Aim backend enabled unfinished input");
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var b=(AssassinSentryBody)World.FindMobile((Serial)saved.Actor);
        Require(JsonSerializer.Serialize(b.Prepared)==JsonSerializer.Serialize(saved.State)&&World.FindMobile((Serial)saved.FormerTarget)==null,"Pending actor aim/target identity changed across actual reload");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);Finish(b,saved.State.CachedAim.Value);Cleanup(b);Persist(p);
        Require(!AssassinCombat.Implemented(CombatAbility.ChargedBoltSentry),"Restored aim enabled unfinished input");
    }
}
