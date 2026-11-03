// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class PetCollisionReadChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PetCollisionReadChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-collision-reads-check","Current collision reads/room resolution/box splitting",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("pet-collision-reads-check: passed");}catch(Exception e){Log.Error(e,"pet-collision-reads-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private sealed class Cells(int room,int count,Action<int,int,int> read):IReadOnlyList<ushort>
    {
        public int Count=>count;
        public ushort this[int index]
        {
            get{ushort value=unchecked((ushort)((room*0x1234+index*0x1021)^(1<<(index%16))));read(room,index,value);return value;}
        }
        public IEnumerator<ushort> GetEnumerator(){for(int i=0;i<Count;i++){yield return this[i];}}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-collision-reads-source-probe.json")));
        int count=0,unsupported=0,bound=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            string kind=row[0].GetString();var p=row[1];int I(string n)=>p.GetProperty(n).GetInt32();
            int[] V(string n)=>p.GetProperty(n).EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            var trace=new List<object[]>();void A(params object[] args)=>trace.Add(args);
            var rooms=new SourceCollisionRoom[5];
            for(int i=1;i<=4;i++)
            {
                int width=I("width"),height=I("height");var cells=(I("cells")&(1<<(i-1)))==0?null:new Cells(i,width*height,(r,index,value)=>A(1,r,index,value));
                rooms[i]=new(20+((i-1)%2)*width,30+((i-1)/2)*height,width,height,width,cells,V("neighbors"));
            }
            int Room(int current,int x,int y){A(0,current,x,y);return SourceRoomTrace.Resolve(current,x,y,id=>rooms[id]);}
            var services=new GenericPetCollisionReadServices(Room,id=>(I("grids")&(1<<(id-1)))==0?null:rooms[id]);
            var raw=V("box");var box=new GenericPetCollisionBox(raw[0],raw[1],raw[2],raw[3]);int result;
            try
            {
                result=kind switch
                {
                    "resolve"=>Room(I("room"),I("x"),I("y")),
                    "point"=>GenericPetCollisionReads.Point(I("room"),I("x"),I("y"),I("mask"),services),
                    "adjacent"=>GenericPetCollisionReads.Adjacent(I("room"),I("x"),I("y"),I("mask"),services),
                    "box"=>GenericPetCollisionReads.Box(I("room"),box,I("mask"),services),
                    _=>throw new InvalidOperationException("Unknown read family")
                };
            }
            catch(NotSupportedException){result=-1;unsupported++;A(99);}
            string Actual(int value)=>JsonSerializer.Serialize(new object[]{value,trace});string expected=JsonSerializer.Serialize(row[2]);
            Require(Actual(result)==expected,$"Current collision case{count}/{kind}: {Actual(result)} expected {expected}");
            // Share these reads with the earlier checked footprint dispatch.
            // Box pattern3 is a centered3x3 request, not arbitrary rectangle.
            bool regularBox=box.Right-box.Left==2&&box.Bottom-box.Top==2;
            if(result>=0&&kind!="resolve"&&(kind!="box"||regularBox))
            {
                trace.Clear();
                var unavailable=new GenericPetCollisionFootprintServices((_,_,_)=>throw new InvalidOperationException(),
                    (_,_,_,_)=>throw new InvalidOperationException(),(_,_,_,_)=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),
                    (_,_,_,_)=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),
                    (_,_,_,_)=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException());
                var request=kind=="box"?new GenericPetFootprintRequest(I("room"),box.Left+1,box.Top+1,3,I("mask"))
                    :new GenericPetFootprintRequest(I("room"),I("x"),I("y"),kind=="point"?0:2,I("mask"));
                int got=GenericPetCollisionFootprints.CheckPattern(request,GenericPetCollisionReads.Bind(unavailable,services));
                Require(Actual(got)==expected,"Collision read/footprint composition changed outputs or read order");bound++;
            }
            count++;
        }
        Require(count==25100&&unsupported>0&&bound>0,"Incomplete collision read/room/split coverage");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Read preparation enabled incomplete summon");
        Log.Information("Pet collision reads:{Count}complete current-code cases,{Unsupported}unsupported controls,{Bound}footprint bindings;room resolution/cardinal edge order/recursive split/low16 masks match;native terrain ownership and writes remain unfinished",count,unsupported,bound);
    }
}
