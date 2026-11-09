// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GenericPetLinkedUnitChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetLinkedUnitChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int N(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static bool B(JsonElement e,string key)=>e.GetProperty(key).GetBoolean();
    private static int? Maybe(JsonElement e,string key)=>e.GetProperty(key).ValueKind==JsonValueKind.Null?null:N(e,key);
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static int Id(IEntity e)=>checked((int)e.Serial.Value);
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-linked-units-check","Current linked-unit death notification and cleanup",_=>Core.LoopContext.Post(()=>
    {try{Source();Composition();Log.Information("pet-linked-units-check: passed");}catch(Exception e){Log.Error(e,"pet-linked-units-check: failed");}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-linked-units-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var expected=row.GetProperty("output");var trace=new List<string>();var ids=Ints(p.GetProperty("ids"));var vector=new GenericPetLinkedVector(ids,ids.Length);var targets=new Dictionary<int,GenericPetTraversalUnit>();
            int index=0;foreach(var target in p.GetProperty("targets").EnumerateArray())
            {
                int id=11+index++;targets[id]=new(N(target,"kind"),N(target,"mode"),id,target.GetProperty("flags124").GetUInt32(),target.GetProperty("flags128").GetUInt32(),null,B(target,"data"),
                    B(target,"ai")?new(B(target,"ownerGame")?71:0,N(target,"ownerKind"),N(target,"owner"),0):null);
            }
            var parent=new GenericPetTraversalUnit(N(p,"kind"),1,100,0,0,null,B(p,"data"),new(71,0,300,0));
            var services=new GenericPetLinkedServices(id=>id==100?parent:targets[id],_=>B(p,"ai")?vector:null,(game,kind,id)=>
            {
                Require(game==71,"Linked registry lost game");trace.Add($"resolve:{kind}:{id}");
                return kind==1&&(id==100||targets.ContainsKey(id))?id:kind==0&&id==300?300:0;
            });
            int callbacks=0;object result=null;bool unsupported=false;
            void Visit(int target)
            {
                trace.Add("death:"+target);callbacks++;if(B(p,"kill")){targets[target]=targets[target] with{Mode=0};}
                if(callbacks==1)
                {
                    if(Maybe(p,"editIndex")is{} at){ids[at]=N(p,"editValue");}
                    if(Maybe(p,"editCount")is{} size){vector.Count=size;}
                }
            }
            try
            {
                int actor=B(p,"actor")?100:0,game=B(p,"game")?71:0;string action=p.GetProperty("action").GetString();
                if(action=="valid"){result=GenericPetLinkedUnits.Valid(actor,N(p,"candidate"),services);}
                else
                {
                    if(action is "notify"or"both"){GenericPetLinkedUnits.Notify(game,actor,Visit,()=>trace.Add("release"),services);}
                    if(action is "clear"or"both"){result=GenericPetLinkedUnits.Clear(game,actor,services);}
                }
            }
            catch(NotSupportedException){unsupported=true;}
            // A fatal source boundary has no return value;its stopped CPU's
            // incidental RAX must not be treated as a mechanical result.
            Require(unsupported==B(expected,"unsupported")&&(unsupported||JsonSerializer.Serialize(result)==expected.GetProperty("result").GetRawText()),$"Linked result/boundary differs at{count}");
            Require(trace.SequenceEqual(expected.GetProperty("trace").EnumerateArray().Select(v=>v.GetString()))&&ids.SequenceEqual(Ints(expected.GetProperty("ids")))&&vector.Count==N(expected,"count")
                &&new[]{11,12,13}.Select(i=>targets[i].Mode).SequenceEqual(Ints(expected.GetProperty("modes"))),$"Linked live traversal/partition/cleanup differs at{count}");count++;
        }
        Require(count==55,"Missing linked-unit controls");
        Log.Information("Linked units:55current contexts/1055bytes;predicate,live IDs/cached end,duplicates,stable partition/backing tail,release order and actual fixed child-death Invoke match. Allocator/functor ABI and death side effects remain explicit.");
    }
    private static void Composition()
    {
        var modes=new Dictionary<int,int>{{100,1},{11,1},{13,1}};var vector=new GenericPetLinkedVector([11,13,11],3);var order=new List<int>();
        GenericPetOwnerControl Owner(int id)=>id==100?new(71,0,300,0):new(71,1,100,0);
        int Resolve(int game,int kind,int id)=>game==71&&(kind==0&&id==300||kind==1&&modes.ContainsKey(id))?id:0;
        var linked=new GenericPetLinkedServices(id=>new(1,modes[id],id,0,0,null,true,Owner(id)),id=>id==100?vector:null,Resolve);
        var loss=new GenericPetOwnerLossServices(()=>1,id=>id==300?new(new(0,true,10,20),-1,1,0,false,false,0,0,0,0,0)
                :new(new(1,true,10,20),742,modes[id],0,true,true,71,id==100?0:1,id==100?300:100,id==100?vector.Count:0,0),
            (_,_)=>true,Resolve,(_,_)=>throw new InvalidOperationException("Unexpected player detach"),(_,_)=>throw new InvalidOperationException("Unbound linked callback"),_=>throw new InvalidOperationException("Unbound cleanup"),
            (_,_)=>throw new InvalidOperationException("Unexpected killer credit"),(actor,mode)=>{Require(mode==0,"Wrong linked death mode");return new(0,0,0,actor);},(_,_,_)=>throw new InvalidOperationException("Unexpected killer direction"),
            (request,facing,force)=>{int actor=(int)request.Opaque;Require(request.Target==0&&facing==0&&force==1,"Child death request changed");modes[actor]=0;order.Add(actor);},(_,_)=>{},
            (_,id)=>new(id,"explicit",0,0,0,0,0),(_,_)=>throw new InvalidOperationException("Unexpected transform"));
        GenericPetOwnerLoss.Run(100,0,0,GenericPetLinkedUnits.Bind(loss,71,linked));
        Require(order.SequenceEqual(new[]{11,13,100})&&modes.Values.All(mode=>mode==0)&&vector.Count==0,"Linked children did not reuse death policy once before parent/cleanup");
        Log.Information("Linked death composition:qualified owner-loss policy recursively handles each live child once,cleans links,then requests parent death;no blanket native Kill/Delete adapter.");
    }
    internal static void Original(SkeletonGuardian parent)
    {
        var child=new GraveyardZombie{CantWalk=true,NoKillAwards=true};var other=new GraveyardZombie{CantWalk=true,NoKillAwards=true};var unowned=new GraveyardZombie{CantWalk=true,NoKillAwards=true};
        try
        {
            foreach(var actor in new[]{child,other,unowned}){actor.MoveToWorld(parent.Location,parent.Map);actor.AIObject.AITimer.Stop();}
            Require(child.SetControlMaster(parent)&&other.SetControlMaster(parent.GetMaster()),"Native direct ownership refused fixture");
            int origin=Id(parent),first=Id(child),foreign=Id(other),free=Id(unowned),missing=int.MaxValue-19;var vector=new GenericPetLinkedVector([first,free,first,foreign,missing],5);
            var world=new GenericPetWorldIdentity(71);var units=new Dictionary<int,BaseCreature>{{origin,parent},{first,child},{foreign,other},{free,unowned}};
            var supplied=new GenericPetLinkedServices(id=>new(1,1,id,0,0,null,true,new(0,0,0,0)),id=>id==origin?vector:null,(_,_,_)=>throw new InvalidOperationException("Native registry not bound"));
            var services=world.Bind(supplied);var visited=new List<int>();
            Require(GenericPetLinkedUnits.Valid(origin,first,services)&&!GenericPetLinkedUnits.Valid(origin,foreign,services),"Typed direct owner projection disagrees with actual native masters");
            bool strict=false;try{world.PlayerOwner(child);}catch(NotSupportedException){strict=true;}Require(strict,"Player-only ownership route silently admitted an NPC owner");
            GenericPetLinkedUnits.Notify(71,origin,target=>{visited.Add(target);Require(child.SetControlMaster(parent.GetMaster()),"Live owner change refused");},()=>{},services);
            Require(visited.SequenceEqual(new[]{first})&&GenericPetLinkedUnits.Clear(71,origin,services)==0,"Live owner change was cached or friendly units were treated as linked children");
            Require(child.SetControlMaster(parent),"Native owner restoration refused");vector.Count=5;Array.Copy(new[]{first,free,first,foreign,missing},vector.Values,5);visited.Clear();
            GenericPetLinkedUnits.Notify(71,origin,visited.Add,()=>{},services);
            Require(visited.SequenceEqual(new[]{first,first})&&GenericPetLinkedUnits.Clear(71,origin,services)==2&&vector.Values.Take(2).SequenceEqual(new[]{first,first}),"Live linked vector lost duplicates or retained unrelated/missing units");
            Log.Information("Linked units original:actual NPC/player direct masters and typed native registry drive live validity;changing ownership immediately affects later duplicate visits;stable cleanup keeps only true child IDs. Flags/mode/vector remain explicit fixtures.");
        }
        finally{child.Delete();other.Delete();unowned.Delete();}
    }
}
