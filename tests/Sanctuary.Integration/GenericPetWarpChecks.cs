// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetWarpChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetWarpChecks));
    private static readonly long[] Opaque=[0x1122334455667788,0x2233445566778899,0x33445566778899aa,0x445566778899aabb];
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(string n in new[]{"generic-pet-warp-check","generic-pet-warp-reload-check"})
        {ConsoleInputHandler.RegisterCommand(n,"Current captured warp recovery and follow composition",_=>Core.LoopContext.Post(()=>{try{Check();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));}
    }
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-warp-source-probe.json")));int count=0,fatal=0,composed=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            string family=row[0].GetString();var p=row[1];int I(string n)=>p.GetProperty(n).GetInt32();bool B(string n)=>p.GetProperty(n).GetBoolean();
            (int,int) XY(string n){var a=p.GetProperty(n);return(a[0].GetInt32(),a[1].GetInt32());}
            var unit=new GenericPetMovementUnit{Kind=I("kind"),TableSelector=I("selector"),HasMonsterData=I("metadata")!=0,HasMovementData=I("movement")!=0,HasAiControl=I("control")!=0,HasPath=I("path")!=0,HasAux88=I("aux")!=0,
                Word20=-11,Word24=22,Byte28=33,PathByteBf=44,Aux88Word1c=I("auxWord"),Aux88FlagsB34=p.GetProperty("auxFlags").GetUInt32(),AiWord38=checked((ushort)I("ai"))};
            int identity=0;foreach(var e in p.GetProperty("events").EnumerateArray()){unit.Events.Add(new(identity++,checked((byte)e[0].GetInt32()),e[1].GetInt32()));}
            var pet=unit.Kind>=0?unit:null;var random=new GenericPetActionRandom(p.GetProperty("seed").GetUInt64());
            var(px,py)=XY("pet");var(ox,oy)=XY("owner");var owner=new GenericPetDecisionUnit(I("ownerKind"),B("ownerPath"),ox,oy,B("playerData"),70,80);
            var responses=p.GetProperty("responses").EnumerateArray().Select(e=>e.GetInt32()).ToArray();int calls=0,regions=0,roomCalls=0,placeCalls=0;var trace=new List<long[]>();void A(params long[] a)=>trace.Add(a);
            var movement=new GenericPetMovementServices(selector=>{A(6,selector,I("table"));return I("table");},
                mode=>{A(0,mode);return new(77,-111,222,Opaque);},
                request=>{int r=responses[Math.Min(calls++,responses.Length-1)];var words=(long[])request.Opaque;A(1,request.Target,request.X,request.Y,words[0],words[1],words[2],words[3],r);return r;},
                id=>{A(2,id);Require(unit.Events.RemoveAll(e=>e.Identity==id)==1,"Follow event identity");},
                (type,frame,a,b,c)=>A(3,type,frame,a,b,c),mode=>A(4,mode),frames=>A(5,frames));
            (int,int) Destination()=>GenericPetFollowMovement.Destination(owner,()=>GenericPetMotionServices.Moving(
                new(owner,I("ownerMode"),B("aux100"),B("aux18"),p.GetProperty("aux14").GetUInt32(),checked((byte)I("path48"))),
                index=>{A(26,I("selector"),743,I("ownerMonster"));return I("ownerMonster")<0?null:unchecked((byte)(I("modeBits")>>(index*8)));}),()=>XY("future"));
            var leaves=new GenericPetFollowExecutionServices(Destination,()=>{A(21);return 1;},
                (room,x,y)=>{A(22,room==2?1:0,x,y);return I("regionState")==0?0:2;},
                (room,x,y)=>{Require(room==2,"Region handle");A(23,x,y);var ids=p.GetProperty("regionIds");int r=ids[Math.Min(regions++,ids.GetArrayLength()-1)].GetInt32();return I("regionState")==1?null:r;},
                (x,y)=>{A(24,x,y);return I("direction");},()=>{A(9,I("selector"),742,I("monster"));return I("monster");},
                (_,_,_)=>throw new InvalidOperationException("Unbound prepare"),()=>throw new InvalidOperationException("Unbound think"),
                (_,_)=>throw new InvalidOperationException("Unbound point"),(_,_,_,_,_)=>throw new InvalidOperationException("Unbound random"),
                (_,_,_,_,_,_,_)=>throw new InvalidOperationException("Unbound bounded"),(_,_,_,_,_,_)=>throw new InvalidOperationException("Unbound alternate"),
                (_,_,_,_)=>throw new InvalidOperationException("Unbound toward"),()=>{A(25,I("warp"));return I("warp");});
            var bound=GenericPetMotionServices.Bind(leaves,pet,pet!=null?new(unit.Kind,unit.HasPath,px,py):null,random,()=>I("frame"),movement);
            uint initialFlags=p.GetProperty("flags128").GetUInt32();var warpPet=pet!=null?new GenericPetWarpUnit(I("classId"),initialFlags):null;
            uint Flags()=>warpPet?.Flags??initialFlags;int Present()=>warpPet!=null?1:0;
            var context=new GenericPetWarpContext(B("ownerPresent")?owner:null,warpPet,checked((byte)I("selector")));
            var warpServices=new GenericPetWarpServices(()=>{A(31,I("ownerRoom"));return I("ownerRoom")!=0?1:0;},
                (room,x,y)=>{Require(room==1,"Warp initial region room");A(32,x,y,I("warpRegion"));return I("warpRegion")!=0?1:0;},
                selector=>{A(30,selector,I("count"));return I("count");},
                (room,region,index)=>{var(x,y)=XY("found");A(33,room,region,index,0,I("findResult"),x,y);return new(I("findResult"),x,y);},
                (room,x,y)=>{var rooms=p.GetProperty("rooms");int next=rooms[Math.Min(roomCalls++,rooms.GetArrayLength()-1)].GetInt32();A(34,room,x,y,next);return next;},
                (room,x,y,force,zero)=>{var results=p.GetProperty("placements");int next=results[Math.Min(placeCalls++,results.GetArrayLength()-1)].GetInt32();A(35,Present(),room,x,y,force,zero,next);return next;},
                ()=>{uint packed=p.GetProperty("packed").GetUInt32();A(36,packed);return packed;},
                ()=>{A(37,Present(),I("size"));return I("size");},
                (room,x,y,size,mask)=>{A(38,room,x,y,size,mask,I("collision"));return I("collision");},
                ()=>A(39,Present(),Flags()),frames=>A(40,Present(),frames,Flags()));
            bound=GenericPetWarp.Bind(bound,context,warpServices);
            int result=0;int[] destination=[];
            try
            {
                if(family=="warp"){result=GenericPetWarp.Execute(context,warpServices);}
                else
                {
                    Require(family=="follow","Unknown warp source family");composed++;
                    result=GenericPetFollowExecutor.Execute(new(pet!=null?new(unit.Kind,unit.HasPath,px,py):null,B("ownerPresent")?owner:null,I("mode"),I("close"),I("wander"),I("word70")),random,bound);
                }
            }
            catch(NotSupportedException){result=-1;fatal++;A(99);}
            object[] actual=[result,new long[]{unit.Word20,unit.Word24,unit.Byte28,unit.PathByteBf,unit.AiWord38,unchecked((uint)random.Seed),(long)(random.Seed>>32)},unit.Events.Select(e=>new[]{e.Identity,(int)e.Type,e.Frame}).ToArray(),trace,destination,Flags()];
            string got=JsonSerializer.Serialize(actual),expected=JsonSerializer.Serialize(row[2]);
            Require(got==expected,"Composed follow "+family+" case "+count+":"+got+" expected:"+expected);count++;
        }
        Require(count==1360&&fatal>0&&composed==197,"Incomplete composed current movement matrix");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Composed services enabled incomplete learned summon");
        Log.Information("GenericPet warp:{Count}complete captured-current cases,{Composed}full executor/service compositions,{Fatal}explicit unsupported paths;warp recovery/order/flags pass,actual world placement and learned skills remain unfinished",count,composed,fatal);
    }
}
