// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class PetRoomLinkChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PetRoomLinkChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-room-links-check","Current monster room/update membership differential",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("pet-room-links-check: passed");}catch(Exception e){Log.Error(e,"pet-room-links-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-room-links-source-probe.json")));int count=0,unsupported=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            string kind=row[0].GetString();var p=row[1];int I(string n)=>p.GetProperty(n).GetInt32();uint U(string n)=>p.GetProperty(n).GetUInt32();bool B(string n)=>p.GetProperty(n).GetBoolean();int[] V(string n)=>p.GetProperty(n).EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            var units=new GenericPetMonsterRoomLink[4];var rooms=new GenericPetLinkedRoom[3];var trace=new List<object[]>();int[] point=V("point"),bounds=V("bounds"),members=V("members"),updates=V("updates");
            for(int i=1;i<=3;i++)
            {
                var path=i==1&&!B("path")?null:new GenericPetCommitPath{X=((uint)(ushort)point[0]<<16)|0x8000,Y=((uint)(ushort)point[1]<<16)|0x8000,Room=i==1?I("room"):1,Actor=new(i,1)};
                units[i]=new(i,path,new(i,i==1?U("flags"):(uint)(0x1234+i),0)){Flags128=i==1?U("flags128"):0,ByteB9=(byte)(i==1?99:10*i)};
            }
            for(int i=0;i<members.Length;i++){units[members[i]].Next160=i+1<members.Length?members[i+1]:0;}
            for(int i=0;i<updates.Length;i++){units[updates[i]].Update.Next150=i+1<updates.Length?updates[i+1]:0;}
            if(I("next")>=0){units[1].Next160=I("next");}if(I("nextUpdate")>=0){units[1].Update.Next150=I("nextUpdate");}
            for(int i=1;i<=2;i++)
            {
                rooms[i]=new(i,new(bounds[0],bounds[1],bounds[2],bounds[3]),new(i,(byte)I("roomFlags"),updates.FirstOrDefault(),B("marker")?new(7+i):null))
                    {Head=members.FirstOrDefault(),FriendlyCount=I("count")};
            }
            var services=new GenericPetRoomLinkServices(id=>units[id],id=>rooms[id],id=>{Require(id==1,"Unexpected alignment subject");trace.Add([0,id,I("alignment")]);return I("alignment");});
            int handle=B("present")?1:0,result=0;
            try
            {
                switch(kind)
                {
                    case "add":result=GenericPetRoomLinks.Add(handle,I("targetRoom"),services);break;
                    case "remove":GenericPetRoomLinks.Remove(handle,services);break;
                    case "removeUpdate":GenericPetRoomLinks.RemoveUpdate(handle,services);break;
                    case "touch":GenericPetRoomLinks.Touch(handle,services);break;
                    default:throw new InvalidOperationException("Unknown room-list operation");
                }
            }
            catch(NotSupportedException){result=-1;unsupported++;trace.Add([99]);}
            var unitFields=units.Skip(1).Select(u=>new object[]{u.Next160,u.Update.Next150,u.Update.Flags124,u.Flags128,u.ByteB9,u.Path?.Room??-1}).ToArray();
            var roomFields=rooms.Skip(1).Select(r=>new object[]{r.Head,r.FriendlyCount,r.Update.Head,r.Update.Marker?.Value??7+r.Handle}).ToArray();
            string actual=JsonSerializer.Serialize(new object[]{result,unitFields,roomFields,trace}),expected=JsonSerializer.Serialize(row[2]);
            Require(actual==expected,$"Current room-link case{count}/{kind}: {actual} expected {expected}");count++;
        }
        Require(count==5952&&unsupported==3222,"Incomplete room-list/partial-state coverage");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Room-link comparison enabled incomplete summon");
        Log.Information("Pet room links:{Count}complete current-code cases,{Unsupported}unsupported controls;all unit/room/update/counter/dirty-byte fields and alignment/fatal order match;global native ownership/visibility remain unfinished",count,unsupported);
    }
}
