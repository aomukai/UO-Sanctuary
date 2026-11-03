// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class PetPathTargetChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PetPathTargetChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-path-targets-check","Current reset with owned primary monster targets",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("pet-path-targets-check: passed");}catch(Exception e){Log.Error(e,"pet-path-targets-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-path-targets-source-probe.json")));
        int count=0,unsupported=0,lookups=0;
        var views=new Dictionary<byte,GenericPetPathTargetView>();
        foreach(byte selector in new byte[]{1,2,3}){views.Add(selector,GenericPetPathTargets.Bind(selector));}
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row[1];int I(string n)=>p.GetProperty(n).GetInt32();uint U(string n)=>p.GetProperty(n).GetUInt32();
            var path=new GenericPetCommitPath{X=p.GetProperty("point")[0].GetUInt32(),Y=p.GetProperty("point")[1].GetUInt32(),ClientX=101,ClientY=-202,
                Room=I("oldRoom"),PreviousRoom=2,Flags=U("flags"),Word30=17,Word34=18,Type=I("type"),Collision68=0x7654,
                Raw96=0x1122334455667788,Word200=19,Point204=unchecked((uint)-20),Actor=new(1,I("actor")),
                Target=new(I("target"),I("targetClass"),p.GetProperty("variant").GetByte(),U("targetFlags"))};
            var trace=new List<object[]>();
            var services=new GenericPetPathCommitServices((_,_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),
                _=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),
                _=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),
                (_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),(selector,id)=>
                {
                    byte? flags=null;lookups++;
                    try{return flags=(views.TryGetValue(selector,out var view)?view:GenericPetPathTargets.Bind(selector)).Flags(selector,id);}
                    finally{trace.Add([6,selector,id,flags]);}
                },0);
            int result=0;
            try{GenericPetPathCommit.Reset(path,services);}
            catch(NotSupportedException){result=-1;unsupported++;trace.Add([99]);}
            object[] fields=[path.X,path.Y,path.ClientX,path.ClientY,path.Room,path.PreviousRoom,path.Flags,path.Word30,path.Word34,path.Collision68,path.Word200,path.Point204,path.Raw96];
            string actual=JsonSerializer.Serialize(new object[]{result,fields,trace}),expected=JsonSerializer.Serialize(row[2]);
            Require(actual==expected,$"Path target case{count}: {actual} expected {expected}");count++;
        }
        Require(count==4826&&unsupported==48&&lookups==4586,"Incomplete path-target reset matrix");
        bool refused=false;try{views[1].Flags(2,148);}catch(NotSupportedException){refused=true;}
        Require(refused,"Path target view accepted another selector");
        var rows=new[]{new GenericPetPathTargetMetadata(0,"original",2)};var detached=new GenericPetPathTargetView(rows,1);rows[0]=new(0,"mutated",0);
        Require(detached.Flags(1,0)==2&&detached.Flags(1,-1)==null&&detached.Flags(1,1)==null,"Path target view changed its captured rows or class bounds");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Target metadata enabled incomplete summon");
        Log.Information("Pet path targets:{Count}complete reset/getter cases,{Unsupported}unsupported selectors,{Lookups}real metadata calls;explicit view mismatch and table identity guards pass",count,unsupported,lookups);
    }
}
