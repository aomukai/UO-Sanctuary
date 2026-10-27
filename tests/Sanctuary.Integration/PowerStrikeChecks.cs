// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary.Integration;

public static class PowerStrikeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PowerStrikeChecks));
    private sealed class Rolls(int value):Random{public override int Next(int maximum)=>Math.Clamp(value,0,Math.Max(0,maximum-1));}
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Target,Point3D Origin,int Mana,int Life,int Durability,int Quantity,int Rank);
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"power-strike-fixtures.json");
    public static void Configure(){Freeze();Register("power-strike-check",Check);Register("power-strike-reload-check",Reload);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=a=>_creating || a.Name is "PowerStrikeOwner" or "PowerStrikeTarget"?TimeSpan.FromHours(1):hp?.Invoke(a)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=a=>_creating || a.Name=="PowerStrikeOwner"?TimeSpan.FromHours(1):mp?.Invoke(a)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Power Strike current AR, prepared lightning/melee and persistence",_=>Core.LoopContext.Post(()=>
    {try{Controlled(action);Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Controlled(Action action)
    {
        var hit=AmazonMelee.HitRoll;var element=AmazonMelee.ElementRoll;var physical=AmazonMelee.PhysicalRoll;var lightning=AmazonMelee.LightningRoll;var effects=AmazonMelee.Effects;var wear=AmazonMelee.WearRoll;
        try{AmazonMelee.HitRoll=()=>0;AmazonMelee.ElementRoll=()=>0;AmazonMelee.PhysicalRoll=_=>0;AmazonMelee.LightningRoll=_=>0;AmazonMelee.Effects=new Rolls(99);AmazonMelee.WearRoll=new Rolls(99);action();}
        finally{AmazonMelee.HitRoll=hit;AmazonMelee.ElementRoll=element;AmazonMelee.PhysicalRoll=physical;AmazonMelee.LightningRoll=lightning;AmazonMelee.Effects=effects;AmazonMelee.WearRoll=wear;}
    }
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"power-strike-scalar-probe.json")));int count=0;
        var sources=SkillMechanics.All.ToDictionary(x=>x.SourceId);
        foreach(var row in data.RootElement.GetProperty("endpoints").EnumerateArray())
        {
            var source=sources[row[0].GetInt32()];int rank=row[1].GetInt32(),bonus=row[2].GetInt32(),mastery=row[3].GetInt32();
            var fields=new Dictionary<string,JsonElement>(source.SourceFields){["EDmgSymPerCalc"]=JsonSerializer.SerializeToElement(bonus)};
            var values=new SkillValues(source with{SourceFields=fields},_=>rank,_=>0,99);
            Require(AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery)==row[4].GetInt32() && AmazonMelee.EnhanceEndpoint(values.Damage256(true),mastery)==row[5].GetInt32(),"Compiled shared minimum/synergy/mastery differential "+source.SourceKey+" rank"+rank);count++;
        }
        using var flow=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"power-strike-flow-probe.json")));int schedules=0;
        foreach(var row in flow.RootElement.GetProperty("timing").EnumerateArray())
        {var timing=AmazonMelee.Timing(row[0].GetInt32()==1?"1HT":"2HT",row[1].GetInt32());Require(row[2].GetInt32()==2 && timing.EffectFrame==row[3].GetInt32() && timing.Frames==row[4].GetInt32(),"Compiled Amazon A1 timing");schedules++;}
        int[] maxima=[16,34,52,70,88,106,124,142,178,214,250,286,322,358,394,430,484,538,592,646];
        for(int rank=1;rank<=20;rank++)
        {var v=new SkillValues(SkillMechanics.For(CombatAbility.PowerStrike),_=>rank,_=>0,99);Require(v.Damage256(false)==256 && v.Damage256(true)==maxima[rank-1]*256 && v.Formula("toht")==20+12*(rank-1) && v.Mana256==(8+rank-1)*64,"Pinned Power Strike golden");}
        Require(count==8712 && schedules==12,"Incomplete Power Strike source differential");Log.Information("Power Strike:8712 compiled shared elemental endpoint cases,12 A1 schedules and20 rank goldens passed");
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="PowerStrikeOwner",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);
        while(owner.RawStr<110){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength");}
        while(owner.RawDex<130){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Dexterity");}
        FixtureSpace.Place(owner,new(2300,1100,0),Map.Trammel);Require(!SpellHelper.IsTown(owner.Location,owner),"Fixture town");Mana(owner);return owner;
    }
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(TalentCatalog.TryGet(DiabloClass.Amazon,id,out var d),"Missing talent");foreach(var prerequisite in d.Prerequisites){Train(owner,prerequisite);}Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}}
    private static void Mana(PlayerMobile owner)=>NecromancerEffects.SetMana256(owner,30*256+17);
    private static void Ready(PlayerMobile owner){AmazonMelee.Cancel(owner);ClassCombat.Stop(owner);var s=ClassCombat.For(owner);s.NextAction=s.RecoveryUntil=Core.TickCount;Mana(owner);}
    private static BaseCreature Enemy(PlayerMobile owner,int dx=1,MonsterDifficulty difficulty=MonsterDifficulty.Normal)
    {
        BaseCreature target;_creating=true;try{target=new GraveyardZombie{Name="PowerStrikeTarget"};}finally{_creating=false;}
        if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(target,difficulty);}
        target.Frozen=target.CantWalk=true;target.NoKillAwards=true;target.AIObject.AITimer.Stop();target.SetHits(50000);target.Hits=target.HitsMax;target.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);return target;
    }
    private static BaseWeapon Equip(PlayerMobile owner,BaseWeapon weapon)
    {if(CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(weapon),"Equip weapon");ClassGear.Refresh(owner);return weapon;}
    private static Item Named(PlayerMobile owner,string source)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);Require(owner.EquipItem(item),"Named "+source);ClassGear.Refresh(owner);return item;}
    private static AmazonMelee.Prepared Begin(PlayerMobile owner,Mobile target,int input=0)
    {
        long now=Core.TickCount;bool ok=input==1?ClassCombat.ContinueAttack(owner,CombatAbility.PowerStrike,target,target.Location,now):input==2?ClassCombat.ContinueCast(owner,CombatAbility.PowerStrike,target,target.Location,now):ClassCombat.Perform(owner,CombatAbility.PowerStrike,target,target.Location,now);
        Require(ok && ClassCombat.For(owner).AmazonAttack!=null,"Power Strike input");ClassCombat.ReleaseCast(owner);return ClassCombat.For(owner).AmazonAttack;
    }
    private static int Commit(PlayerMobile owner,Mobile target,AmazonMelee.Prepared prepared)
    {
        int before=SkillHealth.Balance256(target);Require(!ClassCombat.ResolveAction(owner,prepared.EffectAt-1),"Early prepared strike");Require(ClassCombat.ResolveAction(owner,prepared.EffectAt),"Prepared strike event");Require(!ClassCombat.ResolveAction(owner,prepared.EffectAt),"Duplicate event");return before-SkillHealth.Balance256(target);
    }
    private static void Cases(PlayerMobile owner,BaseWeapon spear)
    {
        Require(!ClassCombat.Available(owner,CombatAbility.PowerStrike),"Unlearned skill");Train(owner,"amazon.powerstrike");Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.PowerStrike) && SkillBindings.Instance.Assign(owner,false,CombatAbility.PowerStrike) && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.PowerStrike),"Power Strike bindings");
        var target=Enemy(owner);Ready(owner);int hp=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(owner),hitCalls=0,skillRolls=0;
        AmazonMelee.HitRoll=()=>{hitCalls++;return 0;};AmazonMelee.LightningRoll=span=>{skillRolls++;return span-1;};var prepared=Begin(owner,target,1);
        Require(prepared.Hit && !prepared.Basic && prepared.Lightning256==16*256-1 && SkillHealth.Balance256(target)==hp && NecromancerEffects.ManaBalance256(owner)==mana-512 && hitCalls==1 && skillRolls==1,"Start preparation/payment/exclusive lightning roll");
        int expected=prepared.Impact.LifeDamage256.Value;target.SetResistance(ResistanceType.Energy,100);Require(Commit(owner,target,prepared)==expected && hitCalls==1 && skillRolls==1 && NecromancerEffects.ManaBalance256(owner)==mana-512,"Prepared packet rerolled/repaid or late mitigation changed it");target.SetResistance(ResistanceType.Energy,0);
        Ready(owner);AmazonMelee.HitRoll=()=>1;int rolls=skillRolls;prepared=Begin(owner,target,2);Require(!prepared.Hit && skillRolls==rolls && Commit(owner,target,prepared)==0,"Miss rolled lightning or dealt damage");AmazonMelee.HitRoll=()=>0;AmazonMelee.LightningRoll=_=>0;
        Ready(owner);prepared=Begin(owner,target);Require(ClassCombatHooks.MovementAllowed(owner) && HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount),"Power Strike incorrectly copied Jab uninterruptibility");Require(ClassCombat.For(owner).AmazonAttack==null && !ClassCombat.ResolveAction(owner,prepared.EffectAt),"Hit recovery retained prepared attack");
        Ready(owner);prepared=Begin(owner,target);var origin=owner.Location;ClassMovement.Moved(owner,Direction.South);Require(ClassCombat.For(owner).AmazonAttack==null && !ClassCombat.ResolveAction(owner,prepared.EffectAt),"Accepted step failed to cancel");
        Ready(owner);prepared=Begin(owner,target);ClassCombat.Stop(owner);Require(!ClassCombat.ResolveAction(owner,prepared.EffectAt),"Stop failed to cancel");
        Ready(owner);prepared=Begin(owner,target);target.Delete();var other=Enemy(owner);hp=SkillHealth.Balance256(other);Require(!ClassCombat.ResolveAction(owner,prepared.EffectAt) && SkillHealth.Balance256(other)==hp,"Dead target transferred prepared packet");
        Ready(owner);prepared=Begin(owner,other);other.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);Require(!ClassCombat.ResolveAction(owner,prepared.EffectAt),"Out-of-range commit");other.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Ready(owner);prepared=Begin(owner,other);owner.AddToBackpack(spear);Require(!ClassCombat.ResolveAction(owner,prepared.EffectAt),"Weapon change retained packet");Require(owner.EquipItem(spear),"Spear restore");ClassGear.Refresh(owner);
        Ready(owner);NecromancerEffects.SetMana256(owner,17);mana=NecromancerEffects.ManaBalance256(owner);prepared=Begin(owner,other);Require(prepared.Basic && prepared.Lightning256==0 && NecromancerEffects.ManaBalance256(owner)==mana && Commit(owner,other,prepared)>0,"AttackNoMana ordinary melee fallback");
        Ready(owner);owner.MoveToWorld(new(1432,1697,0),Map.Trammel);other.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);mana=NecromancerEffects.ManaBalance256(owner);Require(!ClassCombat.Perform(owner,CombatAbility.PowerStrike,other,other.Location,Core.TickCount) && NecromancerEffects.ManaBalance256(owner)==mana,"Town cast/payment");owner.MoveToWorld(origin,Map.Trammel);other.Delete();
    }
    private static void ModernAr(PlayerMobile owner)
    {
        var target=Enemy(owner,difficulty:MonsterDifficulty.Hell);target.SetResistance(ResistanceType.Physical,0);Train(owner,"amazon.powerstrike",20);Ready(owner);
        int defense=NamedCombat.DefenseFor(owner,target,ClassStats.Defense(target),false);double normal=ClassStats.HitChance(ClassStats.AttackRatingAgainst(owner,target,0),defense,ClassStats.Level(owner),ClassStats.Level(target));double enhanced=ClassStats.HitChance(ClassStats.AttackRatingAgainst(owner,target,(int)SkillMechanics.Values(owner,CombatAbility.PowerStrike).Formula("toht")),defense,ClassStats.Level(owner),ClassStats.Level(target));
        Require(enhanced>normal,"No independent AR boundary");double mid=(normal+enhanced)/2;AmazonMelee.HitRoll=()=>mid;Require(ClassStats.CheckHit(owner,target,0,()=>mid)==false,"Ordinary AR control unexpectedly hit");var prepared=Begin(owner,target);Require(prepared.Hit && Commit(owner,target,prepared)>0,"Released2.6 Power Strike AR correction not applied");Ready(owner);NecromancerEffects.SetMana256(owner,17);var basic=Begin(owner,target);Require(basic.Basic && !basic.Hit && basic.Lightning256==0 && Commit(owner,target,basic)==0 && NecromancerEffects.ManaBalance256(owner)==17,"Low-mana fallback bypassed ordinary AR or spent mana");AmazonMelee.HitRoll=()=>0;target.Delete();
    }
    private static void GearAndSynergy(PlayerMobile owner,BaseWeapon spear,out BaseWeapon weapon,out Item helm)
    {
        Ready(owner);owner.AddToBackpack(spear);weapon=(BaseWeapon)Named(owner,"Titan's Revenge");helm=Named(owner,"Griffon's Eye");
        foreach(string id in new[]{"amazon.lightningbolt","amazon.chargedstrike","amazon.lightningstrike"}){Train(owner,id,20);}Train(owner,"amazon.criticalstrike");
        var values=SkillMechanics.Values(owner,CombatAbility.PowerStrike);Require(values.Eval("EDmgSymPerCalc")==840 && values.Damage256(false)==256,"Hard-only14% synergies or fixed minimum");int mastery=NamedElements.MasteryPercent(owner,"ltng");Require(mastery>0 && AmazonMelee.LightningRange(values,mastery).Min==256+256*mastery/100,"Actual Griffon mastery minimum");
        var target=Enemy(owner);int quantity=NamedAmmunition.Remaining(weapon);Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner),rank=SkillMechanics.Rank(owner,CombatAbility.PowerStrike);AmazonMelee.LightningRoll=span=>span-1;AmazonMelee.Effects=new Rolls(0);var prepared=Begin(owner,target);
        Require(prepared.Lightning256==AmazonMelee.LightningRange(values,mastery).Max-1 && NecromancerEffects.ManaBalance256(owner)==mana-(8+rank-1)*64 && prepared.Impact.Parts.Physical>0,"Actual gear/rank/lightning preparation");
        using(WeaponHands.UseMelee(owner,weapon))
        {var raw=GearEffects.Damage(owner,0,target,0,_=>0);var expected=GearEffects.PrepareWeaponImpact(owner,target,0,new Rolls(99),raw,new(0,0,0,prepared.Lightning256/256.0,0,ColdMs:0),projectileCritical:true);Require(prepared.Impact.Parts==expected.Parts,"Critical doubled skill lightning or mastery applied twice");}
        owner.AddToBackpack(helm);ClassGear.Refresh(owner);Require(Commit(owner,target,prepared)==prepared.Impact.LifeDamage256 && NamedAmmunition.Remaining(weapon)==quantity && !WeaponHands.Melee(weapon) && NamedCombat.Ranged(owner),"Captured gear packet changed, javelin debited or melee scope leaked");AmazonMelee.Effects=new Rolls(99);AmazonMelee.LightningRoll=_=>0;
        target.SetResistance(ResistanceType.Energy,100);Ready(owner);prepared=Begin(owner,target);Require(prepared.Impact.Parts.Lightning==0 && prepared.Impact.Parts.Physical>0 && Commit(owner,target,prepared)>0,"Lightning immune target lost ordinary physical weapon damage");target.Delete();
    }
    private static void ProcAndLifecycle(PlayerMobile owner)
    {
        Ready(owner);var boots=Named(owner,"Goblin Toe");var target=Enemy(owner);PlayerMobile caster;_creating=true;try{caster=new PlayerMobile{Body=0x190,Name="PowerStrikeOwner",Player=true};}finally{_creating=false;}
        caster.AddItem(new Backpack());ClassCreation.Profiles.Assign(caster,DiabloClass.Necromancer);caster.MoveToWorld(new(owner.X+8,owner.Y,owner.Z),owner.Map);Require(NecromancerEffects.Apply(caster,target,CombatAbility.LifeTap,1),"Independent Life Tap curse");
        AmazonMelee.LightningRoll=span=>span-1;var prepared=Begin(owner,target);int physical=NamedElements.To256(prepared.Impact.Parts.Physical),total=prepared.Impact.LifeDamage256.Value,crush=NamedCombat.CrushingLoss256(target,false);int leech=(int)((long)Math.Min(physical,Math.Max(0,SkillHealth.Balance256(target)-crush))*(ClassGear.Bonus(owner,"life-steal%")*64)/100*GemCombat.Drain(target)/100/64);
        // Ordinary melee now uses the independently source-checked FP256/6-bit leech path; do not floor to whole HP first.
        Require(prepared.Lightning256>physical,"Need a dominant lightning component to distinguish healing basis");SkillHealth.SetBalance256(owner,10*256+17);int hp=SkillHealth.Balance256(owner);AmazonMelee.Effects=new Rolls(0);Require(Commit(owner,target,prepared)==crush+total && SkillHealth.Balance256(owner)==Math.Min(owner.HitsMax*256,hp+physical/2+leech),"Prepared Power Strike melee CB/Life Tap/leech counted lightning, repeated critical or doubled healing");
        AmazonMelee.Effects=new Rolls(99);AmazonMelee.LightningRoll=_=>0;Ready(owner);owner.AddToBackpack(boots);ClassGear.Refresh(owner);target.Delete();caster.Delete();
        var other=Fresh();Equip(other,new Spear());Train(other,"amazon.powerstrike");target=Enemy(other);Ready(other);prepared=Begin(other,target);int life=SkillHealth.Balance256(target);ClassCreation.Profiles.TryGet(other.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(other,profile);Require(!ClassCombat.ResolveAction(other,prepared.EffectAt) && SkillHealth.Balance256(target)==life && ClassCombat.For(other).AmazonAttack==null,"Respec retained prepared Power Strike");
        Train(other,"amazon.powerstrike");Ready(other);prepared=Begin(other,target);other.Kill();Require(!ClassCombat.ResolveAction(other,prepared.EffectAt) && SkillHealth.Balance256(target)==life && ClassCombat.For(other).AmazonAttack==null,"Death retained prepared Power Strike");target.Delete();other.Delete();
    }
    private static void Persist(PlayerMobile owner,BaseWeapon weapon,Item helm)
    {
        Ready(owner);var target=Enemy(owner);var prepared=Begin(owner,target);int life=SkillHealth.Balance256(target);Require(prepared.Lightning256>0,"Saved prepared attack");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,weapon.Serial.Value,helm.Serial.Value,target.Serial.Value,owner.Location,NecromancerEffects.ManaBalance256(owner),life,weapon.HitPoints,NamedAmmunition.Remaining(weapon),SkillMechanics.Rank(owner,CombatAbility.PowerStrike))));
    }
    private static void Check(){Differential();var owner=Fresh();var spear=Equip(owner,new Spear());Cases(owner,spear);ModernAr(owner);GearAndSynergy(owner,spear,out var weapon,out var helm);ProcAndLifecycle(owner);Persist(owner,weapon,helm);}
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var weapon=World.FindItem((Serial)saved.Weapon) as BaseWeapon;var target=World.FindMobile((Serial)saved.Target) as BaseCreature;
        Require(owner!=null && weapon?.Parent==owner && target!=null && SkillHealth.Balance256(target)==saved.Life && NecromancerEffects.ManaBalance256(owner)==saved.Mana && weapon.HitPoints==saved.Durability && NamedAmmunition.Remaining(weapon)==saved.Quantity && SkillMechanics.Rank(owner,CombatAbility.PowerStrike)==saved.Rank,"Saved resource/gear/health/rank state");
        Require(ClassCombat.Existing(owner)?.AmazonAttack==null && ClassCombat.Existing(owner)?.PendingAbility is null or CombatAbility.None,"Transient prepared packet replayed across restart");Require(World.FindItem((Serial)saved.Helm)?.Parent==owner.Backpack && SkillBindings.Instance.Get(owner).Left==CombatAbility.PowerStrike,"Saved helm/binding");target.Frozen=target.CantWalk=true;target.AIObject.AITimer.Stop();owner.MoveToWorld(saved.Origin,Map.Trammel);
        Ready(owner);var prepared=Begin(owner,target);Require(Commit(owner,target,prepared)>0,"Post-load cast failed");File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Mana=NecromancerEffects.ManaBalance256(owner),Life=SkillHealth.Balance256(target),Durability=weapon.HitPoints,Quantity=NamedAmmunition.Remaining(weapon)}));
    }
}
