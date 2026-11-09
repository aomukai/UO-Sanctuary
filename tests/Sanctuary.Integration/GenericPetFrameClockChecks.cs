// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class GenericPetFrameClockChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetFrameClockChecks));
    private sealed record Entry(uint Body,string Birth,int Life,int Mana,DeathMarkSavedActor Command);
    private sealed record Saved(uint[] Owners,Entry[] Entries,SourceFramePosition Clock,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"pet-frame-clock-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int Id(IEntity e)=>checked((int)e.Serial.Value);
    private static string Birth(SkeletonGuardian b)=>b switch{WarlockGoatmanBody g=>g.SavedText,WarlockTaintedBody t=>t.SavedText,WarlockDefilerBody d=>d.SavedText,_=>throw new InvalidOperationException()};
    private static void Refused(Action action,string why){bool refused=false;try{action();}catch(ArgumentException){refused=true;}catch(NotSupportedException){refused=true;}Require(refused,why);}
    public static void Configure(){Register("pet-frame-clock-check",Check);Register("pet-frame-clock-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared shared40ms pet frame delivery",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Contracts()
    {
        var at=new DateTime(2026,11,9,0,0,0,DateTimeKind.Utc);int comparisons=0;
        foreach(int frame in new[]{0,5,90,SourceFrameTimeline.MaximumFrame-300})
        foreach(long offset in new[]{-1L,0,1,399999,400000,1200127,39600000,119600000})
        {
            var current=new SourceFramePosition(frame,at);var observed=at.AddTicks(offset);var actual=SourceFrameTimeline.Skip(current,observed);
            long count=offset<0?0:offset/TimeSpan.FromMilliseconds(40).Ticks+1;
            var expected=new SourceFramePosition(checked(frame+(int)count),at.AddMilliseconds(count*40));
            Require(actual==expected,"Shared skip math changed existing ground phase");comparisons++;
        }
        Refused(()=>SourceFrameTimeline.Skip(new(-1,at),at),"Negative frame accepted");
        Refused(()=>SourceFrameTimeline.Skip(new(0,DateTime.SpecifyKind(at,DateTimeKind.Local)),at),"Local clock accepted");
        Refused(()=>SourceFrameTimeline.Skip(new(0,at),DateTime.SpecifyKind(at,DateTimeKind.Unspecified)),"Unspecified observation accepted");
        Refused(()=>SourceFrameTimeline.Step(new(SourceFrameTimeline.MaximumFrame,at)),"Exhausted frame advanced");
        Refused(()=>SourceFrameTimeline.Skip(new(SourceFrameTimeline.MaximumFrame-1,at),at.AddMilliseconds(40)),"Overflow advanced timeline");
        var events=new GenericPetTimedEvents();var start=new SourceFramePosition(0,at);var pump=new GenericPetFramePump(events,start);var trace=new List<(int Handle,int Frame,DateTime At)>();
        GenericPetEventClock Live()=>new(pump.Position.Frame,1,1,232,false,0,0,0);
        int first=events.Schedule(101,2,1,Live,()=>throw new InvalidOperationException());
        int cancelled=events.Schedule(102,2,1,Live,()=>throw new InvalidOperationException());
        int future=events.Schedule(103,2,65,Live,()=>throw new InvalidOperationException());int added=0;
        void Route(GenericPetTimedEvent node,int frame,DateTime when)
        {
            Require((node.Flags&1)!=0&&frame==node.Frame,"Frame pump bypassed current processing/due guards");trace.Add((node.Handle,frame,when));
            if(node.Handle==first)
            {
                Refused(()=>pump.Advance(when,Route),"Nested frame delivery admitted");
                added=events.Schedule(104,2,frame,Live,()=>throw new InvalidOperationException());events.Remove(cancelled);
            }
        }
        pump.Advance(at.AddTicks(-1),Route);Require(pump.Position==start&&trace.Count==0,"Early observation advanced a frame");
        pump.Advance(at,Route);Require(trace.SequenceEqual(new[]{(first,1,at)})&&events.Node(cancelled).Flags==2&&events.Node(future).Flags==0,"Oldest/newest chain or upcoming cancellation changed");
        pump.Advance(at,Route);pump.Advance(at.AddMilliseconds(-100),Route);Require(trace.Count==1,"Repeated/backward observation replayed events");
        pump.Advance(at.AddMilliseconds(40),Route);Require(trace.Last()==(added,2,at.AddMilliseconds(40)),"Callback insertion skipped current-frame clamp");
        pump.Advance(at.AddMilliseconds(64*40),Route);Require(trace.Last()==(future,65,at.AddMilliseconds(64*40))&&trace.Count==3,"Same-bucket future event was skipped or delivered early");
        Require(pump.Position==new SourceFramePosition(65,at.AddMilliseconds(65*40)),"Active batch lost phase");
        var order=new GenericPetTimedEvents();var ordered=new GenericPetFramePump(order,start);var seen=new List<int>();
        int a=order.Schedule(1,2,1,()=>new(0,1,1,232,false,0,0,0),()=>{}),b=order.Schedule(2,2,1,()=>new(0,1,1,232,false,0,0,0),()=>{});
        ordered.Advance(at,(n,_,_)=>seen.Add(n.Handle));Require(seen.SequenceEqual(new[]{a,b}),"Shared-world simultaneous event order changed");
        var remaining=new GenericPetTimedEvents();int parked=remaining.Schedule(1,2,10,()=>new(0,1,1,232,false,0,0,0),()=>{});
        _=SourceFrameTimeline.Skip(start,at.AddSeconds(10));Require(remaining.Node(parked).Flags==0,"Clock math silently disposed a suspended event");
        Require(comparisons==32,"Missing phase-regression controls");
        Log.Information("Frame clock:32existing skip-phase comparisons plus bounds/UTC,exact due/FIFO,bucket65,callback insertion/cancellation,reentry and repeated-observation controls pass. Suspension event lifecycle stays explicit.");
        GroundRegression();
    }
    private static void GroundRegression()
    {
        var owner=ClassSkillChecks.Player();FixtureSpace.Corridor(owner,6390,24,3700);
        var original=new Broadsword();ClassGear.Profiles.Ensure(original);original.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        var body=IronGolemWorld.Create(owner,original,original.Location,TimedEffects.Now.AddHours(1),23);
        try
        {
            SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/3+17);int life=SkillHealth.Balance256(body),mana=body.Mana;
            var before=body.Prepared;var state=before.Controller;var parent=original.Parent;var point=original.Location;
            ulong seed=NpcEquipmentRandom.State(body).Seed;var observed=state.NextUtc.AddTicks(1200127);
            long count=(observed-state.NextUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;
            IronGolemWorld.Advance(body,observed,false);var after=body.Prepared.Controller;
            Require(!body.Deleted&&body.Map==Map.Internal&&after.Frame==state.Frame+count&&after.NextUtc==state.NextUtc.AddMilliseconds(count*40)
                &&after.Mode==1&&after.ActionAt==0&&after.EndAt==0&&after.Attempts==state.Attempts&&after.Hits==state.Hits
                &&body.Prepared.Recipe==before.Recipe&&SkillHealth.Balance256(body)==life&&body.Mana==mana&&NpcEquipmentRandom.State(body).Seed==seed
                &&!original.Deleted&&original.Parent==parent&&original.Map==body.Map&&original.Location==point,
                "Existing ground consumer changed phase, wounds, resources, equipment or released a suspended action");
            Log.Information("Frame clock ground:actual original Iron Golem inactive Advance uses shared phase;no healing,contact,RNG or original-equipment change.");
        }
        finally{body.Delete();if(!original.Deleted){original.Delete();}owner.Delete();}
    }
    private static void Start(PlayerMobile[] owners,SkeletonGuardian[] bodies,SourceFramePosition position,int generation)
    {
        var events=new GenericPetTimedEvents();var pump=new GenericPetFramePump(events,position);var world=new GenericPetWorldIdentity(71);
        int end=checked(position.Frame+3);DateTime deadline=position.NextUtc.AddMilliseconds(80);var visits=bodies.ToDictionary(Id,_=>0);var starting=bodies.ToDictionary(Id,b=>DeathMarkWorld.State(b).Control);
        var survey=new GenericPetOwnerSurveyServices(_=>throw new InvalidOperationException("Swarm0 unit"),(_,_)=>throw new InvalidOperationException("Swarm0 metadata"),_=>throw new InvalidOperationException("Swarm0 target"),(_,_,_)=>throw new InvalidOperationException("Swarm0 validity"),_=>throw new InvalidOperationException("Swarm0 path"));
        var leaves=new GenericPetInitializationServices(world.Resolve,_=>throw new InvalidOperationException("Native HP not bound"),who=>World.FindMobile((Serial)(uint)who)is PlayerMobile?0:1,
            (owner,visit)=>{foreach(var body in bodies.Where(b=>Id(b.GetMaster())==owner)){visit(Id(body));}},survey);
        GenericPetEventClock Live()=>new(pump.Position.Frame,1,1,232,false,0,0,0);
        foreach(var body in bodies){events.Schedule(Id(body),2,position.Frame+1,Live,()=>throw new InvalidOperationException());}
        TimerExecutionToken timer=default;
        void Route(GenericPetTimedEvent node,int frame,DateTime when)
        {
            GenericPetMonsterEventRouter.Run(node,_=>new(1,232,false,0,0,0,1),active=>
            {
                Require(active.Type==2,"Clock fixture received an unqualified event type");var body=bodies.Single(b=>Id(b)==active.Actor);
                GenericPetAiEntryChecks.Original(body,Id(body.GetMaster()),world,leaves,events);visits[Id(body)]++;
                if(frame<end){events.Schedule(Id(body),2,frame+1,Live,()=>throw new InvalidOperationException());}
            });
        }
        void Tick()
        {
            try
            {
                DateTime now=TimedEffects.Now;pump.Advance(now<deadline?now:deadline,Route);
                if(pump.Position.Frame<end){return;}timer.Cancel();
                foreach(var body in bodies)
                {
                    int id=Id(body);var before=starting[id];var after=DeathMarkWorld.State(body).Control;
                    Require(visits[id]==3&&after==new GenericPetSavedControl(unchecked(before.Value0+3),unchecked(before.Value1-21),before.Value2^0x5a5a5a5a),"Live timer did not deliver exactly3root updates per original");
                    Require(events.Unit(id).Length==0,"Completed live fixture retained a timer event");
                }
                var saved=new Saved(owners.Select(p=>p.Serial.Value).ToArray(),bodies.Select(b=>new Entry(b.Serial.Value,Birth(b),SkillHealth.Balance256(b),b.Mana,DeathMarkWorld.State(b))).ToArray(),pump.Position,generation);
                File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
                Log.Information("Frame clock original:live40ms timer->shared bucket dispatcher->typed router->full entry/override/default root;9callbacks,no automatic production registration.");
                Log.Information("pet-frame-clock-result: passed");
            }
            catch(Exception e){timer.Cancel();Log.Error(e,"pet-frame-clock-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Tick,out timer);
    }
    private static void Check()
    {
        Contracts();var owners=new[]{ClassSkillChecks.Player(DiabloClass.Warlock),ClassSkillChecks.Player(DiabloClass.Warlock)};owners[1].Female=true;owners[1].Body=0x191;
        FixtureSpace.Corridor(owners[0],6440,40,3720);owners[1].MoveToWorld(new(6440,3722,0),Map.Trammel);
        SkeletonGuardian[] bodies=[WarlockGoatmanBody.CreatePrepared(owners[0],new(6441,3720,0),TimedEffects.Now),WarlockTaintedBody.CreatePrepared(owners[1],new(6443,3720,0),TimedEffects.Now),WarlockDefilerBody.CreatePrepared(owners[0],new(6445,3720,0),TimedEffects.Now)];
        foreach(var body in bodies){DeathMarkWorldChecks.Register(body);SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)-333);}
        // The reused root fixture retains a typed command on Finish. Supply
        // each actual owner explicitly, rather than treating the registration
        // helper's unknown command (-1/0) as an authentic startup target.
        var image=DeathMarkWorld.Capture();var commands=bodies.ToDictionary(Id,b=>Id(b.GetMaster()));
        DeathMarkWorld.Restore(image with{Actors=image.Actors.Select(a=>commands.TryGetValue(a.Handle,out int owner)?a with{Type4c=1,Target48=owner}:a).ToArray()});
        GenericPetControlChecks.Seed(bodies);Start(owners,bodies,new(0,TimedEffects.Now.AddMilliseconds(40)),0);
    }
    private static void Reload()
    {
        Contracts();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Extra clock reload");
        var owners=saved.Owners.Select(s=>(PlayerMobile)World.FindMobile((Serial)s)).ToArray();var bodies=saved.Entries.Select(e=>(SkeletonGuardian)World.FindMobile((Serial)e.Body)).ToArray();
        foreach(var e in saved.Entries)
        {
            var body=(SkeletonGuardian)World.FindMobile((Serial)e.Body);Require(Birth(body)==e.Birth&&SkillHealth.Balance256(body)==e.Life&&body.Mana==e.Mana&&DeathMarkWorld.State(body)==e.Command,"Clock reload changed original snapshot/wounds/commands/control");
        }
        var resumed=SourceFrameTimeline.Skip(saved.Clock,TimedEffects.Now);
        Require(resumed.Frame>=saved.Clock.Frame&&resumed.NextUtc>TimedEffects.Now.AddMilliseconds(-40),"Loaded clock did not skip elapsed phase");
        // Event persistence/rearming is an explicit review fixture. No queued
        // cast/movement/contact is replayed;the saved clock phase is retained.
        Start(owners,bodies,resumed,saved.Generation+1);
    }
}
