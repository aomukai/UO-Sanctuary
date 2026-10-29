// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetMovementChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetMovementChecks));
    private static readonly long[] Opaque=[0x1122334455667788,0x2233445566778899,0x33445566778899aa,0x445566778899aabb];
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Current source movement policy and action bindings",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("generic-pet-movement-check");Register("generic-pet-movement-reload-check");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-movement-source-probe.json")));
        int count=0,fatal=0;var counts=new Dictionary<string,int>();
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            string family=row[0].GetString();var p=row[1];int I(string key)=>p.GetProperty(key).GetInt32();
            var unit=new GenericPetMovementUnit{Kind=I("kind"),TableSelector=I("selector"),HasMonsterData=I("metadata")!=0,HasMovementData=I("movement")!=0,HasAiControl=I("control")!=0,HasPath=I("path")!=0,HasAux88=I("aux")!=0,
                Word20=-11,Word24=22,Byte28=33,PathByteBf=44,Aux88Word1c=I("auxWord"),Aux88FlagsB34=p.GetProperty("auxFlags").GetUInt32(),AiWord38=checked((ushort)I("ai"))};
            int identity=0;foreach(var e in p.GetProperty("events").EnumerateArray()){unit.Events.Add(new(identity++,checked((byte)e[0].GetInt32()),e[1].GetInt32()));}
            var pet=unit.Kind>=0?unit:null;var random=new GenericPetActionRandom(p.GetProperty("seed").GetUInt64());
            var responses=p.GetProperty("responses").EnumerateArray().Select(e=>e.GetInt32()).ToArray();int calls=0;var trace=new List<long[]>();void A(params long[] values)=>trace.Add(values);
            var services=new GenericPetMovementServices(
                selector=>{A(6,selector,I("table"));return I("table");},
                mode=>{A(0,mode);return new(77,-111,222,Opaque);},
                request=>{int accepted=responses[Math.Min(calls++,responses.Length-1)];var words=(long[])request.Opaque;A(1,request.Target,request.X,request.Y,words[0],words[1],words[2],words[3],accepted);return accepted;},
                id=>{A(2,id);Require(unit.Events.RemoveAll(e=>e.Identity==id)==1,"Event identity lost during removal");},
                (type,frame,a,b,c)=>A(3,type,frame,a,b,c),mode=>A(4,mode),frames=>A(5,frames));
            int result=0;
            try
            {
                if(family=="prepare"){GenericPetMovement.Prepare(pet,I("speed"),I("tuning"),I("options"));}
                else if(family=="think"){GenericPetMovement.EnsureThink(pet,I("frame"),services);}
                else if(family=="execute"){result=GenericPetMovement.Execute(pet,random,I("mode"),I("target"),I("x"),I("y"),I("pathByte"),I("flags"),services);}
                else if(family=="approach"){result=GenericPetMovement.Approach(pet,random,I("frame"),I("target"),I("hint"),services)?1:0;}
                else
                {
                    Require(family is "bound" or "bound-range","Unknown source family");
                    var actions=new GenericPetActionServices(
                        (_,_,_)=>throw new InvalidOperationException("Unexpected skill selection"),
                        id=>{Require(id==70,"Unexpected target identity");return new(1,true,45,55);},
                        (x,y)=>{A(7,x,y,I("distance"));return I("distance");},
                        (id,mask)=>{A(10,id,mask,I("collision4"));return I("collision4")!=0;},
                        id=>{A(8,id,0);return false;},()=>throw new InvalidOperationException("Unbound preparation"),
                        (x,y,mode,zero,flags)=>{A(11,x,y,mode,zero,flags,I("move"));return I("move")!=0;},()=>throw new InvalidOperationException("Unbound event maintenance"),
                        selector=>{A(9,selector,unit.Kind>=0?742:-1,I("monster"));return I("monster")<0?null:checked((byte)I("monster"));},
                        (_,_)=>throw new InvalidOperationException("Unbound approach"),
                        (_,_,_,_,_,_)=>throw new InvalidOperationException("Unexpected skill execution"),
                        _=>throw new InvalidOperationException("Unexpected ordinary action mode"),_=>throw new InvalidOperationException("Unexpected ordinary action execution"),
                        (_,_)=>throw new InvalidOperationException("Unexpected skill row"),_=>false,_=>0);
                    var bound=GenericPetMovement.Bind(actions,pet,random,()=>I("frame"),services);
                    var context=new GenericPetCombatContext(checked((byte)I("selector")),unit.Kind<0?null:new(unit.Kind,unit.HasPath,20,30));
                    var frame=new GenericPetDecisionFrame(GenericPetParameters.Decode(null) with{RangedMinSquared=I("minimum"),RangedMaxSquared=I("maximum")},new());
                    var candidates=new List<GenericPetCombatCandidate>{new(70,0,0,0,0,0,0,family=="bound-range"?(byte)5:(byte)4,0,0,0)};
                    result=GenericPetActions.Disposition(context,frame,candidates,0,bound);
                }
            }
            catch(NotSupportedException){result=-1;fatal++;A(99);}
            object[] actual=[result,new long[]{unit.Word20,unit.Word24,unit.Byte28,unit.PathByteBf,unit.AiWord38,unchecked((uint)random.Seed),(long)(random.Seed>>32)},unit.Events.Select(e=>new[]{e.Identity,(int)e.Type,e.Frame}).ToArray(),trace];
            string got=JsonSerializer.Serialize(actual),expected=JsonSerializer.Serialize(row[2]);
            Require(got==expected,"Current movement "+family+" case "+count+":"+got+" expected:"+expected);
            counts[family]=counts.GetValueOrDefault(family)+1;count++;
        }
        Require(count>5672&&fatal>0&&counts["bound-range"]==20,"Missing current movement/binding cases");
        Log.Information("GenericPet movement:{Count}complete current-code cases,{Fatal}unsupported source boundaries;preparation,four ordered approach attempts,mode payloads,RNG,event removal/50frame deadline and action bindings pass;world metadata/modes/clock/seed remain required",count,fatal);
    }
}
