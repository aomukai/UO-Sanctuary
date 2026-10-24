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

public static class MagicArrowChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MagicArrowChecks));
    private static bool _creating;
    private sealed class Minimum:Random{public override int Next(int max)=>0;}
    private sealed record Saved(uint Owner,uint Target,uint Bow,Point3D Origin,WeaponFlight Flight,int Health,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"magic-arrow-cast.json");
    public static void Configure(){Rates();Register("magic-arrow-cast-check",Check);Register("magic-arrow-cast-reload-check",Reload);Register("magic-arrow-input-check",Input);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        var health=Mobile.HitsRegenRateHandler;var mana=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=p=>_creating || p.Name is "MagicArrowOwner" or "MultipleShotOwner" or "FireArrowOwner" or "ColdArrowOwner" or "ExplodingArrowOwner" or "IceArrowOwner"?TimeSpan.FromHours(1):health?.Invoke(p)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=p=>_creating || p.Name is "MagicArrowOwner" or "MultipleShotOwner" or "FireArrowOwner" or "ColdArrowOwner" or "ExplodingArrowOwner" or "IceArrowOwner"?TimeSpan.FromHours(1):mana?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Magic Arrow learned bow action, mana, no ammunition and saved flight",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    internal static PlayerMobile Fresh(string name="MagicArrowOwner",DiabloClass characterClass=DiabloClass.Amazon)
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Name=name,Player=true};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,characterClass);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}
        while(p.RawDex<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}
        p.MoveToWorld(new(2200,1000,0),Map.Trammel);bool found=false;
        for(int x=2500;x<2900 && !found;x+=4)for(int y=1000;y<1450 && !found;y+=4)
        {
            int z=p.Map.GetAverageZ(x,y);var point=new Point3D(x,y,z);
            if(!Enumerable.Range(0,19).All(i=>p.Map.GetAverageZ(x+i,y)==z && p.Map.CanSpawnMobile(x+i,y,z))
                || !p.Map.LineOfSight(new Point3D(x,y,z+12),new Point3D(x+18,y,z+12))){continue;}
            bool empty=true;foreach(var other in p.Map.GetMobilesInRange<Mobile>(point,22)){if(other!=p){empty=false;break;}}
            if(!empty){continue;}p.MoveToWorld(point,p.Map);if(Enumerable.Range(0,19).Any(i=>SpellHelper.IsTown(new Point3D(x+i,y,z),p))){continue;}found=true;
        }
        Require(found,"No independent Magic Arrow corridor");p.Hits=p.HitsMax;Mana(p);return p;
    }
    internal static Point3D Aim(PlayerMobile p,int x=10)=>new(p.X+x,p.Y,p.Z);
    internal static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,Math.Min(p.ManaMax*256,50*256+17));
    internal static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=Core.TickCount;}
    private static void Train(PlayerMobile p,int rank)
    {while(SkillMechanics.Rank(p,CombatAbility.MagicArrow)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,"amazon.magicarrow",out var why),"Train "+why);}}
    internal static BaseWeapon Bow(PlayerMobile p,Type type)
    {
        if(CombatTiming.Equipped(p) is{} old){p.AddToBackpack(old);}
        var bow=(BaseWeapon)RuneSocketChecks.Host(p,type,2);Require(p.EquipItem(bow) && ClassGear.Active(p,bow),"Mapped bow equip");return bow;
    }
    private static WeaponFlight Cast(PlayerMobile p,Mobile target=null,bool held=false,bool left=false)
    {
        Ready(p);int before=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.MagicArrow).Mana256;
        var old=WeaponMissiles.Snapshot().Flights.Select(x=>x.Id).ToHashSet();var point=target?.Location??Aim(p);
        bool start=held?(left?ClassCombat.ContinueAttack(p,CombatAbility.MagicArrow,target,point,Core.TickCount):ClassCombat.ContinueCast(p,CombatAbility.MagicArrow,target,point,Core.TickCount))
            :ClassCombat.Perform(p,CombatAbility.MagicArrow,target,point,Core.TickCount);
        Require(start,"Magic Arrow input");var s=ClassCombat.For(p);
        Require(NecromancerEffects.ManaBalance256(p)==before && s.Animation==ClassCombat.AnimationFor(p,CombatAbility.MagicArrow,CombatTiming.Equipped(p)),"Windup payment or casting pose");
        ClassCombat.ReleaseCast(p);Require(!ClassCombat.ResolveAction(p,s.EffectAt-1) && ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt),"Single timed bow event");
        var flight=WeaponMissiles.Snapshot().Flights.Single(x=>!old.Contains(x.Id));
        Require(flight.Ability==CombatAbility.MagicArrow && flight.Frame==0 && flight.Frames==40 && flight.NextDelay==0 && flight.Flags==33
            && flight.Damage.PhysicalToMagicPercent==Math.Min(100,5+(SkillMechanics.Rank(p,CombatAbility.MagicArrow)-1)*2)
            && NecromancerEffects.ManaBalance256(p)==before-cost,"Captured missile/conversion or once-only fractional payment");return flight;
    }
    private static void Controlled(Action action)
    {
        var critical=AmazonBow.LaunchRandom;var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;
        try{AmazonBow.LaunchRandom=new Minimum();WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;action();}
        finally{AmazonBow.LaunchRandom=critical;WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;}
    }
    private static void Finish(long id)
    {for(int i=0;i<250 && WeaponMissiles.State(id) is{} f;i++){WeaponMissiles.Tick(f.NextUtc);}Require(WeaponMissiles.State(id)==null,"Cast flight did not finish");}
    private static void Cases()
    {
        var p=Fresh();try
        {
            var bow=Bow(p,typeof(Bow));Require(!ClassCombat.Available(p,CombatAbility.MagicArrow),"Unlearned bow skill available");Train(p,1);
            Require(ClassCombat.Available(p,CombatAbility.MagicArrow) && CombatTiming.WeaponAttack(CombatAbility.MagicArrow)
                && SkillBindings.Instance.Assign(p,true,CombatAbility.MagicArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.MagicArrow)
                && SkillBindings.Instance.AssignQuick(p,2,CombatAbility.MagicArrow),"Learned bow bindings");
            var ordinary=CombatTiming.For(p,CombatAbility.MagicArrow);
            var gloves=NamedGear.Create("unique/105",99,new Random(41),true);p.AddToBackpack(gloves);Require(p.EquipItem(gloves),"Real Magefist");
            Require(CombatTiming.Fcr(p)>=20 && CombatTiming.For(p,CombatAbility.MagicArrow)==ordinary,"Magic Arrow incorrectly used FCR");
            var armor=NamedGear.Create("unique/82",99,new Random(42),true);p.AddToBackpack(armor);Require(p.EquipItem(armor),"Real Twitchthroe");
            Require(CombatTiming.Ias(p)>=20 && CombatTiming.For(p,CombatAbility.MagicArrow).Frames<ordinary.Frames,"Magic Arrow ignored actual IAS");
            p.AddToBackpack(gloves);p.AddToBackpack(armor);
            using var timing=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"magic-arrow-action-source-probe.json")));
            foreach(var row in timing.RootElement.GetProperty("rows").EnumerateArray())
            {
                var family=row[0].GetInt32()==1?AttackFamily.Bow:AttackFamily.Crossbow;
                Require(AmazonBow.Timing(family,row[1].GetInt32())==new ActionTiming(row[5].GetInt32(),row[3].GetInt32()),"Compiled shot/original modern cycle differential");
            }
            var f=Cast(p);Require(p.Backpack.FindItemByType<Arrow>()==null && p.Backpack.FindItemByType<Bolt>()==null,"No-quiver fixture has ammo");WeaponMissiles.Cancel(p);
            var arrows=new Arrow(3);var bolts=new Bolt(4);p.AddToBackpack(arrows);p.AddToBackpack(bolts);
            Cast(p,held:true);WeaponMissiles.Cancel(p);Cast(p,held:true,left:true);WeaponMissiles.Cancel(p);
            Require(arrows.Amount==3 && bolts.Amount==4,"Magic Arrow consumed arrows/bolts");
            bow=Bow(p,typeof(Crossbow));Cast(p);WeaponMissiles.Cancel(p);Require(arrows.Amount==3 && bolts.Amount==4,"Magic Arrow crossbow ammo debit");
            p.AddToBackpack(bow);Require(!ClassCombat.Available(p,CombatAbility.MagicArrow),"Backpack bow available");Require(p.EquipItem(bow),"Reequip");
            Ready(p);NecromancerEffects.SetMana256(p,383);int count=WeaponMissiles.Snapshot().Flights.Length;
            Require(!ClassCombat.Perform(p,CombatAbility.MagicArrow,null,Aim(p),Core.TickCount) && WeaponMissiles.Snapshot().Flights.Length==count && NecromancerEffects.ManaBalance256(p)==383,"Insufficient start mana emitted/debited");
            Mana(p);Ready(p);int before=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.MagicArrow,null,Aim(p),Core.TickCount),"Cancel begin");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==before,"Cancelled arrow paid/emitted");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.MagicArrow,null,Aim(p),Core.TickCount),"Movement cancel begin");at=ClassCombat.For(p).EffectAt;var origin=p.Location;
            p.Direction=Direction.East;Require(p.Move(Direction.East),"Actual outward step");p.Direction=Direction.West;Require(p.Move(Direction.West) && p.Location==origin,"Actual return step");
            Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==before,"Out-and-back movement revived shot");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.MagicArrow,null,Aim(p),Core.TickCount),"Equipment cancel begin");at=ClassCombat.For(p).EffectAt;
            p.AddToBackpack(bow);Require(p.EquipItem(bow),"Actual bow re-equip");
            Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==before,"Removed/re-equipped bow revived shot");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.MagicArrow,null,Aim(p),Core.TickCount),"Recovery cancel begin");at=ClassCombat.For(p).EffectAt;
            Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount) && !ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==before,"Hit recovery failed to interrupt unpaid shot");
            ClassCombat.For(p).RecoveryUntil=Core.TickCount;
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.MagicArrow,null,Aim(p),Core.TickCount),"Mana-loss begin");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);
            Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==0,"Mana loss during windup emitted");
            Train(p,2);Require(SkillMechanics.Values(p,CombatAbility.MagicArrow).Mana256==352,"Rank2 fractional mana");Mana(p);Cast(p);WeaponMissiles.Cancel(p);
            Train(p,12);Require(SkillMechanics.Values(p,CombatAbility.MagicArrow).Mana256==32,"Rank12 fractional mana");
            Train(p,13);NecromancerEffects.SetMana256(p,0);Cast(p);WeaponMissiles.Cancel(p);Require(NecromancerEffects.ManaBalance256(p)==0,"Free rank13 cast");
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void Persist()
    {
        var p=Fresh();Train(p,1);var bow=Bow(p,typeof(Bow));Require(SkillBindings.Instance.Assign(p,true,CombatAbility.MagicArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.MagicArrow) && SkillBindings.Instance.AssignQuick(p,2,CombatAbility.MagicArrow),"Saved bindings");
        var target=new BowLaunchTarget{Frozen=true,CantWalk=true,NoKillAwards=true,Name="MagicArrowTarget"};target.SetHits(50000);target.MoveToWorld(Aim(p,5),p.Map);target.AIObject.AITimer.Stop();target.SetResistance(ResistanceType.Physical,0);
        var f=Cast(p,target);Require(SkillHealth.Balance256(target)==50000*256,"Instant instead of travelling cast damage");
        var all=WeaponMissiles.Snapshot();f=f with{NextUtc=TimedEffects.Now.AddMinutes(10)};WeaponMissiles.Restore(all with{Flights=all.Flights.Select(x=>x.Id==f.Id?f:x).ToArray()});
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.MagicArrow,null,Aim(p),Core.TickCount),"Saved unpaid bow windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,bow.Serial.Value,p.Location,f,SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p))));
    }
    internal static BowLaunchTarget Target(PlayerMobile p)
    {
        var target=new BowLaunchTarget{Frozen=true,CantWalk=true,NoKillAwards=true,Name="MagicArrowTarget"};target.SetHits(50000);
        target.MoveToWorld(Aim(p,5),p.Map);target.AIObject.AITimer.Stop();target.SetResistance(ResistanceType.Physical,0);return target;
    }
    private static void ItemEvents()
    {
        var p=Fresh();var effects=WeaponMissiles.ItemEffects;
        try
        {
            Train(p,1);Bow(p,typeof(Bow));
            foreach(string id in new[]{"unique/207","unique/88"})
            {var item=NamedGear.Create(id,99,new Random(71),true);p.AddToBackpack(item);Require(p.EquipItem(item) && ClassGear.Active(p,item),"Real Blackhorn/Boneflesh");}
            WeaponMissiles.ItemEffects=new Minimum();var target=Target(p);
            try
            {
                var flight=Cast(p,target);Finish(flight.Id);
                Require(TimedEffects.Slow(target,TimedEffects.Now)==20 && !TimedEffects.CanRegenerateHits(target)
                    && TimedEffects.Snapshot().Single(x=>x.Owner==target.Serial.Value).BleedMs>0,"Travelling attacker slow/prevent-heal/open-wounds callbacks");
            }finally{target.Delete();}
            target=Target(p);try
            {
                var flight=Cast(p,target);var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Select(x=>x.Id==flight.Id?x with{Flags=x.Flags|0x10000}:x).ToArray()});Finish(flight.Id);
                Require(TimedEffects.Slow(target,TimedEffects.Now)==0 && TimedEffects.CanRegenerateHits(target),"Outer attacker event suppression lost");
            }finally{target.Delete();}
        }finally{WeaponMissiles.ItemEffects=effects;WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void Check()=>Controlled(()=>{Cases();ItemEvents();Persist();});
    private static void Input()
    {
        var p=Fresh();Train(p,1);Bow(p,typeof(Bow));Ready(p);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.MagicArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.MagicArrow)
            && SkillBindings.Instance.AssignQuick(p,2,CombatAbility.MagicArrow),"Connected bindings");
        var target=Target(p);var ammo=new Arrow(3);p.AddToBackpack(ammo);
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Require(protocol!=null,"Protocol bootstrap changed");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int before=NecromancerEffects.ManaBalance256(p),health=SkillHealth.Balance256(target);long deadline=Core.TickCount+5000,id=0;
        var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;
        WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
        void Clean(){WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;WeaponMissiles.Cancel(p);target.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Connected Magic Arrow did not complete");
                if(id==0)
                {
                    var flight=WeaponMissiles.Snapshot().Flights.SingleOrDefault(x=>x.Owner==p.Serial.Value);
                    if(flight==null){Timer.StartTimer(TimeSpan.FromMilliseconds(30),Wait);return;}
                    id=flight.Id;Require(flight.Frame<3 && SkillHealth.Balance256(target)==health && NecromancerEffects.ManaBalance256(p)==before-384,"Natural windup/flight/payment phase");
                }
                if(WeaponMissiles.State(id)!=null){Timer.StartTimer(TimeSpan.FromMilliseconds(30),Wait);return;}
                Require(SkillHealth.Balance256(target)<health && NecromancerEffects.ManaBalance256(p)==before-384 && ammo.Amount==3
                    && ClassCombat.For(p).PendingAbility==CombatAbility.None,"Natural40ms hit or no-ammo/once-only mana");
                Clean();Log.Information("magic-arrow-input-result: passed");
            }
            catch(Exception e){Clean();Log.Error(e,"magic-arrow-input-result: failed");}
        }
        var point=Aim(p);Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>
        {Log.Information("magic-arrow-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.MagicArrow,point.X,point.Y,point.Z);Wait();});
    }
    private static void Reload()=>Controlled(()=>
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BowLaunchTarget)World.FindMobile((Serial)saved.Target);
        Require(WeaponMissiles.State(saved.Flight.Id)==saved.Flight && SkillHealth.Balance256(target)==saved.Health && NecromancerEffects.ManaBalance256(p)==saved.Mana
            && ClassCombat.For(p).PendingAbility==CombatAbility.None && SkillBindings.Instance.Get(p).Left==CombatAbility.MagicArrow
            && SkillBindings.Instance.Get(p).Right==CombatAbility.MagicArrow && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,2)==CombatAbility.MagicArrow,"Actual saved cast state/bindings/resources");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Flight.Map]);var bow=World.FindItem((Serial)saved.Bow);p.AddToBackpack(bow);
        int expected=NamedElements.To256(saved.Flight.Damage.Roll(target,_=>0).Total);
        var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Select(x=>x.Id==saved.Flight.Id?x with{NextUtc=TimedEffects.Now}:x).ToArray()});Finish(saved.Flight.Id);
        Require(SkillHealth.Balance256(target)==saved.Health-expected && NecromancerEffects.ManaBalance256(p)==saved.Mana,"Cast flight changed payload/paid twice");
        all=WeaponMissiles.Snapshot();var held=saved.Flight with{NextUtc=TimedEffects.Now.AddMinutes(10)};WeaponMissiles.Restore(all with{Flights=all.Flights.Append(held).ToArray()});
        Require(p.EquipItem(bow),"Saved bow re-equip");Ready(p);Require(ClassCombat.Perform(p,CombatAbility.MagicArrow,null,Aim(p),Core.TickCount),"Saved unpaid reload windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flight=held,Health=SkillHealth.Balance256(target)}));
    });
}
