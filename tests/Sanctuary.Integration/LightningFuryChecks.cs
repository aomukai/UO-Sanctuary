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

public static class LightningFuryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(LightningFuryChecks));
    private sealed class Minimum:Random{public override int Next(int maximum)=>0;}
    private sealed class Maximum:Random{public override int Next(int maximum)=>Math.Max(0,maximum-1);}
    private sealed record Saved(uint Owner,uint Target,uint Weapon,Point3D Origin,WeaponFlight Flight,PoisonCloud[] Clouds,int Health,int Mana,int Quantity,TimedEffectState Poison,DateTime Cooldown);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"lightning-fury.json");
    public static void Configure(){Register("lightning-fury-check",Check);Register("lightning-fury-reload-check",Reload);Register("lightning-fury-input-check",Input);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Lightning Fury parent/burst/penetrating source0 children and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()=>MagicArrowChecks.Fresh("LightningFuryOwner");
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var t),"Talent");foreach(var id in t.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==id).Ability);}while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train");}}
    private static BowLaunchTarget Target(PlayerMobile p)
    {var t=MagicArrowChecks.Target(p);t.Name="BowLaunchTarget";t.SetResistance(ResistanceType.Energy,0);return t;}
    private static BaseWeapon Weapon(PlayerMobile p)
    {
        if(CombatTiming.Equipped(p) is{} old){p.AddToBackpack(old);}var w=ItemOrigins.Create(ItemOrigin.Drop,()=>new SoulGlaive());ClassGear.Profiles.FromMonster(w,30,GearRarity.Normal,"glv",GearTier.Normal,new Random(173));p.AddToBackpack(w);Require(p.EquipItem(w),"Ordinary javelin equip");ClassGear.Refresh(p);return w;
    }
    private static void Ready(PlayerMobile p)=>MagicArrowChecks.Ready(p);
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static void Controlled(Action action)
    {
        var random=AmazonBow.LaunchRandom;var damage=WeaponMissiles.DamageRoll;var hit=WeaponMissiles.HitRoll;var block=SourceBlocking.BlockRoll;var item=WeaponMissiles.ItemEffects;
        try{AmazonBow.LaunchRandom=new Minimum();WeaponMissiles.DamageRoll=_=>0;WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Lightning Fury called AR");SourceBlocking.BlockRoll=_=>99;WeaponMissiles.ItemEffects=new Maximum();action();}
        finally{AmazonBow.LaunchRandom=random;WeaponMissiles.DamageRoll=damage;WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=block;WeaponMissiles.ItemEffects=item;}
    }
    private static WeaponFlight Cast(PlayerMobile p,Mobile target=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(CombatTiming.Equipped(p));
        Require(ClassCombat.Perform(p,CombatAbility.LightningFury,target,target?.Location??MagicArrowChecks.Aim(p,18),Core.TickCount),"Throw start");var s=ClassCombat.For(p);
        Require(NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,CombatAbility.LightningFury).Mana256 && NamedAmmunition.Remaining(CombatTiming.Equipped(p))==quantity,"Start mana/quantity phase");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1) && ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt+1),"Early/repeated emission");
        var f=Parents(p).Single();Require(f.Missile=="lightningfury" && f.Frames==25 && f.SkillRank==SkillMechanics.Rank(p,CombatAbility.LightningFury) && NamedAmmunition.Remaining(CombatTiming.Equipped(p))==quantity-1 && FireGround.Ready(p,CombatAbility.LightningFury),"Saved source parent/release quantity/localdelay");return f;
    }
    private static WeaponFlight[] Parents(PlayerMobile p)=>Own(p).Where(x=>x.Missile=="lightningfury").ToArray();
    private static WeaponFlight[] Children(PlayerMobile p)=>Own(p).Where(AmazonLightningFury.IsChild).ToArray();
    private static void Keep(PlayerMobile p,params WeaponFlight[] flights)
    {var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value).Concat(flights).ToArray()});}
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"lightning-fury-source-probe.json")));int count=0;
        var fields=data.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();
        foreach(var r in data.RootElement.GetProperty("damage").EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();int S(string field)=>r[6][Array.IndexOf(fields,field)].GetInt32();bool child=V(0)!=0;
            var values=new SkillValues(SkillMechanics.For(CombatAbility.LightningFury),_=>V(1),_=>V(2),99);
            var e=new MissileSourceStats(LightningMin:AmazonMelee.EnhanceEndpoint(values.Damage256(false),V(3)),LightningMax:AmazonMelee.EnhanceEndpoint(values.Damage256(true),V(3)));
            var a=child?new WeaponMissileDamage(0,0,0,0,0,false,e):BowLaunch.FromSource(1280,2560,0,0,128,94,0,11,13,V(5)!=0,e with{FireMin=2560,FireMax=7936,LightningMin=e.LightningMin+V(4)*256,LightningMax=e.LightningMax+(V(4)+19)*256,LifeDrain=7});
            Require(a.Min256==S("nMinDamage") && a.Max256==S("nMaxDamage") && a.Percent==S("nDamagePercent") && a.DemonPercent==S("nDemonDamagePercent") && a.UndeadPercent==S("nUndeadDamagePercent") && a.Critical==((S("nFlags")&2)!=0) && a.Elements.LightningMin==S("nLightMinDamage") && a.Elements.LightningMax==S("nLightMaxDamage") && a.Elements.FireMin==S("nFireMinDamage") && a.Elements.FireMax==S("nFireMaxDamage") && a.Elements.LifeDrain==S("nLifeDrainMinDamage"),"Full source parent/child creator row "+count);count++;
        }
        Require(count==576,"Creator count");count=0;
        foreach(var r in data.RootElement.GetProperty("search").EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();int[] order=V(3)!=0?[3,4,5,0,1,2]:[0,1,2,3,4,5];int flags=V(4);int radius=V(1)>0?V(1):15,limit=V(2)>0?V(2):V(0)+1;
            var expected=order.Where(i=>i*2<=radius && !(i==0 && (flags&1)!=0 || i==1 && (flags&2)!=0 || i==2 && (flags&4)!=0 || i>=3 && (flags&8)!=0 || i==4 && (flags&16)!=0)).Take(limit);
            Require(expected.SequenceEqual(r[6].EnumerateArray().Select(x=>x[0].GetInt32())),"Compiled search list/filter/cap row "+count);count++;
        }
        Require(count==1024,"Source search count");count=0;
        foreach(var r in data.RootElement.GetProperty("initial").EnumerateArray())
        {Require(r[8].GetInt32()==1 && r[9].GetInt32()==r[0].GetInt32() && r[10].GetInt32()==r[1].GetInt32(),"Full allocator initial saved origin");count++;}Require(count==72,"Allocator count");
        Log.Information("Lightning Fury:576 source/native creator comparisons,1024 complete source search rows and72 allocator rows checked;96 router cases retain bounded source scope");
    }
    private static void Cases()
    {
        var p=Fresh();var w=Weapon(p);try
        {
            Require(!ClassCombat.Available(p,CombatAbility.LightningFury),"Unlearned Fury");Train(p,CombatAbility.LightningFury);var values=SkillMechanics.Values(p,CombatAbility.LightningFury);Require(values.Mana256==2560 && values.Damage256(false)==256 && values.Eval("calc1")==2 && values.Eval("aurarangecalc")==15,"Pinned rank1/minimum/count/radius");
            Ready(p);NecromancerEffects.SetMana256(p,2559);Require(!ClassCombat.Perform(p,CombatAbility.LightningFury,null,MagicArrowChecks.Aim(p),Core.TickCount),"Insufficient mana");MagicArrowChecks.Mana(p);Ready(p);int mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(w);Require(ClassCombat.Perform(p,CombatAbility.LightningFury,null,MagicArrowChecks.Aim(p),Core.TickCount),"Cancel start");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-2560 && NamedAmmunition.Remaining(w)==quantity,"Start payment/cancel quantity");
            MagicArrowChecks.Mana(p);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.LightningFury,null,MagicArrowChecks.Aim(p),Core.TickCount),"Paid start");NecromancerEffects.SetMana256(p,0);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Do repeated affordability");AmazonJavelins.Cancel(p);MagicArrowChecks.Mana(p);
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.LightningFury,null,p.Location,Core.TickCount) && ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Self point fallback");var zero=Parents(p).Single();Require(zero.Vx>0 && zero.Vx==zero.Vy,"Diagonal factory fallback");AmazonJavelins.Cancel(p);
            var a=Target(p);a.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);var b=Target(p);b.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);var f=Cast(p,a);int hp=SkillHealth.Balance256(a);while(Parents(p).Length>0){WeaponMissiles.Tick(Parents(p).Single().NextUtc);}var children=Children(p);Require(children.Length==2 && children.All(x=>x.Frame==0 && x.Pierces==0 && x.SkillRank==f.SkillRank && AmazonLightningFury.ValidChild(x.Damage)) && SkillHealth.Balance256(a)<hp,"Parent hit/count/source0 children/primary eligible");Require(f.Damage.Min256>0 && f.Damage.Elements.LightningMin==256 && f.Damage.LightningJavelinPercent==null,"Fury copied Lightning Bolt conversion/reset");
            quantity=NamedAmmunition.Remaining(w);mana=NecromancerEffects.ManaBalance256(p);while(Own(p).Length>0){WeaponMissiles.Tick(Own(p).Min(x=>x.NextUtc));}Require(NamedAmmunition.Remaining(w)==quantity && NecromancerEffects.ManaBalance256(p)==mana,"Child repeated resources");a.Delete();b.Delete();
            // A source0 child passes multiple targets without any Pierce passive.
            var target=Target(p);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);MagicArrowChecks.Mana(p);f=Cast(p,target);while(Parents(p).Length>0){WeaponMissiles.Tick(Parents(p).Single().NextUtc);}var child=Children(p).Single();target.MoveToWorld(new(p.X+15,p.Y,p.Z),p.Map);a=Target(p);a.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);b=Target(p);b.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);int ah=SkillHealth.Balance256(a),bh=SkillHealth.Balance256(b);Keep(p,child);for(int i=0;i<8;i++){WeaponMissiles.Tick(Children(p).Single().NextUtc);}Require(Children(p).Length==1 && Children(p).Single().Pierces==0 && SkillHealth.Balance256(a)==ah-child.Damage.Elements.LightningMin && SkillHealth.Balance256(b)==bh-child.Damage.Elements.LightningMin,"Unlimited child contacts/once most-recent collider/no Pierce");AmazonJavelins.Cancel(p);target.Delete();a.Delete();b.Delete();
            // The independent weapon parent can Pierce and create another group.
            Train(p,CombatAbility.Pierce);MagicArrowChecks.Mana(p);a=Target(p);a.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);b=Target(p);b.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);f=Cast(p,a);quantity=NamedAmmunition.Remaining(w);for(int i=0;i<8;i++){WeaponMissiles.Tick(Parents(p).Single().NextUtc);}Require(Parents(p).Single().Pierces==2 && Children(p).Length==4 && NamedAmmunition.Remaining(w)==quantity,"Parent two-victim Pierce/burst/once quantity");AmazonJavelins.Cancel(p);a.Delete();b.Delete();
            // Null removal/a4 still invokes hit20, unlike an ordinary no-contact frame.
            MagicArrowChecks.Mana(p);f=Cast(p);var nearEnd=Target(p);nearEnd.MoveToWorld(new(p.X+17,p.Y+4,p.Z),p.Map);for(int i=0;i<25;i++){WeaponMissiles.Tick(Parents(p).Single().NextUtc);}Require(Parents(p).Length==0 && Children(p).Length==1,"Null expiry source a4 callback/search");AmazonJavelins.Cancel(p);nearEnd.Delete();
            while(NamedAmmunition.Remaining(w)>1){NamedAmmunition.Consume(w,TimedEffects.Now);}MagicArrowChecks.Mana(p);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.LightningFury,null,MagicArrowChecks.Aim(p),Core.TickCount),"Last quantity start");NamedAmmunition.Consume(w,TimedEffects.Now);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && Parents(p).Length==1 && NamedAmmunition.Remaining(w)==0,"Do repeated quantity gate");AmazonJavelins.Cancel(p);Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.LightningFury,null,MagicArrowChecks.Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==mana,"Empty throw paid");
        }finally{AmazonJavelins.Cancel(p);p.Delete();}
    }
    private static Item Named(PlayerMobile p,string id){var item=NamedGear.Create(id,99,new Random(41),true);p.AddToBackpack(item);Require(p.EquipItem(item) && ClassGear.Active(p,item),"Actual named item "+id);return item;}
    private static void GearCases()
    {
        var p=Fresh();Train(p,CombatAbility.LightningFury);Weapon(p);try
        {
            var eye=Named(p,"unique/336");var ring=Named(p,"unique/122");var captured=BowLaunch.Capture(p,CombatAbility.LightningFury,false);var values=SkillMechanics.Values(p,CombatAbility.LightningFury);int mastery=NamedElements.MasteryPercent(p,"ltng");Require(mastery>0 && captured.Elements.LightningMin==NamedElements.To256(GearEffects.Damage(p,0).Lightning)+AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery),"Real Griffon/SoJ raw item/skill mastery");var child=AmazonLightningFury.Child(p,values.Rank);Require(child.Elements.LightningMin==AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery) && child.Elements.LightningMin<captured.Elements.LightningMin,"Child inherited item lightning");var retained=Cast(p);p.AddToBackpack(eye);p.AddToBackpack(ring);eye.Delete();ring.Delete();var withoutGear=AmazonLightningFury.Child(p,retained.SkillRank);Require(retained.SkillRank==3 && withoutGear.Elements.LightningMax==SkillMechanics.Values(p,CombatAbility.LightningFury,3).Damage256(true),"Retained parent rank/live mastery after gear removal");Train(p,CombatAbility.PowerStrike,2);var afterHard=AmazonLightningFury.Child(p,retained.SkillRank);Require(afterHard.Elements.LightningMax>withoutGear.Elements.LightningMax && withoutGear.Elements.LightningMin==256,"Future child ignored live hard synergy or changed previous packet");AmazonJavelins.Cancel(p);MagicArrowChecks.Mana(p);
            var armor=Named(p,"unique/90");var oldItems=WeaponMissiles.ItemEffects;WeaponMissiles.ItemEffects=new Minimum();try
            {
                var a=Target(p);a.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);var f=Cast(p,a);int hp=SkillHealth.Balance256(a),crush=NamedCombat.CrushingLoss256(a,true);while(Parents(p).Length>0){WeaponMissiles.Tick(Parents(p).Single().NextUtc);}var packet=f.Damage.Roll(a,_=>0);Require(hp-SkillHealth.Balance256(a)==crush+NamedElements.To256(packet.Total),"Weapon parent lost Crushing Blow");var bolt=Children(p).Single();hp=SkillHealth.Balance256(a);WeaponMissiles.Tick(bolt.NextUtc);Require(hp-SkillHealth.Balance256(a)==bolt.Damage.Elements.LightningMin,"Source0 child dispatched weapon Crushing Blow");AmazonJavelins.Cancel(p);a.Delete();
            }finally{WeaponMissiles.ItemEffects=oldItems;}p.AddToBackpack(armor);armor.Delete();var gloves=Named(p,"unique/232");MagicArrowChecks.Mana(p);var poisoned=Target(p);poisoned.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);poisoned.SetResistance(ResistanceType.Poison,33);var parent=Cast(p,poisoned);Require(parent.Damage.Elements.PoisonMin>0,"Real Venom Grip poison");while(Parents(p).Length>0){WeaponMissiles.Tick(Parents(p).Single().NextUtc);}var state=TimedEffects.Snapshot().Single(x=>x.Owner==poisoned.Serial.Value);var expected=PoisonPackets.Mitigate(parent.Damage.Elements.PoisonMin,parent.Damage.Elements.PoisonImpactFrames,false,33,0,0,0,0);Require(state.PoisonClock?.Rate256==expected.Rate256 && state.PoisonMs==expected.Frames*40 && Children(p).All(x=>x.Damage.Elements.PoisonMin==0),"Parent independent raw poison rate/length and source0 exclusion");AmazonJavelins.Cancel(p);poisoned.Delete();p.AddToBackpack(gloves);gloves.Delete();
        }finally{AmazonJavelins.Cancel(p);p.Delete();}
    }
    private static void Lifecycle()
    {
        foreach(bool death in new[]{false,true}){var p=Fresh();Train(p,CombatAbility.LightningFury);Weapon(p);var t=Target(p);t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);Cast(p,t);while(Parents(p).Length>0){WeaponMissiles.Tick(Parents(p).Single().NextUtc);}Require(Children(p).Length==1,"Lifecycle fixture");if(death){p.Kill();}else{ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}Require(Own(p).Length==0,"Death/respec left Fury children");t.Delete();p.Corpse?.Delete();p.Delete();}
    }
    private sealed record SavedFury(uint Owner,uint Target,uint Weapon,Point3D Origin,WeaponFlight Parent,WeaponFlight[] Children,int Health,int Mana,int Quantity);
    private static void Persist()
    {
        var p=Fresh();Train(p,CombatAbility.LightningFury);var w=Weapon(p);var t=Target(p);t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);var f=Cast(p,t);while(Parents(p).Length>0){WeaponMissiles.Tick(Parents(p).Single().NextUtc);}var bolts=Children(p).Select(x=>x with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}).ToArray();f=Cast(p,t);WeaponMissiles.Tick(f.NextUtc);f=Parents(p).Single() with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Keep(p,bolts.Append(f).ToArray());Require(SkillBindings.Instance.Assign(p,true,CombatAbility.LightningFury) && SkillBindings.Instance.Assign(p,false,CombatAbility.LightningFury) && SkillBindings.Instance.AssignQuick(p,3,CombatAbility.LightningFury),"Saved bindings");Ready(p);Require(ClassCombat.Perform(p,CombatAbility.LightningFury,null,MagicArrowChecks.Aim(p),Core.TickCount),"Paid/unemitted save windup");File.WriteAllText(Manifest,JsonSerializer.Serialize(new SavedFury(p.Serial.Value,t.Serial.Value,w.Serial.Value,p.Location,f,bolts,SkillHealth.Balance256(t),NecromancerEffects.ManaBalance256(p),NamedAmmunition.Remaining(w))));
    }
    private static void Check()=>Controlled(()=>{Differential();Cases();GearCases();Lifecycle();Persist();});
    private static void Reload()=>Controlled(()=>
    {
        var s=JsonSerializer.Deserialize<SavedFury>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BowLaunchTarget)World.FindMobile((Serial)s.Target);var w=(BaseWeapon)World.FindItem((Serial)s.Weapon);Require(Parents(p).Single()==s.Parent && Children(p).SequenceEqual(s.Children),"Saved parent/child packets and structural source steps");Require(SkillHealth.Balance256(t)==s.Health && NecromancerEffects.ManaBalance256(p)==s.Mana && NamedAmmunition.Remaining(w)==s.Quantity && ClassCombat.For(p).PendingAbility==CombatAbility.None,"Saved resources/paid unemitted windup");Require(SkillBindings.Instance.Get(p)==(CombatAbility.LightningFury,CombatAbility.LightningFury) && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,3)==CombatAbility.LightningFury,"Saved bindings");p.MoveToWorld(s.Origin,Map.Maps[s.Parent.Map]);t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);t.SetResistance(ResistanceType.Energy,0);p.AddToBackpack(w);var now=TimedEffects.Now;int hp=SkillHealth.Balance256(t);Keep(p,s.Parent with{NextUtc=now});while(Parents(p).Length>0){WeaponMissiles.Tick(Parents(p).Single().NextUtc);}Require(SkillHealth.Balance256(t)==hp-NamedElements.To256(s.Parent.Damage.Roll(t,_=>0).Total) && Children(p).Length==1,"Original saved parent and live child creation");var child=s.Children[0] with{NextUtc=now};Keep(p,child);hp=SkillHealth.Balance256(t);WeaponMissiles.Tick(now);Require(Children(p).Length==1 && SkillHealth.Balance256(t)==hp-(child.LastCollision==t.Serial.Value?0:child.Damage.Elements.LightningMin) && Children(p).Single().LastCollision==t.Serial.Value,"Original source0 child re-hit/continued travel");Require(NecromancerEffects.ManaBalance256(p)==s.Mana && NamedAmmunition.Remaining(w)==s.Quantity,"Reload repeated resource charges");Require(p.EquipItem(w),"Restore weapon");var held=s.Parent with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};var bolts=Children(p).Select(x=>x with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}).ToArray();Keep(p,bolts.Append(held).ToArray());Ready(p);Require(ClassCombat.Perform(p,CombatAbility.LightningFury,null,MagicArrowChecks.Aim(p),Core.TickCount),"Next paid save windup");File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Parent=held,Children=bolts,Health=SkillHealth.Balance256(t),Mana=NecromancerEffects.ManaBalance256(p)}));
    });
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.LightningFury);Weapon(p);Ready(p);var weapon=CombatTiming.Equipped(p);int quantity=NamedAmmunition.Remaining(weapon);var target=Target(p);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.LightningFury) && SkillBindings.Instance.Assign(p,false,CombatAbility.LightningFury) && SkillBindings.Instance.AssignQuick(p,4,CombatAbility.LightningFury),"Connected bindings");
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
                if(!started && ClassCombat.For(p).PendingAbility==CombatAbility.LightningFury)
                {started=true;Require(NecromancerEffects.ManaBalance256(p)==before-2560 && NamedAmmunition.Remaining(weapon)==quantity && Own(p).Length==0,"Natural start4 phase");}
                var fan=Parents(p);if(!emitted && fan.Length>0)
                {emitted=true;Require(started && fan.Length==1 && fan.All(x=>x.Frame<3 && x.Missile=="lightningfury") && SkillHealth.Balance256(target)==health && NamedAmmunition.Remaining(weapon)==quantity-1,"Natural do8/ammunition phase");}
                if(!emitted || Own(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(30),Wait);return;}
                Require(SkillHealth.Balance256(target)<health && NecromancerEffects.ManaBalance256(p)==before-2560 && NamedAmmunition.Remaining(weapon)==quantity-1,"Natural travelling fan/resource accounting");Clean();Log.Information("lightning-fury-input-result: passed");
            }catch(Exception e){Clean();Log.Error(e,"lightning-fury-input-result: failed");}
        }
        var point=target.Location;Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("lightning-fury-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.LightningFury,point.X,point.Y,point.Z);Wait();});
    }
}
