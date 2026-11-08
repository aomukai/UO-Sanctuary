// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DeathMarkInterruptChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DeathMarkInterruptChecks));
    private sealed record Saved(uint Owner,uint Body,uint Goat,uint Target,string Birth,string GoatBirth,int Life,int GoatLife,Point3D OwnerPoint,Point3D BodyPoint,Point3D GoatPoint,DeathMarkSavedActor Command,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"death-mark-interrupt-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {
            string name=reload?"death-mark-interrupt-reload-check":"death-mark-interrupt-check";bool again=reload;
            ConsoleInputHandler.RegisterCommand(name,"Death Mark neutral mode cancels real A2 events without removing issued missiles",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",network=>
            {try{if(again){Reload(network);}else{Check(network);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
        ConsoleInputHandler.RegisterCommand("death-mark-interrupt-save-check","Save a real transient A2 mode after the fixture socket is detached",_=>Core.LoopContext.Post(BeforeSave));
    }
    private static void BeforeSave()
    {
        var deadline=TimedEffects.Now.AddSeconds(5);
        void Try()
        {
            try
            {
                var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)s.Owner);
                if(owner.NetState!=null)
                {
                    Require(TimedEffects.Now<deadline,"Fixture socket did not detach before source-mode save");
                    Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Try));return;
                }
                var body=(WarlockTaintedBody)World.FindMobile((Serial)s.Body);var goat=(WarlockGoatmanBody)World.FindMobile((Serial)s.Goat);var target=(BaseCreature)World.FindMobile((Serial)s.Target);
                owner.MoveToWorld(s.OwnerPoint,Map.Trammel);body.MoveToWorld(s.BodyPoint,owner.Map);goat.MoveToWorld(s.GoatPoint,owner.Map);
                Require(body.SavedText==s.Birth&&SkillHealth.Balance256(body)==s.Life&&goat.SavedText==s.GoatBirth&&SkillHealth.Balance256(goat)==s.GoatLife,"Pre-save detach changed original actor resources");
                TaintedFireBallCasting.Cancel(body);PendingSave(owner,body,goat,target,s.Generation);
                Require(DeathMarkWorld.Capture().Actors.Single(a=>a.Handle==checked((int)body.Serial.Value)).Mode==5,"Actual serialized source mode is not pending A2");
                Log.Information("death-mark-interrupt-save-check: passed");
            }
            catch(Exception e){Log.Error(e,"death-mark-interrupt-save-check: failed");}
        }
        Try();
    }
    private static TaintedFireBallFlight[] Flights(WarlockTaintedBody body)=>TaintedFireBallMissiles.Snapshot().Flights.Where(f=>f.Actor==body.Serial.Value).ToArray();
    private static void Warp(PlayerMobile owner,int x,int y)
    {
        var c=new DeathMarkCastContext(owner.Serial.Value,new(0,true,owner.X*2,owner.Y*2),true,375,1,1,0,20,21,212,232);
        Require(DeathMarkNativeRelease.RunPrepared(owner,c,Serial.Zero,new(owner.X+x,owner.Y+y,owner.Z),TimedEffects.Now).Result==1,"Native ground release refused original actors");
    }
    private static void Drain(WarlockTaintedBody body,long id)
    {
        for(int i=0;i<50&&Flights(body).FirstOrDefault(f=>f.Id==id)is{} flight;i++)
        {TaintedFireBallMissiles.Tick(flight.NextUtc,true,id);Require(Flights(body).All(f=>f.Fault==null),"Issued missile faulted after caster warp");}
        Require(Flights(body).All(f=>f.Id!=id),"Issued missile did not finish naturally");
    }
    private static void Exercise(PlayerMobile owner,WarlockTaintedBody body,BaseCreature target)
    {
        int life=SkillHealth.Balance256(body),mana=NecromancerEffects.ManaBalance256(owner);string birth=body.SavedText;
        Require(TaintedFireBallCasting.BeginRegistered(body,target,TimedEffects.Now),"Registered original A2 start");var action=TaintedFireBallCasting.State(body);
        Require(DeathMarkWorld.State(body).Mode==5,"Real A2 mode did not reach source controller state");
        DeathMarkWorld.ChangeMode(body,5);Require(TaintedFireBallCasting.State(body)==action,"Unchanged mode cancelled live windup");
        Warp(owner,6,1);Require(TaintedFireBallCasting.State(body)==null&&DeathMarkWorld.State(body).Mode==1,"Death Mark left pending A2 release alive");
        TaintedFireBallCasting.Tick(action.EndUtc,true);Require(Flights(body).Length==0,"Cancelled A2 emitted a delayed Fire Ball");

        Require(TaintedFireBallCasting.BeginRegistered(body,target,TimedEffects.Now),"Second registered A2 start");action=TaintedFireBallCasting.State(body);
        TaintedFireBallCasting.Tick(action.ActionUtc,true);var issued=Flights(body).Single();string before=JsonSerializer.Serialize(issued);int hp=SkillHealth.Balance256(target);
        Warp(owner,13,1);
        Require(TaintedFireBallCasting.State(body)==null&&JsonSerializer.Serialize(Flights(body).Single())==before,"Neutral transition deleted or rewrote an issued missile");
        Drain(body,issued.Id);Require(SkillHealth.Balance256(target)<hp&&MonsterUninterruptibles.State(target)?.LastAttacker==body.Serial.Value,"Already-issued original missile stopped damaging after warp");

        Require(TaintedFireBallCasting.BeginRegistered(body,target,TimedEffects.Now),"Natural A2 completion control");action=TaintedFireBallCasting.State(body);
        TaintedFireBallCasting.Tick(action.ActionUtc,true);issued=Flights(body).Single();TaintedFireBallCasting.Tick(action.EndUtc,true);
        Require(TaintedFireBallCasting.State(body)==null&&DeathMarkWorld.State(body).Mode==1&&Flights(body).Single().Id==issued.Id,"Natural completion lost neutral mode or issued missile");Drain(body,issued.Id);
        Require(body.SavedText==birth&&SkillHealth.Balance256(body)==life&&NecromancerEffects.ManaBalance256(owner)==mana,"Action-mode transition healed/rebuilt/paid original actor");
    }
    private static void PendingSave(PlayerMobile owner,WarlockTaintedBody body,WarlockGoatmanBody goat,BaseCreature target,int generation)
    {
        // Explicit future action-clock input ensures this unissued transient
        // cannot race the save;no persisted effect expiry is extended.
        Require(TaintedFireBallCasting.BeginRegistered(body,target,TimedEffects.Now.AddMinutes(2))&&DeathMarkWorld.State(body).Mode==5,"Unissued save-mode fixture");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,body.Serial.Value,goat.Serial.Value,target.Serial.Value,body.SavedText,goat.SavedText,
            SkillHealth.Balance256(body),SkillHealth.Balance256(goat),owner.Location,body.Location,goat.Location,DeathMarkWorld.State(body),generation)));
    }
    private static void Check(NetState network)
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Warlock);FixtureSpace.Corridor(owner,6200,40,3300);FixtureNetwork.Authenticate(network,owner,"DeathMarkInterrupt");
        var body=WarlockTaintedBody.CreatePrepared(owner,new(owner.X+1,owner.Y,owner.Z),TimedEffects.Now);DeathMarkWorldChecks.Register(body);
        var target=FireBoltChecks.Target(owner,10);target.SetHits(50000);SkillHealth.SetBalance256(target,50000*256);target.SetResistance(ResistanceType.Fire,0);
        SkillHealth.SetBalance256(body,body.MaximumLife256-333);Exercise(owner,body,target);
        var goat=WarlockGoatmanBody.CreatePrepared(owner,new(owner.X+2,owner.Y,owner.Z),TimedEffects.Now);DeathMarkWorldChecks.Register(goat);SkillHealth.SetBalance256(goat,goat.MaximumLife256-333);
        var at=TimedEffects.Now;var temporary=GoatmanBerserkEffects.Impact(goat,1,at);Require((temporary.Flags&4)!=0,"Temporary Berserk control missing");
        Warp(owner,4,1);Require(GoatmanBerserkEffects.At(goat,at)==null,"Neutral mode kept animation-only Berserk armor override");
        var timed=GoatmanBerserkEffects.Begin(goat,1,at);DeathMarkWorld.ChangeMode(goat,4);Warp(owner,5,1);
        Require(GoatmanBerserkEffects.At(goat,at)==timed,"Neutral mode erased ordinary timed Berserk state");GoatmanBerserkEffects.Clear(goat);
        PendingSave(owner,body,goat,target,0);
        Log.Information("Death Mark interruption:unchanged A2 mode preserves windup;actual warp cancels unissued event;already-issued missile retains packet/origin/owner and damages with original NPC credit. Natural A2 end returns neutral. Mode change removes TEMPONLY Berserk but preserves timed state. Save drops pending A2 without replay while keeping original bodies/resources/commands.");
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)s.Owner);var body=(WarlockTaintedBody)World.FindMobile((Serial)s.Body);
        var goat=(WarlockGoatmanBody)World.FindMobile((Serial)s.Goat);var target=(BaseCreature)World.FindMobile((Serial)s.Target);
        Require(s.Generation<2&&body.SavedText==s.Birth&&goat.SavedText==s.GoatBirth&&SkillHealth.Balance256(body)==s.Life&&SkillHealth.Balance256(goat)==s.GoatLife
            &&TaintedFireBallCasting.State(body)==null&&Flights(body).Length==0&&DeathMarkWorld.State(body)==s.Command with{Mode=1},"Reload replayed transient A2 or reset original actor/command/resources");
        FixtureNetwork.Authenticate(network,owner,"DeathMarkInterruptReload");owner.MoveToWorld(s.OwnerPoint,Map.Trammel);
        body.MoveToWorld(s.BodyPoint,owner.Map);goat.MoveToWorld(s.GoatPoint,owner.Map);body.AIObject?.AITimer.Stop();goat.AIObject?.AITimer.Stop();
        Exercise(owner,body,target);PendingSave(owner,body,goat,target,s.Generation+1);
    }
}
