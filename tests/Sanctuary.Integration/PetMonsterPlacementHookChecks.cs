// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class PetMonsterPlacementHookChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PetMonsterPlacementHookChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-monster-placement-hooks-check","Current forced monster footprint remove/restore differential",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("pet-monster-placement-hooks-check: passed");}catch(Exception e){Log.Error(e,"pet-monster-placement-hooks-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-monster-placement-hooks-source-probe.json")));
        int count=0,unsupported=0;
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
            var path=new GenericPetCommitPath{Actor=new(1,1),X=((uint)(ushort)I("x")<<16)|0x8000,Y=((uint)(ushort)I("y")<<16)|0x8000,
                Room=I("room"),Mask60=I("mask"),Size5c=I("pattern")};
            var unbound=new GenericPetMonsterPlacementServices(()=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),
                _=>throw new InvalidOperationException(),()=>throw new InvalidOperationException(),()=>throw new InvalidOperationException(),
                _=>throw new InvalidOperationException(),()=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
                ()=>throw new InvalidOperationException(),_=>throw new InvalidOperationException());
            var placement=GenericPetMonsterPlacementHooks.Bind(unbound,path,adapter);int result=0;
            try
            {
                if(kind=="remove"){placement.BeforeSearch(1);}
                else{Require(kind=="restore","Unknown placement-hook operation");placement.AfterSearch();}
            }
            catch(NotSupportedException){result=-1;unsupported++;A(99);}
            string actual=JsonSerializer.Serialize(new object[]{result,buffers.Skip(1).ToArray(),trace.Where(t=>(int)t[0]!=2).ToList()}),expected=JsonSerializer.Serialize(row[2]);
            Require(actual==expected,$"Current monster-hook case{count}/{kind}: {actual} expected {expected}");count++;
        }
        Require(count==9780&&unsupported>0,"Incomplete current monster-hook coverage");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Hook comparison enabled incomplete summon");
        Log.Information("Pet monster placement hooks:{Count}complete current-code cases,{Unsupported}unsupported controls;actual force1monster branch/mask-size getters/composed footprint writes match;other unit/conditional-removal/controller scope remains unfinished",count,unsupported);
    }
}
