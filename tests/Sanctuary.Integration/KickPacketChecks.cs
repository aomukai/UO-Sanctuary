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

public static class KickPacketChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(KickPacketChecks));
    private sealed class Zero:Random{public int Calls;public override int Next(int max){Calls++;return 0;}}
    private sealed class Maximum:Random{public override int Next(int max)=>Math.Max(0,max-1);}
    private sealed class NoCritical:Random{public override int Next(int max)=>throw new InvalidOperationException("Kick consumed ordinary critical RNG");}
    private sealed record Saved(uint Owner,uint Target,Point3D Origin,PreparedKickPacket Packet,MartialCharge[] Charges,int Mana256,int Life256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"kick-packet-fixtures.json");
    private static bool Creating;
    private static void Rates(){var hp=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=p=>Creating || p.Name=="KickPacketFixture"?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;var mp=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>Creating || p.Name=="KickPacketFixture"?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure(){Rates();Register("kick-packet-check",Check);Register("kick-packet-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared kick typed/charge packet and Tail fire component; learned handlers remain disabled",_=>Core.LoopContext.Post(()=>{try{Controlled(action);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Controlled(Action action){var f=FistsFireRelease.DamageRoll;var t=MartialLightning.DamageRoll;var i=MartialCold.DamageRoll;try{FistsFireRelease.DamageRoll=MartialLightning.DamageRoll=MartialCold.DamageRoll=_=>0;action();}finally{FistsFireRelease.DamageRoll=f;MartialLightning.DamageRoll=t;MartialCold.DamageRoll=i;}}
    private static SkillValues Tail(int rank)=>new(SkillMechanics.For(CombatAbility.DragonTail),_=>rank,_=>0,99);
    private static PlayerMobile Fresh(){Creating=true;try{var p=BladeBodyChecks.Fresh(6720,40,3300);p.Name="KickPacketFixture";while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Str");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dex");}return p;}finally{Creating=false;}}
    private static BaseCreature Target(PlayerMobile p,int dx=1){var t=new CobraPassiveTarget{Name="KickPacketTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}return t;}
    private static Item Named(PlayerMobile p,string source){var i=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Zero(),true);p.AddToBackpack(i);Require(p.EquipItem(i),"Equip "+source);ClassGear.Refresh(p);return i;}
    private static void Equip(PlayerMobile p,BaseWeapon weapon){ClassGear.Profiles.Convert(weapon,99,GearRarity.Normal,new Random(173));p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Weapon");ClassGear.Refresh(p);}
    private static PreparedKickPacket Prepare(PlayerMobile p,Mobile t)=>DragonKickPackets.Prepare(p,t,0,0,_=>0,_=>0,new NoCritical());
    private static void Hold(PlayerMobile p,CombatAbility ability,int count=1,int rank=1){var next=new MartialCharge(p.Serial.Value,ability,count,rank,count*(int)new SkillValues(SkillMechanics.For(ability),_=>rank,_=>0,99).Eval("aurastatcalc2"),TimedEffects.Now.AddHours(1));AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value || x.Ability!=ability).Append(next).ToArray());}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"dragon-tail-source-probe.json")));int count=0;
        foreach(var r in doc.RootElement.EnumerateArray()){int V(int i)=>r[i].GetInt32();if(V(8)!=0 && V(7)==0){Require(DragonKickPackets.TailFire256(V(14),Tail(V(0)),V(3))==V(18),"Complete source Tail percentage/copy");}count++;}Require(count==1920,"Tail complete source matrix");
        for(int rank=1;rank<=255;rank++){var v=Tail(rank);Require(v.Mana256==2560 && v.Formula("toht")==20+15*(rank-1) && v.Eval("calc1")==50+20*(rank-1) && v.Eval("aurarangecalc")==6,"Current Tail numeric golden");}
        foreach(var (physical,rank,mastery) in new[]{(1048576,255,0),(int.MaxValue,20,0),(1048576,1,1000000)}){bool refused=false;try{DragonKickPackets.TailFire256(physical,Tail(rank),mastery);}catch(NotSupportedException){refused=true;}Require(refused,"Unverified signed-overflow fast path silently adopted");}
    }
    private static void TailWorld()
    {
        var p=Fresh();Equip(p,new Dagger());Named(p,"Gorerider");var a=Target(p);var b=Target(p,3);a.SetResistance(ResistanceType.Physical,50);b.SetResistance(ResistanceType.Fire,50);
        var packet=Prepare(p,a);int copied=NamedElements.To256(packet.Impact.Parts.Physical),raw=DragonKickPackets.TailFire256(copied,Tail(1),0);int expected=NamedElements.Resolve256(p,b,raw,"fire",enhance:false).Damage256;
        var wall=new Static(0x80);wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);Require(!p.InLOS(b),"Tail no-LOS control wall");int before=SkillHealth.Balance256(b);DragonKickPackets.TailBurst(p,p.Map,a.Location,copied,Tail(1));Require(before-SkillHealth.Balance256(b)==expected && expected>0,"Copied resisted physical/current percentage/no LOS/per-recipient fire resistance");wall.Delete();
        a.SetResistance(ResistanceType.Physical,100);packet=Prepare(p,a);Require(packet.Impact.Parts.Physical==0,"Physical immune seed");before=SkillHealth.Balance256(b);DragonKickPackets.TailBurst(p,p.Map,a.Location,0,Tail(1));Require(SkillHealth.Balance256(b)==before,"Immune primary produced fire");
        a.SetResistance(ResistanceType.Physical,0);a.Hits=1;SkillHealth.Clear(a);packet=Prepare(p,a);copied=NamedElements.To256(packet.Impact.Parts.Physical);var center=a.Location;GearEffects.Strike(p,a,0,new Zero(),preparedImpact:packet.Impact,sourceDrain:packet.Drain,softHit:packet.SoftHit,sourceColdFrames:packet.ColdFrames,sourceFreezeFrames:packet.FreezeFrames);CombatDamage.Finish(a,p);Require(!a.Alive,"Lethal prepared kick");before=SkillHealth.Balance256(b);DragonKickPackets.TailBurst(p,p.Map,center,copied,Tail(1));Require(SkillHealth.Balance256(b)<before && copied>256,"Lethal parent lost copied full physical seed");a.Corpse?.Delete();
        p.Kill();before=SkillHealth.Balance256(b);Require(DragonKickPackets.TailBurst(p,p.Map,center,copied,Tail(1))==0 && SkillHealth.Balance256(b)==before,"Dead owner emitted Tail fire");p.Resurrect();p.Corpse?.Delete();a.Delete();b.Delete();p.Delete();
    }
    private static void Check()
    {
        Differential();TailWorld();var p=Fresh();Equip(p,new Dagger());var boots=Named(p,"Gorerider");var t=Target(p);Require(ClassGear.Bonus(p,"deadly")>0,"Actual deadly boots");
        var packet=Prepare(p,t);int raw=DragonKickDamage.Range256(packet.Input).Min;Require(NamedElements.To256(packet.Impact.Damage.Physical)==raw && NamedElements.To256(packet.Impact.Parts.Physical)==raw,"Weapon physical/critical leaked into kick");
        var rng=new Zero();var ordinary=GearEffects.PrepareWeaponImpact(p,t,0,rng,new(raw/256.0,0,0,0,0));Require(rng.Calls>0 && NamedElements.To256(ordinary.Parts.Physical)==2*raw,"Actual Deadly Strike positive control");
        var raven=Named(p,"Raven Frost");Require(GearEffects.Damage(p,0,t).Cold>0,"Actual Raven cold");packet=Prepare(p,t);Require(packet.Impact.Damage.Cold>0,"Kick omitted selected gear cold");
        Hold(p,CombatAbility.TigerStrike,2);Hold(p,CombatAbility.CobraStrike,3);Hold(p,CombatAbility.FistsOfFire,3,34);Hold(p,CombatAbility.ClawsOfThunder,2);Hold(p,CombatAbility.BladesOfIce,3);Hold(p,CombatAbility.PhoenixStrike,3);
        var charges=AssassinCharges.Finisher(p);packet=Prepare(p,t);Require(packet.Input.SkillDamagePercent==200 && packet.Impact.Parts.Physical==0 && packet.Impact.Parts.Fire>0 && packet.Impact.Parts.Cold>0 && packet.Impact.Parts.Lightning>0 && packet.ColdFrames>0 && packet.FreezeFrames==packet.ColdFrames && packet.SoftHit && packet.Drain.LifePercent>=80 && packet.Drain.ManaPercent>=80,"Six-family typed/conversion/Tiger/Cobra/cold/freeze packet");Require(AssassinCharges.Finisher(p).SequenceEqual(charges),"Preparation consumed/refreshed charges");
        AssassinCharges.Clear(p);p.AddToBackpack(CombatTiming.Equipped(p));Equip(p,new Bow());Require(NamedCombat.Ranged(p),"Bow control");packet=Prepare(p,t);Require(!packet.SoftHit && packet.Impact.Damage.Cold>0 && NamedCombat.Ranged(p),"Kick melee scope leaked or omitted typed stats");p.AddToBackpack(CombatTiming.Equipped(p));Equip(p,new Tekagi());var primary=CombatTiming.Equipped(p);var offhand=(BaseWeapon)Named(p,"Cutthroat1");Require(WeaponHands.Pair(p)!=null,"Actual dual claws");var one=Prepare(p,t);using(WeaponHands.Use(p,offhand)){var second=Prepare(p,t);Require(second==one && CombatTiming.Equipped(p)==offhand,"Kick failed primary source context or did not restore caller hand");}Require(CombatTiming.Equipped(p)==primary,"Hand scope leaked");
        Hold(p,CombatAbility.TigerStrike,2);Hold(p,CombatAbility.PhoenixStrike,3);packet=Prepare(p,t);int held=NamedElements.To256(packet.Impact.Parts.Physical);p.AddToBackpack(boots);ClassGear.Refresh(p);Require(packet.Impact.Parts.Physical*256==held && DragonKickDamage.Capture(p,packet.Input.SkillDamagePercent).Boot==null,"Prepared kick changed with later boot removal");Require(p.EquipItem(boots),"Restore boot");ClassGear.Refresh(p);Require(Prepare(p,t)==packet,"Restored source gear capture");
        Named(p,"Wormskull");var venomValues=new SkillValues(SkillMechanics.For(CombatAbility.Venom),_=>1,_=>0,99);var venom=Venoms.Capture(p.Serial.Value,venomValues,0,TimedEffects.Now);Venoms.Restore(Venoms.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Append(venom).ToArray());packet=Prepare(p,t);Require(packet.Impact.Damage.SourcePoison==new WeaponPoisonRoll(1638,10) && packet.Impact.Damage.PoisonMs==400,"Kick lost actual item poison/Venom override");GearEffects.Strike(p,t,0,new Maximum(),wearWeapon:false,preparedImpact:packet.Impact,sourceDrain:packet.Drain);var poisoned=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);Require(poisoned.PoisonClock.Rate256==1638 && poisoned.PoisonMs==400,"Prepared kick poison delivery");int hp=SkillHealth.Balance256(t);TimedEffects.Settle(t,poisoned.UpdatedUtc.AddMilliseconds(40));Require(hp-SkillHealth.Balance256(t)==1638,$"One source poison frame: actual={hp-SkillHealth.Balance256(t)},state={JsonSerializer.Serialize(poisoned)}");TimedEffects.ClearGear(t);Venoms.Clear(p);packet=Prepare(p,t);
        NecromancerEffects.SetMana256(p,12345);SkillHealth.SetBalance256(p,5432);Require(!AssassinCombat.Implemented(CombatAbility.DragonTail),"Prepared component enabled unsupported learned action");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,p.Location,packet,AssassinCharges.Finisher(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p))));Log.Information("Prepared kick packet:1920 complete source Tail contexts/255 goldens,actual Deadly/Raven/boots/Bartuc,primary/secondary and bow melee scopes,six charge families,conversion/cold/drain,physical immunity/no-LOS/lethal/dead-owner fire controls pass;learned finisher stays disabled");
    }
    private static void Reload(){Differential();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=World.FindMobile((Serial)s.Target);p.MoveToWorld(s.Origin,Map.Trammel);Require(AssassinCharges.Finisher(p).SequenceEqual(s.Charges) && Prepare(p,t)==s.Packet && NecromancerEffects.ManaBalance256(p)==s.Mana256 && SkillHealth.Balance256(p)==s.Life256,"Saved kick gear/captured packet/charges/fractional resources changed");}
}
