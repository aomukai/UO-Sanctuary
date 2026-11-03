// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class GenericPetFootprintChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetFootprintChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("generic-pet-footprints-check","Current footprint dispatch/tables/grid and path composition",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("generic-pet-footprints-check: passed");}catch(Exception e){Log.Error(e,"generic-pet-footprints-check: failed");}}));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-footprints-source-probe.json")));int count=0,unsupported=0,bound=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            string mode=row[0].GetString();var p=row[1];int I(string n)=>p.GetProperty(n).GetInt32();uint U(string n)=>p.GetProperty(n).GetUInt32();bool B(string n)=>p.GetProperty(n).GetBoolean();int[] V(string n)=>p.GetProperty(n).EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            ushort[] memory=[0,1,0xffff,0x1000,0x2000,0x1234,0xabcd,0x8000,7];var gb=V("gridBounds");var grid=new GenericPetCollisionGrid(gb[0],gb[1],gb[2],B("cells")?memory:null);
            var trace=new List<object[]>();void A(params object[] args)=>trace.Add(args);int calls=0;
            int Room(int room,int x,int y){var rooms=V("rooms");int next=rooms[Math.Min(calls++,rooms.Length-1)];A(5,room,x,y,next);return next;}
            void Box(int code,int room,GenericPetCollisionBox box,int mask)=>A(code,room,box.Left,box.Top,box.Right,box.Bottom,mask);
            var footprints=new GenericPetCollisionFootprintServices((room,x,y)=>{int next=Room(room,x,y);return next==0?null:new(next,B("grid")?grid:null);},
                (room,x,y,mask)=>{A(20,room,x,y,mask);return I("collision");},(room,x,y,mask)=>{A(21,room,x,y,mask);return I("collision");},
                (room,box,mask)=>{Box(22,room,box,mask);return I("collision");},(room,x,y,mask)=>A(23,room,x,y,mask),(room,box,mask)=>Box(24,room,box,mask),
                (room,x,y,mask)=>A(25,room,x,y,mask),(room,box,mask)=>Box(26,room,box,mask));
            var path=new GenericPetCommitPath{X=p.GetProperty("point")[0].GetUInt32(),Y=p.GetProperty("point")[1].GetUInt32(),ClientX=101,ClientY=-202,
                Room=I("oldRoom"),PreviousRoom=2,Flags=U("flags"),Word30=17,Word34=18,Type=I("type"),Pattern58=I("pattern"),Size5c=I("size"),Mask60=I("mask"),Mask64=I("checkMask"),
                Collision68=0x7654,Raw96=0x1122334455667788,Word200=19,Point204=unchecked((uint)-20),Actor=I("actor")<0?null:new(1,I("actor")),
                Target=I("target")<0?null:new(I("target"),I("targetClass"),p.GetProperty("variant").GetByte(),U("targetFlags"))};
            int result=0;
            try
            {
                var request=new GenericPetFootprintRequest(I("fpRoom"),I("fx"),I("fy"),I("fpType"),I("fpMask"));
                switch(mode)
                {
                    case "checkPattern":result=GenericPetCollisionFootprints.CheckPattern(request,footprints);break;
                    case "addPattern":GenericPetCollisionFootprints.AddPattern(request,footprints);break;
                    case "removePattern":GenericPetCollisionFootprints.RemovePattern(request,footprints);break;
                    case "addSize":GenericPetCollisionFootprints.AddSize(request,footprints);break;
                    case "removeSize":GenericPetCollisionFootprints.RemoveSize(request,footprints);break;
                    case "bound":
                        var services=new GenericPetPathCommitServices(Room,_=>{var b=V("bounds");return new(b[0],b[1],b[2],b[3]);},
                            _=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),
                            actor=>A(7,actor),(actor,room)=>A(8,actor,room),actor=>A(9,actor),(variant,@class)=>{byte? value=p.GetProperty("metadata").ValueKind==JsonValueKind.Null?null:p.GetProperty("metadata").GetByte();A(6,variant,@class,value);return value;},0);
                        result=GenericPetPathCommit.Commit(path,I("newRoom"),U("packed"),GenericPetCollisionFootprints.Bind(services,footprints));bound++;break;
                    default:throw new InvalidOperationException("Unknown footprint mode");
                }
            }
            catch(NotSupportedException){result=-1;unsupported++;A(99);}
            object[] fields=mode=="bound"?[path.X,path.Y,path.ClientX,path.ClientY,path.Room,path.PreviousRoom,path.Flags,path.Word30,path.Word34,path.Collision68,path.Word200,path.Point204,path.Raw96]:[];
            string actual=JsonSerializer.Serialize(new object[]{result,fields,memory,trace}),expected=JsonSerializer.Serialize(row[2]);
            Require(actual==expected,$"Current footprint case{count}/{mode}: {actual} expected {expected}");count++;
        }
        Require(count==7360&&unsupported>0&&bound>0,"Incomplete footprint dispatch/composition coverage");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Footprint preparation enabled incomplete summon");
        Log.Information("GenericPet footprints:{Count}complete current-code cases,{Unsupported}unsupported controls,{Bound}successful path compositions;five wrappers/two inline tables/mask lanes/direct grid state match;native room/adjacent/box geometry remain unfinished",count,unsupported,bound);
    }
}
