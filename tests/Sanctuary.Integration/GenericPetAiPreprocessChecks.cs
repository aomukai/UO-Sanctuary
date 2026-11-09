// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class GenericPetAiPreprocessChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetAiPreprocessChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int N(JsonElement e,string k)=>e.GetProperty(k).GetInt32();
    private static bool B(JsonElement e,string k)=>e.GetProperty(k).GetBoolean();
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-ai-preprocess-check","Current AI preprocessing and saved-target movement",_=>Core.LoopContext.Post(()=>
    {try{Source();Log.Information("pet-ai-preprocess-check: passed");}catch(Exception e){Log.Error(e,"pet-ai-preprocess-check: failed");}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-ai-preprocess-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var expected=row.GetProperty("output");var trace=new List<string>();
            var movement=new GenericPetMovementUnit{Word20=9,Byte28=7,HasMonsterData=B(p,"data"),HasMovementData=B(p,"movement")};
            var path=new GenericPetAiPrePath(33,N(p,"pathX"),N(p,"pathY"),14,N(p,"pathActive"),checked((ushort)N(p,"collision")));
            var memory=new GenericPetAiSavedTarget(N(p,"id"),N(p,"targetKind"));
            var control=new GenericPetAiControl(0,0x4b60c0,2,false,[]);
            var tick=new GenericPetAiTick{Control=B(p,"control")?control:null,RowA=B(p,"primary")?1:0};
            GenericPetAiPreUnit Unit(int id)=>id switch
            {
                11=>new(new(N(p,"kind"),B(p,"path"),0,0),742,checked((byte)N(p,"selector")),1,0,0,N(p,"count"),B(p,"hasRoot"),N(p,"root1c"),p.GetProperty("rootb30").GetUInt32(),B(p,"path")?path:null,movement),
                22=>new(new(N(p,"candidateKind"),B(p,"targetPath"),N(p,"x"),N(p,"y")),7,1,N(p,"mode"),p.GetProperty("flags124").GetUInt32(),p.GetProperty("flags128").GetUInt32(),0,false,0,0,null,null),
                33=>new(new(2,false,0,0),5,1,0,0,0,0,false,0,0,null,null),
                _=>throw new InvalidOperationException("Wrong preprocessing identity")
            };
            var services=new GenericPetAiPreServices(()=>1,Unit,
                c=>ReferenceEquals(c,control)?memory:throw new NotSupportedException("Missing control"),r=>r==1?checked((byte)N(p,"flags")):null,
                (selector,id)=>
                {
                    trace.Add($"walk:{selector}:{id}");var late=p.GetProperty("lateId");if(late.ValueKind!=JsonValueKind.Null){memory.Id=late.GetInt32();}
                    late=p.GetProperty("lateKind");if(late.ValueKind!=JsonValueKind.Null){memory.Kind=late.GetInt32();}
                    return B(p,"walkRow")?B(p,"walk"):null;
                },(kind,id)=>{trace.Add($"resolve:{kind}:{id}");return B(p,"target")?22:0;},(_,_)=>{trace.Add("distance");return N(p,"distance");},
                (_,x,y,radius,hint,flags)=>{trace.Add($"toward:{x}:{y}:{radius}:{hint}:{flags}");return N(p,"toward");},
                (_,_,speed,flags)=>trace.Add($"targetMove:{speed}:{flags}"),
                (room,x,y,pattern,mask)=>{Require(room==33,"Collision room changed");trace.Add($"collision:{x}:{y}:{pattern}:{mask}");return checked((ushort)N(p,"collisionResult"));},
                (_,limit)=>{trace.Add($"doorSearch:7:{limit}");return B(p,"door")?33:0;},
                (selector,id)=>{trace.Add($"doorMetadata:{selector}:{id}");return B(p,"doorRow")?B(p,"usable"):null;},
                (_,_)=>{trace.Add("canInteract");return B(p,"interact");},(_,_)=>trace.Add("interact"),(_,frames)=>trace.Add($"idle:{frames}"));
            bool result=false,unsupported=false;
            try{result=B(p,"direct")?GenericPetAiPreprocess.SavedTarget(B(p,"actor")?11:0,tick,services):GenericPetAiPreprocess.Run(B(p,"actor")?11:0,tick,services);}
            catch(NotSupportedException){unsupported=true;}
            Require(unsupported==B(expected,"unsupported")&&trace.SequenceEqual(expected.GetProperty("trace").EnumerateArray().Select(v=>v.GetString())), $"AI preprocessing boundary/trace differs at{count}");
            Require(memory.Id==N(expected,"id")&&memory.Kind==N(expected,"targetKind")&&movement.Word20==N(expected,"word20")&&movement.Byte28==N(expected,"byte28")&&path.Collision==N(expected,"collision"),$"AI preprocessing state writes differ at{count}");
            if(!unsupported){Require(result==B(expected,"result"),$"AI preprocessing result differs at{count}");}
            count++;
        }
        Require(count==51,"Missing preprocessing controls");
        Log.Information("AI preprocessing:51current contexts/1008bytes;stun pause,door collision/interaction,invalid saved targets,near/far movement and source coordinate/action arguments match. Native leaves/startup memory remain explicit.");
    }
    internal static bool Original(SkeletonGuardian body,GenericPetAiControl control,GenericPetAiTick tick)
    {
        int id=checked((int)body.Serial.Value),cls=WarlockDemonPlacement.ClassId(body);
        var view=GenericPetSearchTables.Bind(1);var primary=view.Monster(1,cls)??throw new InvalidOperationException("Missing original AI metadata");
        // This no-status/no-door/no-saved-target input is an explicit fixture,
        // not an assertion about authentic birth memory or live world state.
        var path=new GenericPetAiPrePath(0,body.X,body.Y,0,1,0);var memory=new GenericPetAiSavedTarget(-1,0);
        var services=new GenericPetAiPreServices(()=>1,
            actor=>actor==id?new(new(1,true,body.X,body.Y),cls,1,1,0,0,232,false,0,0,path,new()):throw new InvalidOperationException("Wrong original AI identity"),
            c=>ReferenceEquals(c,control)?memory:throw new InvalidOperationException("Wrong original control"),
            r=>r==tick.RowA?primary.Flags60:throw new InvalidOperationException("Wrong primary row"),
            (_,_)=>throw new InvalidOperationException("Unexpected saved-target lookup"),(_,_)=>throw new InvalidOperationException("Unexpected resolve"),
            (_,_)=>throw new InvalidOperationException("Unexpected distance"),(_,_,_,_,_,_)=>throw new InvalidOperationException("Unexpected point movement"),
            (_,_,_,_)=>throw new InvalidOperationException("Unexpected target movement"),(_,_,_,_,_)=>throw new InvalidOperationException("Unexpected collision query"),
            (_,_)=>throw new InvalidOperationException("Unexpected door search"),(_,_)=>throw new InvalidOperationException("Unexpected door metadata"),
            (_,_)=>throw new InvalidOperationException("Unexpected interaction query"),(_,_)=>throw new InvalidOperationException("Unexpected interaction"),
            (_,_)=>throw new InvalidOperationException("Unexpected idle"));
        bool stopped=GenericPetAiPreprocess.Run(id,tick,services);
        Require(!stopped&&memory.Id==-1&&path.Collision==0,"Original preprocessing changed its explicit fixture state");
        return stopped;
    }
}
