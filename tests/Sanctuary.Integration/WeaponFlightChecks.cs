// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary.Integration;

public static class WeaponFlightChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WeaponFlightChecks));
    private sealed record Saved(WeaponFlight Flight,WeaponFlightLease[] Leases,Point3D Origin,uint First,uint Second,int FirstHealth,int SecondHealth,int Mana256,uint Arrow,int Amount);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"weapon-flight-fixtures.json");
    public static void Configure(){Freeze();Register("weapon-flight-check",Check);Register("weapon-flight-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var previous=Mobile.HitsRegenRateHandler;
        Mobile.HitsRegenRateHandler=actor=>actor.Name=="WeaponFlightTarget"?TimeSpan.FromHours(1):previous?.Invoke(actor)??Mobile.DefaultHitsRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Travelling weapon component/source damage/save checks",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int Health(Mobile actor)=>actor.Hits*256-SkillHealth.Remainder256(actor);
    private static void Controlled(Action action)
    {
        var damage=WeaponMissiles.DamageRoll;var hit=WeaponMissiles.HitRoll;var block=SourceBlocking.BlockRoll;
        try{WeaponMissiles.DamageRoll=_=>0;WeaponMissiles.HitRoll=()=>0;SourceBlocking.BlockRoll=_=>99;action();}
        finally{WeaponMissiles.DamageRoll=damage;WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=block;}
    }
    private static PlayerMobile Fresh()
    {
        // Item-only component fixture: learned Pierce is tested separately.
        var owner=new PlayerMobile{Body=0x190,Name="WeaponFlightFixture",Player=true};
        ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);
        owner.MoveToWorld(new(2200,1000,0),Map.Trammel);owner.Hits=owner.HitsMax;owner.Mana=owner.ManaMax;
        bool found=false;
        for(int x=2200;x<=2500 && !found;x+=50)for(int y=1000;y<=1300 && !found;y+=50)
        {
            Point3D point;try{point=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}
            if(SpellHelper.IsTown(point,owner)){continue;}
            bool empty=true;foreach(var other in owner.Map.GetMobilesInRange<Mobile>(point,12)){if(other!=owner){empty=false;break;}}
            if(empty){owner.MoveToWorld(point,owner.Map);found=true;}
        }
        Require(found,"No independent weapon flight corridor");return owner;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile owner,int x=3,int y=0)
    {
        var actor=new GraveyardSkeleton{NoKillAwards=true,Frozen=true,CantWalk=true,Name="WeaponFlightTarget"};actor.AIObject.AITimer.Stop();actor.SetHits(50000);
        actor.SetResistance(ResistanceType.Physical,0);actor.SetResistance(ResistanceType.Fire,0);
        actor.MoveToWorld(new(owner.X+x,owner.Y+y,owner.Z),owner.Map);Require(ClassCombat.IsHostile(owner,actor),"Nonhostile flight target");return actor;
    }
    private static WeaponMissileDamage Physical()=>new(20*256,20*256,50,0,10,true,new());
    private static long Launch(PlayerMobile owner,WeaponMissileDamage damage=null,long cast=0,int pierces=0)=>
        WeaponMissiles.Launch(owner,CombatAbility.MultipleShot,new(20,0,2080,1),damage??Physical(),cast==0?WeaponMissiles.NewCast():cast,pierces);
    private static void Step(long id,int count)
    {
        for(int i=0;i<count;i++){var state=WeaponMissiles.State(id);if(state==null){return;}WeaponMissiles.Tick(state.NextUtc);}
    }
    private static void Finish(long id)
    {Step(id,250);Require(WeaponMissiles.State(id)==null,"Unfinished source-lifetime missile");}
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"weapon-missile-impact-probe.json")));int count=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int min=row[0].GetInt32(),max=row[1].GetInt32(),draw=row[2].GetInt32(),mastery=row[6].GetInt32(),kind=row[4].GetInt32();
            var elements=new MissileSourceStats(min,max,min,max,min,max,min,max,13,min,max,101,row[7].GetInt32());
            var payload=new WeaponMissileDamage(min,max,row[3].GetInt32(),31,47,row[5].GetInt32()!=0,elements,mastery,mastery,mastery,mastery,mastery);
            var actual=payload.Roll(kind!=0,kind==2,kind==3,span=>span<=0?0:Math.Min(draw,span-1));
            Require(NamedElements.To256(actual.Physical)==row[8].GetInt32() && NamedElements.To256(actual.Fire)==row[9].GetInt32()
                && NamedElements.To256(actual.Magic)==row[10].GetInt32() && NamedElements.To256(actual.Lightning)==row[11].GetInt32()
                && NamedElements.To256(actual.Cold)==row[12].GetInt32() && actual.PoisonMs==row[14].GetInt32()*40
                && NamedElements.To256(actual.Poison)==row[13].GetInt32()*row[14].GetInt32(),"Source impact roll/percentage/critical/typed poison differential");count++;
        }
        Require(count==5184,"Incomplete impact source fixture");Log.Information("Weapon missile native impact differential: {Count} compiled cases",count);
        ConversionDifferential();
    }
    private static void ConversionDifferential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"magic-arrow-source-probe.json")));
        int splits=0,impacts=0;
        foreach(var row in data.RootElement.GetProperty("conversionCases").EnumerateArray())
        {
            var result=WeaponMissileDamage.ConvertPhysical(row[0].GetInt32(),row[2].GetInt32());
            Require(result.Physical256==row[3].GetInt32() && result.Magic256+row[1].GetInt32()==row[4].GetInt32(),"Source physical conversion/large-value division");splits++;
        }
        foreach(var row in data.RootElement.GetProperty("impactCases").EnumerateArray())
        {
            int raw=row[0].GetInt32(),mastery=row[4].GetInt32(),kind=row[5].GetInt32();
            var payload=new WeaponMissileDamage(raw,raw,row[1].GetInt32(),31,47,row[2].GetInt32()!=0,
                new(FireMin:1024,FireMax:1024,MagicMin:257,MagicMax:513),FireMastery:mastery,MagicMastery:mastery)
                {PhysicalToMagicPercent=row[3].GetInt32()};
            var actual=payload.Roll(kind!=0,kind==2,kind==3,_=>0);
            Require(NamedElements.To256(actual.Physical)==row[6].GetInt32() && NamedElements.To256(actual.Magic)==row[7].GetInt32()
                && NamedElements.To256(actual.Fire)==row[8].GetInt32(),"Source percentage/critical/mastery then conversion ordering");impacts++;
        }
        Require(splits==216 && impacts==1536,"Incomplete Magic Arrow source fixture");
        var old=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(Physical()));old.AsObject().Remove("PhysicalToMagicPercent");old.AsObject().Remove("PhysicalSupplement");old.AsObject().Remove("Conversion");
        Require(old.Deserialize<WeaponMissileDamage>()==Physical(),"Older flight payload without conversion changed");
        Log.Information("Magic Arrow component:216 conversion/1536 integrated source-native cases; legacy zero-conversion payload retained");
    }
    private static void ConvertedDelivery()
    {
        var owner=Fresh();var enemy=Enemy(owner);
        try
        {
            Require(NamedElements.MagicResistance(enemy)==0,"Magic Arrow component fixture magic resistance");
            enemy.SetResistance(ResistanceType.Physical,100);enemy.SetResistance(ResistanceType.Fire,50);
            var damage=new WeaponMissileDamage(20*256,20*256,50,0,10,true,
                new(FireMin:512,FireMax:512,MagicMin:256,MagicMax:256),FireMastery:25,MagicMastery:100)
                {PhysicalToMagicPercent=50};
            int before=Health(enemy),mana=NecromancerEffects.ManaBalance256(owner);
            long id=Launch(owner,damage);Require(id>0 && Health(enemy)==before,"Converted projectile hit before travel");Finish(id);
            Require(Health(enemy)==before-9024 && NecromancerEffects.ManaBalance256(owner)==mana,
                "Physical immunity incorrectly blocked converted magic, doubled original magic, reapplied mastery or paid mana");
            before=Health(enemy);id=Launch(owner,damage with{PhysicalToMagicPercent=100});Finish(id);
            Require(Health(enemy)==before-17216,"Full conversion did not retain all physical as magic plus independent elements");
        }
        finally{WeaponMissiles.Cancel(owner);owner.Delete();enemy.Delete();}
    }
    private static void Movement()
    {
        var owner=Fresh();var enemy=Enemy(owner,3,2);int health=Health(enemy),mana=NecromancerEffects.ManaBalance256(owner);
        try
        {
            long id=Launch(owner);Require(id>0 && Health(enemy)==health,"Immediate weapon-ray damage");Step(id,2);
            Require(Health(enemy)==health,"Off-axis target hit");enemy.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);Step(id,2);
            Require(Health(enemy)==health-64*256 && WeaponMissiles.State(id)==null,"Swept moving-target impact/source payload/once-only collision");
            Require(NecromancerEffects.ManaBalance256(owner)==mana,"Travelling component replayed resource payment");
            var fire=new WeaponMissileDamage(0,0,0,0,0,false,new(FireMin:2560,FireMax:2560),FireMastery:25);
            health=Health(enemy);id=Launch(owner,fire);Step(id,2);enemy.SetResistance(ResistanceType.Fire,50);Finish(id);
            Require(Health(enemy)==health-1600,"Impact-time resistance/saved fire mastery");enemy.SetResistance(ResistanceType.Fire,0);
            id=Launch(owner);health=Health(enemy);enemy.MoveToWorld(new(owner.X+3,owner.Y+2,owner.Z),owner.Map);Finish(id);
            Require(Health(enemy)==health,"Moved-away target hit at old location");enemy.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);
            id=Launch(owner);health=Health(enemy);var state=WeaponMissiles.State(id);WeaponMissiles.Tick(state.NextUtc.AddSeconds(3));
            Require(Health(enemy)==health-64*256 && WeaponMissiles.State(id)==null,"Overdue frames skipped an impact");
        }
        finally{WeaponMissiles.Cancel(owner);owner.Delete();enemy.Delete();}
    }
    private static void LiveHitCheck()
    {
        var owner=Fresh();var enemy=Enemy(owner);try
        {
            owner.RawDex=1;double low=ClassStats.HitChance(ClassStats.AttackRatingAgainst(owner,enemy),ClassStats.Defense(enemy),ClassStats.Level(owner),ClassStats.Level(enemy));
            long id=Launch(owner);owner.RawDex=1000;double high=ClassStats.HitChance(ClassStats.AttackRatingAgainst(owner,enemy),ClassStats.Defense(enemy),ClassStats.Level(owner),ClassStats.Level(enemy));
            Require(high>low,"No fixture live-AR contrast");WeaponMissiles.HitRoll=()=>(low+high)/2;int health=Health(enemy);Finish(id);
            Require(Health(enemy)==health-64*256,"Owner AR was incorrectly snapshotted at launch");
            id=Launch(owner);owner.RawDex=1;health=Health(enemy);Finish(id);Require(Health(enemy)==health,"Live AR loss ignored");
        }
        finally{WeaponMissiles.Cancel(owner);owner.Delete();enemy.Delete();WeaponMissiles.HitRoll=()=>0;}
    }
    private static void LeaseAndSave()
    {
        var owner=Fresh();var first=Enemy(owner);var second=Enemy(owner,5);if(owner.Backpack==null){owner.AddItem(new Backpack());}
        var belt=NamedGear.Create("unique/243",99,new Random(13),true);owner.AddToBackpack(belt);Require(owner.EquipItem(belt) && WeaponMissiles.CurrentPierce(owner)>0,"Actual Razortail fixture");
        long cast=WeaponMissiles.NewCast();long a=Launch(owner,cast:cast),b=Launch(owner,cast:cast);int before=Health(first);Finish(a);Finish(b);
        Require(Health(first)==before-64*256,"Same-cast four-frame lease did not suppress another missile");
        a=Launch(owner);b=Launch(owner);before=Health(first);Finish(a);Finish(b);Require(Health(first)==before-128*256,"Separate casts shared a hit-delay lease");
        a=Launch(owner,pierces:2);Step(a,4);Require(WeaponMissiles.State(a)?.LastCollision==first.Serial.Value,"Pierced first collision state");
        int afterFirst=Health(first),beforeSecond=Health(second);owner.AddToBackpack(belt);Step(a,4);
        Require(Health(first)==afterFirst && Health(second)==beforeSecond-64*256 && WeaponMissiles.State(a)==null,"Live pierce loss failed to stop after the next direct impact");
        Require(owner.EquipItem(belt),"Restore Razortail");var arrows=new Arrow(5);owner.AddToBackpack(arrows);
        a=Launch(owner,damage:Physical() with{PhysicalToMagicPercent=7},pierces:2);Step(a,4);var flight=WeaponMissiles.State(a);Require(flight is{Frame:4,Pierces:1},"Saved moving phase");
        Hold(flight);var leases=WeaponMissiles.Snapshot().Leases.Where(l=>l.Cast==flight.Cast).ToArray();Require(leases.Length>0,"Missing saved hit-delay lease");
        var saved=new Saved(WeaponMissiles.State(a),leases,owner.Location,first.Serial.Value,second.Serial.Value,Health(first),Health(second),NecromancerEffects.ManaBalance256(owner),arrows.Serial.Value,arrows.Amount);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Hold(WeaponFlight flight)
    {
        var all=WeaponMissiles.Snapshot();var delta=TimedEffects.Now.Add(FixtureSpace.Retention)-flight.NextUtc;
        WeaponMissiles.Restore(all with{Flights=all.Flights.Select(f=>f.Id==flight.Id?f with{NextUtc=f.NextUtc+delta}:f).ToArray(),
            Leases=all.Leases.Select(l=>l.Cast==flight.Cast?l with{UntilUtc=l.UntilUtc+delta}:l).ToArray()});
    }
    private static void Natural()
    {
        var owner=Fresh();var enemy=Enemy(owner);int before=Health(enemy);long id=0;
        var damage=WeaponMissiles.DamageRoll;var hit=WeaponMissiles.HitRoll;var block=SourceBlocking.BlockRoll;
        WeaponMissiles.DamageRoll=_=>0;WeaponMissiles.HitRoll=()=>0;SourceBlocking.BlockRoll=_=>99;
        try{id=Launch(owner);Require(Health(enemy)==before,"Natural launch hit immediately");}
        catch{WeaponMissiles.DamageRoll=damage;WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=block;owner.Delete();enemy.Delete();throw;}
        Timer.StartTimer(TimeSpan.FromMilliseconds(400),()=>
        {
            try{Require(Health(enemy)==before-64*256 && WeaponMissiles.State(id)==null,"Natural40ms travel/impact");Log.Information("weapon-flight-result: passed");}
            catch(Exception ex){Log.Error(ex,"weapon-flight-result: failed");}
            finally{WeaponMissiles.Cancel(owner);owner.Delete();enemy.Delete();WeaponMissiles.DamageRoll=damage;WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=block;}
        });
    }
    private static void Check(){Differential();Controlled(()=>{Movement();ConvertedDelivery();LiveHitCheck();LeaseAndSave();});Natural();}
    private static void Reload()
    {
        Differential();Controlled(()=>
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Flight.Owner);
            var first=World.FindMobile((Serial)saved.First);var second=World.FindMobile((Serial)saved.Second);var arrows=World.FindItem((Serial)saved.Arrow);
            if(owner.Map==Map.Internal){owner.MoveToWorld(saved.Origin,Map.Maps[saved.Flight.Map]);}
            var flight=WeaponMissiles.State(saved.Flight.Id);Require(WeaponMissileProfiles.LoadedFlights>0 && flight==saved.Flight,
                $"Actual saved moving missile/payload lost: loaded={WeaponMissileProfiles.LoadedFlights}, map={owner?.Map?.MapID}, alive={owner?.Alive}, actual={JsonSerializer.Serialize(flight)}, saved={JsonSerializer.Serialize(saved.Flight)}");
            Require(flight.Damage.PhysicalToMagicPercent==7,"Saved conversion percentage lost");
            Require(WeaponMissiles.Snapshot().Leases.Where(l=>l.Cast==flight.Cast).OrderBy(l=>l.Target).SequenceEqual(saved.Leases.OrderBy(l=>l.Target)),"Saved cast hit-delay leases lost");
            Require(Health(first)==saved.FirstHealth && Health(second)==saved.SecondHealth && NecromancerEffects.ManaBalance256(owner)==saved.Mana256 && arrows.Amount==saved.Amount,"Offline motion/damage/resource replay");
            var all=WeaponMissiles.Snapshot();var delta=TimedEffects.Now-flight.NextUtc;
            WeaponMissiles.Restore(all with{Flights=all.Flights.Select(f=>f.Id==flight.Id?f with{NextUtc=f.NextUtc+delta}:f).ToArray(),
                Leases=all.Leases.Select(l=>l.Cast==flight.Cast?l with{UntilUtc=l.UntilUtc+delta}:l).ToArray()});
            Finish(flight.Id);Require(Health(first)==saved.FirstHealth && Health(second)==saved.SecondHealth-64*256
                && NecromancerEffects.ManaBalance256(owner)==saved.Mana256 && arrows.Amount==saved.Amount,"Resumed collision/last-target/resource semantics");
            // Keep the already-proven moving payload inspectable for the second restart.
            all=WeaponMissiles.Snapshot();var held=saved.Flight with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};delta=held.NextUtc-saved.Flight.NextUtc;
            var leases=saved.Leases.Select(l=>l with{UntilUtc=l.UntilUtc+delta}).ToArray();
            WeaponMissiles.Restore(all with{Flights=all.Flights.Where(f=>f.Id!=held.Id).Append(held).ToArray(),
                Leases=all.Leases.Where(l=>l.Cast!=held.Cast).Concat(leases).ToArray()});
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flight=WeaponMissiles.State(saved.Flight.Id),Leases=leases,SecondHealth=Health(second)}));
        });Natural();
    }
}
