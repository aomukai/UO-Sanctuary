// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class GenericPetNearbyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetNearbyChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("generic-pet-nearby-check","Current no-field nearby search and placement composition",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("generic-pet-nearby-check: passed");}catch(Exception e){Log.Error(e,"generic-pet-nearby-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-nearby-source-probe.json")));
        int count=0,unsupported=0,bound=0,placed=0,refused=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row[0];int I(string n)=>p.GetProperty(n).GetInt32();
            int[] V(string n)=>p.GetProperty(n).EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            int[] start=V("start"),rooms=V("rooms"),responses=V("responses");bool present=p.GetProperty("present").GetBoolean();
            var trace=new List<object[]>();int roomCalls=0,checks=0;
            void A(params object[] args)=>trace.Add(args);
            int Room(int room,int x,int y){int next=rooms[Math.Min(roomCalls++,rooms.Length-1)];A(5,room,x,y,next);return next;}
            int Collision(int x,int y)
            {
                long dx=(long)x-start[0],dy=(long)y-start[1];
                int result=I("style") switch
                {
                    0=>responses[Math.Min(checks,responses.Length-1)],
                    1=>Math.Abs(dx)+Math.Abs(dy)<=2?1:0,
                    2=>dx>=0?1:0,
                    3=>dx==-1&&dy==0?0:1,
                    4=>dy==3?0:1,
                    _=>throw new InvalidOperationException("Unknown collision policy")
                };
                checks++;return result;
            }
            var footprints=new GenericPetCollisionFootprintServices((_,_,_)=>throw new InvalidOperationException("Search attempted a direct grid update"),
                (room,x,y,mask)=>{A(20,room,x,y,mask);return Collision(x,y);},
                (room,x,y,mask)=>{A(21,room,x,y,mask);return Collision(x,y);},
                (room,box,mask)=>{A(22,room,box.Left,box.Top,box.Right,box.Bottom,mask);return Collision(unchecked(box.Left+1),unchecked(box.Top+1));},
                (_,_,_,_)=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException(),
                (_,_,_,_)=>throw new InvalidOperationException(),(_,_,_)=>throw new InvalidOperationException());
            var search=GenericPetNearbySearch.WithFootprints(Room,footprints);
            var point=new GenericPetNearbyPoint(start[0],start[1]);int result;
            try{result=GenericPetNearbySearch.Find(I("initialRoom"),present?point:null,I("pattern"),I("mask"),I("fallback"),I("distance"),I("increment"),search);}
            catch(NotSupportedException){result=-1;unsupported++;A(99);}
            string expected=JsonSerializer.Serialize(row[1]);
            string Actual(int room,int x,int y)=>JsonSerializer.Serialize(new object[]{room,new[]{x,y},trace});
            Require(Actual(result,point.X,point.Y)==expected,$"Current nearby case{count}: {Actual(result,point.X,point.Y)} expected {expected}");
            GenericPetMonsterPlacementServices Placement()=>new(()=>1,(_,_,_)=>throw new InvalidOperationException(),
                _=>{},()=>I("pattern"),()=>2,_=>throw new InvalidOperationException(),()=>{},(_,_)=>1,()=>{},_=>{});
            if(present)
            {
                roomCalls=checks=0;trace.Clear();var adapter=GenericPetNearbySearch.Bind(Placement(),search);
                var found=adapter.Nearby(new(I("initialRoom"),start[0],start[1],0,I("pattern"),I("mask"),I("mask2"),I("fallback"),I("distance"),I("increment")));
                Require(Actual(found.Room,found.X,found.Y)==expected,"Nearby service adapter changed outputs or query order");bound++;
            }
            // These cases use the real pet caller's distance/masks/fallback/step.
            // Retain its before/search/after and success-only path/flag/sync order.
            if(I("distance")==50)
            {
                roomCalls=checks=0;trace.Clear();var events=new List<string>();var pet=new GenericPetWarpUnit(742,0);
                var unit=new GenericPetMonsterPlacementUnit(pet,true,1,742,I("pattern"),2,99);
                var placement=Placement() with
                {
                    Room=()=>{events.Add("room");return 1;},BeforeSearch=n=>{Require(n==1,"Before-search argument");events.Add("before");},
                    AfterSearch=()=>events.Add("after-search"),UpdatePath=(room,packed)=>
                    {
                        Require(room==result&&packed==unchecked((uint)(ushort)point.X|((uint)(ushort)point.Y<<16)),"Placement committed different coordinates");
                        events.Add("commit");return 1;
                    },AfterPlacement=()=>events.Add("after-placement"),Synchronize=n=>{Require(n==0,"Synchronization argument");events.Add("sync");}
                };
                int got=GenericPetMonsterPlacement.Place(unit,I("initialRoom"),start[0],start[1],0,GenericPetNearbySearch.Bind(placement,search));
                Require(got==(result==0?0:1)&&JsonSerializer.Serialize(trace)==JsonSerializer.Serialize(row[1][2]),"Search/placement composition changed output or query order");
                Require(string.Join(",",events)==(got==0?"room,before,after-search":"room,before,after-search,commit,after-placement,sync"),"Search/placement event boundary changed");
                Require(unit.PathWord34==(got==0?99:0)&&pet.Flags==(got==0?0u:0x10000u),"Failed search mutated success-only fields");placed++;
            }
            count++;
        }
        void Refuse(Action run){try{run();throw new InvalidOperationException("Unsupported search was admitted");}catch(NotSupportedException){refused++;}}
        var blocked=new GenericPetNearbyServices((room,_,_)=>room,_=>1);
        foreach(var (x,y,distance,increment) in new[]{(0,0,51,1),(0,0,3,0),(0,0,3,-1),(0,0,3,51),(int.MaxValue,0,3,1),(0,int.MinValue,3,1)})
        {Refuse(()=>GenericPetNearbySearch.Find(1,new(x,y),2,0x1c09,0,distance,increment,blocked));}
        var unused=new GenericPetMonsterPlacementServices(()=>1,(_,_,_)=>1,_=>{},()=>2,()=>2,_=>new(1,0,0),()=>{},(_,_)=>1,()=>{},_=>{});
        Refuse(()=>GenericPetNearbySearch.Bind(unused,blocked).Nearby(new(1,0,0,1,2,0x1c09,0x1c09,0,50,1)));
        Require(count==3010&&unsupported==6&&bound==3004&&placed==20&&refused==7,"Incomplete nearby differential/binding/guard coverage");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Search preparation enabled incomplete summon");
        Log.Information("GenericPet nearby:{Count}complete current-code cases,{Unsupported}unsupported controls,{Bound}service bindings,{Placed}actual caller compositions,{Refused}explicit guard controls;native room/terrain/controller remain unfinished",count,unsupported,bound,placed,refused);
    }
}
