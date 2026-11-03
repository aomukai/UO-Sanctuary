// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class GenericPetPathCommitChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetPathCommitChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("generic-pet-path-commit-check","Complete current path commitment/coordinate/reset comparisons",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("generic-pet-path-commit-check: passed");}catch(Exception e){Log.Error(e,"generic-pet-path-commit-check: failed");}}));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-path-commit-source-probe.json")));int count=0,unsupported=0,bound=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            string family=row[0].GetString();var p=row[1];int I(string n)=>p.GetProperty(n).GetInt32();uint U(string n)=>p.GetProperty(n).GetUInt32();int[] V(string n)=>p.GetProperty(n).EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            GenericPetCommitPath Fresh()=>new(){X=p.GetProperty("point")[0].GetUInt32(),Y=p.GetProperty("point")[1].GetUInt32(),ClientX=101,ClientY=-202,
                Room=I("oldRoom"),PreviousRoom=2,Flags=U("flags"),Word30=17,Word34=18,Type=I("type"),Pattern58=I("pattern"),Size5c=I("size"),Mask60=I("mask"),Mask64=I("checkMask"),
                Collision68=0x7654,Raw96=0x1122334455667788,Word200=19,Point204=unchecked((uint)-20),Actor=I("actor")<0?null:new(1,I("actor")),
                Target=I("target")<0?null:new(I("target"),I("targetClass"),p.GetProperty("variant").GetByte(),U("targetFlags"))};
            var trace=new List<object[]>();int roomCalls=0;void A(params object[] values)=>trace.Add(values);
            void Footprint(int code,GenericPetFootprintRequest r)=>A(code,r.Room,r.X,r.Y,r.Pattern,r.Mask);
            var services=new GenericPetPathCommitServices((room,x,y)=>{int[] rooms=V("rooms");int next=rooms[Math.Min(roomCalls++,rooms.Length-1)];A(5,room,x,y,next);return next;},
                _=>{var b=V("bounds");return new(b[0],b[1],b[2],b[3]);},r=>Footprint(0,r),r=>{Footprint(1,r);return I("collision");},r=>Footprint(2,r),r=>Footprint(3,r),r=>Footprint(4,r),
                actor=>A(7,actor),(actor,room)=>A(8,actor,room),actor=>A(9,actor),(variant,@class)=>
                {byte? value=p.GetProperty("metadata").ValueKind==JsonValueKind.Null?null:p.GetProperty("metadata").GetByte();A(6,variant,@class,value);return value;},0);
            var path=Fresh();int result=0;
            try
            {
                if(family=="commit"){result=GenericPetPathCommit.Commit(path,I("newRoom"),U("packed"),services);}
                else if(family=="coordinates"){var point=p.GetProperty("precise");GenericPetPathCommit.Coordinates(path,point[0].GetUInt32(),point[1].GetUInt32(),I("newRoom"),services);}
                else{Require(family=="reset","Unknown path family");GenericPetPathCommit.Reset(path,services);}
            }
            catch(NotSupportedException){result=-1;unsupported++;A(99);}
            object[] Fields()=>[path.X,path.Y,path.ClientX,path.ClientY,path.Room,path.PreviousRoom,path.Flags,path.Word30,path.Word34,path.Collision68,path.Word200,path.Point204,path.Raw96];
            string actual=JsonSerializer.Serialize(new object[]{result,Fields(),trace}),expected=JsonSerializer.Serialize(row[2]);
            Require(actual==expected,$"Current path case{count}/{family}: {actual} expected {expected}");
            // Existing monster placement binds the real checked commitment,
            // preserving its success-only path-word/room/flag/sync boundary.
            if(family=="commit"&&result==1&&I("actor")==1&&I("newRoom")!=0&&U("packed")!=0)
            {
                path=Fresh();roomCalls=0;trace.Clear();var pet=new GenericPetWarpUnit(742,0);var unit=new GenericPetMonsterPlacementUnit(pet,true,1,742,2,2,99);int after=0,sync=0;
                var placement=new GenericPetMonsterPlacementServices(()=>I("oldRoom"),(_,_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),()=>2,()=>2,
                    _=>throw new InvalidOperationException(),()=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),()=>after++,_=>sync++);
                var boundServices=GenericPetPathCommit.Bind(placement,path,services);uint packed=U("packed");
                int got=GenericPetMonsterPlacement.Place(unit,I("newRoom"),(ushort)packed,(ushort)(packed>>16),1,boundServices);
                Require(got==1&&after==1&&sync==1&&unit.PathWord34==0&&pet.Flags==0x10000,"Bound commitment lost placement success/order");
                Require(JsonSerializer.Serialize(new object[]{got,Fields(),trace})==expected,"Bound placement/path output differs from current commitment reference");bound++;
            }
            count++;
        }
        Require(count==8184&&unsupported>0&&bound>0,"Incomplete current path matrix");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Prepared commitment enabled incomplete summon");
        Log.Information("GenericPet path commitment:{Count}complete current-code cases,{Unsupported}unsupported controls,{Bound}placement bindings;all commit/coordinate/reset bodies match;native world geometry/controller remain unfinished",count,unsupported,bound);
    }
}
