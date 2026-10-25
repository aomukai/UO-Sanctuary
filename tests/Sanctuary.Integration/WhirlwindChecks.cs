// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Spells;

namespace Sanctuary.Integration;

public static class WhirlwindChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WhirlwindChecks));
    private static bool _creating;
    private sealed record Saved(WhirlwindFlight Flight,int Mana,int Fraction);
    private sealed class HighRoll:Random{public override int Next(int max)=>max-1;}
    private static string Manifest=>Path.Join(Core.BaseDirectory,"whirlwind-fixtures.json");
    public static void Configure(){Freeze();Register("whirlwind-check",Check);Register("whirlwind-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var previous=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>p is PlayerMobile && (_creating || p.Name=="WhirlwindFixture")?TimeSpan.FromHours(1):previous?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Whirlwind walking, hand cadence, damage and saved-state checks",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Barbarian,id,out var talent),"Missing talent");
        foreach(var prerequisite in talent.Prerequisites){Train(owner,prerequisite);}
        while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
    }
    private static BaseWeapon Named(PlayerMobile owner,string key)
    {
        var weapon=(BaseWeapon)NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true);
        owner.AddToBackpack(weapon);Require(owner.EquipItem(weapon),"Equip "+key);return weapon;
    }
    private static PlayerMobile Fresh(bool dual=true)
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Player=true,Name="WhirlwindFixture"};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(owner,99);
        while(owner.RawStr<250){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength");}
        while(owner.RawDex<180){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Dexterity");}
        Train(owner,"barbarian.whirlwind");FixtureSpace.Place(owner,new(5450,1160,0),Map.Trammel);bool found=false;
        for(int x=2200;x<=2500 && !found;x+=50)for(int y=1000;y<=1300 && !found;y+=50)
        {
            Point3D point;try{point=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}
            if(SpellHelper.IsTown(point,owner)){continue;}
            bool clear=true;foreach(var other in owner.Map.GetMobilesInRange<Mobile>(point,10)){if(other!=owner){clear=false;break;}}
            if(clear){owner.MoveToWorld(point,owner.Map);found=true;}
        }
        Require(found,"No independent Whirlwind corridor");Named(owner,"Hellplague");if(dual){Named(owner,"Deathspade");}
        owner.Hits=owner.HitsMax;owner.Mana=owner.ManaMax;return owner;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile owner,int dx,int dy=0)
    {
        var target=new GraveyardSkeleton{NoKillAwards=true,Frozen=true,CantWalk=true};target.AIObject.AITimer.Stop();target.SetHits(50000);
        target.MoveToWorld(new(owner.X+dx,owner.Y+dy,owner.Z),owner.Map);Require(GemCombat.Drain(target)==0,"Fixture source skeleton drain");return target;
    }
    private static void Ready(PlayerMobile owner)
    {ClassCombat.Stop(owner);var action=ClassCombat.For(owner);action.NextAction=action.RecoveryUntil=Core.TickCount;}
    private static bool Begin(PlayerMobile owner,int distance=4,Mobile target=null)=>ClassCombat.Perform(owner,CombatAbility.Whirlwind,target,target?.Location??new(owner.X+distance,owner.Y,owner.Z),Core.TickCount);
    private static void Step(PlayerMobile owner,int count=1)
    {for(int i=0;i<count;i++){var state=BarbarianWhirlwind.State(owner);Require(state!=null,"Missing spin state");BarbarianWhirlwind.Tick(state.NextUtc);}}
    private static void Finish(PlayerMobile owner)
    {for(int i=0;i<300 && BarbarianWhirlwind.State(owner)!=null;i++){Step(owner);}Require(BarbarianWhirlwind.State(owner)==null,"Unfinished spin");}
    private static int Health(Mobile target)=>target.Hits*256-SkillHealth.Remainder256(target);
    private static int Expected(PlayerMobile owner,BaseWeapon weapon,Mobile target)
    {
        using(WeaponHands.Use(owner,weapon))
        {
            var damage=GearEffects.Damage(owner,0,target,(int)SkillMechanics.Values(owner,CombatAbility.Whirlwind).Eval("calc1"),_=>0);
            return NamedElements.To256(GearEffects.PrepareWeaponImpact(owner,target,0,new HighRoll(),damage).Parts.Total);
        }
    }
    private static void DamageAndMotion()
    {
        var owner=Fresh();var origin=owner.Location;var pair=WeaponHands.Pair(owner).Value;var a=Enemy(owner,1);var b=Enemy(owner,1,1);
        int damageA=Expected(owner,pair.Primary,a),damageB=Expected(owner,pair.Secondary,b),mana=NecromancerEffects.ManaBalance256(owner);
        int stamina=owner.Stam;Require(Begin(owner),"Spin start");var flight=BarbarianWhirlwind.State(owner);
        Require(flight.Path.Length==4 && flight.Velocity256==1536 && NecromancerEffects.ManaBalance256(owner)==mana-3200,"Path/source walking velocity/once-only12.5mana");
        var marker=World.FindMobile((Serial)flight.Reservation);
        BarbarianLeap.Tick(TimedEffects.Now);Require(marker is{Deleted:false} && !owner.Map.CanSpawnMobile(marker.Location),"Shared prune removed Whirlwind reservation");
        Require(!owner.Move(Direction.East) && !ClassCombatHooks.ActionAllowed(owner) && !HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount),"Spin input/get-hit locks");
        HitRecovery.Block(owner,Core.TickCount);ClassCombat.Stop(owner);Require(BarbarianWhirlwind.State(owner)!=null && ClassCombat.For(owner).PendingAbility==CombatAbility.Whirlwind,"Block/stop interrupted spin");
        int first,second;using(WeaponHands.Use(owner,pair.Primary)){first=CombatTiming.For(owner,CombatAbility.BasicAttack).EffectFrame;}
        using(WeaponHands.Use(owner,pair.Secondary)){second=CombatTiming.For(owner,CombatAbility.BasicAttack).EffectFrame;}
        Require(BarbarianWhirlwind.Interval(owner)==(first+second+1)/2,"Released upward-averaged basic effect frame");
        int healthA=Health(a),healthB=Health(b);Step(owner,2);Require(Health(a)==healthA && Health(b)==healthB,"Early spin strike");Step(owner);
        Require(healthA-Health(a)==damageA && healthB-Health(b)==damageB && BarbarianWhirlwind.State(owner).NextHit==3+BarbarianWhirlwind.Interval(owner),"First pulse did not use both hands/targets or skill ED");
        Step(owner,3);Require(owner.Location==a.Location && owner.X==origin.X+1,"Walking spin stopped on hostile contact");
        a.Delete();b.Delete();Finish(owner);
        Require(owner.Location==new Point3D(origin.X+4,origin.Y,origin.Z) && owner.Stam==stamina && NecromancerEffects.ManaBalance256(owner)==mana-3200 && marker.Deleted,"Finish position/stamina/debit/reservation");
        Require(ClassCombat.Perform(owner,CombatAbility.Leap,null,new(owner.X+1,owner.Y,owner.Z),Core.TickCount),"Immediate Leap after spin");
        while(BarbarianLeap.State(owner)!=null){BarbarianLeap.Tick(BarbarianLeap.State(owner).NextUtc);}owner.Delete();
    }
    private static void ShortAndSnapshot()
    {
        var owner=Fresh();var target=Enemy(owner,1);var point=target.Location;int before=Health(target);
        Require(Begin(owner,target:target),"Adjacent target did not spin");var state=BarbarianWhirlwind.State(owner);
        Require(state.Path[^1]==point,"Targeted destination not captured");target.MoveToWorld(new(point.X+3,point.Y,point.Z),owner.Map);
        Finish(owner);Require(owner.Location==point && Health(target)==before,"Spin chased a moving target or hit outside its radius");target.Delete();
        Ready(owner);target=Enemy(owner,1);before=Health(target);Require(Begin(owner,target:target),"Short spin start");Finish(owner);
        Require(Health(target)<before,"Short spin finished without a hit");target.Delete();owner.Delete();
    }
    private static void WalkingVersusFlying()
    {
        var owner=Fresh();var origin=owner.Location;var obstacle=LeapChecks.WalkingObstacle(owner);
        Require(Begin(owner),"Partial walking path start");Require(BarbarianWhirlwind.State(owner).Path.Length==1,"Spin path crossed walking-only collision");
        Finish(owner);Require(owner.Location==new Point3D(origin.X+1,origin.Y,origin.Z),"Spin did not stop before walking obstacle");
        Require(ClassCombat.Perform(owner,CombatAbility.Leap,null,new(origin.X+4,origin.Y,origin.Z),Core.TickCount),"Leap could not cross shared walking-only obstacle");
        while(BarbarianLeap.State(owner)!=null){BarbarianLeap.Tick(BarbarianLeap.State(owner).NextUtc);}
        Require(owner.Location==new Point3D(origin.X+4,origin.Y,origin.Z),"Leap incorrectly inherited walking stop");obstacle.Delete();owner.Delete();
    }
    private static void CleanupAndDeath()
    {
        var owner=Fresh();Require(Begin(owner),"Deferred death spin");Step(owner,6);var state=BarbarianWhirlwind.State(owner);var end=state.Path[^1];
        owner.Damage(owner.Hits+1);Require(owner.Alive && owner.Hits==0 && BarbarianWhirlwind.State(owner).DelayedDeath,"Spin lethal damage did not defer death");
        Finish(owner);Require(!owner.Alive && owner.Location==end,"Deferred death did not resolve at end");owner.Corpse?.Delete();owner.Delete();
        owner=Fresh();Require(Begin(owner),"Foreign map spin");state=BarbarianWhirlwind.State(owner);owner.MoveToWorld(new(100,100,0),Map.Felucca);BarbarianWhirlwind.Tick(state.NextUtc);
        Require(BarbarianWhirlwind.State(owner)==null && owner.Map==Map.Felucca && World.FindMobile((Serial)state.Reservation)==null,"Foreign map retained spin/reservation");owner.Delete();
        owner=Fresh();Require(Begin(owner),"Broken weapon spin");state=BarbarianWhirlwind.State(owner);CombatTiming.Equipped(owner).HitPoints=0;BarbarianWhirlwind.Tick(state.NextUtc);
        Require(BarbarianWhirlwind.State(owner)==null && World.FindMobile((Serial)state.Reservation)==null,"Broken weapon continued spin");owner.Delete();
    }
    private static Saved Save(bool moved)
    {
        var owner=Fresh();Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.Whirlwind),"Spin binding");Require(Begin(owner),"Saved spin start");if(moved){Step(owner,7);}
        var state=BarbarianWhirlwind.State(owner);Require(state!=null && (moved?state.Index>0:state.Index==0),"Saved phase");
        state=state with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};BarbarianWhirlwind.Restore(BarbarianWhirlwind.Snapshot().Select(s=>s.Owner==state.Owner?state:s).ToArray());
        return new(state,owner.Mana,NecromancerEffects.Profiles.Find(owner)?.ManaFraction??0);
    }
    private static void Check()
    {
        var oldHit=BarbarianWhirlwind.HitRoll;var oldElement=BarbarianWhirlwind.ElementRoll;var oldPhysical=BarbarianWhirlwind.PhysicalRoll;var oldEffects=BarbarianWhirlwind.Effects;
        BarbarianWhirlwind.HitRoll=()=>0;BarbarianWhirlwind.ElementRoll=()=>0;BarbarianWhirlwind.PhysicalRoll=_=>0;BarbarianWhirlwind.Effects=new HighRoll();
        try{DamageAndMotion();ShortAndSnapshot();WalkingVersusFlying();CleanupAndDeath();File.WriteAllText(Manifest,JsonSerializer.Serialize(new[]{Save(false),Save(true)}));}
        finally{BarbarianWhirlwind.HitRoll=oldHit;BarbarianWhirlwind.ElementRoll=oldElement;BarbarianWhirlwind.PhysicalRoll=oldPhysical;BarbarianWhirlwind.Effects=oldEffects;}
        Natural();
    }
    private static void Natural()
    {
        var owner=Fresh(false);var point=new Point3D(owner.X+2,owner.Y,owner.Z);int mana=NecromancerEffects.ManaBalance256(owner);
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=owner;owner.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Network bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(owner);
        Require(Begin(owner,2),"Natural connected spin");long start=Core.TickCount,deadline=start+5000;
        void Poll()
        {
            try
            {
                if(BarbarianWhirlwind.State(owner)==null)
                {
                    Require(owner.Location==point && NecromancerEffects.ManaBalance256(owner)==mana-3200 && Core.TickCount-start>=400,"Natural movement/payment/duration");
                    network.Mobile=null;owner.NetState=null;owner.Delete();Log.Information("whirlwind-result: passed");return;
                }
                Require(Core.TickCount-deadline<0,"Natural spin timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(50),Poll);
            }
            catch(Exception ex){network.Mobile=null;owner.NetState=null;owner.Delete();Log.Error(ex,"whirlwind-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(50),Poll);
    }
    private static void Reload()
    {
        Require(WhirlwindProfiles.LoadedFlights==2,"Saved flight profile did not deserialize both inspection states");
        foreach(var row in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var owner=(PlayerMobile)World.FindMobile((Serial)row.Flight.Owner);
            Require(BarbarianWhirlwind.State(owner)==null && World.FindMobile((Serial)row.Flight.Reservation)==null && owner.LogoutMap?.MapID==row.Flight.Map
                && owner.Mana==row.Mana && (NecromancerEffects.Profiles.Find(owner)?.ManaFraction??0)==row.Fraction
                && SkillBindings.Instance.Get(owner).Left==CombatAbility.Whirlwind,"Reload replayed spin or changed resources/binding");
            Require(owner.LogoutLocation==row.Flight.Position,"Reload advanced saved spin instead of retaining legal position");owner.Delete();
        }
        Check();
    }
}
