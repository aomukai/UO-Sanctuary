// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class MissileDamageScaleChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(MissileDamageScaleChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"missile-damage-scale-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true}){string name=reload?"missile-damage-scale-reload-check":"missile-damage-scale-check";ConsoleInputHandler.RegisterCommand(name,"Current candidate missile packet scaling, without learned admission",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static void Check(bool reload)
    {
        int generation=reload?int.Parse(File.ReadAllText(Manifest))+1:0;
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"missile-damage-scale-cases.json")));int n=0;var world=NpcWorldAllocation.Capture();
        foreach(var row in json.RootElement.GetProperty("cases").EnumerateArray())
        {
            var input=row.GetProperty("input");int I(string k)=>input.GetProperty(k).GetInt32();bool B(string k)=>input.GetProperty(k).GetBoolean();
            var c=new MissileDamageScaleContext(B("present"),I("kind"),I("id"),I("selector"),B("data"),(short)I("word0e"),B("ownerFlag"),2,77);
            var expected=row.GetProperty("output");int[] packet=Ints(expected.GetProperty("before"));var trace=new List<long[]>();
            MissileDamageScale.Run(c,new((selector,id)=>{trace.Add([0,selector,unchecked((uint)id)]);return B("row")?I("token"):null;},(kind,id)=>{Require(kind==2&&id==77,"Changed owner identity");trace.Add([1,kind,id]);return B("owner")?1U:0;},f=>{Require(f.Missile==B("present"),"Changed actual missile identity");trace.Add([2,f.Selector,f.Owner,f.Token,f.MissileId,f.Word0e]);return I("percent");}),packet);
            var wanted=expected.GetProperty("trace").EnumerateArray().Select(e=>new[]{e[0].GetString()switch{"row"=>0L,"owner"=>1L,"formula"=>2L,_=>throw new InvalidOperationException("Unknown trace")}}.Concat(e.EnumerateArray().Skip(1).Select(x=>x.GetInt64())).ToArray()).ToArray();
            Require(packet.SequenceEqual(Ints(expected.GetProperty("after")))&&trace.Count==wanted.Length&&trace.Where((t,i)=>!t.SequenceEqual(wanted[i])).Any()==false,"Candidate current guards/context/float rounding or untouched packet fields differ");n++;
        }
        Require(n==112&&NpcWorldAllocation.Capture()==world&&!WarlockCombat.Implemented(CombatAbility.RingOfFire),"Prepared scaling changed world allocation or unfinished handler availability");
        File.WriteAllText(Manifest,generation.ToString());Require(generation<=2,"Unexpected fixture generation");
        Log.Information("Missile candidate scale:112current instruction contexts;guard/owner/formula order,signed context word,float32 multiplication/truncation and overflow,all seven raw offsets and17untouched fields match. Dispatcher identity and public Ring remain unqualified.");
    }
}
