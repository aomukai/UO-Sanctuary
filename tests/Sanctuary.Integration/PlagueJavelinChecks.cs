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

namespace Sanctuary.Integration;

public static class PlagueJavelinChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PlagueJavelinChecks));
    private sealed class Minimum:Random{public override int Next(int maximum)=>0;}

    private static string Manifest=>Path.Join(Core.BaseDirectory,"plague-javelin.json");
    public static void Configure(){Register("plague-javelin-check",Check);Register("plague-javelin-reload-check",Reload);Register("plague-javelin-input-check",Input);Register("plague-javelin-snapshot-check",Snapshot);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Plague Javelin do-mana, burst/moving poison clouds and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()=>MagicArrowChecks.Fresh("PlagueJavelinOwner");
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var t),"Talent");foreach(var id in t.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==id).Ability);}while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train");}}
    private static BowLaunchTarget Target(PlayerMobile p)
    {var t=MagicArrowChecks.Target(p);t.Name="BowLaunchTarget";t.SetResistance(ResistanceType.Poison,0);return t;}
    private static BaseWeapon Weapon(PlayerMobile p)
    {
        if(CombatTiming.Equipped(p) is{} old){p.AddToBackpack(old);}var w=ItemOrigins.Create(ItemOrigin.Drop,()=>new SoulGlaive());ClassGear.Profiles.FromMonster(w,30,GearRarity.Normal,"glv",GearTier.Normal,new Random(173));p.AddToBackpack(w);Require(p.EquipItem(w),"Ordinary javelin equip");ClassGear.Refresh(p);return w;
    }
    private static void Delay(PlayerMobile p,DateTime? until=null)
    {var all=FireGround.Snapshot();FireGround.Restore(all with{Cooldowns=all.Cooldowns.Where(x=>x.Owner!=p.Serial.Value||x.Ability!=CombatAbility.PlagueJavelin).Concat(until is{} u?new[]{new FireCooldown(p.Serial.Value,CombatAbility.PlagueJavelin,u)}:[]).ToArray()});}
    private static void Ready(PlayerMobile p){MagicArrowChecks.Ready(p);Delay(p);}
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static PlagueCloud[] Clouds(PlayerMobile p)=>PlagueJavelins.Snapshot().Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static void Hold(PlayerMobile p,WeaponFlight[] flights,PlagueCloud[] clouds)
    {var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value).Concat(flights).ToArray(),PlagueClouds=all.PlagueClouds.Where(x=>x.Owner!=p.Serial.Value).Concat(clouds).ToArray()});}
    private static TimedEffectState State(Mobile p)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==p.Serial.Value);
    private static void HoldPoison(Mobile target,DateTime at)
    {TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==target.Serial.Value?x with{UpdatedUtc=at}:x).ToArray());}
    private static void Controlled(Action action)
    {
        var random=AmazonBow.LaunchRandom;var damage=WeaponMissiles.DamageRoll;var hit=WeaponMissiles.HitRoll;var block=SourceBlocking.BlockRoll;
        try{AmazonBow.LaunchRandom=new Minimum();WeaponMissiles.DamageRoll=_=>0;WeaponMissiles.HitRoll=()=>0;SourceBlocking.BlockRoll=_=>99;action();}
        finally{AmazonBow.LaunchRandom=random;WeaponMissiles.DamageRoll=damage;WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=block;}
    }
    private static WeaponFlight Cast(PlayerMobile p,Mobile target=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(CombatTiming.Equipped(p));
        Require(ClassCombat.Perform(p,CombatAbility.PlagueJavelin,target,target?.Location??MagicArrowChecks.Aim(p,18),Core.TickCount),"Throw start");var s=ClassCombat.For(p);
        Require(NecromancerEffects.ManaBalance256(p)==mana && NamedAmmunition.Remaining(CombatTiming.Equipped(p))==quantity,"Start mana/quantity phase");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1) && ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt+1),"Early/repeated emission");
        var f=Own(p).Single();Require(f.Missile=="plaguejavelin" && f.Frames==15 && f.SkillRank==SkillMechanics.Rank(p,CombatAbility.PlagueJavelin) && !f.PathChanged && NamedAmmunition.Remaining(CombatTiming.Equipped(p))==quantity-1 && NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,CombatAbility.PlagueJavelin).Mana256 && !FireGround.Ready(p,CombatAbility.PlagueJavelin),"Saved source parent/release quantity/localdelay");return f;
    }
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"plague-javelin-source-probe.json")));int count=0;
        var fields=data.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();
        foreach(var r in data.RootElement.GetProperty("damage").EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();int S(string field)=>r[8][Array.IndexOf(fields,field)].GetInt32();bool cloud=V(0)!=0;
            var values=new SkillValues(SkillMechanics.For(CombatAbility.PlagueJavelin),_=>V(1),_=>V(2),99);
            var stats=PoisonJavelinPackets.Prepare(new(FireMin:2560,FireMax:7936,PoisonMin:V(4),PoisonMax:V(4)+19,PoisonFrames:V(5),PoisonCount:V(6),LifeDrain:7),values,V(3),cloud);
            var a=cloud?new WeaponMissileDamage(0,0,0,0,0,false,stats):BowLaunch.FromSource(1280,2560,0,0,128,94,0,11,13,V(7)!=0,stats);
            Require(a.Min256==S("nMinDamage") && a.Max256==S("nMaxDamage") && a.Percent==S("nDamagePercent") && a.DemonPercent==S("nDemonDamagePercent") && a.UndeadPercent==S("nUndeadDamagePercent") && a.Critical==((S("nFlags")&2)!=0) && stats.PoisonMin==S("nPoisonMinDamage") && stats.PoisonMax==S("nPoisonMaxDamage") && stats.PoisonFrames==S("nPoisonLength") && stats.PoisonCount==S("nPoisonCount") && stats.FireMin==S("nFireMinDamage") && stats.FireMax==S("nFireMaxDamage") && stats.LifeDrain==S("nLifeDrainMinDamage"),"Complete Plague parent/cloud creator row "+count);count++;
        }
        Require(count==3456,"Creator count");count=0;
        foreach(var r in data.RootElement.GetProperty("emissions").EnumerateArray())
        {
            var pattern=PlagueJavelins.Pattern();Require(pattern.Length==r[5].GetArrayLength(),"23 ordered cloud emissions");int i=0;
            foreach(var e in r[5].EnumerateArray()){Require(pattern[i].X==e[3].GetInt32() && pattern[i].Y==e[4].GetInt32() && e[9].GetInt32()==60+9*r[3].GetInt32() && e[10].GetInt32()==(i<8?192:384)/(r[1].GetInt32()!=0?2:1),"Complete emitter/factory order/override/loops");i++;}count++;
        }
        Require(count==40,"Emitter count");Log.Information("Plague Javelin:3456 native/source creator comparisons and40 source emitter/factory rows checked;192 full router cases are bounded source evidence");
    }
    private static void Cases()
    {
        var p=Fresh();var w=Weapon(p);try
        {
            Require(!ClassCombat.Available(p,CombatAbility.PlagueJavelin),"Unlearned Plague");Train(p,CombatAbility.PlagueJavelin);var values=SkillMechanics.Values(p,CombatAbility.PlagueJavelin);
            Require(values.Mana256==1792 && values.Damage256(false)==109 && values.Damage256(true)==164 && values.DurationFrames==75,"Pinned rank1 plus prerequisite hard Poison1");
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(w);Require(ClassCombat.Perform(p,CombatAbility.PlagueJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount),"Cancelled start");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana && NamedAmmunition.Remaining(w)==quantity,"Plague cancellation charged mana or quantity");
            Ready(p);NecromancerEffects.SetMana256(p,1791);Require(!ClassCombat.Perform(p,CombatAbility.PlagueJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount),"Low mana start");MagicArrowChecks.Mana(p);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.PlagueJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount),"Mana-loss start");NecromancerEffects.SetMana256(p,0);Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && Own(p).Length==0 && NamedAmmunition.Remaining(w)==quantity,"usemanaondo affordability not repeated");MagicArrowChecks.Mana(p);
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.PlagueJavelin,null,p.Location,Core.TickCount) && ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Self point fallback");var f=Own(p).Single();Require(f.Vx>0 && f.Vx==f.Vy && NecromancerEffects.ManaBalance256(p)==mana-1792,"Fallback release debit");AmazonJavelins.Cancel(p);
            foreach(int mode in new[]{0,1,2})
            {
                MagicArrowChecks.Mana(p);var target=Target(p);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);var hit=WeaponMissiles.HitRoll;var block=SourceBlocking.BlockRoll;
                try
                {
                    if(mode==1){WeaponMissiles.HitRoll=()=>1;}
                    if(mode==2){SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=target.Serial.Value).Append(new SourceShield(target.Serial.Value,CreatureProfiles.Definition(target).Reference,1)).ToArray());SourceBlocking.BlockRoll=_=>0;}
                    int health=SkillHealth.Balance256(target);f=Cast(p,target);WeaponMissiles.Tick(f.NextUtc);Require(Clouds(p).Length==0,"Plague copied Poison Javelin trail");
                    while(Own(p).Length>0){WeaponMissiles.Tick(Own(p).Single().NextUtc);}var clouds=Clouds(p);Require(clouds.Length==23 && clouds.All(x=>x.X==target.X && x.Y==target.Y && x.Frames==87 && x.Frame==0 && x.Damage.Min256==0 && x.Damage.Elements.PoisonMin==109 && x.Damage.Elements.PoisonFrames==75),"Actual hit/miss/block victim-origin pure cloud burst");Require(clouds[2].Vx==.0234375 && clouds[2].Vy==0 && clouds[11].Vx==.046875 && clouds[11].Vy==0,"Completed FP8 override velocities");
                    Require((SkillHealth.Balance256(target)<health)==(mode==0) && (State(target)?.PoisonClock!=null)==(mode==0),"Parent AR/block/direct poison");
                    if(mode==0)
                    {
                        var c=clouds[2];Hold(p,[],[c]);TimedEffects.Clear(target);PlagueJavelins.Tick(c.NextUtc);Require(State(target)?.PoisonClock?.Rate256==109 && Clouds(p).Single().LastCollision==target.Serial.Value,"Cloud first contact");var state=State(target);PlagueJavelins.Tick(c.NextUtc.AddMilliseconds(40));Require(State(target).PoisonMs==state.PoisonMs,"Cloud repeated LastCollide refresh");
                        target.MoveToWorld(new(p.X+12,p.Y,p.Z),p.Map);var second=Target(p);second.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);PlagueJavelins.Tick(c.NextUtc.AddMilliseconds(80));Require(State(second)?.PoisonClock?.Rate256==109,"Cloud changed first collider");second.Delete();
                        var only=Clouds(p).Single();Require(only.X>c.X && only.Y==c.Y,"Cloud axis motion");PlagueJavelins.Tick(c.NextUtc.AddMilliseconds(85*40));Require(Clouds(p).Single().Frame==86,"87-frame cloud lost contact pass86");PlagueJavelins.Tick(c.NextUtc.AddMilliseconds(86*40));Require(Clouds(p).Length==0,"Cloud survived frame87");
                    }
                }finally{WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=block;AmazonJavelins.Cancel(p);target.Delete();}
            }
            // No unit contact still bursts at the missile's final source position.
            MagicArrowChecks.Mana(p);f=Cast(p);while(Own(p).Length>0){WeaponMissiles.Tick(Own(p).Single().NextUtc);}Require(Clouds(p).Length==23 && Clouds(p).All(x=>x.X>p.X && x.Y==p.Y),"Null lifetime collision lost AlwaysExplode");AmazonJavelins.Cancel(p);
            Train(p,CombatAbility.Pierce);MagicArrowChecks.Mana(p);var a=Target(p);a.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);var b=Target(p);b.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);f=Cast(p,a);Require(f.Pierces==4,"Pierce budget");quantity=NamedAmmunition.Remaining(w);for(int i=0;i<10;i++){WeaponMissiles.Tick(Own(p).Single().NextUtc);}Require(Own(p).Single().Pierces==2 && Clouds(p).Length==46 && NamedAmmunition.Remaining(w)==quantity,"Pierce two distinct bursts/once quantity");AmazonJavelins.Cancel(p);a.Delete();b.Delete();
            while(NamedAmmunition.Remaining(w)>1){NamedAmmunition.Consume(w,TimedEffects.Now);}Ready(p);MagicArrowChecks.Mana(p);Require(ClassCombat.Perform(p,CombatAbility.PlagueJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount),"Last quantity start");mana=NecromancerEffects.ManaBalance256(p);NamedAmmunition.Consume(w,TimedEffects.Now);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && Own(p).Length==1 && NecromancerEffects.ManaBalance256(p)==mana-1792 && NamedAmmunition.Remaining(w)==0,"Release regated quantity or lost do payment");AmazonJavelins.Cancel(p);Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.PlagueJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==mana,"Empty throw started/paid");
        }finally{AmazonJavelins.Cancel(p);p.Delete();}
    }
    private static void GearCases()
    {
        var p=Fresh();Train(p,CombatAbility.PlagueJavelin);Weapon(p);try
        {
            var helm=NamedGear.Create("unique/72",99,new Random(31),true);var gloves=NamedGear.Create("unique/232",99,new Random(32),true);foreach(var i in new[]{helm,gloves}){p.AddToBackpack(i);Require(p.EquipItem(i) && ClassGear.Active(p,i),"Real Tarnhelm/Venom Grip");}ClassGear.Refresh(p);
            var values=SkillMechanics.Values(p,CombatAbility.PlagueJavelin);var captured=BowLaunch.Capture(p,CombatAbility.PlagueJavelin,false);Require(values.Rank==2 && values.Damage256(false)==182 && captured.Elements.PoisonCount==1 && captured.Elements.PoisonMin>182,"Actual soft rank/hard-only synergy/prepared item poison");var f=Cast(p);int mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(CombatTiming.Equipped(p));PlagueJavelins.Burst(p,f,null,TimedEffects.Now);var old=Clouds(p)[0];Require(old.Rank==2 && old.Damage.Elements is{PoisonMin:182,PoisonFrames:80,PoisonCount:0},"Source-zero child inherited weapon poison or soft synergy");p.AddToBackpack(helm);ClassGear.Refresh(p);PlagueJavelins.Burst(p,f,null,TimedEffects.Now);Require(Clouds(p).All(x=>x.Damage.Elements.PoisonMin==182),"Child lost retained parent rank after helm removal");Train(p,CombatAbility.PoisonJavelin,2);PlagueJavelins.Burst(p,f,null,TimedEffects.Now);Require(Clouds(p).Count(x=>x.Damage.Elements.PoisonMin==204)==23 && old.Damage.Elements.PoisonMin==182 && NecromancerEffects.ManaBalance256(p)==mana && NamedAmmunition.Remaining(CombatTiming.Equipped(p))==quantity,"Live hard synergy/new child snapshot or repeated burst resource cost");
        }finally{AmazonJavelins.Cancel(p);p.Delete();}
    }
    private static void Lifecycle()
    {
        foreach(bool death in new[]{false,true})
        {
            var p=Fresh();Train(p,CombatAbility.PlagueJavelin);Weapon(p);var f=Cast(p);PlagueJavelins.Burst(p,f,null,TimedEffects.Now);Require(Own(p).Length==1 && Clouds(p).Length==23,"Lifecycle fixture");if(death){p.Kill();}else{ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}Require(Own(p).Length==0 && Clouds(p).Length==0,"Death/respec kept Plague parent/clouds");p.Corpse?.Delete();p.Delete();
        }
    }
    private sealed record SavedPlague(uint Owner,uint Target,uint Weapon,Point3D Origin,WeaponFlight Flight,PlagueCloud[] Clouds,int Health,int Mana,int Quantity,TimedEffectState Poison,DateTime Cooldown);
    private static void Persist()
    {
        var p=Fresh();Train(p,CombatAbility.PlagueJavelin);var w=Weapon(p);var target=Target(p);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);Require(SkillBindings.Instance.Assign(p,true,CombatAbility.PlagueJavelin) && SkillBindings.Instance.Assign(p,false,CombatAbility.PlagueJavelin) && SkillBindings.Instance.AssignQuick(p,3,CombatAbility.PlagueJavelin),"Saved bindings");
        var f=Cast(p,target);while(Own(p).Length>0){WeaponMissiles.Tick(Own(p).Single().NextUtc);}var first=Clouds(p)[2];Hold(p,[],[first]);PlagueJavelins.Tick(first.NextUtc.AddMilliseconds(40));HoldPoison(target,TimedEffects.Now.AddMinutes(10));var clouds=Clouds(p).Select(x=>x with{NextUtc=TimedEffects.Now.AddMinutes(10)}).ToArray();f=Cast(p,target);WeaponMissiles.Tick(f.NextUtc);f=Own(p).Single() with{NextUtc=TimedEffects.Now.AddMinutes(10)};Hold(p,[f],clouds);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.PlagueJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount),"Unpaid/unemitted windup save");var until=TimedEffects.Now.AddMinutes(10);Delay(p,until);target.Blessed=true;File.WriteAllText(Manifest,JsonSerializer.Serialize(new SavedPlague(p.Serial.Value,target.Serial.Value,w.Serial.Value,p.Location,f,clouds,SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),NamedAmmunition.Remaining(w),State(target),until)));
    }
    private static void Check()=>Controlled(()=>{Differential();Cases();GearCases();Lifecycle();Persist();Parked();});
    private static void Parked()
    {
        Snapshot();var saved=JsonSerializer.Deserialize<SavedPlague>(File.ReadAllText(Manifest));
        var target=(BowLaunchTarget)World.FindMobile((Serial)saved.Target);int before=SkillHealth.Balance256(target);
        var attacker=MagicArrowChecks.Fresh("MultipleShotOwner");BowLaunchTarget control=null;
        try
        {
            Train(attacker,CombatAbility.MultipleShot);MagicArrowChecks.Bow(attacker,typeof(Bow));attacker.AddToBackpack(new Arrow(30));
            attacker.MoveToWorld(new(saved.Origin.X+1,saved.Origin.Y,saved.Origin.Z),target.Map);control=MagicArrowChecks.Target(attacker);control.MoveToWorld(target.Location,target.Map);
            control.SetResistance(ResistanceType.Physical,0);int controlLife=SkillHealth.Balance256(control);
            MagicArrowChecks.Mana(attacker);MagicArrowChecks.Ready(attacker);
            bool began=ClassCombat.Perform(attacker,CombatAbility.MultipleShot,null,new Point3D(saved.Origin.X+10,saved.Origin.Y,saved.Origin.Z),Core.TickCount);
            Require(began,$"Cross-fixture Multiple Shot start: point={attacker.Location}/{target.Location}, available={AmazonCombat.Available(attacker,CombatAbility.MultipleShot)}, overlap={AlliedOverlap.Allowed(attacker)}, ammo={BowAmmunition.CanRelease(attacker,CombatTiming.Equipped(attacker))}, mana={NecromancerEffects.ManaBalance256(attacker)}, los={attacker.InLOS(target.Location)}");
            ClassCombat.ReleaseCast(attacker);
            Require(ClassCombat.ResolveAction(attacker,ClassCombat.For(attacker).EffectAt),"Cross-fixture Multiple Shot release");
            for(int i=0;i<100&&SkillHealth.Balance256(control)==controlLife;i++)
            {
                var flights=WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==attacker.Serial.Value).ToArray();
                if(flights.Length==0){break;}WeaponMissiles.Tick(flights.Min(x=>x.NextUtc));
            }
            Require(SkillHealth.Balance256(control)<controlLife&&SkillHealth.Balance256(target)==before,
                "Parked target took collateral damage or protection test did not deliver real travelling arrow damage");
            WeaponMissiles.Cancel(attacker);Snapshot();Log.Information("Plague parked target:actual Multiple Shot arrows damage an overlapping vulnerable control while saved inspection health/resources/poison remain unchanged");
        }
        finally{WeaponMissiles.Cancel(attacker);control?.Delete();attacker.Delete();}
    }
    private static void Snapshot()
    {
        if(!File.Exists(Manifest)){return;}
        var s=JsonSerializer.Deserialize<SavedPlague>(File.ReadAllText(Manifest));
        var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var target=(BowLaunchTarget)World.FindMobile((Serial)s.Target);var w=(BaseWeapon)World.FindItem((Serial)s.Weapon);
        Require(p!=null&&target!=null&&w!=null&&target.Blessed,"Saved Plague fixture entity/protection missing before snapshot");
        Require(SkillHealth.Balance256(target)==s.Health&&NecromancerEffects.ManaBalance256(p)==s.Mana
            &&NamedAmmunition.Remaining(w)==s.Quantity&&JsonSerializer.Serialize(State(target))==JsonSerializer.Serialize(s.Poison),
            $"Snapshot resources/poison: health={SkillHealth.Balance256(target)}/{s.Health}, mana={NecromancerEffects.ManaBalance256(p)}/{s.Mana}, quantity={NamedAmmunition.Remaining(w)}/{s.Quantity}, actual={JsonSerializer.Serialize(State(target))}, expected={JsonSerializer.Serialize(s.Poison)}");
    }
    private static void Reload()=>Controlled(()=>
    {
        var s=JsonSerializer.Deserialize<SavedPlague>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var target=(BowLaunchTarget)World.FindMobile((Serial)s.Target);var w=(BaseWeapon)World.FindItem((Serial)s.Weapon);Require(Own(p).Single()==s.Flight && Clouds(p).SequenceEqual(s.Clouds),"Saved parent/moving cloud structural steps/packet/LastCollide");Require(SkillHealth.Balance256(target)==s.Health && NecromancerEffects.ManaBalance256(p)==s.Mana && NamedAmmunition.Remaining(w)==s.Quantity && JsonSerializer.Serialize(State(target))==JsonSerializer.Serialize(s.Poison),$"Saved resources/poison: health={SkillHealth.Balance256(target)}/{s.Health}, mana={NecromancerEffects.ManaBalance256(p)}/{s.Mana}, quantity={NamedAmmunition.Remaining(w)}/{s.Quantity}, actual={JsonSerializer.Serialize(State(target))}, expected={JsonSerializer.Serialize(s.Poison)}");Require(ClassCombat.For(p).PendingAbility==CombatAbility.None && SkillBindings.Instance.Get(p)==(CombatAbility.PlagueJavelin,CombatAbility.PlagueJavelin) && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,3)==CombatAbility.PlagueJavelin && !FireGround.Ready(p,CombatAbility.PlagueJavelin),"Saved bindings/delay and discarded unpaid windup");
        Require(target.Blessed,"Saved Plague inspection protection lost");target.Blessed=false;
        p.MoveToWorld(s.Origin,Map.Maps[s.Flight.Map]);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);target.SetResistance(ResistanceType.Poison,0);TimedEffects.Clear(target);p.AddToBackpack(w);int hp=SkillHealth.Balance256(target),physical=NamedElements.To256(s.Flight.Damage.Roll(target,_=>0).Physical);var now=TimedEffects.Now;Hold(p,[s.Flight with{NextUtc=now}],s.Clouds);while(Own(p).Length>0){WeaponMissiles.Tick(Own(p).Single().NextUtc);}Require(SkillHealth.Balance256(target)==hp-physical && Clouds(p).Length==24 && State(target)?.PoisonClock?.Rate256==109,"Resumed original parent plus fresh burst");
        var cloud=s.Clouds[0] with{NextUtc=now};Hold(p,[],[cloud]);TimedEffects.Clear(target);PlagueJavelins.Tick(now);Require((State(target)==null)==(cloud.LastCollision==target.Serial.Value) && Clouds(p).Single().Frame==cloud.Frame+1,"Saved LastCollide or motion lost");var other=Target(p);other.MoveToWorld(target.Location,p.Map);target.MoveToWorld(new(p.X+12,p.Y,p.Z),p.Map);PlagueJavelins.Tick(now.AddMilliseconds(40));Require(State(other)?.PoisonSource==s.Owner && State(other)?.PoisonClock?.Rate256==109,"Resumed cloud new collider/source");other.Delete();Require(NecromancerEffects.ManaBalance256(p)==s.Mana && NamedAmmunition.Remaining(w)==s.Quantity,"Reload repaid resources");target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);Require(p.EquipItem(w),"Restore weapon");TimedEffects.Clear(target);var held=s.Flight with{NextUtc=TimedEffects.Now.AddMinutes(10)};var cs=Clouds(p).Select(x=>x with{NextUtc=TimedEffects.Now.AddMinutes(10)}).ToArray();Hold(p,[held],cs);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.PlagueJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount),"Next unpaid windup");var until=TimedEffects.Now.AddMinutes(10);Delay(p,until);target.Blessed=true;File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Flight=held,Clouds=cs,Health=SkillHealth.Balance256(target),Poison=State(target),Cooldown=until}));Parked();
    });
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.PlagueJavelin);Weapon(p);Ready(p);var weapon=CombatTiming.Equipped(p);int quantity=NamedAmmunition.Remaining(weapon);var target=Target(p);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.PlagueJavelin) && SkillBindings.Instance.Assign(p,false,CombatAbility.PlagueJavelin) && SkillBindings.Instance.AssignQuick(p,4,CombatAbility.PlagueJavelin),"Connected bindings");
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap changed");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int before=NecromancerEffects.ManaBalance256(p),health=SkillHealth.Balance256(target);bool started=false,emitted=false;long deadline=Core.TickCount+5000;
        var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
        void Clean(){WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;WeaponMissiles.Cancel(p);target.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Connected fan did not complete");
                if(!started && ClassCombat.For(p).PendingAbility==CombatAbility.PlagueJavelin)
                {started=true;Require(NecromancerEffects.ManaBalance256(p)==before && NamedAmmunition.Remaining(weapon)==quantity && Own(p).Length==0,"Natural start4 phase");}
                var fan=Own(p);if(!emitted && fan.Length>0)
                {emitted=true;Require(started && fan.Length==1 && fan.All(x=>x.Frame<3 && x.Missile=="plaguejavelin") && SkillHealth.Balance256(target)==health && NamedAmmunition.Remaining(weapon)==quantity-1,"Natural do8/ammunition phase");}
                if(!emitted || fan.Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(30),Wait);return;}
                Require(SkillHealth.Balance256(target)<health && NecromancerEffects.ManaBalance256(p)==before-1792 && NamedAmmunition.Remaining(weapon)==quantity-1,"Natural travelling fan/resource accounting");Clean();Log.Information("plague-javelin-input-result: passed");
            }catch(Exception e){Clean();Log.Error(e,"plague-javelin-input-result: failed");}
        }
        var point=target.Location;Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("plague-javelin-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.PlagueJavelin,point.X,point.Y,point.Z);Wait();});
    }
}
