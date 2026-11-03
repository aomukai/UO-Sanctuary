// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class PetCollisionWriteChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PetCollisionWriteChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-collision-writes-check","Current collision writes and complete footprint composition",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("pet-collision-writes-check: passed");}catch(Exception e){Log.Error(e,"pet-collision-writes-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-collision-writes-source-probe.json")));
        int count=0,unsupported=0,bound=0,composed=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            string kind=row[0].GetString();var p=row[1];int I(string n)=>p.GetProperty(n).GetInt32();int[] V(string n)=>p.GetProperty(n).EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            var trace=new List<object[]>();void A(params object[] args)=>trace.Add(args);
            var rooms=new SourceCollisionRoom[5];var buffers=new ushort[5][];
            for(int i=1;i<=4;i++)
            {
                int width=I("width"),height=I("height");buffers[i]=Enumerable.Range(0,width*height).Select(j=>unchecked((ushort)((i*0x1234+j*0x1021)^(1<<(j%16))))).ToArray();
                rooms[i]=new(20+((i-1)%2)*width,30+((i-1)/2)*height,width,height,width,(I("cells")&(1<<(i-1)))==0?null:buffers[i],V("neighbors"));
            }
            int Room(int current,int x,int y){A(0,current,x,y);return SourceRoomTrace.Resolve(current,x,y,id=>rooms[id]);}
            SourceCollisionRoom Grid(int id)=>(I("grids")&(1<<(id-1)))==0?null:rooms[id];
            var services=new GenericPetCollisionWriteServices(Room,Grid,(id,index,value)=>{buffers[id][index]=value;A(2,id,index,value);});
            var raw=V("box");var box=new GenericPetCollisionBox(raw[0],raw[1],raw[2],raw[3]);
            var footprints=new GenericPetCollisionFootprintServices((room,x,y)=>
            {
                int next=Room(room,x,y);var grid=next==0?null:Grid(next);
                return next==0?null:new(next,grid==null?null:new(grid.X,grid.Y,grid.Width,grid.Cells as ushort[]));
            },(_,_,_,_)=>throw new InvalidOperationException(),(_,_,_,_)=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),
                (_,_,_,_)=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),
                (_,_,_,_)=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException());
            var adapter=GenericPetCollisionWrites.Bind(footprints,services);
            bool direct=kind is "addAdjacent" or "removeAdjacent" or "addBox" or "removeBox";
            void Run(bool binding)
            {
                bool add=kind.StartsWith("add",StringComparison.Ordinal);
                if(direct)
                {
                    if(binding)
                    {
                        switch(kind)
                        {
                            case "addAdjacent":adapter.AddAdjacent(I("room"),I("x"),I("y"),I("mask"));break;
                            case "removeAdjacent":adapter.RemoveAdjacent(I("room"),I("x"),I("y"),I("mask"));break;
                            case "addBox":adapter.AddBox(I("room"),box,I("mask"));break;
                            default:adapter.RemoveBox(I("room"),box,I("mask"));break;
                        }
                    }
                    else if(kind.EndsWith("Adjacent",StringComparison.Ordinal)){GenericPetCollisionWrites.Adjacent(I("room"),I("x"),I("y"),I("mask"),add,services);}
                    else{GenericPetCollisionWrites.Box(I("room"),box,I("mask"),add,services);}
                }
                else
                {
                    var request=new GenericPetFootprintRequest(I("room"),I("x"),I("y"),I("pattern"),I("mask"));
                    switch(kind)
                    {
                        case "addPattern":GenericPetCollisionFootprints.AddPattern(request,adapter);break;
                        case "removePattern":GenericPetCollisionFootprints.RemovePattern(request,adapter);break;
                        case "addSize":GenericPetCollisionFootprints.AddSize(request,adapter);break;
                        case "removeSize":GenericPetCollisionFootprints.RemoveSize(request,adapter);break;
                        default:throw new InvalidOperationException("Unknown write family");
                    }
                }
            }
            int result=0;try{Run(false);}catch(NotSupportedException){result=-1;unsupported++;A(99);}
            string Actual()=>JsonSerializer.Serialize(new object[]{result,buffers.Skip(1).ToArray(),direct?trace:trace.Where(t=>(int)t[0]!=2).ToList()});
            string expected=JsonSerializer.Serialize(row[2]);Require(Actual()==expected,$"Current collision write{count}/{kind}: {Actual()} expected {expected}");
            if(direct&&result==0)
            {
                trace.Clear();for(int i=1;i<=4;i++){for(int j=0;j<buffers[i].Length;j++){buffers[i][j]=unchecked((ushort)((i*0x1234+j*0x1021)^(1<<(j%16))));}}
                Run(true);Require(Actual()==expected,"Collision write adapter changed cells or ordered writes");bound++;
            }
            if(!direct){composed++;}count++;
        }
        Require(count==39984&&unsupported>0&&bound>0&&composed==9216,"Incomplete collision write coverage");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Write preparation enabled incomplete summon");
        Log.Information("Pet collision writes:{Count}complete current-code cases,{Unsupported}unsupported controls,{Bound}direct bindings,{Composed}complete footprint compositions;ordered writes/final buffers/partial failure match;native terrain ownership and room links remain unfinished",count,unsupported,bound,composed);
    }
}
