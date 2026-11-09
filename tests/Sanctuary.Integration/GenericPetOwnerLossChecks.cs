// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;
public static class GenericPetOwnerLossChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetOwnerLossChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int N(JsonElement e,string k)=>e.GetProperty(k).GetInt32();
    private static bool B(JsonElement e,string k)=>e.GetProperty(k).GetBoolean();
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-owner-loss-check","Current missing-owner death orchestration",_=>Core.LoopContext.Post(()=>
    {try{Source();Log.Information("pet-owner-loss-check: passed");}catch(Exception e){Log.Error(e,"pet-owner-loss-check: failed");}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-owner-loss-cases.json")));int count=0,composed=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var expected=row.GetProperty("output");var trace=new List<string>();int kind=N(p,"kind"),cls=N(p,"classId"),ai90=N(p,"ai90");uint flags=p.GetProperty("flags").GetUInt32();var token=new object();
            void MutateInt(string key,ref int value){var e=p.GetProperty(key);if(e.ValueKind!=JsonValueKind.Null){value=e.GetInt32();}}
            void MutateFlags(string key){var e=p.GetProperty(key);if(e.ValueKind!=JsonValueKind.Null){flags=e.GetUInt32();}}
            GenericPetOwnerLossUnit Unit(int who)=>who switch
            {
                11=>new(new(kind,B(p,"path"),0,0),cls,N(p,"mode"),flags,B(p,"data"),B(p,"ai"),B(p,"ownerGame")?1:0,0,77,ai90,BitConverter.Int32BitsToSingle(unchecked((int)p.GetProperty("facingBits").GetUInt32()))),
                22=>new(new(N(p,"ownerKind"),true,0,0),0,N(p,"ownerMode"),0,false,false,0,0,0,0,0),
                33=>new(new(N(p,"killerKind"),B(p,"killerPath"),N(p,"x"),N(p,"y")),0,1,0,false,false,0,0,0,0,0),
                _=>throw new InvalidOperationException("Wrong owner-loss identity")
            };
            var services=new GenericPetOwnerLossServices(()=>1,Unit,
                (selector,id)=>{trace.Add($"primary:{selector}:{id}");MutateInt("afterPrimaryKind",ref kind);return B(p,"primary")?B(p,"killable"):null;},
                (game,type,id)=>{Require(game==1&&type==0&&id==77,"Owner-loss registry arguments changed");trace.Add("resolve:0:77");return B(p,"owner")?22:0;},
                (owner,actor)=>{Require(owner==22&&actor==11,"Detach arguments changed");trace.Add("detach");MutateFlags("afterDetachFlags");MutateInt("afterDetachAi",ref ai90);},
                (actor,vtable)=>{Require(actor==11&&vtable==0x1722cd0,"Linked callback context changed");trace.Add($"notify:{vtable}");},
                _=>trace.Add("clear"),(_,_)=>trace.Add("credit"),
                (_,mode)=>{trace.Add($"initialize:{mode}");MutateInt("afterInitKind",ref kind);return new(123,7,8,token);},
                (_,x,y)=>{trace.Add($"direction:{x}:{y}");return N(p,"direction");},
                (request,facing,force)=>
                {
                    Require(ReferenceEquals(request.Opaque,token)&&request.Target==(B(p,"killer")?33:0)&&request.X==7&&request.Y==8,"Death request lost initializer fields");
                    trace.Add($"execute:{facing}:{force}");MutateFlags("afterExecuteFlags");
                },(_,_)=>trace.Add("after"),
                (selector,id)=>{trace.Add($"secondary:{selector}:{id}");MutateInt("afterSecondaryClass",ref cls);return B(p,"secondary")?new(id,"explicit",0,0,checked((byte)N(p,"flags6")),0,0):null;},
                (_,replacement)=>trace.Add($"transform:{replacement}"));
            bool unsupported=false;
            try
            {
                if(B(p,"viaOverride"))
                {
                    composed++;var control=new GenericPetAiControl(0,0x4b60c0,1,false,[]);var tick=new GenericPetAiTick{Control=control};
                    var original=new GenericPetAiOverrideServices(_=>new(0,1),who=>
                    {
                        var u=Unit(who);return new(u.Position,u.Mode,u.Flags124,0,u.HasData,u.HasAi,u.OwnerGame,u.OwnerKind,u.OwnerId);
                    },services.Resolve,
                        (_,_)=>throw new InvalidOperationException("Missing-owner transition initialized a normal frame"),
                        (_,_,_)=>throw new InvalidOperationException("Missing-owner transition followed"),(_,_,_)=>throw new InvalidOperationException("Missing-owner transition called default"),
                        _=>throw new InvalidOperationException("Missing-owner transition queried movement"),(_,_,_,_,_)=>throw new InvalidOperationException("Missing-owner transition searched"),
                        _=>throw new InvalidOperationException("Missing-owner transition queried events"),_=>throw new InvalidOperationException("Missing-owner transition queried event type"),
                        _=>throw new InvalidOperationException("Missing-owner transition removed events directly"),(_,_)=>throw new InvalidOperationException("Missing-owner transition idled"),
                        _=>throw new InvalidOperationException("Missing-owner transition released search"),_=>throw new InvalidOperationException("Missing-owner transition released frame"),
                        (_,_,_)=>throw new InvalidOperationException("Unbound missing-owner action"));
                    GenericPetAiOverrides.Run(1,11,tick,0,GenericPetOwnerLoss.Bind(original,services));
                }
                else{GenericPetOwnerLoss.Run(B(p,"actor")?11:0,B(p,"killer")?33:0,N(p,"detach"),services);}
            }
            catch(NotSupportedException){unsupported=true;}
            Require(unsupported==B(expected,"unsupported")&&trace.SequenceEqual(expected.GetProperty("trace").EnumerateArray().Select(v=>v.GetString())),$"Owner-loss source boundary/order differs at{count}");
            Require(kind==N(expected,"kind")&&cls==N(expected,"classId")&&flags==expected.GetProperty("flags").GetUInt32()&&ai90==N(expected,"ai90"),$"Owner-loss state rereads differ at{count}");
            count++;
        }
        Require(count==48&&composed==6,"Missing direct/composed owner-loss controls");
        Log.Information("Owner loss:48current contexts/706bytes plus933callback;6actual missing-owner transitions match. Killable/dead guards,detach/linked notifications,credit,mode/facing,flag rereads and special transforms retain source order. Native death services remain explicit.");
    }
}
