// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class SentryUnitPathChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SentryUnitPathChecks));
    private sealed record Saved(uint Owner,uint Actor,uint Primary,uint Low,uint[] Walls,Point3D Origin,int Map,SentryBodyState State,int Life256,int Mana256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sentry-unit-path-fixture.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("sentry-unit-path-check",Check);Register("sentry-unit-path-reload-check",Reload);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)){var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Stop(World.FindMobile((Serial)s.Primary) as BaseCreature);Stop(World.FindMobile((Serial)s.Low) as BaseCreature);}};
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Bound Sentry stationary unit path, low-threat choice and actual saved controller",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Point2D Point(JsonElement e)=>new(e[0].GetInt32(),e[1].GetInt32());
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sentry-unit-path-source-probe.json")));int cases=0,total=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var input=row.GetProperty("input");int N(int i)=>input[i].GetInt32();var target=new Point2D(100+N(1),100+N(2));var queries=new List<Point2D>();
            int Mask(int x,int y)
            {
                queries.Add(new(x,y));if(x<80||x>=128||y<80||y>=128){return 0x3c01;}
                return N(0) switch{1 when x==103=>1,2 when x>=97&&x<=103&&y>=97&&y<=103&&(x==97||x==103||y==97||y==103)=>1,
                    3 when y==102=>0x100,4 when x==target.X&&y==target.Y=>1,5 when x==100&&y==100=>1,_=>0};
            }
            var points=AssassinSentryUnitPath.Points((uint)((100<<16)+N(3)),(uint)((100<<16)+N(3)),target,N(4),new(80,80,48,48),Mask);
            Require(points.Length==row.GetProperty("count").GetInt32()&&points.SequenceEqual(row.GetProperty("points").EnumerateArray().Select(Point)),"Full composed source unit-path waypoint differential "+cases);
            Require(queries.SequenceEqual(row.GetProperty("queries").EnumerateArray().Select(Point)),"Full composed source unit-path collision order "+cases);
            total+=queries.Count;cases++;
        }
        Require(cases==4032,"Unit-path source case inventory");
        foreach(var target in new[]{new Point2D(0,104),new Point2D(110,0),new Point2D(0,0)})
        {Require(AssassinSentryUnitPath.Points(100*65536u+32768,100*65536u+32768,target,14,new(80,80,48,48),(_,_)=>throw new InvalidOperationException("Zero target queried collision")).Length==0,"Either-zero unit target reached point-path code");}
        Require(AssassinSentryWorldTargets.PrimaryPath?.Method.DeclaringType==typeof(AssassinSentryUnitPath),"Production unit-path service is unbound/replaced");
        Log.Information("Sentry unit path: {Count} complete source compositions / {Queries} exact collision queries match; zero-coordinate unit rejection retained",cases,total);
    }
    private static void Stop(BaseCreature c){if(c!=null){c.Frozen=true;c.CantWalk=true;c.AIObject?.AITimer.Stop();}}
    private static void LowThreat(BaseCreature low,Action action)
    {
        var variants=(Dictionary<(string,MonsterDifficulty),CreatureVariant>)typeof(CreatureProfiles).GetField("_variants",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
        var key=(CreatureProfiles.Definition(low).Type,MonsterVariants.Difficulty(low));var old=variants[key];
        try{variants[key]=old with{Targeting=old.Targeting with{Threat=1}};Require(CreatureProfiles.For(low).Targeting.Threat==1,"Scoped low-threat service");action();}
        finally{variants[key]=old;}
    }
    private static uint[] Walls(AssassinSentryBody b)
    {
        int id=Enumerable.Range(0x100,TileData.ItemTable.Length-0x100).First(i=>TileData.ItemTable[i].Impassable
            &&TileData.ItemTable[i].Height>=16&&(TileData.ItemTable[i].Flags&(TileFlag.NoShoot|TileFlag.Window))==0);
        var result=new List<uint>();for(int x=0;x<2;x++)for(int y=0;y<2;y++){var item=new Static(id);item.MoveToWorld(new(b.X+x,b.Y+y,b.Z),b.Map);result.Add(item.Serial.Value);}return result.ToArray();
    }
    private static void RemoveWalls(uint[] walls){foreach(uint id in walls){World.FindItem((Serial)id)?.Delete();}}
    private static Saved Stage(PlayerMobile p)
    {
        var b=AssassinSentryBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now.AddDays(1),SourceRandom.InitLow(91));
        var primary=new GraveyardSkeleton{Name="SentryUnitPathPrimary",NoKillAwards=true};primary.MoveToWorld(new(b.X+6,b.Y,b.Z),b.Map);Stop(primary);
        var low=new GraveyardZombie{Name="SentryUnitPathLow",NoKillAwards=true};low.MoveToWorld(new(b.X+2,b.Y,b.Z),b.Map);Stop(low);
        var walls=Walls(b);SkillHealth.SetBalance256(b,12850);
        return new(p.Serial.Value,b.Serial.Value,primary.Serial.Value,low.Serial.Value,walls,p.Location,p.Map.MapID,b.Prepared,SkillHealth.Balance256(b),NecromancerEffects.ManaBalance256(p));
    }
    private static void Clean(Saved s)
    {RemoveWalls(s.Walls);World.FindMobile((Serial)s.Primary)?.Delete();World.FindMobile((Serial)s.Low)?.Delete();World.FindMobile((Serial)s.Actor)?.Delete();}
    private static void Check()
    {
        Differential();var p=BladeBodyChecks.Fresh(6520,40);p.Name="SentryUnitPathFixture";var s=Stage(p);
        var b=(AssassinSentryBody)World.FindMobile((Serial)s.Actor);var primary=(BaseCreature)World.FindMobile((Serial)s.Primary);var low=(BaseCreature)World.FindMobile((Serial)s.Low);
        try
        {
            LowThreat(low,()=>
            {
                Require(b.InLOS(primary)&&b.InLOS(low),"Walk-only blockers also block missile visibility");
                Require(!AssassinSentryUnitPath.Probe(b,primary)&&AssassinSentryWorldTargets.Select(b)?.Serial==low.Serial.Value,"Unreachable primary did not yield close low-threat alternative");
                var third=new GraveyardSkeleton{Name="SentryUnitPathFallback",NoKillAwards=true};third.MoveToWorld(new(b.X+8,b.Y,b.Z),b.Map);Stop(third);
                try{Require(AssassinSentryWorldTargets.Select(b)?.Serial==third.Serial.Value,"Callback7 did not choose the other primary below distance20");}finally{third.Delete();}
                var near=low.Location;low.MoveToWorld(new(b.X+5,b.Y,b.Z),b.Map);
                Require(AssassinSentryWorldTargets.Select(b)?.Serial==primary.Serial.Value,"Distant low-threat target incorrectly invoked close fallback");low.MoveToWorld(near,b.Map);
                RemoveWalls(s.Walls);Require(AssassinSentryUnitPath.Probe(b,primary)&&AssassinSentryWorldTargets.Select(b)?.Serial==primary.Serial.Value,"Reachable primary was replaced by low threat");
                s=s with{Walls=Walls(b)};
                p.MoveToWorld(s.Origin,Map.Felucca);Require(AssassinSentryWorldTargets.Select(b)?.Serial==low.Serial.Value,"Owner facet changed actor-local low-threat choice");p.MoveToWorld(s.Origin,Map.Maps[s.Map]);
            });
            Require(JsonSerializer.Serialize(b.Prepared)==JsonSerializer.Serialize(s.State)&&SkillHealth.Balance256(b)==s.Life256
                &&NecromancerEffects.ManaBalance256(p)==s.Mana256,"Reachability inspection mutated saved aim/controller/RNG/clock/pose/resources");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(s));s=null;
            Require(!AssassinCombat.Implemented(CombatAbility.ChargedBoltSentry),"Unit path enabled unfinished player input");
        }
        finally{if(s!=null){Clean(s);p.Delete();}}
    }
    private static void Reload()
    {
        Differential();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        p.MoveToWorld(s.Origin,Map.Maps[s.Map]);
        var b=(AssassinSentryBody)World.FindMobile((Serial)s.Actor);var low=(BaseCreature)World.FindMobile((Serial)s.Low);
        Require(JsonSerializer.Serialize(b.Prepared)==JsonSerializer.Serialize(s.State)&&SkillHealth.Balance256(b)==s.Life256
            &&NecromancerEffects.ManaBalance256(p)==s.Mana256,"Saved low-threat scene changed on actual reload");
        LowThreat(low,()=>
        {
            Require(AssassinSentryWorldTargets.Select(b)?.Serial==low.Serial.Value,"Restored low-threat scene lost bound path query");
            b.AdvanceController(b.Prepared.NextFrameUtc.AddMilliseconds(24*40),true);
            Require(b.Prepared.Control is{Mode:14,Thinks:1,Attempts:1}&&b.Prepared.Control.Target==low.Serial.Value&&b.Prepared.ControllerFault==null
                &&b.Prepared.CachedAim==new Point2D(low.X*2,low.Y*2),"Actual restored controller faulted or did not wind up on low threat");
        });
        Clean(s);s=Stage(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(s));
    }
}
