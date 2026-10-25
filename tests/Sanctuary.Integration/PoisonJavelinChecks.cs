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

public static class PoisonJavelinChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PoisonJavelinChecks));
    private sealed class Minimum:Random{public override int Next(int maximum)=>0;}
    private sealed record Saved(uint Owner,uint Target,uint Weapon,Point3D Origin,WeaponFlight Flight,PoisonCloud[] Clouds,int Health,int Mana,int Quantity,TimedEffectState Poison,DateTime Cooldown);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"poison-javelin.json");
    public static void Configure(){Register("poison-javelin-check",Check);Register("poison-javelin-reload-check",Reload);Register("poison-javelin-input-check",Input);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Poison Javelin input, direct poison, trail/cloud contacts and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()=>MagicArrowChecks.Fresh("PoisonJavelinOwner");
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var t),"Talent");foreach(var id in t.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==id).Ability);}while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train");}}
    private static BowLaunchTarget Target(PlayerMobile p)
    {var t=MagicArrowChecks.Target(p);t.SetResistance(ResistanceType.Poison,0);return t;}
    private static BaseWeapon Weapon(PlayerMobile p)
    {
        if(CombatTiming.Equipped(p) is{} old){p.AddToBackpack(old);}var w=ItemOrigins.Create(ItemOrigin.Drop,()=>new SoulGlaive());ClassGear.Profiles.FromMonster(w,30,GearRarity.Normal,"glv",GearTier.Normal,new Random(173));p.AddToBackpack(w);Require(p.EquipItem(w),"Ordinary javelin equip");ClassGear.Refresh(p);return w;
    }
    private static void Delay(PlayerMobile p,DateTime? until=null)
    {var all=FireGround.Snapshot();FireGround.Restore(all with{Cooldowns=all.Cooldowns.Where(x=>x.Owner!=p.Serial.Value||x.Ability!=CombatAbility.PoisonJavelin).Concat(until is{} u?new[]{new FireCooldown(p.Serial.Value,CombatAbility.PoisonJavelin,u)}:[]).ToArray()});}
    private static void Ready(PlayerMobile p){MagicArrowChecks.Ready(p);Delay(p);}
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static PoisonCloud[] Clouds(PlayerMobile p)=>PoisonJavelins.Snapshot().Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static void Hold(PlayerMobile p,WeaponFlight f,PoisonCloud[] clouds)
    {var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(f).ToArray(),PoisonClouds=all.PoisonClouds.Where(x=>x.Owner!=p.Serial.Value).Concat(clouds).ToArray()});}
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
        Require(ClassCombat.Perform(p,CombatAbility.PoisonJavelin,target,target?.Location??MagicArrowChecks.Aim(p,18),Core.TickCount),"Throw start");var s=ClassCombat.For(p);
        Require(NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,CombatAbility.PoisonJavelin).Mana256 && NamedAmmunition.Remaining(CombatTiming.Equipped(p))==quantity,"Start mana/quantity phase");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1) && ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt+1),"Early/repeated emission");
        var f=Own(p).Single();Require(f.Missile=="poisonjav" && f.Frames==25 && f.SkillRank==SkillMechanics.Rank(p,CombatAbility.PoisonJavelin) && !f.PathChanged && NamedAmmunition.Remaining(CombatTiming.Equipped(p))==quantity-1 && !FireGround.Ready(p,CombatAbility.PoisonJavelin),"Saved source parent/release quantity/localdelay");return f;
    }
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"poison-contact-source-probe.json")));int count=0;
        foreach(var r in data.RootElement.EnumerateArray()){Require(CombatHitboxes.SourceTouches(r[0].GetInt32(),r[1].GetInt32(),r[2].GetInt32(),r[3].GetInt32())==(r[4].GetInt32()!=0),"Complete source finder footprint");count++;}Require(count==735,"Finder count");
        foreach(int advance in new[]{64,128,256,384,512,768}){var timing=PoisonJavelins.Timing(advance);Require(timing.EffectFrame==(9*256+advance-1)/advance && timing.Frames==(16*256+advance-1)/advance-1,"Pinned TH metadata/rate");}
    }
    private static void Cases()
    {
        var p=Fresh();var w=Weapon(p);try
        {
            Require(!ClassCombat.Available(p,CombatAbility.PoisonJavelin),"Unlearned throw");Train(p,CombatAbility.PoisonJavelin);
            Require(SkillBindings.Instance.Assign(p,true,CombatAbility.PoisonJavelin) && SkillBindings.Instance.Assign(p,false,CombatAbility.PoisonJavelin) && SkillBindings.Instance.AssignQuick(p,3,CombatAbility.PoisonJavelin),"Bindings");
            var v=SkillMechanics.Values(p,CombatAbility.PoisonJavelin);Require(v.Mana256==1024 && v.Damage256(false)==32 && v.Damage256(true)==48 && v.DurationFrames==200,"Pinned rank1");
            var timing=CombatTiming.For(p,CombatAbility.PoisonJavelin);var casterGloves=NamedGear.Create("unique/105",99,new Random(31),true);p.AddToBackpack(casterGloves);Require(p.EquipItem(casterGloves) && CombatTiming.For(p,CombatAbility.PoisonJavelin)==timing,"TH used FCR");p.AddToBackpack(casterGloves);casterGloves.Delete();var armor=NamedGear.Create("unique/82",99,new Random(32),true);p.AddToBackpack(armor);Require(p.EquipItem(armor) && CombatTiming.For(p,CombatAbility.PoisonJavelin).DurationMs<timing.DurationMs,"TH ignored real IAS");p.AddToBackpack(armor);armor.Delete();
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.PoisonJavelin,null,p.Location,Core.TickCount) && ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Zero-target factory fallback");var zero=Own(p).Single();Require(zero.Vx>0 && zero.Vx==zero.Vy,"Zero target did not use source diagonal fallback");PoisonJavelins.Cancel(p);
            Ready(p);NecromancerEffects.SetMana256(p,1023);Require(!ClassCombat.Perform(p,CombatAbility.PoisonJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount),"Insufficient mana");MagicArrowChecks.Mana(p);
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(w);Require(ClassCombat.Perform(p,CombatAbility.PoisonJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount),"Cancelled start");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-1024 && NamedAmmunition.Remaining(w)==quantity,"Cancel/refund/quantity");
            var f=Cast(p);var first=f.NextUtc;WeaponMissiles.Tick(first);Require(Clouds(p).Length==0 && Own(p).Single().PathChanged,"Initial trail flag");WeaponMissiles.Tick(first.AddMilliseconds(40));Require(Clouds(p).Length==1 && Clouds(p)[0].Damage.Min256==0 && Clouds(p)[0].Damage.Elements.PoisonMin==32,"Delayed source-zero trail");
            var firstCloud=Clouds(p)[0];Train(p,CombatAbility.PlagueJavelin);WeaponMissiles.Tick(first.AddMilliseconds(80));Require(Clouds(p).Any(x=>x.Damage.Elements.PoisonMin==35) && firstCloud.Damage.Elements.PoisonMin==32,"Live child hard synergy/immutable earlier cloud");
            PoisonJavelins.Cancel(p);
            // Complete source finder skips the previous victim before selecting another.
            var target=Target(p);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);var second=Target(p);second.MoveToWorld(target.Location,p.Map);
            var cloud=PoisonJavelins.Trail(p,f with{PathChanged=true,X=target.X,Y=target.Y,NextUtc=TimedEffects.Now});var selected=(BaseCreature)PoisonJavelins.Recipient(p,cloud);var other=selected==target?second:target;
            PoisonJavelins.Tick(cloud.NextUtc);Require(State(selected)?.PoisonClock?.Rate256==35 && State(other)==null,"Cloud applied multi-target area");
            var state=State(selected);PoisonJavelins.Tick(cloud.NextUtc.AddMilliseconds(40));Require(State(other)?.PoisonClock?.Rate256==35 && State(selected).PoisonMs==state.PoisonMs && Clouds(p).Single().LastCollision==other.Serial.Value,"LastCollide did not expose overlapping other victim");
            selected.MoveToWorld(new(p.X+12,p.Y,p.Z),p.Map);PoisonJavelins.Tick(cloud.NextUtc.AddMilliseconds(80));Require(State(other)?.PoisonClock?.Rate256==35,"Changed first collider not poisoned");
            other.MoveToWorld(new(p.X+13,p.Y,p.Z),p.Map);selected.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);PoisonJavelins.Tick(cloud.NextUtc.AddMilliseconds(120));Require(Clouds(p).Single().LastCollision==selected.Serial.Value,"Returning victim treated as permanent hit set");
            PoisonJavelins.Tick(cloud.NextUtc.AddMilliseconds(58*40));Require(Clouds(p).Single().Frame==59,"Cloud lost its59th contact pass");PoisonJavelins.Tick(cloud.NextUtc.AddMilliseconds(59*40));Require(Clouds(p).Length==0,"Cloud survived60th-frame expiry");target.Delete();second.Delete();
            // Parent Pierce keeps one release debit across independent victims.
            Train(p,CombatAbility.Pierce);MagicArrowChecks.Mana(p);var firstTarget=Target(p);firstTarget.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);var secondTarget=Target(p);secondTarget.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);
            f=Cast(p,firstTarget);Require(f.Pierces==4,"Actual passive Pierce budget");int firstLife=SkillHealth.Balance256(firstTarget),secondLife=SkillHealth.Balance256(secondTarget),charged=NamedAmmunition.Remaining(w);
            for(int i=0;i<10;i++){Require(Own(p).Length==1,"Piercing parent removed early");WeaponMissiles.Tick(Own(p)[0].NextUtc);}
            Require(SkillHealth.Balance256(firstTarget)<firstLife && SkillHealth.Balance256(secondTarget)<secondLife && Own(p).Single().Pierces==2 && NamedAmmunition.Remaining(w)==charged,"Parent two-victim Pierce/once quantity");PoisonJavelins.Cancel(p);firstTarget.Delete();secondTarget.Delete();
            var missed=Target(p);missed.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);MagicArrowChecks.Mana(p);f=Cast(p,missed);int life=SkillHealth.Balance256(missed);var oldHit=WeaponMissiles.HitRoll;WeaponMissiles.HitRoll=()=>1;
            try{for(int i=0;i<4;i++){WeaponMissiles.Tick(Own(p).Single().NextUtc);}}finally{WeaponMissiles.HitRoll=oldHit;}
            Require(Own(p).Length==0 && SkillHealth.Balance256(missed)==life && State(missed)?.PoisonClock==null,"AR miss continued on Pierce or applied direct poison");PoisonJavelins.Cancel(p);missed.Delete();
            // Low mana after paid start cannot cancel the do event.
            Ready(p);MagicArrowChecks.Mana(p);Require(ClassCombat.Perform(p,CombatAbility.PoisonJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount),"Paid start");NecromancerEffects.SetMana256(p,0);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && Own(p).Length==1,"Do repeated affordability");PoisonJavelins.Cancel(p);
            MagicArrowChecks.Mana(p);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.PoisonJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount),"Equipment cancel start");at=ClassCombat.For(p).EffectAt;p.AddToBackpack(w);Require(p.EquipItem(w),"Reequip");Require(!ClassCombat.ResolveAction(p,at),"Equipment cycle revived windup");
            while(NamedAmmunition.Remaining(w)>1){NamedAmmunition.Consume(w,TimedEffects.Now);}Ready(p);MagicArrowChecks.Mana(p);Require(ClassCombat.Perform(p,CombatAbility.PoisonJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount),"Last-quantity start");NamedAmmunition.Consume(w,TimedEffects.Now);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && Own(p).Length==1 && NamedAmmunition.Remaining(w)==0,"Do repeated quantity gate or underflowed");PoisonJavelins.Cancel(p);Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.PoisonJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==mana,"Empty throw started/paid");
        }finally{PoisonJavelins.Cancel(p);p.Delete();}
    }
    private static void Lifecycle()
    {
        foreach(bool death in new[]{false,true})
        {
            var p=Fresh();Train(p,CombatAbility.PoisonJavelin);Weapon(p);var f=Cast(p);WeaponMissiles.Tick(f.NextUtc.AddMilliseconds(40));Require(Own(p).Length==1 && Clouds(p).Length==1,"Lifecycle fixture");
            if(death){p.Kill();}else{ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}
            Require(Own(p).Length==0 && Clouds(p).Length==0,"Death/respec retained parent/cloud");p.Corpse?.Delete();p.Delete();
        }
    }
    private static void Persist()
    {
        var p=Fresh();Train(p,CombatAbility.PoisonJavelin);var w=Weapon(p);var target=Target(p);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.PoisonJavelin) && SkillBindings.Instance.Assign(p,false,CombatAbility.PoisonJavelin) && SkillBindings.Instance.AssignQuick(p,3,CombatAbility.PoisonJavelin),"Saved bindings");
        var f=Cast(p,target);for(int i=0;i<3;i++){WeaponMissiles.Tick(Own(p).Single().NextUtc);}f=Own(p).Single();Require(f.Frame==3 && f.PathChanged && Clouds(p).Length==2,"Saved actual trail/flight setup");
        f=f with{NextUtc=TimedEffects.Now.AddMinutes(10)};var clouds=Clouds(p).Select(x=>x with{NextUtc=TimedEffects.Now.AddMinutes(10)}).ToArray();Hold(p,f,clouds);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.PoisonJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount),"Paid/unemitted save start");var cooldown=TimedEffects.Now.AddMinutes(10);Delay(p,cooldown);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,w.Serial.Value,p.Location,f,clouds,SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),NamedAmmunition.Remaining(w),State(target),cooldown)));
    }
    private static void Check()=>Controlled(()=>{Differential();Cases();Lifecycle();Persist();});
    private static void Reload()=>Controlled(()=>
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var target=(BowLaunchTarget)World.FindMobile((Serial)s.Target);var w=(BaseWeapon)World.FindItem((Serial)s.Weapon);
        Require(Own(p).Length==1 && Own(p).Single()==s.Flight,$"Saved flight: {JsonSerializer.Serialize(Own(p))} expected {JsonSerializer.Serialize(s.Flight)}");
        Require(JsonSerializer.Serialize(Clouds(p))==JsonSerializer.Serialize(s.Clouds),$"Saved clouds: {JsonSerializer.Serialize(Clouds(p))} expected {JsonSerializer.Serialize(s.Clouds)}");
        Require(SkillHealth.Balance256(target)==s.Health && NecromancerEffects.ManaBalance256(p)==s.Mana && NamedAmmunition.Remaining(w)==s.Quantity,$"Saved resources hp={SkillHealth.Balance256(target)}/{s.Health} mana={NecromancerEffects.ManaBalance256(p)}/{s.Mana} quantity={NamedAmmunition.Remaining(w)}/{s.Quantity}");
        Require(ClassCombat.For(p).PendingAbility==CombatAbility.None && JsonSerializer.Serialize(State(target))==JsonSerializer.Serialize(s.Poison),$"Saved pending/state: {ClassCombat.For(p).PendingAbility}; {JsonSerializer.Serialize(State(target))} expected {JsonSerializer.Serialize(s.Poison)}");
        Require(!FireGround.Ready(p,CombatAbility.PoisonJavelin) && FireGround.Snapshot().Cooldowns.Any(x=>x.Owner==s.Owner&&x.Ability==CombatAbility.PoisonJavelin&&x.UntilUtc==s.Cooldown) && SkillBindings.Instance.Get(p)==(CombatAbility.PoisonJavelin,CombatAbility.PoisonJavelin) && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,3)==CombatAbility.PoisonJavelin,"Saved delay/bindings");
        p.MoveToWorld(s.Origin,Map.Maps[s.Flight.Map]);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);TimedEffects.Clear(target);p.AddToBackpack(w);var now=TimedEffects.Now;
        int before=SkillHealth.Balance256(target),physical=NamedElements.To256(s.Flight.Damage.Roll(target,_=>0).Physical);Hold(p,s.Flight with{NextUtc=now},s.Clouds);WeaponMissiles.Tick(now);
        Require(Own(p).Length==0 && SkillHealth.Balance256(target)==before-physical && target.PoisonResistance==50 && State(target)?.PoisonClock?.Rate256==16,$"Resumed original parent: flights={JsonSerializer.Serialize(Own(p))}; hp={SkillHealth.Balance256(target)}/{before-physical}; state={JsonSerializer.Serialize(State(target))}");
        // Resume an actual saved trail cloud independently, with a new collider.
        TimedEffects.Clear(target);var cloud=s.Clouds[0] with{NextUtc=now};target.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{PoisonClouds=all.PoisonClouds.Where(x=>x.Owner!=s.Owner).Append(cloud).ToArray()});before=SkillHealth.Balance256(target);PoisonJavelins.Tick(now);var contact=now;if(cloud.LastCollision==target.Serial.Value){Require(State(target)==null,"Saved LastCollide was forgotten");target.MoveToWorld(new(p.X+12,p.Y,p.Z),p.Map);var other=Target(p);other.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);PoisonJavelins.Tick(now.AddMilliseconds(40));Require(State(other)!=null,"Alternate collider");other.MoveToWorld(new(p.X+13,p.Y,p.Z),p.Map);target.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);contact=now.AddMilliseconds(80);PoisonJavelins.Tick(contact);other.Delete();}TimedEffects.Settle(target,contact.AddMilliseconds(40));Require(State(target)?.PoisonSource==s.Owner && before-SkillHealth.Balance256(target)==16 && Clouds(p).Single().LastCollision==target.Serial.Value,"Resumed original cloud raw poison/source/LastCollide");
        Require(NecromancerEffects.ManaBalance256(p)==s.Mana && NamedAmmunition.Remaining(w)==s.Quantity,"Reload repaid mana/quantity");HoldPoison(target,TimedEffects.Now.AddMinutes(10));Require(p.EquipItem(w),"Restore javelin");
        var held=s.Flight with{NextUtc=TimedEffects.Now.AddMinutes(10)};var savedClouds=Clouds(p).Select(x=>x with{NextUtc=TimedEffects.Now.AddMinutes(10)}).ToArray();Hold(p,held,savedClouds);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.PoisonJavelin,null,MagicArrowChecks.Aim(p),Core.TickCount),"Next paid/unemitted windup");var cooldown=TimedEffects.Now.AddMinutes(10);Delay(p,cooldown);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Flight=held,Clouds=savedClouds,Health=SkillHealth.Balance256(target),Mana=NecromancerEffects.ManaBalance256(p),Poison=State(target),Cooldown=cooldown}));
    });
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.PoisonJavelin);Weapon(p);Ready(p);var weapon=CombatTiming.Equipped(p);int quantity=NamedAmmunition.Remaining(weapon);var target=Target(p);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.PoisonJavelin) && SkillBindings.Instance.Assign(p,false,CombatAbility.PoisonJavelin) && SkillBindings.Instance.AssignQuick(p,4,CombatAbility.PoisonJavelin),"Connected bindings");
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
                if(!started && ClassCombat.For(p).PendingAbility==CombatAbility.PoisonJavelin)
                {started=true;Require(NecromancerEffects.ManaBalance256(p)==before-1024 && NamedAmmunition.Remaining(weapon)==quantity && Own(p).Length==0,"Natural start4 phase");}
                var fan=Own(p);if(!emitted && fan.Length>0)
                {emitted=true;Require(started && fan.Length==1 && fan.All(x=>x.Frame<3 && x.Missile=="poisonjav") && SkillHealth.Balance256(target)==health && NamedAmmunition.Remaining(weapon)==quantity-1,"Natural do8/ammunition phase");}
                if(!emitted || fan.Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(30),Wait);return;}
                Require(SkillHealth.Balance256(target)<health && NecromancerEffects.ManaBalance256(p)==before-1024 && NamedAmmunition.Remaining(weapon)==quantity-1,"Natural travelling fan/resource accounting");Clean();Log.Information("poison-javelin-input-result: passed");
            }catch(Exception e){Clean();Log.Error(e,"poison-javelin-input-result: failed");}
        }
        var point=target.Location;Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("poison-javelin-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.PoisonJavelin,point.X,point.Y,point.Z);Wait();});
    }
}
