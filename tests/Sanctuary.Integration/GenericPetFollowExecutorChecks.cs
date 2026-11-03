// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetFollowExecutorChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetFollowExecutorChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(string n in new[]{"generic-pet-follow-executor-check","generic-pet-follow-executor-reload-check"})
        {ConsoleInputHandler.RegisterCommand(n,"Current captured six-way follow executor",_=>Core.LoopContext.Post(()=>{try{Check();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));}
    }
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-follow-executor-source-probe.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row[0];int I(string n)=>p.GetProperty(n).GetInt32();bool B(string n)=>p.GetProperty(n).GetBoolean();
            (int,int) XY(string n){var a=p.GetProperty(n);return(a[0].GetInt32(),a[1].GetInt32());}
            var(px,py)=XY("pet");var(ox,oy)=XY("owner");var(dx,dy)=XY("destination");var(tx,ty)=XY("playerDestination");
            var pet=B("petPresent")?new GenericPetDecisionUnit(I("petKind"),B("petPath"),px,py):null;
            var owner=B("ownerPresent")?new GenericPetDecisionUnit(I("ownerKind"),B("ownerPath"),ox,oy,B("playerData"),tx,ty):null;
            var random=new GenericPetActionRandom(p.GetProperty("seed").GetUInt64());
            var trace=new List<int[]>();void A(params int[] a)=>trace.Add(a);int calls=0,regions=0;
            int Respond(params int[] a){var responses=p.GetProperty("responses");int r=responses[Math.Min(calls++,responses.GetArrayLength()-1)].GetInt32();A([..a,r]);return r;}
            var services=new GenericPetFollowExecutionServices(()=>{A(0,dx,dy);return(dx,dy);},()=>{A(1);return 1;},
                (room,x,y)=>{A(2,room==2?1:0,x,y);return I("regionState")==0?0:2;},
                (room,x,y)=>{Require(room==2,"Follow region handle");A(3,x,y);int region=I("regionId");if(p.TryGetProperty("regionIds",out var ids)){region=ids[Math.Min(regions,ids.GetArrayLength()-1)].GetInt32();}regions++;return I("regionState")==1?null:region;},
                (x,y)=>{A(4,x,y);return I("direction");},()=>{A(5,I("selector"),742);return I("monsterFlags");},
                (speed,tuning,options)=>A(6,speed,tuning,options),()=>A(7),
                (x,y)=>Respond(8,x,y),(x,y,b,c,f)=>Respond(9,x,y,b,c,f),
                (x,y,a,b,c,d,e)=>Respond(10,x,y,a,b,c,d,e),
                (x,y,a,b,c,d)=>Respond(11,x,y,a,b,c,d),(x,y,a,b)=>Respond(12,x,y,a,b),()=>Respond(13));
            var context=new GenericPetFollowExecution(pet,owner,I("mode"),I("close"),I("wander"),I("word70"));
            byte result=GenericPetFollowExecutor.Execute(context,random,services);
            string got=JsonSerializer.Serialize(new object[]{result,random.Seed,trace});string expected=JsonSerializer.Serialize(row[1]);
            Require(got==expected,"Follow executor source case "+count+":"+got+" expected:"+expected);count++;
        }
        Require(count>=5001,"Missing complete current follow matrix");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Detached follow executor enabled incomplete learned summon");
        Log.Information("GenericPet follow executor:{Count}complete captured-current cases,all six arms,ordered leaf calls,byte results,coordinate fallback and actor RNG;actual world/path/warp remains separate",count);
    }
}
