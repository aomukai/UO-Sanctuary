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

public static class JabChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(JabChecks));
    private sealed class Rolls(int value):Random{public override int Next(int maximum)=>Math.Clamp(value,0,Math.Max(0,maximum-1));}
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Target,Point3D Origin,int Mana,int Life,int Durability,int Quantity);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"jab-fixtures.json");
    private static bool _creating;
    public static void Configure(){Freeze();Register("jab-check",Check);Register("jab-reload-check",Reload);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=a=>_creating || a.Name is "JabOwner" or "JabTarget"?TimeSpan.FromHours(1):hp?.Invoke(a)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=a=>_creating || a.Name=="JabOwner"?TimeSpan.FromHours(1):mp?.Invoke(a)??Mobile.DefaultManaRate;
    }
    private static void Register(string command,Action action)=>ConsoleInputHandler.RegisterCommand(command,"Jab sequence, melee javelins, source wear and persistence",_=>Core.LoopContext.Post(()=>
    {try{Controlled(action);Log.Information("{Command}: passed",command);}catch(Exception ex){Log.Error(ex,"{Command}: failed",command);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Controlled(Action action)
    {
        var hit=AmazonJab.HitRoll;var element=AmazonJab.ElementRoll;var physical=AmazonJab.PhysicalRoll;var effects=AmazonJab.Effects;var wear=AmazonJab.WearRoll;
        try{AmazonJab.HitRoll=()=>0;AmazonJab.ElementRoll=()=>0;AmazonJab.PhysicalRoll=_=>0;AmazonJab.Effects=new Rolls(99);AmazonJab.WearRoll=new Rolls(99);action();}
        finally{AmazonJab.HitRoll=hit;AmazonJab.ElementRoll=element;AmazonJab.PhysicalRoll=physical;AmazonJab.Effects=effects;AmazonJab.WearRoll=wear;}
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="JabOwner",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);
        while(owner.RawStr<110){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength");}
        while(owner.RawDex<130){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Dexterity");}
        FixtureSpace.Place(owner,new(2300,1100,0),Map.Trammel);Require(!SpellHelper.IsTown(owner.Location,owner),"Jab fixture town");Mana(owner);return owner;
    }
    private static void Train(PlayerMobile owner,int rank=1)
    {while(ClassCreation.Profiles.GetRank(owner.Serial,"amazon.jab")<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"amazon.jab",out _),"Jab train");}}
    private static void Mana(PlayerMobile owner)=>NecromancerEffects.SetMana256(owner,30*256+17);
    private static void Ready(PlayerMobile owner){AmazonJab.Cancel(owner);ClassCombat.Stop(owner);var s=ClassCombat.For(owner);s.NextAction=s.RecoveryUntil=Core.TickCount;Mana(owner);}
    private static BaseCreature Enemy(PlayerMobile owner,int dx=1)
    {
        BaseCreature target;_creating=true;try{target=new GraveyardZombie{Name="JabTarget"};}finally{_creating=false;}
        target.Frozen=target.CantWalk=true;target.NoKillAwards=true;target.AIObject.AITimer.Stop();target.SetHits(5000);target.Hits=target.HitsMax;target.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);return target;
    }
    private static BaseWeapon Equip(PlayerMobile owner,BaseWeapon weapon)
    {if(CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(weapon),"Equip "+weapon.GetType().Name);ClassGear.Refresh(owner);return weapon;}
    private static Item Named(PlayerMobile owner,string source)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);Require(owner.EquipItem(item),"Named "+source);ClassGear.Refresh(owner);return item;}
    private static AmazonJab.Series Begin(PlayerMobile owner,Mobile target,int input=0)
    {
        long now=Core.TickCount;bool ok=input==1?ClassCombat.ContinueAttack(owner,CombatAbility.Jab,target,target.Location,now):input==2?ClassCombat.ContinueCast(owner,CombatAbility.Jab,target,target.Location,now):ClassCombat.Perform(owner,CombatAbility.Jab,target,target.Location,now);
        Require(ok && ClassCombat.For(owner).Jab!=null,"Jab input rejected");ClassCombat.ReleaseCast(owner);return ClassCombat.For(owner).Jab;
    }
    private static int Hit(PlayerMobile owner,BaseCreature target,AmazonJab.Series series,int index)
    {int before=SkillHealth.Balance256(target);long at=series.Started+series.Events[index];Require(ClassCombat.ResolveAction(owner,at),"Scheduled strike "+index);Require(!ClassCombat.ResolveAction(owner,at),"Duplicate scheduled strike");return before-SkillHealth.Balance256(target);}
    private static void Differential(PlayerMobile owner,BaseWeapon spear,BaseWeapon javelin)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"jab-source-probe.json")));int schedules=0,wearCases=0;
        foreach(var row in doc.RootElement.GetProperty("schedules").EnumerateArray())
        {var result=AmazonJab.Schedule(row[0].GetInt32()==1?"1HT":"2HT",row[1].GetInt32());Require(result.End==row[3].GetInt32() && result.Events.SequenceEqual(row[2].EnumerateArray().Select(x=>x[0].GetInt32())),"Compiled three-event schedule");schedules++;}
        foreach(var row in doc.RootElement.GetProperty("wear").EnumerateArray())
        {
            bool stack=row[0].GetInt32()!=0;if(row[1].GetInt32()==0 || !stack && row[3].GetInt32()!=0){continue;}
            var item=stack?javelin:spear;item.MaxHitPoints=5;item.HitPoints=row[2].GetInt32();
            if(stack){NamedAmmunition.Track(item);NamedAmmunition.Quantities[item.Serial]=new(row[3].GetInt32(),DateTime.MaxValue);}
            AmazonJab.Wear(item,new Rolls(row[4].GetInt32()));Require(item.HitPoints==row[5].GetInt32() && (!stack || NamedAmmunition.Remaining(item)==row[6].GetInt32()),"Compiled durability/stack differential");wearCases++;
        }
        spear.MaxHitPoints=spear.HitPoints=30;javelin.MaxHitPoints=javelin.HitPoints=6;NamedAmmunition.Quantities[javelin.Serial]=new(NamedAmmunition.Maximum(javelin),DateTime.MaxValue);
        for(int rank=1;rank<=20;rank++)
        {var v=new SkillValues(SkillMechanics.For(CombatAbility.Jab),_=>rank,_=>rank,99);Require(v.Mana256==(8+rank-1)*64 && v.Formula("toht")==10+9*(rank-1) && v.Eval("calc1")==-15+3*(rank-1),"Pinned Jab rank golden");}
        Require(CombatTiming.Calculate(DiabloClass.Amazon,CombatAbility.Jab,new(AttackFamily.Thrust,0),fcr:1000)==new ActionTiming(26,5) && CombatTiming.Calculate(DiabloClass.Amazon,CombatAbility.Jab,new(AttackFamily.TwoHandedThrust,0))==new ActionTiming(31,5) && CombatTiming.Calculate(DiabloClass.Amazon,CombatAbility.Jab,new(AttackFamily.Thrust,0),ias:100).Frames<26,"Jab compiled sequence rate179, attack-rate/FCR separation");
        Require(schedules==12 && wearCases==72,"Incomplete Jab native differential");Log.Information("Jab:12 compiled three-event schedules,72 applicable wear cases and20 rank goldens pass");
    }
    private static void Cases(PlayerMobile owner,BaseWeapon spear,BaseWeapon javelin,Item helm)
    {
        Require(!ClassCombat.Available(owner,CombatAbility.Jab),"Unlearned Jab");Train(owner);Require(owner.EquipItem(spear),"Initial spear restore");ClassGear.Refresh(owner);Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.Jab) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Jab) && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Jab),"Jab left/right/quick bindings");
        Require(AmazonJab.Code(owner)=="2HT","Source spear selector");var target=Enemy(owner);Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner),calls=0;AmazonJab.HitRoll=()=>{calls++;return 0;};var series=Begin(owner,target,1);
        Require(!series.Basic && series.Events.Length==3 && NecromancerEffects.ManaBalance256(owner)==mana-512 && !ClassCombat.ResolveAction(owner,series.Started+series.Events[0]-1),"Start payment/early strike");
        Require(AmazonJab.Locked(owner,series.Started) && !ClassCombatHooks.MovementAllowed(owner) && !HitRecovery.Apply(owner,owner.HitsMax,series.Started),"Jab interruption/movement guard");ClassCombat.Stop(owner);Require(ClassCombat.For(owner).Jab==series,"Stop interrupted Jab sequence");
        for(int i=0;i<3;i++){Require(Hit(owner,target,series,i)>0,"Spear hit damage");}
        Require(calls==3 && NecromancerEffects.ManaBalance256(owner)==mana-512,"Repeated mana or attack count");ClassCombat.ResolveAction(owner,series.Until);Require(ClassCombat.For(owner).Jab==null,"Sequence recovery not released");
        Ready(owner);AmazonJab.HitRoll=()=>1;series=Begin(owner,target,2);int hp=SkillHealth.Balance256(target);ClassCombat.ResolveAction(owner,series.Until);Require(SkillHealth.Balance256(target)==hp && ClassCombat.For(owner).Jab==null,"Missed overdue three-event cycle");AmazonJab.HitRoll=()=>0;
        Ready(owner);series=Begin(owner,target);target.Delete();var other=Enemy(owner);hp=SkillHealth.Balance256(other);ClassCombat.ResolveAction(owner,series.Until);Require(SkillHealth.Balance256(other)==hp,"Dead target redirected Jab");
        Ready(owner);series=Begin(owner,other);var origin=other.Location;other.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);Require(Hit(owner,other,series,0)==0,"Out-of-range target hit");other.MoveToWorld(origin,owner.Map);Require(Hit(owner,other,series,1)>0 && Hit(owner,other,series,2)>0,"Same target failed after returning to range");
        Ready(owner);series=Begin(owner,other);var start=owner.Location;owner.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);ClassMovement.Moved(owner,Direction.South);Require(!ClassCombat.ResolveAction(owner,series.Until) && ClassCombat.For(owner).Jab==null,"External movement cancellation");owner.MoveToWorld(start,owner.Map);
        Ready(owner);series=Begin(owner,other);owner.Frozen=true;Require(!ClassCombat.ResolveAction(owner,series.Until) && ClassCombat.For(owner).Jab==null,"Incapacitation cancellation");owner.Frozen=false;
        owner.AddToBackpack(spear);Require(owner.EquipItem(javelin),"Equip actual Titan javelin");ClassGear.Refresh(owner);Require(AmazonJab.Code(owner)=="1HT" && AmazonJab.WeaponAllowed(owner),"Javelin selector/ancestry");Require(CombatTiming.Range(owner,CombatAbility.Jab)==1 && !AmazonJab.ValidTarget(owner,Enemy(owner,3).Serial),"Thrown reach leaked into Jab melee");
        var thrown=ClassStats.WeaponBaseDamage(owner);using(WeaponHands.UseMelee(owner,javelin)){Require(!NamedCombat.Ranged(owner) && ClassStats.WeaponBaseDamage(owner)!=thrown,"Javelin melee endpoints/ranged flag");}Require(NamedCombat.Ranged(owner) && ClassStats.WeaponBaseDamage(owner)==thrown && !WeaponHands.Melee(javelin),"Melee scope leaked");
        Ready(owner);mana=NecromancerEffects.ManaBalance256(owner);int quantity=NamedAmmunition.Remaining(javelin);series=Begin(owner,other);Require(series.Events.Length==3,"Javelin strike count");for(int i=0;i<3;i++){Require(Hit(owner,other,series,i)>0,"Javelin melee hit");}Require(NamedAmmunition.Remaining(javelin)==quantity && NecromancerEffects.ManaBalance256(owner)==mana-SkillMechanics.Values(owner,CombatAbility.Jab).Mana256,"Javelin quantity/cycle mana");
        Ready(owner);NecromancerEffects.SetMana256(owner,17);mana=NecromancerEffects.ManaBalance256(owner);series=Begin(owner,other);Require(series.Basic && series.Events.Length==1 && !AmazonJab.Locked(owner,series.Started) && NecromancerEffects.ManaBalance256(owner)==mana,"AttackNoMana ordinary melee fallback");Require(Hit(owner,other,series,0)>0,"Fallback ordinary hit");ClassCombat.ResolveAction(owner,series.Until);
        Ready(owner);Require(owner.EquipItem(helm),"Actual Tarnhelm");ClassGear.Refresh(owner);mana=NecromancerEffects.ManaBalance256(owner);int cost=SkillMechanics.Values(owner,CombatAbility.Jab).Mana256;series=Begin(owner,other);Require(cost==(8+SkillMechanics.Rank(owner,CombatAbility.Jab)-1)*64 && NecromancerEffects.ManaBalance256(owner)==mana-cost,"Actual soft-rank quarter-mana payment");owner.AddToBackpack(helm);ClassGear.Refresh(owner);Require(Hit(owner,other,series,0)>0,"Current soft-rank change at do-event");
        Ready(owner);series=Begin(owner,other);owner.AddToBackpack(javelin);Require(!ClassCombat.ResolveAction(owner,series.Until) && ClassCombat.For(owner).Jab==null,"Weapon switch cancellation");Require(owner.EquipItem(javelin),"Javelin restore");ClassGear.Refresh(owner);
        Ready(owner);NamedAmmunition.Quantities[javelin.Serial]=new(0,DateTime.MaxValue);Require(!ClassCombat.Available(owner,CombatAbility.Jab),"Empty javelin available");NamedAmmunition.Quantities[javelin.Serial]=new(quantity,DateTime.MaxValue);
        javelin.HitPoints=0;Require(!ClassCombat.Available(owner,CombatAbility.Jab),"Broken javelin available");javelin.HitPoints=javelin.MaxHitPoints;
        owner.AddToBackpack(javelin);var wrong=Equip(owner,new Halberd());Require(!ClassCombat.Available(owner,CombatAbility.Jab),"Polearm accepted as spear");wrong.Delete();Require(owner.EquipItem(javelin),"Final javelin restore");ClassGear.Refresh(owner);other.Delete();
    }
    private static void ProcDelivery(PlayerMobile owner)
    {
        Ready(owner);Require(ClassCreation.Profiles.TryTrain(owner.Serial,"amazon.innersight",out _),"Inner Sight coexistence");int mana=NecromancerEffects.ManaBalance256(owner);Require(!AmazonCombat.Release(owner,CombatAbility.Jab) && NecromancerEffects.ManaBalance256(owner)==mana,"Jab fell through to the unrelated Inner Sight release");Require(ClassCreation.Profiles.TryTrain(owner.Serial,"amazon.criticalstrike",out _),"Critical Strike training for actual Jab");var boots=Named(owner,"Goblin Toe");var target=Enemy(owner);
        PlayerMobile caster;_creating=true;try{caster=new PlayerMobile{Body=0x190,Name="JabOwner",Player=true};}finally{_creating=false;}
        caster.AddItem(new Backpack());ClassCreation.Profiles.Assign(caster,DiabloClass.Necromancer);caster.MoveToWorld(new(owner.X+8,owner.Y,owner.Z),owner.Map);
        Require(NecromancerEffects.Apply(caster,target,CombatAbility.LifeTap,1),"Independent Life Tap curse");
        var series=Begin(owner,target);PreparedWeaponImpact packet;
        using(WeaponHands.UseMelee(owner,CombatTiming.Equipped(owner)))
        {var values=SkillMechanics.Values(owner,CombatAbility.Jab);var damage=GearEffects.Damage(owner,0,target,(int)values.Eval("calc1"),_=>0);packet=GearEffects.PrepareWeaponImpact(owner,target,0,new Rolls(99),damage,projectileCritical:true);}
        int before=SkillHealth.Balance256(target),crush=NamedCombat.CrushingLoss256(target,false),physical=NamedElements.To256(packet.Parts.Physical),total=NamedElements.To256(packet.Parts.Total);
        int leech=(int)((long)Math.Min(physical,Math.Max(0,before-crush))*(ClassGear.Bonus(owner,"life-steal%")*64)/100*GemCombat.Drain(target)/100/64);
        // Ordinary melee now uses the independently source-checked FP256/6-bit leech path; do not floor to whole HP first.
        SkillHealth.SetBalance256(owner,10*256+17);int hp=SkillHealth.Balance256(owner);AmazonJab.Effects=new Rolls(0);
        Require(Hit(owner,target,series,0)==crush+total && before-SkillHealth.Balance256(target)==crush+total,"Actual Jab Critical Strike/quarter-life melee Crushing Blow/typed damage order");
        Require(SkillHealth.Balance256(owner)==Math.Min(owner.HitsMax*256,hp+physical/2+leech),"Actual Jab Life Tap then ordinary Titan leech, once each");
        AmazonJab.Effects=new Rolls(99);Ready(owner);owner.AddToBackpack(boots);ClassGear.Refresh(owner);target.Delete();caster.Delete();
    }
    private static void Lifecycle()
    {
        var owner=Fresh();Equip(owner,new Spear());Train(owner);var target=Enemy(owner);Ready(owner);var origin=owner.Location;
        owner.MoveToWorld(new(1432,1697,0),Map.Trammel);target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);int mana=NecromancerEffects.ManaBalance256(owner);
        Require(!ClassCombat.Perform(owner,CombatAbility.Jab,target,target.Location,Core.TickCount) && NecromancerEffects.ManaBalance256(owner)==mana,"Town Jab charged/cast");owner.MoveToWorld(origin,Map.Trammel);target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Ready(owner);var series=Begin(owner,target);int hp=SkillHealth.Balance256(target);Require(!ClassCreation.Profiles.TryActivateHistory(owner,DiabloClass.Paladin),"History switch bypassed ordinary active/equipped guard");Ready(owner);var weapon=CombatTiming.Equipped(owner);owner.AddToBackpack(weapon);Require(ClassCreation.Profiles.TryActivateHistory(owner,DiabloClass.Paladin),"Unequipped idle history exit");Require(owner.EquipItem(weapon),"Foreign history spear equip");ClassGear.Refresh(owner);Require(!ClassCombat.Available(owner,CombatAbility.Jab),"Foreign class retained Jab");owner.AddToBackpack(weapon);
        Require(ClassCreation.Profiles.TryActivateHistory(owner,DiabloClass.Amazon),"History restore");Require(owner.EquipItem(weapon),"Restored history spear equip");ClassGear.Refresh(owner);Ready(owner);Require(ClassCombat.Available(owner,CombatAbility.Jab),"Restored Jab missing");series=Begin(owner,target);ClassCreation.Profiles.TryGet(owner.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(owner,profile);Require(!ClassCombat.ResolveAction(owner,series.Until) && SkillHealth.Balance256(target)==hp,"Respec sequence continued");
        Train(owner);Ready(owner);series=Begin(owner,target);owner.Kill();Require(!ClassCombat.ResolveAction(owner,series.Until) && SkillHealth.Balance256(target)==hp && ClassCombat.For(owner).Jab==null,"Death sequence continued");target.Delete();owner.Delete();
    }
    private static void Persist(PlayerMobile owner,BaseWeapon weapon,Item helm)
    {
        Ready(owner);var target=Enemy(owner);var series=Begin(owner,target);Require(Hit(owner,target,series,0)>0,"Pre-save first Jab hit");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,weapon.Serial.Value,helm.Serial.Value,target.Serial.Value,owner.Location,NecromancerEffects.ManaBalance256(owner),SkillHealth.Balance256(target),weapon.HitPoints,NamedAmmunition.Remaining(weapon))));
        // Transient sessions are not serialized; pending strikes must not replay on load.
    }
    private static void Check()
    {
        var owner=Fresh();var spear=Equip(owner,new Spear());owner.AddToBackpack(spear);var javelin=(BaseWeapon)Named(owner,"Titan's Revenge");owner.AddToBackpack(javelin);var helm=Named(owner,"Tarnhelm");owner.AddToBackpack(helm);ClassGear.Refresh(owner);
        Differential(owner,spear,javelin);Cases(owner,spear,javelin,helm);ProcDelivery(owner);Lifecycle();Persist(owner,javelin,helm);
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var weapon=World.FindItem((Serial)saved.Weapon) as BaseWeapon;var target=World.FindMobile((Serial)saved.Target) as BaseCreature;
        Require(owner!=null && weapon?.Parent==owner && target!=null && NecromancerEffects.ManaBalance256(owner)==saved.Mana && SkillHealth.Balance256(target)==saved.Life && weapon.HitPoints==saved.Durability && NamedAmmunition.Remaining(weapon)==saved.Quantity,"Saved gear/mana/health/wear/quantity");
        Require(ClassCombat.Existing(owner)?.Jab==null && ClassCombat.Existing(owner)?.PendingAbility is null or CombatAbility.None,"Restart replayed a transient Jab sequence");
        target.Frozen=target.CantWalk=true;target.AIObject.AITimer.Stop();owner.MoveToWorld(saved.Origin,Map.Trammel);Require(SkillBindings.Instance.Get(owner).Left==CombatAbility.Jab,"Saved Jab left binding");
        Ready(owner);var series=Begin(owner,target);Require(Hit(owner,target,series,0)>0,"Post-load first hit");ClassCombat.ResolveAction(owner,series.Until);
        AmazonJab.Cancel(owner);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Mana=NecromancerEffects.ManaBalance256(owner),Life=SkillHealth.Balance256(target),Durability=weapon.HitPoints,Quantity=NamedAmmunition.Remaining(weapon)}));
    }
}
