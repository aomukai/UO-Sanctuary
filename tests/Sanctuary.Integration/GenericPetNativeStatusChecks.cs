// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GenericPetNativeStatusChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetNativeStatusChecks));
    private sealed record Entry(uint Body,string Birth,int Life,int Mana,DeathMarkSavedActor Command);
    private sealed record Saved(uint[] Owners,Entry[] Entries,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"pet-native-status-fixtures.json");
    private static int Id(IEntity e)=>checked((int)e.Serial.Value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Birth(SkeletonGuardian b)=>b switch{WarlockGoatmanBody g=>g.SavedText,WarlockTaintedBody t=>t.SavedText,WarlockDefilerBody d=>d.SavedText,_=>throw new NotSupportedException()};
    public static void Configure(){Register("pet-native-status-check",Check);Register("pet-native-status-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Live freeze/stun projection through shared pet AI",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Refused(Action action,string why)
    {bool refused=false;try{action();}catch(NotSupportedException){refused=true;}Require(refused,why);}
    private static void Run(PlayerMobile[] owners,SkeletonGuardian[] bodies,int generation)
    {
        GenericPetPlayerIterationChecks.Original(owners);
        var world=new GenericPetWorldIdentity(71);
        var survey=new GenericPetOwnerSurveyServices(_=>throw new InvalidOperationException("Swarm0 unit"),(_,_)=>throw new InvalidOperationException("Swarm0 metadata"),_=>throw new InvalidOperationException("Swarm0 target"),(_,_,_)=>throw new InvalidOperationException("Swarm0 validity"),_=>throw new InvalidOperationException("Swarm0 path"));
        var leaves=new GenericPetInitializationServices(world.Resolve,_=>throw new InvalidOperationException("HP supplier not bound"),who=>World.FindMobile((Serial)(uint)who)is PlayerMobile?0:1,
            (owner,visit)=>{foreach(var body in bodies.Where(b=>Id(b.GetMaster())==owner)){visit(Id(body));}},survey);
        foreach(var body in bodies)
        {
            GenericPetSchedulingStateChecks.Original(body);
            GenericPetLinkedUnitChecks.Original(body);
            GenericPetOwnerDetachChecks.Original(body);
            GenericPetOwnerRemoveChecks.Original(body);
            int id=Id(body),cls=WarlockDemonPlacement.ClassId(body),life=SkillHealth.Balance256(body),mana=body.Mana;string birth=Birth(body);var command=DeathMarkWorld.State(body);
            Require(GenericPetNativeControlStates.Read(body)==0,"Fresh/reloaded status fixture retained a control effect");
            body.Frozen=true;body.Paralyzed=true;
            Require(GenericPetNativeControlStates.Read(body)==0,"Native movement locks were invented as D2 status bits");body.Frozen=false;body.Paralyzed=false;
            Refused(()=>GenericPetNativeControlStates.Events(body,2,0,1),"Unqualified selector alias accepted");
            var original=new GenericPetAiPreUnit(new(1,true,body.X,body.Y),cls,1,1,0x1234,0x5678,232,true,-1,GenericPetNativeControlStates.Mask|0x80,
                new(0,body.X,body.Y,0,1,0),new());
            var healthy=GenericPetNativeControlStates.Preprocess(body,original);
            Require(healthy.Rootb30==0x80&&healthy.Flags124==original.Flags124&&healthy.Flags128==original.Flags128&&ReferenceEquals(healthy.Path,original.Path)
                &&ReferenceEquals(healthy.Movement,original.Movement),"Projection lost unrelated status/world fields or retained stale control bits");
            Refused(()=>GenericPetNativeControlStates.Preprocess(body,original with{Root1c=0}),"Nonextended root silently replaced");
            Refused(()=>GenericPetNativeControlStates.Preprocess(body,original with{StateCount=21}),"Unknown state-count view accepted");
            var events=new GenericPetTimedEvents();var at=TimedEffects.Now;var pump=new GenericPetFramePump(events,new(0,at));int roots=0,entries=0,pauses=0;
            GenericPetEventClock Clock()=>new(pump.Position.Frame,1,1,232,false,0,0,0);
            void Schedule(int frame)=>events.Schedule(id,2,frame,Clock,()=>throw new InvalidOperationException());
            bool Preprocess(GenericPetAiControl control,GenericPetAiTick tick)
            {
                var memory=new GenericPetAiSavedTarget(-1,0);var primary=GenericPetSearchTables.Bind(1).Monster(1,cls);
                var services=new GenericPetAiPreServices(()=>1,actor=>actor==id?GenericPetNativeControlStates.Preprocess(body,original):throw new InvalidOperationException("Wrong live status actor"),
                    c=>ReferenceEquals(c,control)?memory:throw new InvalidOperationException("Wrong control"),_=>primary.Flags60,
                    (_,_)=>throw new InvalidOperationException("Saved-target walk"),(_,_)=>throw new InvalidOperationException("Saved-target resolve"),(_,_)=>throw new InvalidOperationException("Saved-target distance"),
                    (_,_,_,_,_,_)=>throw new InvalidOperationException("Saved-target point move"),(_,_,_,_)=>throw new InvalidOperationException("Saved-target target move"),
                    (_,_,_,_,_)=>throw new InvalidOperationException("Collision"),(_,_)=>throw new InvalidOperationException("Door search"),(_,_)=>throw new InvalidOperationException("Door metadata"),
                    (_,_)=>throw new InvalidOperationException("Door predicate"),(_,_)=>throw new InvalidOperationException("Door interaction"),
                    (actor,frames)=>{Require(actor==id&&frames==3,"Live stun changed source pause length");pauses++;Schedule(pump.Position.Frame+frames);});
                return GenericPetAiPreprocess.Run(id,tick,services);
            }
            void Route(GenericPetTimedEvent node,int frame,DateTime when)
                =>GenericPetMonsterEventRouter.Run(node,actor=>{Require(actor==id,"Wrong event actor");return GenericPetNativeControlStates.Events(body,1,0,1);},active=>
                {
                    Require(active.Type==2,"Unexpected live control event");entries++;bool stun=CombatStuns.Remaining(body)>0;
                    GenericPetAiEntryChecks.Original(body,Id(body.GetMaster()),world,leaves,events,Preprocess,stun);if(!stun){roots++;}
                });
            Require(FreezeStates.Commit(body.GetMaster(),body,100,true,1,Core.TickCount),"Actual freeze producer refused fixture");
            Require(GenericPetNativeControlStates.Read(body)==2,"Active freeze was absent or confused with stun");
            var live=GenericPetNativeControlStates.Events(body,1,0x80000000,12);
            Require(live.Flags124==0x80000000&&live.Mode==12&&live.Rootb30==2&&live.StatCount==232,"Event projection rewrote caller mode/flags");
            Schedule(1);pump.Advance(at,Route);Require(entries==0&&events.Unit(id).Length==0,"Frozen AI event reached entry or was retained");
            Require(FreezeStates.Remove(body,TimedEffects.Now),"Freeze removal refused");
            Require(FreezeStates.Waiting(body)&&GenericPetNativeControlStates.Read(body)==0,"Post-freeze wait was incorrectly projected as active freeze");
            // This test owns only its fixture actor's control. Remove the wait
            // before testing stun;production wait behavior is not redefined.
            TimedEffects.Actor(body,TimedEffects.Now).Control=null;
            Require(CombatStuns.ApplyPreparedDuration(body,250,TimedEffects.Now),"Actual stun producer refused fixture");
            Require(GenericPetNativeControlStates.Read(body)==0x200000,"Stun projection missing");
            Schedule(2);pump.Advance(at.AddMilliseconds(40),Route);
            Require(entries==1&&roots==0&&pauses==1&&events.Unit(id).Length==1&&events.Node(events.Unit(id)[0]).Frame==5
                &&DeathMarkWorld.State(body).Control==command.Control,"Stunned entry reached root,published control or changed three-frame rearm");
            CombatStuns.Clear(body);Require(GenericPetNativeControlStates.Read(body)==0,"Removed stun retained source bit");
            pump.Advance(at.AddMilliseconds(120),Route);Require(entries==1,"Stun pause ended early");
            pump.Advance(at.AddMilliseconds(160),Route);
            var expected=new GenericPetSavedControl(unchecked(command.Control.Value0+1),unchecked(command.Control.Value1-7),command.Control.Value2^0x5a5a5a5a);
            Require(entries==2&&roots==1&&pauses==1&&events.Unit(id).Length==0&&DeathMarkWorld.State(body).Control==expected
                &&DeathMarkWorld.State(body).Target48==command.Target48&&DeathMarkWorld.State(body).Type4c==command.Type4c
                &&Birth(body)==birth&&SkillHealth.Balance256(body)==life&&body.Mana==mana,"Recovered live entry lost original state or replayed a root");
            Log.Information("Native pet status original:freeze suppresses AI delivery;live stun entry rearms3frames without root;removal permits exactly1full root,retaining original birth/wounds/resources/typed command.");
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owners.Select(o=>o.Serial.Value).ToArray(),bodies.Select(b=>new Entry(b.Serial.Value,Birth(b),SkillHealth.Balance256(b),b.Mana,DeathMarkWorld.State(b))).ToArray(),generation)));
    }
    private static void Check()
    {
        var owners=new[]{ClassSkillChecks.Player(DiabloClass.Warlock),ClassSkillChecks.Player(DiabloClass.Warlock)};owners[1].Female=true;owners[1].Body=0x191;
        FixtureSpace.Corridor(owners[0],6490,40,3720);owners[1].MoveToWorld(new(6490,3722,0),Map.Trammel);
        SkeletonGuardian[] bodies=[WarlockGoatmanBody.CreatePrepared(owners[0],new(6491,3720,0),TimedEffects.Now),WarlockTaintedBody.CreatePrepared(owners[1],new(6493,3720,0),TimedEffects.Now),WarlockDefilerBody.CreatePrepared(owners[0],new(6495,3720,0),TimedEffects.Now)];
        foreach(var body in bodies){DeathMarkWorldChecks.Register(body);SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)-333);}
        var image=DeathMarkWorld.Capture();var commands=bodies.ToDictionary(Id,b=>Id(b.GetMaster()));
        DeathMarkWorld.Restore(image with{Actors=image.Actors.Select(a=>commands.TryGetValue(a.Handle,out int owner)?a with{Type4c=1,Target48=owner}:a).ToArray()});
        GenericPetControlChecks.Seed(bodies);Run(owners,bodies,0);
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Extra status reload");
        var owners=saved.Owners.Select(s=>(PlayerMobile)World.FindMobile((Serial)s)).ToArray();var bodies=saved.Entries.Select(e=>(SkeletonGuardian)World.FindMobile((Serial)e.Body)).ToArray();
        foreach(var entry in saved.Entries)
        {
            var body=(SkeletonGuardian)World.FindMobile((Serial)entry.Body);
            Require(Birth(body)==entry.Birth&&SkillHealth.Balance256(body)==entry.Life&&body.Mana==entry.Mana&&DeathMarkWorld.State(body)==entry.Command,"Reload changed original status-test body/controller");
        }
        Run(owners,bodies,saved.Generation+1);
    }
}
