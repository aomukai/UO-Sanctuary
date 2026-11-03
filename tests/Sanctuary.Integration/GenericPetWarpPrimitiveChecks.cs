// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetWarpPrimitiveChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetWarpPrimitiveChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(string n in new[]{"generic-pet-warp-primitives-check","generic-pet-warp-primitives-reload-check"})
        {ConsoleInputHandler.RegisterCommand(n,"Current room-seed warp search and room/path primitives",_=>Core.LoopContext.Post(()=>{try{Check();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));}
    }
    private static GenericPetWarpServices Unbound()=>new(()=>throw new InvalidOperationException("Unbound room"),(_,_,_)=>throw new InvalidOperationException("Unbound region"),
        _=>throw new InvalidOperationException("Unbound count"),(_,_,_)=>throw new InvalidOperationException("Unbound search"),(_,_,_)=>throw new InvalidOperationException("Unbound room lookup"),
        (_,_,_,_,_)=>throw new InvalidOperationException("Unbound placement"),()=>throw new InvalidOperationException("Unbound packed"),()=>throw new InvalidOperationException("Unbound path word"),
        (_,_,_,_,_)=>throw new InvalidOperationException("Unbound collision"),()=>throw new InvalidOperationException("Unbound room update"),_=>throw new InvalidOperationException("Unbound idle"));
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-warp-primitives-source-probe.json")));int count=0,fatal=0,boundCount=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            string family=row[0].GetString();bool bound=family.StartsWith("bound-",StringComparison.Ordinal);if(bound){boundCount++;}
            string action=bound?family[6..]:family;var p=row[1];int I(string n)=>p.GetProperty(n).GetInt32();bool B(string n)=>p.GetProperty(n).GetBoolean();
            int[] Values(string n)=>p.GetProperty(n).EnumerateArray().Select(e=>e.GetInt32()).ToArray();
            var bounds=Values("bounds");var area=Values("areaBounds");var xy=Values("point");var output=Values("out");
            var room=new GenericPetWarpSearchRoom(1,bounds[0],bounds[1],bounds[2],bounds[3],p.GetProperty("seed").GetUInt64());
            var region=B("area")?new GenericPetWarpSearchRegion(1,I("areaId"),area[0],area[1],area[2],area[3]):null;
            var marker=B("marker")?new GenericPetRoomMarker(I("mark")):null;
            var updateRoom=new GenericPetRoomUpdateRoom(1,checked((byte)I("roomFlags")),I("head"),marker);
            var shadow=new GenericPetRoomUpdateUnit(1,p.GetProperty("flags").GetUInt32(),I("next"));var unit=I("kind")>=0?new GenericPetDecisionUnit(I("kind"),B("path"),xy[0],xy[1]):null;
            var trace=new List<object[]>();void A(params object[] a)=>trace.Add(a);int calls=0,roomCalls=0,regionCalls=0;
            var search=new GenericPetWarpSearchServices((handle,x,y)=>{Require(handle==1,"Search origin room");var rooms=Values("rooms");int next=rooms[Math.Min(roomCalls++,rooms.Length-1)];A(1,1,x,y,next);return next!=0?2:0;},
                (handle,x,y)=>{Require(handle==2,"Candidate room handle");var states=p.GetProperty("regionStates");var e=states[Math.Min(regionCalls++,states.GetArrayLength()-1)];int? value=e.ValueKind==JsonValueKind.Null?null:e.GetInt32();A(2,x,y,value);return value;},
                request=>{var responses=p.GetProperty("responses");long next=responses[Math.Min(calls++,responses.GetArrayLength()-1)].GetInt64();A(3,request.Game,request.Room,request.Region,request.ClassId,request.X,request.Y,request.Word28,request.Word32,request.Word44,request.Word48,request.Pointer56,next);return next;});
            GenericPetRoomUpdateRoom Room(int handle){Require(handle==1,"Room-update actor handle");A(0,1,I("roomPresent"));return I("roomPresent")!=0?updateRoom:null;}
            var services=GenericPetWarpSearch.Bind(Unbound(),1,id=>id==1?room:null,id=>id==1?region:null,search);
            services=GenericPetRoomUpdates.Bind(services,()=>unit,()=>unit,()=>I("word"),unit!=null?shadow:null,Room);
            long result=0;int x=output[0],y=output[1];
            try
            {
                for(int repeat=0;repeat<((action=="touch"||family=="search")?I("repeat"):1);repeat++)
                {
                    if(action=="search")
                    {
                        if(bound){var found=services.Find(1,region!=null?1:0,I("classId"));result=found.Result;x=found.X;y=found.Y;}
                        else{result=GenericPetWarpSearch.Find(1,room,region,I("classId"),search,ref x,ref y)?1:0;}
                    }
                    else if(action=="touch"){if(bound){services.AfterPlacement();}else{GenericPetRoomUpdates.Touch(unit!=null?shadow:null,Room);}}
                    else if(action=="packed"){result=bound?services.PackedOwnerCoordinates():GenericPetRoomUpdates.PackedCoordinates(unit);}
                    else{Require(action=="word","Unknown primitive family");result=bound?services.UnitSize():GenericPetRoomUpdates.PathWord5c(unit,I("word"));}
                }
            }
            catch(NotSupportedException){result=-1;fatal++;A(99);}
            object[] actual=[result,room.Seed,new object[]{shadow.Flags124,shadow.Next150,updateRoom.Head,marker?.Value??I("mark")},new[]{x,y},trace];
            string got=JsonSerializer.Serialize(actual),expected=JsonSerializer.Serialize(row[2]);
            Require(got==expected,"Warp primitives "+family+" case "+count+":"+got+" expected:"+expected);count++;
        }
        Require(count==2352&&fatal>0&&boundCount==24,"Incomplete warp primitive source matrix");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Primitive backend enabled incomplete learned summon");
        Log.Information("GenericPet warp primitives:{Count}complete current-code cases,{Fatal}unsupported controls,{Bound}bound service contexts;room RNG,20attempt limit,output retention,room lists and packed/path words pass;candidate/world/controller remain unfinished",count,fatal,boundCount);
    }
}
