// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GrimWardLifecycleChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrimWardLifecycleChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Equal(int[] actual,JsonElement expected,string label)=>Require(JsonSerializer.Serialize(actual)==JsonSerializer.Serialize(expected),label+":"+JsonSerializer.Serialize(actual));
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Grim Ward lifecycle/source contract",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("grim-ward-lifecycle-check");Register("grim-ward-lifecycle-reload-check");}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"grim-ward-lifecycle-source-probe.json")));
        int gates=0,size=0,factory=1,rangeOverride=0,duration=0,period=6,creates=0,hidden=0,reserved=0,refreshed=0,formulaCaster=0,dispatch=0,handled=0,dispatchCaster=0,order=0;
        GrimWardSpawn created=null;bool placement=false;
        void Reset(){creates=hidden=reserved=refreshed=formulaCaster=dispatch=handled=dispatchCaster=order=0;created=null;}
        var services=new GrimWardLifecycleServices(
            skill=>(gates&(placement?1:16))!=0?new((gates&64)!=0?252:-1,(gates&64)!=0?249:-1,(gates&64)!=0?255:-1):null,
            caster=>(gates&2)!=0?new(2,1,1,123,456,0,0):null,
            corpse=>(gates&4)!=0,(corpse,x,y)=>(gates&8)!=0,
            (corpse,x,y,radius,mask,mode)=>{Require(corpse==2&&x==123&&y==456&&radius==2&&mask==0x1000&&mode==1,"Ward free-coordinate contract");return((gates&16)!=0,x+1,y);},
            cls=>(gates&32)!=0?((size&1)!=0,(size&2)!=0):null,()=>700,
            spawn=>{created=spawn;creates++;order=order*10+1;return factory;},
            corpse=>{hidden++;order=order*10+2;},corpse=>{reserved++;order=order*10+3;},corpse=>{refreshed++;order=order*10+4;},
            cls=>(gates&2)!=0?new((gates&4)!=0?253:-1,rangeOverride,period,30):null,
            missile=>(gates&8)!=0?1:0,
            (owner,skill,rank)=>{formulaCaster=owner==1?1:2;return duration;},
            (body,fn,skill,rank)=>{Require(body==3&&fn==30,"Periodic curse must retain missile/dispatch identity");dispatch++;dispatchCaster=body==3?1:2;},
            body=>{Require(body==3,"Collision context differs");handled++;return 17;});
        int count=0;
        placement=true;
        foreach(var row in doc.RootElement.GetProperty("placementCases").EnumerateArray())
        {
            gates=row[0].GetInt32();size=row[1].GetInt32();factory=row[2].GetInt32();Reset();bool result=GrimWardLifecycle.Place(1,150,20,services);
            Equal([gates,size,factory,result?1:0,creates,hidden,reserved,refreshed,created?.Missile??0,created?.Owner==1?1:0,created?.X??0,created?.Y??0,order],row,"Source ward placement "+count);
            if(created!=null){Require(created.Flags==1&&created.Skill==150&&created.Rank==20&&created.Range==0,"Placement payload differs");}count++;
        }
        placement=false;factory=1;
        foreach(var row in doc.RootElement.GetProperty("childCases").EnumerateArray())
        {
            gates=row[0].GetInt32();rangeOverride=row[1].GetInt32();duration=row[2].GetInt32();Reset();
            int result=GrimWardLifecycle.StartHit((gates&1)!=0?new(3,3,252,123,456,150,21):null,services);
            Equal([gates,rangeOverride,duration,result,creates,(int)(created?.Flags??0),created?.Range??0,created?.Missile??0,created?.Rank??0,created?.Owner==1?1:0,formulaCaster,created?.X??0],row,"Source ward child "+count);
            if(created!=null){Require(created.Skill==150&&created.Y==456,"Child payload differs");}count++;
        }
        foreach(var row in doc.RootElement.GetProperty("periodCases").EnumerateArray())
        {
            int present=row[0].GetInt32(),valid=row[1].GetInt32(),elapsed=row[3].GetInt32(),skill=row[4].GetInt32(),rank=row[5].GetInt32();period=row[2].GetInt32();gates=valid!=0?2:0;Reset();
            int result=GrimWardLifecycle.Tick(present!=0?new(3,3,253,123,456,skill,rank):null,GrimWardLifecycle.Elapsed(1000,1000-elapsed),services);
            Equal([present,valid,period,elapsed,skill,rank,result,dispatch,handled,dispatchCaster],row,"Source ward periodic "+count);count++;
        }
        var pulses=new List<int>();gates=2;period=6;var bodyUnit=new GrimWardSourceUnit(3,3,253,123,456,150,20);
        for(int current=1000;current>0;current--){Reset();int elapsed=GrimWardLifecycle.Elapsed(1000,current);GrimWardLifecycle.Tick(bodyUnit,elapsed,services);if(dispatch!=0){pulses.Add(elapsed);}}
        Require(JsonSerializer.Serialize(pulses)==JsonSerializer.Serialize(doc.RootElement.GetProperty("timeline"))&&pulses.Count==167,"Ward elapsed pulse timeline differs");
        Require(count==3144&&!BarbarianCombat.Implemented(CombatAbility.GrimWard),"Lifecycle enabled unfinished curse/handler");
        Log.Information("Grim Ward lifecycle:3144complete legacy-source comparisons,167elapsed pulse values;placement factory/no-draw/no-select order,owner-evaluated child and missile-context curse dispatch pass;modern curse/terrain/clock and learned cast remain gated");
    }
}
