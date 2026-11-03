// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetMonsterPlacementChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetMonsterPlacementChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(string n in new[]{"generic-pet-monster-placement-check","generic-pet-monster-placement-reload-check"})
        {ConsoleInputHandler.RegisterCommand(n,"Current monster relocation policy and room-update composition",_=>Core.LoopContext.Post(()=>{try{Check();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));}
    }
    private static GenericPetWarpServices Unbound()=>new(()=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),
        (_,_,_)=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),(_,_,_,_,_)=>throw new InvalidOperationException(),
        ()=>throw new InvalidOperationException(),()=>throw new InvalidOperationException(),(_,_,_,_,_)=>throw new InvalidOperationException(),()=>throw new InvalidOperationException(),_=>throw new InvalidOperationException());
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-monster-placement-source-probe.json")));int count=0,fatal=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row[0];int I(string n)=>p.GetProperty(n).GetInt32();bool B(string n)=>p.GetProperty(n).GetBoolean();int[] Values(string n)=>p.GetProperty(n).EnumerateArray().Select(e=>e.GetInt32()).ToArray();
            var warpPet=new GenericPetWarpUnit(I("classId"),p.GetProperty("flags128").GetUInt32());
            var shadow=new GenericPetMonsterPlacementUnit(warpPet,B("path"),I("cacheKind"),I("cacheClass"),I("cache180"),I("cache184"),I("path34"));
            var update=new GenericPetRoomUpdateUnit(1,p.GetProperty("flags").GetUInt32(),I("next"));
            var marker=B("marker")?new GenericPetRoomMarker(I("mark")):null;
            var room=new GenericPetRoomUpdateRoom(1,checked((byte)I("roomFlags")),I("head"),marker);
            var trace=new List<object[]>();void A(params object[] a)=>trace.Add(a);int roomCalls=0;
            int Room(){A(0,1,I("roomPresent"));return I("roomPresent")!=0?1:0;}
            var services=new GenericPetMonsterPlacementServices(Room,
                (handle,x,y)=>{var rooms=Values("rooms");int next=rooms[Math.Min(roomCalls++,rooms.Length-1)];A(11,handle,x,y,next);return next;},
                flag=>A(12,flag),()=>{A(13,I("get180"));return I("get180");},()=>{A(14,I("get184"));return I("get184");},
                request=>{var point=Values("nearPoint");A(15,request.Room,request.X,request.Y,request.Zero,request.Word180,request.Mask1,request.Mask2,request.Zero2,request.Distance,request.One,I("nearRoom"),point[0],point[1]);return new(I("nearRoom"),point[0],point[1]);},
                ()=>A(16),(handle,packed)=>{A(17,handle,packed,I("update"));return I("update");},
                ()=>GenericPetRoomUpdates.Touch(update,_=>Room()!=0?room:null),flag=>A(18,flag,warpPet.Flags));
            var bound=GenericPetMonsterPlacement.Bind(Unbound(),B("present")?shadow:null,services);var target=Values("target");int result;
            try{result=bound.Place(I("targetRoom"),target[0],target[1],I("skip"),0);}
            catch(NotSupportedException){result=-1;fatal++;A(99);}
            object[] actual=[result,p.GetProperty("seed").GetUInt64(),new object[]{update.Flags124,update.Next150,room.Head,marker?.Value??I("mark")},Values("out"),trace,
                new object[]{warpPet.Flags,shadow.Kind178,shadow.Class17c,shadow.Word180,shadow.Word184,shadow.PathWord34}];
            string got=JsonSerializer.Serialize(actual),expected=JsonSerializer.Serialize(row[1]);
            Require(got==expected,"Monster placement case "+count+":"+got+" expected:"+expected);count++;
        }
        Require(count==5229&&fatal>0,"Incomplete current monster placement matrix");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Placement backend enabled incomplete learned summon");
        Log.Information("GenericPet monster placement:{Count}complete current-code cases,{Fatal}unsupported controls;all6source fragments,cache/room/nearby/packed/path result and actual room-update composition pass;world commit and controller remain unfinished",count,fatal);
    }
}
