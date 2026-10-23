// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class LeapChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(LeapChecks));
    private static bool _creating;
    private sealed record Saved(LeapFlight Flight,int Mana,int Fraction,uint Obstacle);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"leap-fixtures.json");
    public static void Configure(){FreezeMana();Register("leap-check",Check);Register("leap-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>FreezeMana();
    private static void FreezeMana()
    {
        var prior=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=actor=>actor is PlayerMobile && (_creating || actor.Name=="LeapFixture")?TimeSpan.FromHours(1):prior?.Invoke(actor)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Leap motion, reservation, mana, knockback and saved flight checks",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static PlayerMobile Fresh(int rank=1)
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="LeapFixture",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(owner,99);
        while(ClassCreation.Profiles.GetRank(owner.Serial,"barbarian.leap")<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"barbarian.leap",out _),"Leap talent allocation");}
        FixtureSpace.Place(owner,new(5450,1160,0),Map.Trammel);
        for(int x=2200;x<=2500;x+=50)for(int y=1000;y<=1300;y+=50)
        {
            Point3D point;try{point=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}
            bool clear=true;foreach(var actor in owner.Map.GetMobilesInRange<Mobile>(point,10)){if(actor!=owner){clear=false;break;}}
            if(LeapTerrain.Allowed(owner,owner.Map,point) && clear)
            {owner.MoveToWorld(point,owner.Map);owner.Hits=owner.HitsMax;owner.Mana=owner.ManaMax;return owner;}
        }
        owner.Delete();throw new InvalidOperationException("No independent Leap fixture corridor");
    }
    private static void Ready(PlayerMobile owner)
    {Require(BarbarianLeap.State(owner)==null,"Previous flight remains active");ClassCombat.Stop(owner);var action=ClassCombat.For(owner);action.NextAction=action.RecoveryUntil=Core.TickCount;}
    private static void Step(PlayerMobile owner,int count=1)
    {for(int i=0;i<count;i++){var state=BarbarianLeap.State(owner);Require(state!=null,"Missing flight phase");BarbarianLeap.Tick(state.NextUtc);}}
    private static void Finish(PlayerMobile owner)
    {for(int i=0;i<200 && BarbarianLeap.State(owner)!=null;i++){Step(owner);}Require(BarbarianLeap.State(owner)==null,"Unfinished Leap");}
    private static bool Begin(PlayerMobile owner,Point3D point)=>ClassCombat.Perform(owner,CombatAbility.Leap,null,point,Core.TickCount);
    private static Point3D End(PlayerMobile owner,int distance=4)=>new(owner.X+distance,owner.Y,owner.Z);
    private static void Reservations()
    {
        var owner=Fresh();var point=End(owner);owner.Mana=10;int stamina=owner.Stam;
        Require(Begin(owner,point),"Unarmed Leap begin");var flight=BarbarianLeap.State(owner);
        Require(owner.Mana==8 && flight.Destination==point && flight.Phase==LeapPhase.Windup && flight.Velocity256==4032
            && flight.MinimumAirFrames==8,"Leap mana/start/source velocity");
        Require(World.FindMobile((Serial)flight.Reservation) is SkillLandingReservation marker && marker.Alive && marker.Blessed && marker.Hidden
            && !marker.CanBeDamaged() && !owner.Map.CanSpawnMobile(point) && Teleportation.Space(owner.Map,point,owner),"Native landing reservation/owner clearance");
        var observer=Fresh();observer.MoveToWorld(new(point.X+1,point.Y,point.Z),owner.Map);observer.Direction=Direction.West;
        Require(!observer.Move(Direction.West) && observer.X==point.X+1 && !Teleportation.Space(owner.Map,point,observer),"Player walked/teleported into landing reservation");observer.Delete();
        var monster=new GraveyardSkeleton{NoKillAwards=true};monster.AIObject.AITimer.Stop();monster.MoveToWorld(new(point.X+1,point.Y,point.Z),owner.Map);monster.Direction=Direction.West;
        Require(!monster.Move(Direction.West) && monster.X==point.X+1,"NPC walked into reservation");monster.Delete();
        Require(!owner.Move(Direction.East) && !ClassCombatHooks.ActionAllowed(owner) && !HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount),"Leap input/get-hit lock");
        ClassCombat.Stop(owner);Require(BarbarianLeap.State(owner)!=null,"Stop canceled uninterruptible Leap");Step(owner,8);
        flight=BarbarianLeap.State(owner);Require(flight.Phase==LeapPhase.Airborne && owner.Location!=flight.Origin && owner.Location!=point,"Flight did not move separately from windup");
        monster=new GraveyardSkeleton{NoKillAwards=true};monster.AIObject.AITimer.Stop();monster.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);monster.Direction=Direction.West;
        Require(monster.Move(Direction.West) && monster.Location==owner.Location,"Airborne player retained blocking footprint");monster.Delete();
        Finish(owner);Require(owner.Location==point && owner.Mana==8 && owner.Stam==stamina && World.FindMobile((Serial)flight.Reservation)==null
            && ClassCombatHooks.ActionAllowed(owner),"Landing/once-only debit/stamina/reservation release");
        Ready(owner);owner.Mana=1;Require(!Begin(owner,End(owner)) && owner.Mana==1 && BarbarianLeap.State(owner)==null,"Insufficient mana invented an attack or a paid flight");
        owner.Mana=10;Require(Begin(owner,owner.Location),"In-place Leap unavailable");Step(owner,5+7);Require(BarbarianLeap.State(owner) is{Phase:LeapPhase.Airborne},"Minimum airborne duration not retained");Finish(owner);owner.Delete();
    }
    private static Item WalkingObstacle(PlayerMobile owner)
    {
        var origin=owner.Location;var end=End(owner,5);
        for(int art=1;art<TileData.ItemTable.Length;art++)
        {
            var data=TileData.ItemTable[art];
            if(data.CalcHeight<16 || (data.Flags&(TileFlag.Impassable|TileFlag.NoShoot|TileFlag.Surface|TileFlag.Door))!=TileFlag.Impassable){continue;}
            var item=new Item(art){Movable=false,Name="[fixture walking-only obstacle]"};item.MoveToWorld(new(origin.X+2,origin.Y,origin.Z),owner.Map);
            if(LeapTerrain.Flying(owner.Map,origin,end) && !owner.Map.CanSpawnMobile(origin.X+2,origin.Y,origin.Z)){return item;}item.Delete();
        }
        throw new InvalidOperationException("No native walking-only obstacle art");
    }
    private static void TerrainAndKnockback()
    {
        var owner=Fresh();var origin=owner.Location;var obstacle=WalkingObstacle(owner);var point=End(owner,5);
        Require(Begin(owner,point),"Leap could not cross a walking-only obstacle");Finish(owner);Require(owner.Location==point,"Flying path stopped on walking collision");obstacle.Delete();
        Ready(owner);owner.MoveToWorld(origin,owner.Map);
        var target=new GraveyardSkeleton{NoKillAwards=true};target.AIObject.AITimer.Stop();target.MoveToWorld(new(point.X+1,point.Y,point.Z),owner.Map);target.SetHits(5000);
        int health=target.Hits;
        var previous=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Leap consulted shield block");
        try
        {
            Require(Begin(owner,point),"Knockback Leap start");
            while(BarbarianLeap.State(owner).Phase!=LeapPhase.Recovery){Step(owner);}
            Require(target.Hits==health && SourceKnockbacks.Remaining(target)>0,"Zero-damage landing failed mapped knockback");Finish(owner);
        }
        finally{SourceBlocking.BlockRoll=previous;SourceKnockbacks.Clear(target);target.Delete();owner.Delete();}
    }
    private static void DeathAndCancellation()
    {
        var owner=Fresh();var point=End(owner);Require(Begin(owner,point),"Death-delay start");Step(owner,6);
        owner.Damage(owner.Hits+1);Require(owner.Alive && owner.Hits==0 && BarbarianLeap.State(owner).DelayedDeath,"Lethal hit interrupted instead of deferring death");
        Finish(owner);Require(!owner.Alive && owner.Location==point,"Deferred death did not resolve at landing");owner.Corpse?.Delete();owner.Delete();
        owner=Fresh();Require(Begin(owner,End(owner)),"External move start");Step(owner,8);var state=BarbarianLeap.State(owner);
        World.FindMobile((Serial)state.Reservation).Delete();BarbarianLeap.Tick(state.NextUtc);
        Require(BarbarianLeap.State(owner)==null && owner.Location==state.Destination && owner.Mana==owner.ManaMax-2,
            "Lost reservation did not safely settle a paid flight");
        Ready(owner);Require(Begin(owner,owner.Location),"Foreign map cleanup start");Step(owner,6);state=BarbarianLeap.State(owner);
        owner.MoveToWorld(new(100,100,0),Map.Felucca);BarbarianLeap.Tick(state.NextUtc);
        Require(BarbarianLeap.State(owner)==null && owner.Map==Map.Felucca && owner.Location==new Point3D(100,100,0)
            && World.FindMobile((Serial)state.Reservation)==null,"Foreign map change moved actor back or retained reservation");owner.Delete();
    }
    private static Saved Save(LeapPhase phase)
    {
        var owner=Fresh();Item obstacle=phase==LeapPhase.Airborne?WalkingObstacle(owner):null;Require(Begin(owner,End(owner,5)),"Saved phase begin");
        if(phase==LeapPhase.Airborne){Step(owner,10);}
        else if(phase==LeapPhase.Recovery){while(BarbarianLeap.State(owner).Phase!=LeapPhase.Recovery){Step(owner);}}
        var state=BarbarianLeap.State(owner);Require(state.Phase==phase,"Wrong saved phase");
        state=state with{NextUtc=TimedEffects.Now.AddMinutes(10)};
        BarbarianLeap.Restore(BarbarianLeap.Snapshot().Select(s=>s.Owner==state.Owner?state:s).ToArray());
        return new(state,owner.Mana,NecromancerEffects.Profiles.Find(owner)?.ManaFraction??0,obstacle?.Serial.Value??0);
    }
    private static void Check()
    {
        Reservations();TerrainAndKnockback();DeathAndCancellation();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(Enum.GetValues<LeapPhase>().Select(Save).ToArray()));
        Natural();
    }
    private static void Natural()
    {
        var owner=Fresh();var point=End(owner,2);int mana=owner.Mana;long began=Core.TickCount,deadline=began+5000;
        Require(Begin(owner,point),"Natural timer Leap begin");
        void Poll()
        {
            try
            {
                if(BarbarianLeap.State(owner)==null)
                {
                    Require(Core.TickCount-began>=13*40 && owner.Location==point && owner.Mana==mana-2,"Natural timer duration/landing/payment");
                    owner.Delete();Log.Information("leap-result: passed");return;
                }
                Require(Core.TickCount-deadline<0,"Natural Leap did not finish");Timer.StartTimer(TimeSpan.FromMilliseconds(50),Poll);
            }
            catch(Exception ex){owner.Delete();Log.Error(ex,"leap-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(50),Poll);
    }
    private static void Reload()
    {
        foreach(var row in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var owner=(PlayerMobile)World.FindMobile((Serial)row.Flight.Owner);
            Require(BarbarianLeap.State(owner)==null && World.FindMobile((Serial)row.Flight.Reservation)==null,"Offline load left an active flight/reservation");
            Require(owner.LogoutMap?.MapID==row.Flight.Map && owner.LogoutLocation==row.Flight.Destination && owner.Mana==row.Mana
                && (NecromancerEffects.Profiles.Find(owner)?.ManaFraction??0)==row.Fraction,"Saved flight did not settle safely without re-payment");
            owner.MoveToWorld(owner.LogoutLocation,owner.LogoutMap);Require(Teleportation.Space(owner.Map,owner.Location,owner),"Saved landing is not legal");
            World.FindItem((Serial)row.Obstacle)?.Delete();owner.Delete();
        }
        Check();
    }
}
