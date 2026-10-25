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

public sealed class BowLaunchTarget : GraveyardSkeleton
{
    // Snapshot inspection requires a fixed injured target. The parent has its
    // own OnThink recovery, independent of ordinary mobile regeneration rates.
    public BowLaunchTarget(){}
    public BowLaunchTarget(Serial serial):base(serial){}
    public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
    public override void Deserialize(IGenericReader reader)
    {base.Deserialize(reader);if(reader.ReadInt()!=0){throw new InvalidDataException("Invalid bow launch fixture version");}}
    public override void OnHitsChange(int oldValue){base.OnHitsChange(oldValue);PlagueJavelinChecks.TraceParkedHealth(this,oldValue);}
    public override void OnThink(){}
    public override void OnSectorActivate()=>AIObject?.AITimer.Stop();
}

public static class BowLaunchChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BowLaunchChecks));
    private static bool _creating;
    private sealed class Maximum:Random{public override int Next(int max)=>Math.Max(0,max-1);public override int Next(int min,int max)=>Math.Max(min,max-1);}
    private sealed record Saved(uint Owner,uint Target,uint Bow,Point3D Origin,WeaponFlight Flight,int Health,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bow-launch-fixtures.json");
    public static void Configure(){Freeze();Register("bow-launch-check",Check);Register("bow-launch-reload-check",Reload);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var health=Mobile.HitsRegenRateHandler;var mana=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=p=>(_creating && p is PlayerMobile || p.Name is "BowLaunchOwner" or "BowLaunchTarget")?TimeSpan.FromHours(1):health?.Invoke(p)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=p=>(_creating && p is PlayerMobile || p.Name=="BowLaunchOwner")?TimeSpan.FromHours(1):mana?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Collected native bow launch payload and source/save checks",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int Health(Mobile target)=>target.Hits*256-SkillHealth.Remainder256(target);
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bow-launch-source-probe.json")));
        var fields=data.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();int count=0;
        foreach(var row in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            int lo=row[0].GetInt32(),src=row[1].GetInt32(),skill=row[2].GetInt32();
            int Value(string field)=>row[5][Array.IndexOf(fields,field)].GetInt32();
            var elements=new MissileSourceStats(10*256,31*256,12*256,49*256,256,512,7*256,13*256,15,257,513,101,2,7,5);
            var actual=BowLaunch.FromSource(lo*256,(lo+5)*256,skill,skill+512,src,row[3].GetInt32()+57,0,11,13,row[4].GetInt32()!=0,elements);
            Require(actual.Min256==Value("nMinDamage") && actual.Max256==Value("nMaxDamage") && actual.Percent==Value("nDamagePercent")
                && actual.DemonPercent==Value("nDemonDamagePercent") && actual.UndeadPercent==Value("nUndeadDamagePercent") && actual.Critical==((Value("nFlags")&2)!=0),"Complete source creator physical/endpoints/percent/critical differential");
            var e=actual.Elements;
            Require(e==new MissileSourceStats(Value("nFireMinDamage"),Value("nFireMaxDamage"),Value("nLightMinDamage"),Value("nLightMaxDamage"),
                Value("nMagicMinDamage"),Value("nMagicMaxDamage"),Value("nColdMinDamage"),Value("nColdMaxDamage"),Value("nColdLength"),
                Value("nPoisonMinDamage"),Value("nPoisonMaxDamage"),Value("nPoisonLength"),Value("nPoisonCount"),Value("nLifeDrainMinDamage"),Value("nManaDrainMinDamage")),"Complete source creator typed/rate/count/duration/drain differential");count++;
        }
        Require(count==288,"Incomplete bow creator fixture");Log.Information("Bow launch:288 complete compiled source/native creator cases passed");
    }
    private static PlayerMobile Fresh(DiabloClass characterClass=DiabloClass.Amazon)
    {
        PlayerMobile p;bool previous=_creating;_creating=true;
        try{p=new PlayerMobile{Body=0x190,Name="BowLaunchOwner",Player=true};}finally{_creating=previous;}
        p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,characterClass);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}
        while(p.RawDex<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}
        bool found=false;p.MoveToWorld(new(2200,1000,0),Map.Trammel);
        for(int x=2500;x<2900 && !found;x+=4)for(int y=1000;y<1450 && !found;y+=4)
        {
            int z=p.Map.GetAverageZ(x,y);var point=new Point3D(x,y,z);
            if(!Enumerable.Range(0,19).All(i=>p.Map.GetAverageZ(x+i,y)==z && p.Map.CanSpawnMobile(x+i,y,z))
                || !p.Map.LineOfSight(new Point3D(x,y,z+12),new Point3D(x+18,y,z+12))){continue;}
            bool empty=true;foreach(var other in p.Map.GetMobilesInRange<Mobile>(point,22)){if(other!=p){empty=false;break;}}
            if(!empty){continue;}p.MoveToWorld(point,p.Map);
            if(Enumerable.Range(0,19).Any(i=>SpellHelper.IsTown(new Point3D(x+i,y,z),p))){continue;}found=true;
        }
        Require(found,"No independent bow launch corridor");
        if(characterClass==DiabloClass.Amazon){Require(ClassCreation.Profiles.TryTrain(p.Serial,"amazon.magicarrow",out _),"Magic Arrow hard point");}
        p.Hits=p.HitsMax;p.Mana=p.ManaMax;return p;
    }
    private static Item Named(PlayerMobile p,string id)
    {var item=NamedGear.Create(id,99,new Maximum(),true);p.AddToBackpack(item);Require(p.EquipItem(item) && ClassGear.Active(p,item),"Actual active named gear "+id);return item;}
    private static void Train(PlayerMobile p,string id)
    {
        if(ClassCreation.Profiles.GetRank(p.Serial,id)>0){return;}
        ClassCreation.Profiles.TryGet(p.Serial,out var profile);Require(TalentCatalog.TryGet(profile.Class,id,out var talent),"Talent "+id);
        foreach(var prerequisite in talent.Prerequisites){Train(p,prerequisite);}
        Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Training "+id);
    }
    private static BaseWeapon Plain(PlayerMobile p,CraftResource resource=CraftResource.RegularWood,string material=null)
    {
        var bow=(BaseWeapon)RuneSocketChecks.Host(p,typeof(Bow),2);((Bow)bow).Resource=resource;
        if(material!=null){var s=ClassGear.State(bow);ClassGear.Profiles.Store(bow,s with{CraftedEquipment=true,MaterialRolls=[new(material,5)]});}
        Require(p.EquipItem(bow) && ClassGear.Active(p,bow),"Actual socket/material bow");return bow;
    }
    private static void GearAndPoison()
    {
        var p=Fresh();try
        {
            var bow=Named(p,"unique/68");var skull=Named(p,"unique/75");var belt=Named(p,"unique/113");
            int mana=NecromancerEffects.ManaBalance256(p);var capture=BowLaunch.Capture(p,CombatAbility.MagicArrow,false);
            Require(capture.Elements is{PoisonMin:244,PoisonMax:244,PoisonFrames:350,PoisonCount:3} && capture.Elements.PoisonImpactFrames==116,"Actual Ichorsting/Wormskull/Snakecord raw poison lost count/aggregate frames");
            var ordinary=NamedGear.Poison(p,.37);Require(ordinary.Count==3 && ordinary.Length==14000 && Math.Abs(ordinary.Rate-(102/10240.0+102/10240.0+40/10240.0))<1e-14,"Existing ordinary poison view changed");
            Train(p,"amazon.multipleshot");Train(p,"amazon.guidedarrow");Require(ClassCreation.Profiles.TryTrain(p.Serial,"amazon.guidedarrow",out _),"Guided hard2");
            var multiple=BowLaunch.Capture(p,CombatAbility.MultipleShot,false);
            Require(multiple.Elements is{PoisonMin:183,PoisonMax:183,PoisonFrames:350,PoisonCount:2}
                && multiple.Percent==ClassStats.PhysicalDamageBonus(p,(BaseWeapon)bow)+24,"Multiple Shot source96 rate/count versus aggregate duration or current hard Guided synergy");
            p.AddToBackpack(skull);var tarnhelm=Named(p,"unique/72");
            Require(SkillMechanics.Rank(p,CombatAbility.GuidedArrow)==3 && BowLaunch.Capture(p,CombatAbility.MultipleShot,false).Percent==multiple.Percent,
                "Actual Tarnhelm soft rank changed hard-point Guided synergy");
            p.AddToBackpack(tarnhelm);Require(p.EquipItem(skull),"Restore poison skull");
            Require(capture.PhysicalToMagicPercent==5 && capture.FireMastery==0 && capture.PoisonMastery==0 && NecromancerEffects.ManaBalance256(p)==mana,"Collector paid mana or lost captured conversion/prepared mastery");
            p.AddToBackpack(bow);Require(BowLaunch.Capture(p,CombatAbility.MagicArrow,false)==null,"Backpack bow stayed active");
            var plain=Plain(p);p.AddToBackpack(plain);var emerald=GemActivationChecks.Gem(p,"Emerald",GemQuality.Chipped);GemActivationChecks.Install(p,emerald,plain);Require(p.EquipItem(plain),"Re-equip socketed bow");
            capture=BowLaunch.Capture(p,CombatAbility.MagicArrow,false);
            Require(capture.Elements is{PoisonMin:176,PoisonMax:176,PoisonFrames:350,PoisonCount:3},"Actual socket gem rate projection or named-source combination wrong");
            // Only Wormskull/Snakecord remain worn; their142 raw rate plus34
            // chipped-emerald FP256/frame, with200+75+75 aggregate frames.
            var state=ClassGear.State(plain);plain.HitPoints=0;Require(BowLaunch.Capture(p,CombatAbility.MagicArrow,false)==null,"Broken bow captured");plain.HitPoints=plain.MaxHitPoints;
            p.AddToBackpack(skull);p.AddToBackpack(belt);ClassGear.Profiles.Store(plain,state with{Identified=false});Require(BowLaunch.Capture(p,CombatAbility.MagicArrow,false)==null,"Unidentified bow captured");
        }
        finally{p.Delete();}
    }
    private static SkillValues Prepared(CombatAbility ability,int rank=1)=>new(SkillMechanics.For(ability),_=>rank,_=>0,99);
    private static void MaterialsAndBuffs()
    {
        var p=Fresh();try
        {
            foreach(var (resource,code,channel) in new[]{(CraftResource.OakWood,"damage-fire","fire"),(CraftResource.Frostwood,"damage-cold","cold"),(CraftResource.AshWood,"damage-lightning","ltng"),(CraftResource.YewWood,"damage-poison","pois"),(CraftResource.Bloodwood,"damage-physical","physical")})
            {
                var bow=Plain(p,resource,code);var d=BowLaunch.Capture(p,CombatAbility.MagicArrow,true);var raw=GearEffects.Damage(p,0);
                if(channel=="physical")
                {
                    var basic=ClassStats.PhysicalDamageRange(p);var high=GearEffects.Damage(p,1);
                    Require(d.PhysicalSupplement==new WeaponPhysicalSupplement(NamedElements.To256(raw.Physical-basic.Min),NamedElements.To256(high.Physical-basic.Max)),"Existing physical material endpoints changed");
                    var roll=d.Roll(true,false,false,_=>0);Require(NamedElements.To256(roll.Physical)+NamedElements.To256(roll.Magic)==(d.Min256+(int)((long)d.Min256*d.Percent/100)+d.PhysicalSupplement.Min256)*2,"Material physical lost critical or conversion integration");
                }
                else if(channel=="pois"){Require(d.Elements is{PoisonFrames:75,PoisonCount:1} && d.Elements.PoisonMin==NamedElements.To256(raw.Poison/raw.PoisonMs*40),"Material poison raw source projection changed");}
                else
                {int actual=channel=="fire"?d.Elements.FireMin:channel=="cold"?d.Elements.ColdMin:d.Elements.LightningMin;double expected=channel=="fire"?raw.Fire:channel=="cold"?raw.Cold:raw.Lightning;Require(actual==NamedElements.To256(expected),"Material typed endpoint changed");}
                bow.Delete();
            }
            var weapon=Plain(p);Require(CombatPotions.Apply(p,2),"Deep Wounds");var potion=BowLaunch.Capture(p,CombatAbility.MagicArrow,false);
            Require(potion.PhysicalSupplement==new WeaponPhysicalSupplement(2560,2560),"Deep Wounds flat source not captured");
            // Explicit component context supplies the staged skill rank to a
            // real Sorceress bow owner; this enables no foreign-class cast.
            var sorc=Fresh(DiabloClass.Sorceress);try
            {
                Require(ClassCreation.Profiles.TryTrain(sorc.Serial,"sorceress.firebolt",out _) && ClassCreation.Profiles.TryTrain(sorc.Serial,"sorceress.warmth",out _)
                    && ClassCreation.Profiles.TryTrain(sorc.Serial,"sorceress.fireball",out _) && ClassCreation.Profiles.TryTrain(sorc.Serial,"sorceress.enchant",out _)
                    && ClassCreation.Profiles.TryTrain(sorc.Serial,"sorceress.firemastery",out _),"Real Enchant/mastery training");
                var b=Plain(sorc);SorceressEffects.CastEnchant(sorc,sorc,SkillMechanics.Values(sorc,CombatAbility.Enchant));
                var captured=BowLaunch.Capture(sorc,CombatAbility.MagicArrow,false,preparedValues:Prepared(CombatAbility.MagicArrow));
                int expected=NamedElements.To256(NamedElements.Enhance(sorc,SorceressEffects.EnchantFire(sorc,0),"fire"));
                Require(captured.Elements.FireMin==expected && captured.FireMastery==0,"Ranged Enchant/wielder mastery capture changed or would enhance twice");
                SorceressEffects.Profiles.Store(sorc,new());Require(captured.Elements.FireMin==expected && BowLaunch.Capture(sorc,CombatAbility.MagicArrow,false,preparedValues:Prepared(CombatAbility.MagicArrow)).Elements.FireMin==0,"Enchant endpoint was read live after capture");
            }finally{sorc.Delete();}
            var paladin=Fresh(DiabloClass.Paladin);try
            {
                var b=Plain(paladin);Named(paladin,"unique/343");var now=TimedEffects.Now;
                PaladinElements.Pulse(paladin,CombatAbility.HolyFreeze,Prepared(CombatAbility.HolyFreeze),now);
                int expected=NamedElements.To256(PaladinElements.Weapon(paladin,"cold",0));
                var captured=BowLaunch.Capture(paladin,CombatAbility.MagicArrow,false,preparedValues:Prepared(CombatAbility.MagicArrow));
                Require(expected>0 && captured.Elements.ColdMin==expected && captured.ColdMastery==0,"Ranged aura was enhanced twice by actual Nightwing");
            }finally{paladin.Delete();}
        }
        finally{p.Delete();}
    }
    private static void Hold(WeaponFlight flight,DateTime until)
    {
        var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Select(f=>f.Id==flight.Id?f with{NextUtc=until}:f).ToArray()});
    }
    private static void Controlled(Action action)
    {
        var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;
        try{WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;action();}
        finally{WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;}
    }
    private static void Finish(long id){for(int i=0;i<250 && WeaponMissiles.State(id) is{} flight;i++){WeaponMissiles.Tick(flight.NextUtc);}Require(WeaponMissiles.State(id)==null,"Collected flight never finished");}
    private static void Persist()
    {
        var p=Fresh();var bow=Named(p,"unique/65");Require(CombatPotions.Apply(p,2),"Saved Deep Wounds");var damage=BowLaunch.Capture(p,CombatAbility.MagicArrow,true);
        Require(damage.Elements.FireMin==15*256 && damage.Elements.FireMax==50*256,"Actual Hellclap fire endpoints");
        Require(damage.PhysicalSupplement==new WeaponPhysicalSupplement(2560,2560) && CombatPotions.Apply(p,1) && CombatPotions.Bonus(p,"deep-wounds")==0,"Saved physical supplement or actual potion replacement");
        var target=new BowLaunchTarget{NoKillAwards=true,Frozen=true,CantWalk=true,Name="BowLaunchTarget"};target.AIObject.AITimer.Stop();target.SetHits(50000);target.SetResistance(ResistanceType.Physical,0);target.SetResistance(ResistanceType.Fire,0);
        Require(CreatureProfiles.Definition(target).Type==typeof(GraveyardSkeleton).FullName,"Fixture lost mapped skeleton species");
        target.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);Require(ClassCombat.IsHostile(p,target),"Nonhostile collected target");
        long id=WeaponMissiles.Launch(p,CombatAbility.MagicArrow,new(20,0,2080,1),damage,WeaponMissiles.NewCast(),frames:40,nextDelay:0);
        Require(id>0,"Collected Hellclap launch");var flight=WeaponMissiles.State(id);Hold(flight,TimedEffects.Now.AddMinutes(10));p.AddToBackpack(bow);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,bow.Serial.Value,p.Location,WeaponMissiles.State(id),Health(target),NecromancerEffects.ManaBalance256(p))));
    }
    private static void Check(){Differential();GearAndPoison();MaterialsAndBuffs();Persist();}
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BowLaunchTarget)World.FindMobile((Serial)saved.Target);
        Require(WeaponMissiles.State(saved.Flight.Id)==saved.Flight && saved.Flight.Damage.PhysicalSupplement==new WeaponPhysicalSupplement(2560,2560)
            && CombatPotions.Bonus(p,"deep-wounds")==0 && World.FindItem((Serial)saved.Bow)?.Parent==p.Backpack && Health(target)==saved.Health && NecromancerEffects.ManaBalance256(p)==saved.Mana,
            $"Collected state changed: flightEqual={WeaponMissiles.State(saved.Flight.Id)==saved.Flight}, actual={JsonSerializer.Serialize(WeaponMissiles.State(saved.Flight.Id))}, expected={JsonSerializer.Serialize(saved.Flight)}, deep={CombatPotions.Bonus(p,"deep-wounds")}, bowInPack={World.FindItem((Serial)saved.Bow)?.Parent==p.Backpack}, health={Health(target)}/{saved.Health}, mana={NecromancerEffects.ManaBalance256(p)}/{saved.Mana}");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Flight.Map]);int expected=NamedElements.To256(saved.Flight.Damage.Roll(target,_=>0).Total);
        Controlled(()=>{Hold(saved.Flight,TimedEffects.Now);Finish(saved.Flight.Id);});
        target.Map.GetSector(target.Location).Activate();target.OnSectorActivate();target.OnThink();
        Require(Health(target)==saved.Health-expected && NecromancerEffects.ManaBalance256(p)==saved.Mana,"Resumed collected payload used removed bow or repaid resources");
        var all=WeaponMissiles.Snapshot();var held=saved.Flight with{NextUtc=TimedEffects.Now.AddMinutes(10)};WeaponMissiles.Restore(all with{Flights=all.Flights.Append(held).ToArray()});
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flight=held,Health=Health(target)}));
    }
}
