// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetFollowMovementChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetFollowMovementChecks));
    private static readonly long[] Opaque=[0x1122334455667788,0x2233445566778899,0x33445566778899aa,0x445566778899aabb];
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(string n in new[]{"generic-pet-follow-movement-check","generic-pet-follow-movement-reload-check"})
        {ConsoleInputHandler.RegisterCommand(n,"Current composed follow movement services",_=>Core.LoopContext.Post(()=>{try{Check();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));}
    }
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-follow-movement-source-probe.json")));int count=0,fatal=0,composed=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            string family=row[0].GetString();var p=row[1];int I(string n)=>p.GetProperty(n).GetInt32();bool B(string n)=>p.GetProperty(n).GetBoolean();
            (int,int) XY(string n){var a=p.GetProperty(n);return(a[0].GetInt32(),a[1].GetInt32());}
            var unit=new GenericPetMovementUnit{Kind=I("kind"),TableSelector=I("selector"),HasMonsterData=I("metadata")!=0,HasMovementData=I("movement")!=0,HasAiControl=I("control")!=0,HasPath=I("path")!=0,HasAux88=I("aux")!=0,
                Word20=-11,Word24=22,Byte28=33,PathByteBf=44,Aux88Word1c=I("auxWord"),Aux88FlagsB34=p.GetProperty("auxFlags").GetUInt32(),AiWord38=checked((ushort)I("ai"))};
            int identity=0;foreach(var e in p.GetProperty("events").EnumerateArray()){unit.Events.Add(new(identity++,checked((byte)e[0].GetInt32()),e[1].GetInt32()));}
            var pet=unit.Kind>=0?unit:null;var random=new GenericPetActionRandom(p.GetProperty("seed").GetUInt64());
            var(px,py)=XY("pet");var(ox,oy)=XY("owner");var owner=new GenericPetDecisionUnit(I("ownerKind"),B("ownerPath"),ox,oy,B("playerData"),70,80);
            var responses=p.GetProperty("responses").EnumerateArray().Select(e=>e.GetInt32()).ToArray();int calls=0,regions=0;var trace=new List<long[]>();void A(params long[] a)=>trace.Add(a);
            var movement=new GenericPetMovementServices(selector=>{A(6,selector,I("table"));return I("table");},
                mode=>{A(0,mode);return new(77,-111,222,Opaque);},
                request=>{int r=responses[Math.Min(calls++,responses.Length-1)];var words=(long[])request.Opaque;A(1,request.Target,request.X,request.Y,words[0],words[1],words[2],words[3],r);return r;},
                id=>{A(2,id);Require(unit.Events.RemoveAll(e=>e.Identity==id)==1,"Follow event identity");},
                (type,frame,a,b,c)=>A(3,type,frame,a,b,c),mode=>A(4,mode),frames=>A(5,frames));
            (int,int) Destination()=>GenericPetFollowMovement.Destination(owner,()=>{A(20,I("moving"));return (I("moving")&255)!=0;},()=>XY("future"));
            var leaves=new GenericPetFollowExecutionServices(Destination,()=>{A(21);return 1;},
                (room,x,y)=>{A(22,room==2?1:0,x,y);return I("regionState")==0?0:2;},
                (room,x,y)=>{Require(room==2,"Region handle");A(23,x,y);var ids=p.GetProperty("regionIds");int r=ids[Math.Min(regions++,ids.GetArrayLength()-1)].GetInt32();return I("regionState")==1?null:r;},
                (x,y)=>{A(24,x,y);return I("direction");},()=>{A(9,I("selector"),742,I("monster"));return I("monster");},
                (_,_,_)=>throw new InvalidOperationException("Unbound prepare"),()=>throw new InvalidOperationException("Unbound think"),
                (_,_)=>throw new InvalidOperationException("Unbound point"),(_,_,_,_,_)=>throw new InvalidOperationException("Unbound random"),
                (_,_,_,_,_,_,_)=>throw new InvalidOperationException("Unbound bounded"),(_,_,_,_,_,_)=>throw new InvalidOperationException("Unbound alternate"),
                (_,_,_,_)=>throw new InvalidOperationException("Unbound toward"),()=>{A(25,I("warp"));return I("warp");});
            var bound=GenericPetFollowMovement.Bind(leaves,pet,random,()=>I("frame"),movement,
                (x,y,radius,hint,flags)=>{A(11,x,y,radius,hint,flags,I("move"));return I("move");});
            int result=0;int[] destination=[];
            try
            {
                if(family=="destination"){var(x,y)=Destination();destination=[x,y];}
                else if(family=="point"){result=bound.Point(I("x"),I("y"));}
                else if(family=="random"){result=bound.Move(I("x"),I("y"),unchecked((byte)I("radius")),I("hint"),unchecked((ushort)I("flags")));}
                else if(family=="alternate"){result=bound.Alternate(I("x"),I("y"),I("hint"),I("speed"),unchecked((byte)I("tuning")),I("options"));}
                else if(family=="bound"){result=bound.Bound(I("x"),I("y"),I("hint"),unchecked((byte)I("radius")),I("speed"),unchecked((byte)I("tuning")),I("options"));}
                else if(family=="toward"){result=bound.Toward(I("x"),I("y"),unchecked((byte)I("radius")),unchecked((byte)I("tuning")));}
                else
                {
                    Require(family=="follow","Unknown composed source family");composed++;
                    result=GenericPetFollowExecutor.Execute(new(pet!=null?new(unit.Kind,unit.HasPath,px,py):null,B("ownerPresent")?owner:null,I("mode"),I("close"),I("wander"),I("word70")),random,bound);
                }
            }
            catch(NotSupportedException){result=-1;fatal++;A(99);}
            object[] actual=[result,new long[]{unit.Word20,unit.Word24,unit.Byte28,unit.PathByteBf,unit.AiWord38,unchecked((uint)random.Seed),(long)(random.Seed>>32)},unit.Events.Select(e=>new[]{e.Identity,(int)e.Type,e.Frame}).ToArray(),trace,destination];
            string got=JsonSerializer.Serialize(actual),expected=JsonSerializer.Serialize(row[2]);
            Require(got==expected,"Composed follow "+family+" case "+count+":"+got+" expected:"+expected);count++;
        }
        Require(count>3000&&fatal>0&&composed>=294,"Incomplete composed current movement matrix");
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Composed services enabled incomplete learned summon");
        Log.Information("GenericPet follow movement:{Count}complete captured-current cases,{Composed}full executor/service compositions,{Fatal}explicit unsupported paths;source RNG/mode/event/byte outcomes pass,actual world and warp remain unfinished",count,composed,fatal);
    }
}
