// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcOriginalEventChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcOriginalEventChecks));
    private sealed record Held(uint Actor,string Body,string[] Items,NpcEquipmentSeed Random,int Life,string Resources);
    private sealed record Saved(uint Owner,Point3D Point,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-original-events-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string[] Items(AmazonValkyrieBody b)=>ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray();
    private static Held Store(AmazonValkyrieBody b)=>new(b.Serial.Value,b.SavedText,Items(b),NpcEquipmentRandom.State(b),SkillHealth.Balance256(b),Json(GeneratedNpcLiveResources.State(b)));
    public static void Configure(){Register("npc-original-events-check",false);Register("npc-original-events-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original full-graph item event lifecycle and persisted registration history",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Verify(AmazonValkyrieBody b,JsonElement expected,int index,int step)
    {
        var graph=b.EquipmentNumbers;var events=graph.ItemEvents;Require(events!=null,"Missing original full-graph event service");
        var captured=events.Capture();var rows=captured.Events.Entries;
        var actual=new object[]{captured.Events.NextId,rows.Select(e=>new long[]{e.Id,e.Event,e.Guid1,e.Guid2,e.Function,e.Queue,e.Key,e.Flags}).ToArray()};
        Require(Json(actual)==expected.GetRawText(),"Original event head/IDs/order differs case="+index+" step="+step+" actual="+Json(actual)+" expected="+expected.GetRawText());
        Require(captured.Actor==b.Serial.Value&&captured.Owner==b.ControlMaster.Serial.Value&&captured.ActiveItems.SequenceEqual(GeneratedNpcLiveResources.State(b).ActiveItems),"Original event owner/attachment binding differs");
        for(byte phase=0;phase<14;phase++)
        {
            var called=new List<int[]>();events.Trigger(phase,(f,p,a,c)=>{called.Add([f,p,a,c]);return 7;});
            var wanted=rows.Where(e=>e.Event==phase).Select(e=>new[]{e.Function,(int)e.Event,e.Guid1,e.Guid2}).ToArray();
            Require(Json(called)==Json(wanted)&&Json(events.Capture())==Json(captured),"Original full-graph callback traversal changed order/state or obeyed callback return");
        }
        if(rows.Length>0)
        {
            bool threw=false,refused=false;var phase=rows[0].Event;
            try{events.Trigger(phase,(_,_,_,_)=>{try{events.Capture();}catch(NotSupportedException){refused=true;}throw new InvalidOperationException("Fixture callback refusal");});}
            catch(InvalidOperationException e){Require(e.Message=="Fixture callback refusal","Unexpected callback failure");threw=true;}
            Require(threw&&refused&&Json(events.Capture())==Json(captured),"Throwing callback lost restoration or allowed active capture");
            byte other=(byte)((phase+1)%14);var nested=new List<int[]>();bool once=false;
            events.Trigger(phase,(f,p,a,c)=>
            {
                nested.Add([f,p,a,c]);if(!once){once=true;events.Trigger(other,(nf,np,na,nc)=>{nested.Add([nf,np,na,nc]);return 0;});}return 0;
            });
            var wanted=new List<int[]>();bool first=true;
            foreach(var r in rows.Where(e=>e.Event==phase))
            {wanted.Add([r.Function,r.Event,r.Guid1,r.Guid2]);if(first){first=false;wanted.AddRange(rows.Where(e=>e.Event==other).Select(e=>new[]{e.Function,(int)e.Event,e.Guid1,e.Guid2}));}}
            Require(Json(nested)==Json(wanted)&&Json(events.Capture())==Json(captured),"Nested full-graph dispatch lost outer callback context/history");
        }
    }
    private static void Guards(AmazonValkyrieBody b)
    {
        var events=b.EquipmentNumbers.ItemEvents;var original=events.Capture();
        var invalid=new List<GeneratedNpcEventState>{original with{Actor=original.Owner},original with{Owner=original.Actor},original with{Originals=new string('0',64)},original with{ActiveItems=[]},original with{Events=original.Events with{HasOwner=false}},original with{Events=original.Events with{NextId=-1}}};
        if(original.Events.Entries.Length>0){invalid.Add(original with{Events=original.Events with{Entries=[]}});}
        foreach(var changed in invalid)
        {
            bool refused=false;try{events.Restore(changed);}catch(InvalidDataException){refused=true;}
            Require(refused&&Json(events.Capture())==Json(original),"Corrupt/foreign full-graph event history changed committed state");
        }
        if(original.Events.Entries.Length>0)
        {
            bool refused=false;events.Trigger(original.Events.Entries[0].Event,(_,_,_,_)=>
            {try{GeneratedNpcLiveResources.SetItemActive(b,original.ActiveItems[0],false);}catch(NotSupportedException){refused=true;}return 0;});
            Require(refused&&Json(events.Capture())==Json(original)&&Json(GeneratedNpcLiveResources.State(b).ItemEvents)==Json(original),"Active callback silently replaced the committed event graph");
        }
        var clone=GeneratedNpcLiveResources.State(b);clone.ItemEvents.ActiveItems[0]=0;
        Require(Json(events.Capture())==Json(original)&&GeneratedNpcLiveResources.State(b).ItemEvents.ActiveItems[0]!=0,"Event snapshot aliases live state");
    }
    private static void Cycle(AmazonValkyrieBody b,JsonElement row,int index,int generation)
    {
        int steps=row.GetProperty("StepsPerCycle").GetInt32(),step=generation*steps;var states=row.GetProperty("States");
        var originals=ValkyrieEquipment.Originals(b);var ids=originals.Select(i=>i.Serial.Value).ToArray();string items=Json(Items(b));var rng=NpcEquipmentRandom.State(b);int life=SkillHealth.Balance256(b);
        Verify(b,states[step],index,step);
        void Next(){step++;Verify(b,states[step],index,step);}
        // Repeated Update is a no-op; actual resource transactions exercise the
        // same original attachment/removal service, with source vital effects.
        foreach(uint id in ids){GeneratedNpcLiveResources.SetItemActive(b,id,true);}Next();
        foreach(uint id in ids.Reverse()){GeneratedNpcLiveResources.SetItemActive(b,id,false);Next();}
        foreach(uint id in ids){GeneratedNpcLiveResources.SetItemActive(b,id,true);Next();}
        BattleOrders.Apply((PlayerMobile)b.ControlMaster,b,1,TimedEffects.Now);Next();BattleOrders.Clear(b);Next();
        GeneratedNpcLiveResources.SetItemActive(b,ids[0],false);Next();GeneratedNpcLiveResources.SetItemActive(b,ids[0],true);Next();
        Require(step==(generation+1)*steps&&Json(Items(b))==items&&NpcEquipmentRandom.State(b)==rng,"Original event lifecycle changed saved items/RNG or skipped source steps");
        // Difficulty changes rebuild numeric bases, retaining the actual saved
        // callback history and shifted original item attachment order.
        string history=Json(b.EquipmentNumbers.ItemEvents.Capture());var old=b.Prepared;var next=(MonsterDifficulty)(((int)old.Difficulty+1)%3);
        b.RestorePrepared(old with{Difficulty=next});GeneratedNpcLiveResources.RefreshValkyrieBasis(b);
        Require(Json(b.EquipmentNumbers.ItemEvents.Capture())==history,"Difficulty transition rebuilt event order/IDs");
        b.RestorePrepared(old);GeneratedNpcLiveResources.RefreshValkyrieBasis(b);
        Require(Json(b.EquipmentNumbers.ItemEvents.Capture())==history&&Json(Items(b))==items&&NpcEquipmentRandom.State(b)==rng,"Returning difficulty changed original event state");
        Guards(b);Require(SkillHealth.Balance256(b)>0&&life>0,"Source resource transactions killed fixture");
    }
    private static void Check(bool reload)
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-original-events-cases.json")));var cases=source.RootElement.EnumerateArray().ToArray();Require(cases.Length==57,"Original event source corpus");
        PlayerMobile p;Point3D point;int generation=0;var bodies=new List<AmazonValkyrieBody>();
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;Require(generation<=2,"Extra original event reload");p=World.FindMobile((Serial)s.Owner)as PlayerMobile;point=s.Point;Require(p is{Alive:true,Deleted:false},"Original event owner lost");
            foreach(var h in s.Bodies)
            {var b=World.FindMobile((Serial)h.Actor)as AmazonValkyrieBody;Require(b is{Alive:true,Deleted:false}&&b.ControlMaster==p&&Json(Store(b))==Json(h),"Original full-graph event/source/items/resources/wounds/RNG changed on reload");bodies.Add(b);}
            p.MoveToWorld(point,Map.Trammel);
        }
        else
        {
            p=new PlayerMobile{Name="NpcOriginalEventsFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6600,32,3190);point=p.Location;
            using var raw=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var originals=raw.RootElement.EnumerateArray().Where(c=>c.GetProperty("Critical").GetInt32()==1).ToArray();Require(originals.Length==57,"Original damage contexts");
            for(int i=0;i<cases.Length;i++)
            {
                var b=ValkyrieDamageChecks.Body(p,originals[i]);ValkyrieEquipmentResources.Activate(b,0,0,0,0,300);var before=Store(b);GeneratedNpcLiveResources.ActivateItemEvents(b);var after=Store(b);
                Require(before.Body==after.Body&&Json(before.Items)==Json(after.Items)&&before.Random==after.Random&&before.Life==after.Life&&NpcItemProcs.Get(b)==null,"Original event attachment rewrote original items/admission/wounds/RNG or created carrier copies");bodies.Add(b);
            }
        }
        for(int i=0;i<bodies.Count;i++){Cycle(bodies[i],cases[i],i,generation);PersistentMinions.Park(bodies[i]);}
        PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,point,bodies.Select(Store).ToArray(),generation)));
        Require(!ClassCombat.Available(p,CombatAbility.Valkyrie),"Original event history enabled learned cast");
        Log.Information("Original NPC item events:57unfiltered loadouts/330original items;complete source registration head/order/IDs through3cycles,all removals/re-additions,percentage/difficulty/cache rebuilds and two actual reloads;all14phase traversal,nested/throwing callbacks,corrupt-history/alias guards;no proc carrier copies or automatic proc admission");
    }
}
