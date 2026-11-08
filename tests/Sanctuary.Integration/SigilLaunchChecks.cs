// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class SigilLaunchChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SigilLaunchChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sigil-launch-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true}){string name=reload?"sigil-launch-reload-check":"sigil-launch-check";ConsoleInputHandler.RegisterCommand(name,"Current shared Sigil launch composition over explicit services",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static long[] Numbers(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt64()).ToArray();
    private static void Check(bool reload)
    {
        int generation=reload?int.Parse(File.ReadAllText(Manifest))+1:0;
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sigil-launch-cases.json")));int n=0;var world=NpcWorldAllocation.Capture();
        foreach(var row in json.RootElement.GetProperty("cases").EnumerateArray())
        {
            var input=row.GetProperty("input");int I(string k)=>input.GetProperty(k).GetInt32();bool B(string k)=>input.GetProperty(k).GetBoolean();
            var c=new SigilLaunchContext(B("caster")?1U:0,I("skill"),I("rank"),I("selector"),I("count"),I("context"),I("state"),I("frame"));
            var expected=row.GetProperty("output");var trace=new List<long[]>();var requests=new List<long[]>();uint flags=(uint)I("flags");
            int actual=SigilLaunch.Run(c,new(()=>{trace.Add([1]);return B("row");},()=>{trace.Add([2]);return I("missile");},()=>flags|=64,
                id=>{Require(id==I("missile"),"Changed placement missile");trace.Add([3]);return B("place");},()=>{trace.Add([4]);return new(I("x"),I("y"));},point=>{Require(point==new Point2D(I("x"),I("y")),"Changed distance point");trace.Add([5]);return I("distance");},r=>
                {Require(r.Caster==1,"Lost actual caster");trace.Add([6]);requests.Add([r.Body.Flags,r.Body.Missile,r.Body.Point.X,r.Body.Point.Y,r.Body.Skill,r.Body.Rank,r.Context12c]);return B("allocate")?new(2,I("kind"),B("data"),input.GetProperty("milliseconds").GetSingle()):null;},field=>{Require(field==0x198,"Changed limit formula field");trace.Add([7]);return I("limit");},r=>
                {Require(r.Missile==2&&r.Limit==I("limit")&&r.State==I("state")&&r.RemovalCallbackRva==0x3a7f30,"Changed tracking identity/context/callback");trace.Add([9,r.Start,r.End]);return B("track");},end=>trace.Add([10,unchecked((uint)end)])));
            // The source rounding helper is executed in the oracle; its
            // entry marker is not an external C# service.
            var wanted=expected.GetProperty("trace").EnumerateArray().Select(Numbers).Where(t=>t[0]!=8).ToArray();var wantedRequests=expected.GetProperty("requests").EnumerateArray().Select(Numbers).ToArray();
            Require(actual==expected.GetProperty("result").GetInt32()&&flags==expected.GetProperty("flags").GetUInt32()&&trace.Count==wanted.Length&&!trace.Where((t,i)=>!t.SequenceEqual(wanted[i])).Any(),"Current Sigil guard/service/flag/tracking order or result differs");
            Require(requests.Count==wantedRequests.Length&&!requests.Where((r,i)=>!r.SequenceEqual(wantedRequests[i])).Any(),"Sigil factory request differs");n++;
        }
        Require(n==126&&NpcWorldAllocation.Capture()==world&&new[]{CombatAbility.SigilLethargy,CombatAbility.SigilRancor,CombatAbility.SigilDeath}.All(a=>!WarlockCombat.Implemented(a)),"Prepared launch changed world allocation or admitted unfinished Sigil status");
        File.WriteAllText(Manifest,generation.ToString());Require(generation<=2,"Unexpected fixture generation");
        Log.Information("Sigil launch:126current do157contexts;all three skills,rank/selector guards,mark/placement/coordinates/distance,factory failure,limit/state tracking,finite current rounding,expiry overflow and event ordering match. Recipient effects,tracking cleanup and learned casts remain separate.");
    }
}
