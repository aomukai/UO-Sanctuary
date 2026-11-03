// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetWarpCandidateChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetWarpCandidateChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(string n in new[]{"generic-pet-warp-candidate-check","generic-pet-warp-candidate-reload-check"})
        {ConsoleInputHandler.RegisterCommand(n,"Current validation-only landing candidate and room-seed search",_=>Core.LoopContext.Post(()=>{try{Check();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));}
    }
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-warp-candidate-source-probe.json")));int count=0,composed=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            string family=row[0].GetString();var p=row[1];int I(string n)=>p.GetProperty(n).GetInt32();bool B(string n)=>p.GetProperty(n).GetBoolean();
            int[] Values(string n)=>p.GetProperty(n).EnumerateArray().Select(e=>e.GetInt32()).ToArray();
            var bounds=Values("bounds");var area=Values("areaBounds");var output=Values("out");
            var room=new GenericPetWarpSearchRoom(1,bounds[0],bounds[1],bounds[2],bounds[3],p.GetProperty("seed").GetUInt64());
            var region=B("area")?new GenericPetWarpSearchRegion(1,I("areaId"),area[0],area[1],area[2],area[3]):null;
            var trace=new List<object[]>();void A(params object[] a)=>trace.Add(a);int roomCalls=0,regionCalls=0;
            int RoomAt(int handle,int x,int y){Require(handle==1,"Candidate original room");var rooms=Values("rooms");int next=rooms[Math.Min(roomCalls++,rooms.Length-1)];A(1,1,x,y,next);return next!=0?2:0;}
            int? Region(int handle,int x,int y){Require(handle==2,"Candidate lookup room");var states=p.GetProperty("regionStates");var e=states[Math.Min(regionCalls++,states.GetArrayLength()-1)];int? value=e.ValueKind==JsonValueKind.Null?null:e.GetInt32();A(2,x,y,value);return value;}
            var services=new GenericPetWarpCandidateServices((selector,id)=>{A(4,selector,id,I("secondary"));return I("secondary")!=0;},
                (selector,id)=>{A(5,selector,id,I("primary"));return I("primary")!=0?new(checked((byte)I("walk")),checked((sbyte)I("size"))):null;},
                (zero,handle)=>{var pt=Values("special");A(6,zero,handle,pt[0],pt[1]);return(pt[0],pt[1]);},RoomAt,Region,
                (selector,id,handle,x,y,flag)=>{A(7,selector,id,handle,x,y,flag,I("allowed"));return I("allowed");},
                (handle,x,y,mask)=>{A(8,handle,x,y,mask,I("collision"));return I("collision");},
                (handle,x,y,mask)=>{A(9,handle,x,y,mask,I("collision"));return I("collision");},
                (handle,left,top,right,bottom,mask)=>{A(10,handle,left,top,right,bottom,mask,I("collision"));return I("collision");});
            var search=new GenericPetWarpSearchServices(RoomAt,Region,_=>throw new InvalidOperationException("Unbound candidate"));
            search=GenericPetWarpCandidateCheck.Bind(search,checked((byte)I("selector")),id=>id==1&&B("candidateRoom")?room:null,id=>id==1?region:null,services);
            long result;int x=output[0],y=output[1];
            if(family=="candidate")
            {var point=Values("candidate");result=GenericPetWarpCandidateCheck.Check(new(1,1,region!=null?1:0,I("classId"),point[0],point[1]),checked((byte)I("selector")),B("candidateRoom")?room:null,region,services);}
            else
            {Require(family=="search","Unknown candidate source family");composed++;result=GenericPetWarpSearch.Find(1,room,region,I("classId"),search,ref x,ref y)?1:0;}
            object[] actual=[result,room.Seed,new object[]{p.GetProperty("flags").GetUInt32(),I("next"),I("head"),I("mark")},new[]{x,y},trace];
            string got=JsonSerializer.Serialize(actual),expected=JsonSerializer.Serialize(row[2]);
            Require(got==expected,"Warp candidate "+family+" case "+count+":"+got+" expected:"+expected);count++;
        }
        Require(count==8553&&composed==512,"Incomplete validation-only candidate source matrix");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Candidate backend enabled incomplete learned summon");
        Log.Information("GenericPet warp candidate:{Count}complete current-code cases,{Composed}full search/candidate compositions;metadata order,room RNG,footprint/masks,region and special-point decisions pass;actual geometry/placement/controller remain unfinished",count,composed);
    }
}
