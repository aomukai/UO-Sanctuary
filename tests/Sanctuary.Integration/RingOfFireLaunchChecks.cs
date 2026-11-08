// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class RingOfFireLaunchChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(RingOfFireLaunchChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"ring-of-fire-launch-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true}){string name=reload?"ring-of-fire-launch-reload-check":"ring-of-fire-launch-check";ConsoleInputHandler.RegisterCommand(name,"Current captured Ring launch composition over explicit service boundaries",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static void Check(bool reload)
    {
        int generation=reload?int.Parse(File.ReadAllText(Manifest))+1:0;
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"ring-launch-current-cases.json")));var root=json.RootElement;int n=0;
        foreach(var vector in root.GetProperty("vectors").EnumerateArray()){var point=FrozenOrbs.Direction(n++);Require(point.X==vector[0].GetInt32()&&point.Y==vector[1].GetInt32(),"Existing shared vectors differ from owned current tables");}Require(n==64,"Missing current vectors");n=0;
        var world=NpcWorldAllocation.Capture();var requests=new List<int[]>();var trace=new List<int>();
        foreach(var row in root.GetProperty("cases").EnumerateArray())
        {
            requests.Clear();trace.Clear();var c=Ints(row.GetProperty("input"));uint flags=(uint)c[15];
            var context=new RingOfFireLaunchContext((uint)c[0],c[5],c[6]!=0,c[7],c[8],c[9],c[10],c[4],c[3],c[14]);
            int result=RingOfFireLaunch.Run(context,new(()=>flags|=64,()=>{trace.Add(1);return c[1]!=0;},()=>{trace.Add(2);return c[2];},m=>{Require(m==c[2],"Velocity getter lost actual missile");trace.Add(3);return c[11];},field=>{trace.Add(field==0x198?4:5);Require(field is 0x198 or 0x194,"Unknown current formula field");return field==0x198?c[12]:c[13];},r=>{trace.Add(6);requests.Add([r.Flags,(int)r.Caster,r.Missile,r.X,r.Y,r.TargetX,r.TargetY,r.Velocity,r.Skill,r.Rank,r.Context12c]);return c[16]==0;}));
            var expected=row.GetProperty("output");Require(result==expected.GetProperty("result").GetInt32()&&flags==expected.GetProperty("flags").GetUInt32()&&trace.SequenceEqual(Ints(expected.GetProperty("trace"))),"Current launch guards/mark/order differ from captured instructions");
            var entries=expected.GetProperty("requests").EnumerateArray().Select(Ints).ToArray();Require(entries.Length==requests.Count&&entries.Where((r,i)=>!r.SequenceEqual(requests[i])).Any()==false,"Current launch requests/count/coordinates/flags differ");n++;
        }
        Require(n==148&&NpcWorldAllocation.Capture()==world&&!WarlockCombat.Implemented(CombatAbility.RingOfFire),"Prepared launch allocated world state or enabled unfinished damage");
        File.WriteAllText(Manifest,generation.ToString());Require(generation<=2,"Ring fixture generation changed");
        Log.Information("Ring launch:148current x64 contexts,64existing vectors,ordered guards/formula calls,source path widths,flags3/7,count rounding including signed edges,failed allocation continuation and return match. World factory,damage17,player resources and learned route remain separate.");
    }
}
