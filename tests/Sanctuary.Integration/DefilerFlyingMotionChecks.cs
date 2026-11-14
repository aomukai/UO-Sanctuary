// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class DefilerFlyingMotionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DefilerFlyingMotionChecks));
    private sealed record Saved(int Generation,uint[] Bodies,ActorMotionState Motion);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"defiler-flying-motion-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"defiler-flying-motion-reload-check":"defiler-flying-motion-check";
            ConsoleInputHandler.RegisterCommand(name,"Defiler flying-mask shared kernel and current species inputs",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static void Check(bool reload)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"defiler-flying-motion-source-cases.json")));int contexts=0,frames=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var input=row.GetProperty("input");int wall=input[4].GetInt32(),mask=input[7].GetInt32();
            frames+=ActorMotionChecks.Compare(row,6,256,(x,y)=>x<80||x>=128||y<80||y>=128?0xffff:x==103?wall:0,contexts,mask);contexts++;
        }
        Require(contexts==192&&frames==3840,"Incomplete explicit flying-mask source comparison");
        var motion=DruidRavenMotion.Center(100,100,13)with{Flags=0x100,Velocity=1536,MaximumVelocity=1536};
        var room=new ActorPathRoom(80,80,48,48);
        foreach(int mask in new[]{0x3c01,0x3401,0x1804})foreach(int bit in new[]{1,4,0x100,0x400,0x800,0x1000,0x2000})
        {
            var path=SourceActorPath.Initialize(new(motion,new(108,100),new(100,100)),16,room,(x,y)=>x==103?bit:0,mask);
            Require((mask&bit)==0?path.Motion.Points.Any(p=>p.X>103):path.Motion.Points.All(p=>p.X<103),"Point-path planning ignored its explicit movement mask");
        }
        bool refused=false;try{SourceActorMotion.Advance(motion,(_,_)=>0,0);}catch(NotSupportedException){refused=true;}
        Require(refused,"Unqualified empty mask accepted");
        using var bodyDoc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"defiler-body-fixtures.json")));
        uint[] bodies=bodyDoc.RootElement.EnumerateArray().Select(x=>x.GetProperty("Body").GetUInt32()).ToArray();
        foreach(uint id in bodies)
        {
            var b=(WarlockDefilerBody)World.FindMobile((Serial)id);string text=b.SavedText;int life=SkillHealth.Balance256(b);
            int percent=100+b.Prepared.Snapshot.Birth.Suppliers.VelocityPercent;
            Require(DefilerFlyingMovement.Species is{ClassId:744,Size:2,Flying:true,Velocity:6,WalkSpeed:256}
                &&DefilerFlyingMovement.Speed(b,TimedEffects.Now)==(percent*1536/100,percent*256/100),"Original Defiler species/snapshot speed changed");
            Require(b.SavedText==text&&SkillHealth.Balance256(b)==life&&b.Map==Map.Internal,"Pure movement suppliers moved/healed or rewrote the parked original");
        }
        var saved=reload?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):new Saved(0,bodies,motion);
        Require(saved.Bodies.SequenceEqual(bodies)&&JsonSerializer.Serialize(saved.Motion)==JsonSerializer.Serialize(motion),"Flying component DTO/original body identities changed on reload");
        // Record equality compares array identity; compare serialized DTO for
        // empty arrays rather than claiming a saved native flight controller.
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+(reload?1:0)}));
        Require(!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Unfinished native flying host enabled learned Defiler");
        Log.Information("Defiler flying motion:192sourcecontexts/3840exactframes,explicit-mask path controls,current2x2/6/256suppliers and original parked bodies;native terrain/automatic host remain gated");
    }
}
