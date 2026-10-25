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

public static class MultipleShotChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MultipleShotChecks));
    private sealed class Minimum:Random{public override int Next(int max)=>0;}
    private sealed class Alternating:Random{public int Calls;public override int Next(int max)=>(Calls++%2==0?0:max-1);}
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Ammo,Point3D Origin,WeaponFlight[] Flights,int Health,int Mana,int Quantity);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"multiple-shot.json");
    public static void Configure(){Register("multiple-shot-check",Check);Register("multiple-shot-reload-check",Reload);Register("multiple-shot-input-check",Input);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Multiple Shot actual fan, start mana, once-only ammunition and saved projectiles",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()
    {
        var p=MagicArrowChecks.Fresh("MultipleShotOwner");
        while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}
        return p;
    }
    private static Point3D Aim(PlayerMobile p)=>MagicArrowChecks.Aim(p);
    private static void Ready(PlayerMobile p)=>MagicArrowChecks.Ready(p);
    private static void Mana(PlayerMobile p)=>MagicArrowChecks.Mana(p);
    private static BaseWeapon Bow(PlayerMobile p,Type type)=>MagicArrowChecks.Bow(p,type);
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(DiabloClass.Amazon,row.TalentId,out var talent),"Talent");
        foreach(string id in talent.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==id).Ability);}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out var why),"Train "+why);}
    }
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static WeaponFlight[] Cast(PlayerMobile p,bool held=false,bool left=false,Mobile target=null)
    {
        Ready(p);int before=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.MultipleShot).Mana256;
        var weapon=CombatTiming.Equipped(p);var ammo=BowAmmunition.Stack(p,weapon);int quantity=ammo?.Amount??0;var old=Own(p).Select(x=>x.Id).ToHashSet();
        bool start=held?(left?ClassCombat.ContinueAttack(p,CombatAbility.MultipleShot,target,target?.Location??Aim(p),Core.TickCount):ClassCombat.ContinueCast(p,CombatAbility.MultipleShot,target,target?.Location??Aim(p),Core.TickCount))
            :ClassCombat.Perform(p,CombatAbility.MultipleShot,target,target?.Location??Aim(p),Core.TickCount);
        Require(start && NecromancerEffects.ManaBalance256(p)==before-cost && ammo.Amount==quantity,"Accepted start4 pays mana once, not ammo");
        var session=ClassCombat.For(p);ClassCombat.ReleaseCast(p);
        Require(!ClassCombat.ResolveAction(p,session.EffectAt-1) && ClassCombat.ResolveAction(p,session.EffectAt) && !ClassCombat.ResolveAction(p,session.EffectAt),"Single do8 event");
        var fan=Own(p).Where(x=>!old.Contains(x.Id)).ToArray();var values=SkillMechanics.Values(p,CombatAbility.MultipleShot);
        Require(fan.Length==Math.Min(values.Rank+1,24) && fan.Select(x=>x.Cast).Distinct().Count()==1 && fan.Count(x=>(x.Flags&0x10000)==0)==2
            && fan.All(x=>x.Ability==CombatAbility.MultipleShot && x.Activation==1 && x.Frames==50 && x.NextDelay==4 && x.Damage.PhysicalToMagicPercent==0)
            && fan.All(x=>x.Missile==(weapon is Crossbow?"multipleshotbolt":"multipleshotarrow")),"Actual fan count/central flags/activation/profile/lease");
        Require(NecromancerEffects.ManaBalance256(p)==before-cost && (ammo.Deleted?0:ammo.Amount)==quantity-1,"Fan repaid mana or consumed more than one ammunition");return fan;
    }
    private static void Controlled(Action action)
    {
        var launch=AmazonBow.LaunchRandom;var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;var effects=WeaponMissiles.ItemEffects;
        try{AmazonBow.LaunchRandom=new Minimum();WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;WeaponMissiles.ItemEffects=new Minimum();action();}
        finally{AmazonBow.LaunchRandom=launch;WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;WeaponMissiles.ItemEffects=effects;}
    }
    private static void Finish(PlayerMobile p)
    {for(int i=0;i<1500 && Own(p).Length>0;i++){WeaponMissiles.Tick(Own(p).Min(x=>x.NextUtc));}Require(Own(p).Length==0,"Fan did not finish");}
    private static BowLaunchTarget Target(PlayerMobile p)
    {var target=MagicArrowChecks.Target(p);target.SetResistance(ResistanceType.Fire,0);return target;}
    private static void Cases()
    {
        var p=Fresh();try
        {
            var bow=Bow(p,typeof(Bow));Require(!ClassCombat.Available(p,CombatAbility.MultipleShot),"Unlearned Multiple Shot available");Train(p,CombatAbility.MultipleShot);
            Require(CombatTiming.WeaponAttack(CombatAbility.MultipleShot) && CombatTiming.For(p,CombatAbility.MultipleShot)==CombatTiming.For(p,CombatAbility.MagicArrow)
                && SkillBindings.Instance.Assign(p,true,CombatAbility.MultipleShot) && SkillBindings.Instance.Assign(p,false,CombatAbility.MultipleShot)
                && SkillBindings.Instance.AssignQuick(p,4,CombatAbility.MultipleShot),"Bow action/bindings");
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.MultipleShot,null,Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==mana,"No-ammunition start paid");
            var bolts=new Bolt(4);p.AddToBackpack(bolts);Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.MultipleShot,null,Aim(p),Core.TickCount),"Bow accepted bolts");
            var arrows=new Arrow(10);p.AddToBackpack(arrows);Cast(p);WeaponMissiles.Cancel(p);Cast(p,held:true);WeaponMissiles.Cancel(p);Cast(p,held:true,left:true);WeaponMissiles.Cancel(p);
            Require(arrows.Amount==7 && bolts.Amount==4,"Once-only bow stack selection");bow=Bow(p,typeof(Crossbow));Cast(p);WeaponMissiles.Cancel(p);Require(arrows.Amount==7 && bolts.Amount==3,"Crossbow stack selection");
            bow=Bow(p,typeof(Bow));Mana(p);Ready(p);mana=NecromancerEffects.ManaBalance256(p);int quantity=arrows.Amount;
            Require(ClassCombat.Perform(p,CombatAbility.MultipleShot,null,Aim(p),Core.TickCount),"Paid cancellation start");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-1024 && arrows.Amount==quantity,"Cancelled start4 refunded or consumed ammunition");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.MultipleShot,null,Aim(p),Core.TickCount),"Paid movement start");at=ClassCombat.For(p).EffectAt;var origin=p.Location;
            p.Direction=Direction.East;Require(p.Move(Direction.East),"Outward step");p.Direction=Direction.West;Require(p.Move(Direction.West) && p.Location==origin,"Return step");
            Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-2048 && arrows.Amount==quantity,"Movement revived/refunded paid fan");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.MultipleShot,null,Aim(p),Core.TickCount),"Depleted mana release start");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);
            Require(ClassCombat.ResolveAction(p,at) && Own(p).Length==2 && arrows.Amount==quantity-1 && NecromancerEffects.ManaBalance256(p)==0,"Paid fan required/charged mana twice");WeaponMissiles.Cancel(p);
            Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.MultipleShot,null,Aim(p),Core.TickCount),"Zero-mana new fan accepted");
            Mana(p);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.MultipleShot,null,Aim(p),Core.TickCount),"Stale ammo start");at=ClassCombat.For(p).EffectAt;
            arrows.MoveToWorld(origin,p.Map);Require(ClassCombat.ResolveAction(p,at) && Own(p).Length==2 && arrows.Amount==quantity-1,"Legacy do8 stale-stack projection changed");WeaponMissiles.Cancel(p);p.AddToBackpack(arrows);
            Train(p,CombatAbility.MultipleShot,4);Train(p,CombatAbility.CriticalStrike);Mana(p);var random=new Alternating();var previous=AmazonBow.LaunchRandom;AmazonBow.LaunchRandom=random;
            try{var fan=Cast(p);Require(random.Calls==5 && fan.Select((x,i)=>x.Damage.Critical==(i%2==0)).All(x=>x),"Critical shared across fan instead of per creation");}finally{AmazonBow.LaunchRandom=previous;WeaponMissiles.Cancel(p);}
            Train(p,CombatAbility.MultipleShot,20);Mana(p);Require(Cast(p).Length==21 && SkillMechanics.Values(p,CombatAbility.MultipleShot).Mana256==23*256,"Rank20 fan/mana");WeaponMissiles.Cancel(p);
            var item=NamedGear.Create("unique/282",99,new Random(17),true);p.AddToBackpack(bow);p.AddToBackpack(item);Require(p.EquipItem(item) && ClassGear.Active(p,item),"Actual Lycander's Aim");Mana(p);
            Require(SkillMechanics.Rank(p,CombatAbility.MultipleShot)==24 && Cast(p).Length==24,"Real class/tab soft ranks and24-arrow cap");WeaponMissiles.Cancel(p);
            Train(p,CombatAbility.GuidedArrow);Mana(p);var payload=Cast(p)[0].Damage;
            Require(payload.Percent==ClassStats.PhysicalDamageBonus(p,CombatTiming.Equipped(p))+12 && SkillMechanics.Rank(p,CombatAbility.GuidedArrow)>1,"Guided synergy used soft ranks");WeaponMissiles.Cancel(p);
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void ItemEvents()
    {
        var p=Fresh();try
        {
            Train(p,CombatAbility.MultipleShot,2);Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(3));
            foreach(string id in new[]{"unique/207","unique/88"}){var item=NamedGear.Create(id,99,new Random(23),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Actual proc gear");}
            foreach(bool outer in new[]{false,true})
            {
                Mana(p);var target=Target(p);try
                {
                    int before=SkillHealth.Balance256(target);var fan=Cast(p,target:target);var selected=fan.First(x=>((x.Flags&0x10000)!=0)==outer);
                    var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value || x.Id==selected.Id).ToArray()});Finish(p);
                    Require(SkillHealth.Balance256(target)<before,"Chosen actual fan ray missed fixture");
                    Require(TimedEffects.Slow(target,TimedEffects.Now)==(outer?0:20) && TimedEffects.CanRegenerateHits(target)==outer,"Actual central/outer item callback partition");
                }finally{target.Delete();}
            }
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void Hold(PlayerMobile p,WeaponFlight[] flights)
    {
        var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value).Concat(flights).ToArray()});
    }
    private static void Persist()
    {
        var p=Fresh();Train(p,CombatAbility.MultipleShot,4);var bow=NamedGear.Create("unique/65",99,new Random(53),true);p.AddToBackpack(bow);Require(p.EquipItem(bow),"Saved actual Hellclap");
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.MultipleShot) && SkillBindings.Instance.Assign(p,false,CombatAbility.MultipleShot) && SkillBindings.Instance.AssignQuick(p,4,CombatAbility.MultipleShot),"Saved bindings");
        var ammo=new Arrow(10);p.AddToBackpack(ammo);var target=Target(p);var fan=Cast(p,target:target);var until=TimedEffects.Now.Add(FixtureSpace.Retention);fan=fan.Select(x=>x with{NextUtc=until}).ToArray();Hold(p,fan);
        Require(SkillHealth.Balance256(target)==50000*256,"Instant instead of travelling fan damage");Ready(p);
        Require(ClassCombat.Perform(p,CombatAbility.MultipleShot,null,Aim(p),Core.TickCount),"Saved paid/unemitted fan windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,bow.Serial.Value,ammo.Serial.Value,p.Location,fan,SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),ammo.Amount)));
    }
    private static void Check()=>Controlled(()=>{Cases();ItemEvents();Persist();});
    private static void Reload()=>Controlled(()=>
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BowLaunchTarget)World.FindMobile((Serial)saved.Target);var ammo=World.FindItem((Serial)saved.Ammo);
        Require(Own(p).SequenceEqual(saved.Flights) && SkillHealth.Balance256(target)==saved.Health && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity
            && ClassCombat.For(p).PendingAbility==CombatAbility.None && SkillBindings.Instance.Get(p)==(CombatAbility.MultipleShot,CombatAbility.MultipleShot)
            && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,4)==CombatAbility.MultipleShot,"Saved fan/paid mana/ammo/bindings or replayed windup");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Flights[0].Map]);var bow=World.FindItem((Serial)saved.Bow);p.AddToBackpack(bow);
        int expected=NamedElements.To256(saved.Flights[0].Damage.Roll(target,_=>0).Total);Hold(p,saved.Flights.Select(x=>x with{NextUtc=TimedEffects.Now}).ToArray());Finish(p);
        Require(SkillHealth.Balance256(target)==saved.Health-expected && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity,"Fan shotgun/NHD or resumed resource repayment");
        var until=TimedEffects.Now.Add(FixtureSpace.Retention);var held=saved.Flights.Select(x=>x with{NextUtc=until}).ToArray();Hold(p,held);Require(p.EquipItem(bow),"Reload bow re-equip");Ready(p);
        Require(ClassCombat.Perform(p,CombatAbility.MultipleShot,null,Aim(p),Core.TickCount),"Saved next paid/unemitted windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flights=held,Health=SkillHealth.Balance256(target),Mana=NecromancerEffects.ManaBalance256(p)}));
    });
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.MultipleShot);Bow(p,typeof(Bow));Ready(p);var ammo=new Arrow(3);p.AddToBackpack(ammo);var target=Target(p);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.MultipleShot) && SkillBindings.Instance.Assign(p,false,CombatAbility.MultipleShot) && SkillBindings.Instance.AssignQuick(p,4,CombatAbility.MultipleShot),"Connected bindings");
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
                if(!started && ClassCombat.For(p).PendingAbility==CombatAbility.MultipleShot)
                {started=true;Require(NecromancerEffects.ManaBalance256(p)==before-1024 && ammo.Amount==3 && Own(p).Length==0,"Natural start4 phase");}
                var fan=Own(p);if(!emitted && fan.Length>0)
                {emitted=true;Require(started && fan.Length==2 && fan.All(x=>x.Frame<3) && SkillHealth.Balance256(target)==health && ammo.Amount==2,"Natural do8/ammunition phase");}
                if(!emitted || fan.Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(30),Wait);return;}
                Require(SkillHealth.Balance256(target)<health && NecromancerEffects.ManaBalance256(p)==before-1024 && ammo.Amount==2,"Natural travelling fan/resource accounting");Clean();Log.Information("multiple-shot-input-result: passed");
            }catch(Exception e){Clean();Log.Error(e,"multiple-shot-input-result: failed");}
        }
        var point=Aim(p);Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("multiple-shot-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.MultipleShot,point.X,point.Y,point.Z);Wait();});
    }
}
