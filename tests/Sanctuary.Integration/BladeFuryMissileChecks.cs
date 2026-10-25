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

public static class BladeFuryMissileChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeFuryMissileChecks));
    private sealed record Saved(uint Owner,uint Target,uint Weapon,Point3D Origin,WeaponFlight Flight,int Health,int Mana);
    private sealed class Rolls(params int[] values):Random
    {private int _index;public override int Next(int max)=>Math.Clamp(_index<values.Length?values[_index++]:max-1,0,max-1);}
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-fury-missile-fixture.json");
    public static void Configure()
    {
        Register("blade-fury-missile-check",Check);Register("blade-fury-missile-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("blade-fury-missile-natural-check","Natural server-timer Fury flight; no learned input",_=>Core.LoopContext.Post(Natural));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,
        "Prepared Fury player packet, source-size travelling impact and actual saved flight; learned input gated",_=>Core.LoopContext.Post(()=>
        {try{Controlled(run);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Controlled(Action run)
    {
        var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;var items=WeaponMissiles.ItemEffects;
        try{WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;WeaponMissiles.ItemEffects=new Rolls();run();}
        finally{WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;WeaponMissiles.ItemEffects=items;}
    }
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-fury-damage-source-probe.json")));
        var fields=doc.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();int count=0;
        foreach(var r in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int N(int i)=>r[i].GetInt32();int Expected(string name)=>r[5][Array.IndexOf(fields,name)].GetInt32();
            int rank=N(0),hard=N(1),gear=N(2),fraction=gear==1?48:96;
            var row=SkillMechanics.For(CombatAbility.BladeFury);
            var values=new SkillValues(row,k=>k==row.SourceKey?rank:200,k=>k=="Blade Sentinel"?Math.Min(20,hard):k=="Blade Shield"?Math.Max(0,hard-20):0,99);
            int low=gear==3?40:gear==4?25:9,high=gear==3?60:gear==4?35:14;
            // Fixed stat sentinels are the declared source-ABI inputs, not
            // expected output values copied into the native implementation.
            var elements=new MissileSourceStats(FireMin:10*256,FireMax:31*256,LightningMin:12*256,LightningMax:49*256,
                MagicMin:256,MagicMax:2*256,ColdMin:7*256,ColdMax:13*256,ColdFrames:15,
                PoisonMin:257,PoisonMax:513,PoisonFrames:101,PoisonCount:2,LifeDrain:7,ManaDrain:5);
            var packet=BladeFuryLaunch.FromSource(values,low*256,high*256,gear==1,N(3)+15+(gear==2?0:118),11,13,N(4)!=0,elements);
            Require(packet.Min256==Expected("nMinDamage")&&packet.Max256==Expected("nMaxDamage")&&packet.Percent==Expected("nDamagePercent")
                &&packet.DemonPercent==Expected("nDemonDamagePercent")&&packet.UndeadPercent==Expected("nUndeadDamagePercent")
                &&packet.Critical==(N(4)!=0)&&packet.SkillAttackPercent==rank*10&&packet.SourcePoisonFrames,"Complete Fury creator/skill/source-fraction/AR metadata differential "+count);
            var e=packet.Elements;
            Require(e.FireMin==Expected("nFireMinDamage")&&e.FireMax==Expected("nFireMaxDamage")
                &&e.LightningMin==Expected("nLightMinDamage")&&e.LightningMax==Expected("nLightMaxDamage")
                &&e.MagicMin==Expected("nMagicMinDamage")&&e.MagicMax==Expected("nMagicMaxDamage")
                &&e.ColdMin==Expected("nColdMinDamage")&&e.ColdMax==Expected("nColdMaxDamage")&&e.ColdFrames==Expected("nColdLength")
                &&e.PoisonMin==Expected("nPoisonMinDamage")&&e.PoisonMax==Expected("nPoisonMaxDamage")&&e.PoisonFrames==Expected("nPoisonLength")
                &&e.PoisonCount==Expected("nPoisonCount")&&e.LifeDrain==Expected("nLifeDrainMinDamage")&&e.ManaDrain==Expected("nManaDrainMinDamage"),"Complete typed/poison/drain source differential "+count);
            Require(fraction==48||e.PoisonImpactFrames==101,"Source poison length/count");count++;
        }
        Require(count==960,"Incomplete Fury source creator inventory");
        Log.Information("Blade Fury player packets:960 complete source creator/tier/typed controls match; current10percent-per-rank AR is an explicit modern bridge, not supplied by old source");
    }
    private static PlayerMobile Fresh()
    {
        var p=BladeBodyChecks.Fresh(6600,35);p.Name="BladeFuryMissileFixture";
        while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Persistent strength allocation");}
        while(p.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Persistent dexterity allocation");}
        ClassGear.Refresh(p);return p;
    }
    private static T Equip<T>(PlayerMobile p,T item) where T:BaseWeapon
    {
        if(CombatTiming.Equipped(p) is{} old){p.AddToBackpack(old);}
        ClassGear.Profiles.Convert(item,99,GearRarity.Normal,new Random(173));p.AddToBackpack(item);
        Require(p.EquipItem(item),"Equip "+typeof(T).Name);ClassGear.Refresh(p);return item;
    }
    private static void Train(PlayerMobile p,string id,int rank)
    {while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
    private static BladeMissileTarget Target(PlayerMobile p,int dx=5)
    {
        var t=new BladeMissileTarget{Name="BladeFuryMissileTarget",NoKillAwards=true,Frozen=true,CantWalk=true};
        t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}
        return t;
    }
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value&&x.Ability==CombatAbility.BladeFury).ToArray();
    private static WeaponFlight Launch(PlayerMobile p,int rank=3,int dx=12)
    {
        int mana=NecromancerEffects.ManaBalance256(p);long id=BladeFuryMissiles.Launch(p,new(p.X+dx,p.Y,p.Z),rank,new Rolls());
        Require(id>0&&NecromancerEffects.ManaBalance256(p)==mana,"Prepared launch failed or stole caller payment");return WeaponMissiles.State(id);
    }
    private static void Hold(PlayerMobile p,WeaponFlight flight)
    {
        var saved=WeaponMissiles.Snapshot();WeaponMissiles.Restore(saved with{Flights=saved.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(flight).ToArray()});
    }
    private static void Drain(PlayerMobile p)
    {int guard=60;while(Own(p).Length>0&&guard-->0){WeaponMissiles.Tick(Own(p).Single().NextUtc);}Require(guard>0,"Nonterminating Fury flight");}
    private static void Collect()
    {
        var p=Fresh();try
        {
            var unarmed=BladeFuryLaunch.Capture(p,3,new Rolls());Require(unarmed.Percent==0&&!unarmed.Critical,"Unarmed acquired weapon strength/mastery");
            var claw=Equip(p,new Tekagi());Train(p,"assassin.clawmastery",20);var values=SkillMechanics.Values(p,CombatAbility.BladeFury,3);
            var packet=BladeFuryLaunch.Capture(p,3,new Rolls(99,0));var raw=ClassStats.WeaponBaseDamage(p);
            Require(packet.Critical&&packet.Min256==raw.Min*192+values.PhysicalDamage256(false)&&packet.Max256==raw.Max*192+values.PhysicalDamage256(true)
                &&packet.SkillAttackPercent==30,"Actual selected claw/mastery critical/current AR/source fraction");
            var target=Target(p);int ar=BladeFuryMissiles.AttackRating(p,target,30);
            Require(ar==ClassStats.AttackRatingAgainst(p,target,30-WeaponMasteries.Bonus(p,claw,1)),"Explicit staged missile AR pool");target.Delete();
            var staff=Equip(p,new QuarterStaff());var two=BladeFuryLaunch.Capture(p,3,new Rolls());raw=ClassStats.WeaponBaseDamage(p);
            Require(two.Min256==raw.Min*96+values.PhysicalDamage256(false)&&two.UndeadPercent==50,"Two-hand half source/blunt bonus");
            var bow=Equip(p,new Bow());var arrows=new Arrow(9);p.AddToBackpack(arrows);int durability=bow.HitPoints;
            var flight=Launch(p);Require(arrows.Amount==9&&bow.HitPoints==durability&&flight is{Frames:40,Activation:1,NextDelay:0,Pierces:0,Flags:32,MechanicalProfile:BladeFuryMissiles.Profile}
                &&flight.SourceSteps.Cells.Length==1&&flight.Damage.SourcePoisonFrames,"Fury consumed ammunition/wear or failed its saved contract");WeaponMissiles.Cancel(p);
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Wormskull").Id,99,new Random(173),true);
            p.AddToBackpack(bow);Require(p.EquipItem(claw)&&p.EquipItem(helm),"Actual poison gear");ClassGear.Refresh(p);
            var poison=BladeFuryLaunch.Capture(p,3,new Rolls());Require(poison.Elements.PoisonMin>0&&poison.Elements.PoisonFrames==200,"Actual poison capture");
            var text=JsonSerializer.Serialize(poison);p.AddToBackpack(helm);p.AddToBackpack(claw);p.RawStr+=20;ClassGear.Refresh(p);
            Require(JsonSerializer.Serialize(poison)==text,"Later gear rewrote released damage");
        }
        finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void Impact()
    {
        var p=Fresh();try
        {
            var weapon=Equip(p,new Tekagi());var first=Target(p,5);var second=Target(p,9);var f=Launch(p);
            int a=SkillHealth.Balance256(first),b=SkillHealth.Balance256(second),wear=weapon.HitPoints;
            var damage=f.Damage.Roll(first,_=>0);Drain(p);
            Require(SkillHealth.Balance256(first)==a-NamedElements.To256(damage.Total)&&SkillHealth.Balance256(second)==b
                &&weapon.HitPoints==wear,"Actual travelling impact/size1/no penetration/no wear");
            f=Launch(p);WeaponMissiles.HitRoll=()=>1;Drain(p);Require(SkillHealth.Balance256(second)==b&&SkillHealth.Balance256(first)==a-NamedElements.To256(damage.Total),"Attack miss damaged a victim or penetrated");WeaponMissiles.HitRoll=()=>0;
            var basePacket=BladeFuryLaunch.Capture(p,3,new Rolls());var slow=new SlowMissilePayload(50,40);var scaled=BladeFuryMissiles.Scale(basePacket,slow);
            Require(scaled.Min256==basePacket.Min256*40/100&&scaled.Elements.PoisonFrames==basePacket.Elements.PoisonFrames,"Slow raw endpoint/length contract");
            var normal=BladeFuryMissiles.Velocity(new(10,0,32,1),3,SlowMissilePayload.Normal);
            var reduced=BladeFuryMissiles.Velocity(new(10,0,32,1),3,slow);Require(reduced.X<normal.X&&reduced.Y==normal.Y,"Captured source slow velocity");
            var states=AmazonSlowMissiles.Snapshot();
            try
            {
                AmazonSlowMissiles.Restore(states.Append(new(p.Serial.Value,p.Serial.Value,3,50,40,TimedEffects.Now.AddMinutes(1))).ToArray());
                var slowed=Launch(p);Require(slowed.Slow==slow&&slowed.Vx==reduced.X&&slowed.Damage.Min256==scaled.Min256
                    &&slowed.Damage.Elements.PoisonFrames==basePacket.Elements.PoisonFrames,"Actual factory failed to capture source slow context");
                WeaponMissiles.Cancel(p);
            }
            finally{AmazonSlowMissiles.Restore(states);}
            f=Launch(p);Hold(p,f with{NextUtc=TimedEffects.Now.AddMinutes(10)});int health=SkillHealth.Balance256(first);p.MoveToWorld(p.Location,Map.Internal);
            WeaponMissiles.Tick(TimedEffects.Now.AddMinutes(11));Require(Own(p).Length==0&&SkillHealth.Balance256(first)==health,"Internal-map offline flight damaged or failed expiry");
            p.MoveToWorld(first.Location,first.Map);first.Delete();second.Delete();
            Require(!AssassinCombat.Implemented(CombatAbility.BladeFury)&&!ClassCombat.Available(p,CombatAbility.BladeFury),"Prepared flight exposed learned input");
        }
        finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void Persist()
    {
        var p=Fresh();var w=Equip(p,new Tekagi());var target=Target(p,15);var f=Launch(p,3,22);
        WeaponMissiles.Tick(f.NextUtc.AddMilliseconds(40));f=Own(p).Single();Require(f.Frame==2&&f.SourceSteps.Cells.Length>0,"Saved real flight progression");
        f=f with{NextUtc=TimedEffects.Now.AddMinutes(10)};Hold(p,f);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,w.Serial.Value,p.Location,f,SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p))));
    }
    private static void Lifecycle()
    {
        foreach(bool death in new[]{false,true})
        {
            var p=Fresh();Equip(p,new Tekagi());Launch(p);
            if(death){p.Kill();}else{ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}
            Require(Own(p).Length==0,"Death/respec retained a prepared Fury flight");p.Corpse?.Delete();p.Delete();
        }
    }
    private static void Check(){Differential();Collect();Impact();Lifecycle();Persist();}
    private static void Reload()
    {
        Differential();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        var target=(BladeMissileTarget)World.FindMobile((Serial)s.Target);var w=(BaseWeapon)World.FindItem((Serial)s.Weapon);
        Require(Own(p).Single()==s.Flight&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(target)==s.Health,"Actual saved Fury flight/packet/clock/mana changed");
        p.MoveToWorld(s.Origin,Map.Maps[s.Flight.Map]);target.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);p.AddToBackpack(w);ClassGear.Refresh(p);
        var f=s.Flight with{NextUtc=TimedEffects.Now};Hold(p,f);int health=SkillHealth.Balance256(target);var expected=f.Damage.Roll(target,_=>0);
        Drain(p);Require(SkillHealth.Balance256(target)==health-NamedElements.To256(expected.Total)&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Restored immutable packet failed damaging continuation or repaid mana");
        Require(p.EquipItem(w),"Restore saved selected weapon");ClassGear.Refresh(p);f=s.Flight with{NextUtc=TimedEffects.Now.AddMinutes(10)};Hold(p,f);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Flight=f,Health=SkillHealth.Balance256(target)}));
        Log.Information("Blade Fury flight reload: actual player/weapon/source path/packet/profile/clock retained; gear swap does not rewrite released damage; continuation damages once without payment; learned input remains gated");
    }
    private static void Natural()
    {
        PlayerMobile p=null;BladeMissileTarget target=null;
        var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;var items=WeaponMissiles.ItemEffects;
        void Clean()
        {
            WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;WeaponMissiles.ItemEffects=items;
            if(p!=null){WeaponMissiles.Cancel(p);p.Delete();}target?.Delete();
        }
        try
        {
            p=Fresh();var weapon=Equip(p,new Tekagi());target=Target(p,5);
            WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;WeaponMissiles.ItemEffects=new Rolls();
            int health=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p),wear=weapon.HitPoints;
            var flight=Launch(p);int expected=NamedElements.To256(flight.Damage.Roll(target,_=>0).Total);long deadline=Core.TickCount+2500;
            void Observe()
            {
                try
                {
                    if(SkillHealth.Balance256(target)==health&&Core.TickCount<deadline)
                    {Timer.StartTimer(TimeSpan.FromMilliseconds(25),Observe);return;}
                    Require(SkillHealth.Balance256(target)==health-expected&&Own(p).Length==0
                        &&NecromancerEffects.ManaBalance256(p)==mana&&weapon.HitPoints==wear,"Natural Fury travel/impact/no extra payment or wear");
                    Clean();Log.Information("blade-fury-missile-natural-result: passed");
                }
                catch(Exception e){Clean();Log.Error(e,"blade-fury-missile-natural-result: failed");}
            }
            Timer.StartTimer(TimeSpan.FromMilliseconds(25),Observe);Log.Information("blade-fury-missile-natural-check: passed");
        }
        catch(Exception e){Clean();Log.Error(e,"blade-fury-missile-natural-check: failed");}
    }
}
