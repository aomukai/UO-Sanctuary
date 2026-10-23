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

public static class DoubleThrowChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DoubleThrowChecks));
    private static bool _creating;
    private sealed record Saved(uint Owner,uint Primary,uint Secondary,int Mana,int Fraction,int First,int Second,int Rank);
    private sealed class HighRoll:Random{public override int Next(int max)=>max-1;}
    private static string Manifest=>Path.Join(Core.BaseDirectory,"double-throw-fixtures.json");
    public static void Configure(){Freeze();Register("double-throw-check",Check);Register("double-throw-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var previous=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>p is PlayerMobile && (_creating || p.Name=="DoubleThrowFixture")?TimeSpan.FromHours(1):previous?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Double Throw hand, event, resource, damage and reload checks",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Barbarian,id,out var talent),"Missing talent");
        foreach(var prerequisite in talent.Prerequisites){Train(owner,prerequisite);}
        while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Player=true,Name="DoubleThrowFixture"};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(owner,99);
        while(owner.RawStr<250){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength");}
        while(owner.RawDex<180){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Dexterity");}
        Train(owner,"barbarian.doublethrow");FixtureSpace.Place(owner,new(5450,1160,0),Map.Trammel);
        bool found=false;
        for(int x=2200;x<=2500 && !found;x+=50)for(int y=1000;y<=1300 && !found;y+=50)
        {
            Point3D point;try{point=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}
            if(SpellHelper.IsTown(point,owner)){continue;}
            bool clear=true;foreach(var other in owner.Map.GetMobilesInRange<Mobile>(point,10)){if(other!=owner){clear=false;break;}}
            if(clear){owner.MoveToWorld(point,owner.Map);found=true;}
        }
        Require(found,"No independent Double Throw corridor");
        var first=(BaseWeapon)NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Gimmershred").Id,99,new Random(173),true);
        var second=new NamedThrowingKnife();ClassGear.Profiles.Convert(second,99,GearRarity.Normal,new Random(173));
        owner.AddToBackpack(first);owner.AddToBackpack(second);Require(owner.EquipItem(first) && owner.EquipItem(second),"Native throwing pair equip");
        Require(BarbarianDoubleThrow.Pair(owner)==new WeaponPair(first,second),"Throwing pair identity");
        Quantity(first,9);Quantity(second,11);owner.Hits=owner.HitsMax;owner.Mana=owner.ManaMax;return owner;
    }
    private static void Quantity(BaseWeapon weapon,int value)
    {NamedAmmunition.Track(weapon);NamedAmmunition.Quantities[weapon.Serial]=new(value,new(DateTime.MaxValue.Ticks,DateTimeKind.Utc));}
    private static void Ready(PlayerMobile owner)
    {ClassCombat.Stop(owner);var action=ClassCombat.For(owner);action.NextAction=action.RecoveryUntil=Core.TickCount;}
    private static bool Begin(PlayerMobile owner,Mobile target=null)=>ClassCombat.Perform(owner,CombatAbility.DoubleThrow,target,target?.Location??new(owner.X+5,owner.Y,owner.Z),Core.TickCount);
    private static void Event(PlayerMobile owner)=>Require(ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt),"Missing due throw event");
    private static Ogre Enemy(PlayerMobile owner,int dx)
    {
        var enemy=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.SetHits(50000);enemy.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);enemy.AIObject.AITimer.Stop();
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){enemy.SetResistance(type,0);}
        return enemy;
    }
    private static int Health(Mobile actor)=>actor.Hits*256-SkillHealth.Remainder256(actor);
    private static int Expected(PlayerMobile owner,BaseWeapon weapon,Mobile target)
    {
        using(WeaponHands.Use(owner,weapon))
        {
            var values=SkillMechanics.Values(owner,CombatAbility.DoubleThrow);
            var damage=GearEffects.Damage(owner,0,target,(int)values.Eval("calc1"),_=>0);
            return NamedElements.To256(GearEffects.PrepareWeaponImpact(owner,target,0,new HighRoll(),damage,projectileCritical:false).Parts.Total);
        }
    }
    private static void Goldens()
    {
        var baseTiming=CombatTiming.Calculate(DiabloClass.Barbarian,CombatAbility.DoubleThrow,new(AttackFamily.Throw,0));
        Require(baseTiming==new ActionTiming(12,5) && CombatTiming.Calculate(DiabloClass.Barbarian,CombatAbility.DoubleThrow,new(AttackFamily.Throw,0),0,1000)==baseTiming,
            "SQ15 base12/5 or UseAttackRate incorrectly used FCR");
        var owner=Fresh();var values=SkillMechanics.Values(owner,CombatAbility.DoubleThrow);
        Require(values.Rank==1 && values.Eval("calc1")==24 && values.Formula("toht")==20 && values.Mana256==256,"Rank1/hard prerequisite source values");
        Train(owner,"barbarian.doublethrow",20);Train(owner,"barbarian.doubleswing",20);
        values=SkillMechanics.Values(owner,CombatAbility.DoubleThrow);
        Require(values.Rank==20 && values.Eval("calc1")==328 && values.Formula("toht")==210,"Hard synergy source values");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
        owner.AddToBackpack(helm);Require(owner.EquipItem(helm),"Actual allskills helm");ClassGear.Refresh(owner);
        values=SkillMechanics.Values(owner,CombatAbility.DoubleThrow);
        Require(values.Rank==21 && values.Eval("calc1")==336 && values.Formula("toht")==220 && values.Mana256==256,"Soft rank increased hard synergy or changed mana");owner.Delete();
    }
    private static void HandsAndDamage()
    {
        var owner=Fresh();var pair=BarbarianDoubleThrow.Pair(owner).Value;var enemy=Enemy(owner,3);owner.Mana=10;
        int first=Expected(owner,pair.Primary,enemy),second=Expected(owner,pair.Secondary,enemy);
        Require(first!=second && first>0 && second>0,"Independent source throwing damage");
        Require(SkillMechanics.Rank(owner,CombatAbility.ThrowingMastery)==0 && Begin(owner,enemy),"Double Throw incorrectly requires mastery");
        var action=ClassCombat.For(owner);Require(owner.Mana==10 && NamedAmmunition.Remaining(pair.Primary)==9 && !ClassCombat.ResolveAction(owner,action.EffectAt-1),"Pre-event debit");
        int before=Health(enemy);Event(owner);
        Require(before-Health(enemy)==first && owner.Mana==9 && NamedAmmunition.Remaining(pair.Primary)==8 && NamedAmmunition.Remaining(pair.Secondary)==11
            && action.DoubleThrow is{Released:1} && CombatTiming.Equipped(owner)==pair.Primary,"First hand/damage/payment/scope");
        Require(!ClassCombat.ResolveAction(owner,action.EffectAt-1),"Secondary throw too early");before=Health(enemy);Event(owner);
        Require(before-Health(enemy)==second && owner.Mana==8 && NamedAmmunition.Remaining(pair.Secondary)==10 && action.DoubleThrow==null
            && !ClassCombat.ResolveAction(owner,action.EffectAt),"Second hand/damage/payment/repeated event");
        enemy.Delete();Ready(owner);enemy=Enemy(owner,3);enemy.SetHits(1);owner.Mana=10;
        Require(Begin(owner,enemy),"Kill-first target start");ClassCombat.For(owner).Target=enemy.Serial;
        ClassCombat.UpdateCombat(owner,ClassCombat.For(owner).EffectAt);
        Require(!enemy.Alive && ClassCombat.For(owner).DoubleThrow is{Released:1},"Primary kill canceled the secondary event");
        ClassCombat.UpdateCombat(owner,ClassCombat.For(owner).EffectAt);
        Require(ClassCombat.For(owner).DoubleThrow==null && owner.Mana==8 && NamedAmmunition.Remaining(pair.Secondary)==9,"Lost target prevented empty-ray secondary release");
        enemy.Corpse?.Delete();enemy.Delete();Ready(owner);owner.Mana=1;Require(Begin(owner),"One-mana initial threshold");Event(owner);Event(owner);
        Require(owner.Mana==0 && NamedAmmunition.Remaining(pair.Primary)==6 && NamedAmmunition.Remaining(pair.Secondary)==8,"Declared source ignored post-do debit failure");
        Ready(owner);Require(!Begin(owner),"Zero-mana started a new cycle");
        owner.Mana=10;Quantity(pair.Secondary,0);Require(Begin(owner),"Empty secondary refused the source primary start");Event(owner);Event(owner);
        Require(owner.Mana==8 && NamedAmmunition.Remaining(pair.Secondary)==0,"Failed quantity should still reach post-do debit");owner.Delete();
    }
    private static void Cancellation()
    {
        var owner=Fresh();var pair=BarbarianDoubleThrow.Pair(owner).Value;owner.Mana=10;Require(Begin(owner),"Cancel start");
        long deadline=ClassCombat.For(owner).NextAction;ClassCombat.Stop(owner);
        Require(!ClassCombat.ResolveAction(owner,deadline) && owner.Mana==10 && NamedAmmunition.Remaining(pair.Primary)==9 && ClassCombat.For(owner).NextAction==deadline,"Pre-release stop/deadline");
        Ready(owner);Require(Begin(owner),"Movement start");Event(owner);var origin=owner.Location;
        owner.Direction=Direction.East;bool outStep=owner.Move(Direction.East);owner.Direction=Direction.West;
        Require(outStep && owner.Move(Direction.West) && owner.Location==origin,"Native out-and-back fixture movement");
        Require(ClassCombat.For(owner).DoubleThrow==null && !ClassCombat.ResolveAction(owner,deadline+5000) && owner.Mana==9 && NamedAmmunition.Remaining(pair.Secondary)==11,"Movement revived secondary or refunded first");
        Ready(owner);Require(Begin(owner),"Weapon change start");Event(owner);owner.AddToBackpack(pair.Secondary);
        Require(!ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt) && ClassCombat.For(owner).DoubleThrow==null && owner.Mana==8,"Changed secondary emitted/refunded");owner.Delete();
    }
    private static Saved Save(bool released)
    {
        var owner=Fresh();Train(owner,"barbarian.doublethrow",20);var pair=BarbarianDoubleThrow.Pair(owner).Value;
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.DoubleThrow),"Left Double Throw binding");
        Require(Begin(owner),"Saved throw start");if(released){Event(owner);}
        return new(owner.Serial.Value,pair.Primary.Serial.Value,pair.Secondary.Serial.Value,owner.Mana,NecromancerEffects.Profiles.Find(owner)?.ManaFraction??0,
            NamedAmmunition.Remaining(pair.Primary),NamedAmmunition.Remaining(pair.Secondary),SkillMechanics.Rank(owner,CombatAbility.DoubleThrow));
    }
    private static void Check()
    {
        var random=BarbarianThrowing.RandomSource;var hit=BarbarianDoubleThrow.HitRoll;var element=BarbarianDoubleThrow.ElementRoll;var physical=BarbarianDoubleThrow.PhysicalRoll;
        BarbarianThrowing.RandomSource=()=>new HighRoll();BarbarianDoubleThrow.HitRoll=()=>0;BarbarianDoubleThrow.ElementRoll=()=>0;BarbarianDoubleThrow.PhysicalRoll=_=>0;
        try{Goldens();HandsAndDamage();Cancellation();File.WriteAllText(Manifest,JsonSerializer.Serialize(new[]{Save(false),Save(true)}));}
        finally{BarbarianThrowing.RandomSource=random;BarbarianDoubleThrow.HitRoll=hit;BarbarianDoubleThrow.ElementRoll=element;BarbarianDoubleThrow.PhysicalRoll=physical;}
        Natural();
    }
    private static void Natural()
    {
        var owner=Fresh();var pair=BarbarianDoubleThrow.Pair(owner).Value;int mana=owner.Mana;
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=owner;owner.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Network bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(owner);
        var random=BarbarianThrowing.RandomSource;BarbarianThrowing.RandomSource=()=>new HighRoll();Require(Begin(owner),"Natural connected cycle");long began=Core.TickCount,deadline=began+5000;
        void Poll()
        {
            try
            {
                if(ClassCombat.For(owner).DoubleThrow==null)
                {
                    Require(Core.TickCount-began>=200 && owner.Mana==mana-2 && NamedAmmunition.Remaining(pair.Primary)==8 && NamedAmmunition.Remaining(pair.Secondary)==10,"Natural connected event/mana/quantity");
                    BarbarianThrowing.RandomSource=random;network.Mobile=null;owner.NetState=null;owner.Delete();Log.Information("double-throw-result: passed");return;
                }
                Require(Core.TickCount-deadline<0,"Natural Double Throw timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(50),Poll);
            }
            catch(Exception ex){BarbarianThrowing.RandomSource=random;network.Mobile=null;owner.NetState=null;owner.Delete();Log.Error(ex,"double-throw-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(50),Poll);
    }
    private static void Reload()
    {
        foreach(var row in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var owner=(PlayerMobile)World.FindMobile((Serial)row.Owner);owner.MoveToWorld(owner.LogoutLocation,owner.LogoutMap);
            var pair=BarbarianDoubleThrow.Pair(owner);Require(pair?.Primary.Serial.Value==row.Primary && pair?.Secondary.Serial.Value==row.Secondary,"Saved pair identity");
            Require(ClassCombat.For(owner).DoubleThrow==null && ClassCombat.For(owner).PendingAbility==CombatAbility.None && !ClassCombat.ResolveAction(owner,Core.TickCount+5000)
                && owner.Mana==row.Mana && (NecromancerEffects.Profiles.Find(owner)?.ManaFraction??0)==row.Fraction && NamedAmmunition.Remaining(pair.Value.Primary)==row.First
                && NamedAmmunition.Remaining(pair.Value.Secondary)==row.Second && SkillMechanics.Rank(owner,CombatAbility.DoubleThrow)==row.Rank
                && SkillBindings.Instance.Get(owner).Left==CombatAbility.DoubleThrow,"Reload replayed transient release/debit or lost rank/binding");owner.Delete();
        }
        Check();
    }
}
