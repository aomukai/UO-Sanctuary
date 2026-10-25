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

public static class ImpaleChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ImpaleChecks));
    private sealed class Rolls(int value):Random{public int Calls;public override int Next(int maximum){Calls++;return Math.Clamp(value,0,Math.Max(0,maximum-1));}}
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Target,Point3D Origin,int Mana,int Life,int Durability,int Quantity,int Rank,ImpaleSlowState Slow);
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"impale-fixtures.json");
    public static void Configure(){Freeze();Register("impale-check",Check);Register("impale-reload-check",Reload);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=a=>_creating || a.Name is "ImpaleOwner" or "ImpaleTarget"?TimeSpan.FromHours(1):hp?.Invoke(a)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=a=>_creating || a.Name=="ImpaleOwner"?TimeSpan.FromHours(1):mp?.Invoke(a)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Impale wear, prepared physical, uninterruptible SQ8 and slow persistence",_=>Core.LoopContext.Post(()=>
    {try{Controlled(action);Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Controlled(Action action)
    {
        var hit=AmazonImpale.HitRoll;var element=AmazonImpale.ElementRoll;var physical=AmazonImpale.PhysicalRoll;var effects=AmazonImpale.Effects;var special=AmazonImpale.SpecialWearRoll;var wear=AmazonImpale.WearRoll;
        try{AmazonImpale.HitRoll=()=>0;AmazonImpale.ElementRoll=()=>0;AmazonImpale.PhysicalRoll=_=>0;AmazonImpale.Effects=new Rolls(99);AmazonImpale.SpecialWearRoll=new Rolls(99);AmazonImpale.WearRoll=new Rolls(99);action();}
        finally{AmazonImpale.HitRoll=hit;AmazonImpale.ElementRoll=element;AmazonImpale.PhysicalRoll=physical;AmazonImpale.Effects=effects;AmazonImpale.SpecialWearRoll=special;AmazonImpale.WearRoll=wear;}
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="ImpaleOwner",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);
        while(owner.RawStr<110){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength");}
        while(owner.RawDex<130){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Dexterity");}
        FixtureSpace.Place(owner,new(2300,1100,0),Map.Trammel);Require(!SpellHelper.IsTown(owner.Location,owner),"Fixture town");Mana(owner);return owner;
    }
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(TalentCatalog.TryGet(DiabloClass.Amazon,id,out var d),"Missing talent");foreach(var prerequisite in d.Prerequisites){Train(owner,prerequisite);}Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}}
    private static void Mana(PlayerMobile owner)=>NecromancerEffects.SetMana256(owner,30*256+17);
    private static void Ready(PlayerMobile owner){AmazonMelee.Cancel(owner);AmazonImpale.Cancel(owner);ClassCombat.Stop(owner);var s=ClassCombat.For(owner);s.NextAction=s.RecoveryUntil=Core.TickCount;Mana(owner);}
    private static BaseCreature Enemy(PlayerMobile owner,int dx=1,MonsterDifficulty difficulty=MonsterDifficulty.Normal)
    {
        BaseCreature target;_creating=true;try{target=new GraveyardZombie{Name="ImpaleTarget"};}finally{_creating=false;}
        if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(target,difficulty);}
        target.Frozen=target.CantWalk=true;target.NoKillAwards=true;target.AIObject.AITimer.Stop();target.SetHits(50000);target.Hits=target.HitsMax;target.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);return target;
    }
    private static BaseWeapon Equip(PlayerMobile owner,BaseWeapon weapon)
    {if(CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(weapon),"Equip weapon");ClassGear.Refresh(owner);return weapon;}
    private static Item Named(PlayerMobile owner,string source)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);Require(owner.EquipItem(item),"Named "+source);ClassGear.Refresh(owner);return item;}
    private static AmazonImpale.Attack Begin(PlayerMobile owner,Mobile target,int input=0)
    {
        long now=Core.TickCount;bool ok=input==1?ClassCombat.ContinueAttack(owner,CombatAbility.Impale,target,target.Location,now):input==2?ClassCombat.ContinueCast(owner,CombatAbility.Impale,target,target.Location,now):ClassCombat.Perform(owner,CombatAbility.Impale,target,target.Location,now);
        Require(ok && ClassCombat.For(owner).Impale!=null,"Impale input");ClassCombat.ReleaseCast(owner);return ClassCombat.For(owner).Impale;
    }
    private static int Commit(PlayerMobile owner,Mobile target,AmazonImpale.Attack attack)
    {
        int before=SkillHealth.Balance256(target);Require(!ClassCombat.ResolveAction(owner,attack.EffectAt-1),"Early Impale");Require(ClassCombat.ResolveAction(owner,attack.EffectAt),"Impale event");Require(!ClassCombat.ResolveAction(owner,attack.EffectAt),"Duplicate Impale event");return before-SkillHealth.Balance256(target);
    }
    private static void Differential(PlayerMobile owner,BaseWeapon spear)
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"impale-source-probe.json")));int schedules=0,wear=0;
        foreach(var row in data.RootElement.GetProperty("schedules").EnumerateArray())
        {var timing=AmazonImpale.Timing(row[0].GetInt32()==1?"1HT":"2HT",row[1].GetInt32());Require(timing.Frames==row[3].GetInt32() && timing.EffectFrame==row[2][0][0].GetInt32() && row[2][0][1].GetInt32()==0,"Compiled SQ8 schedule");schedules++;}
        var javelin=(BaseWeapon)NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Titan's Revenge").Id,99,new Random(173),true);owner.AddToBackpack(javelin);
        var steel=(BaseWeapon)NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Steelpillar").Id,99,new Random(173),true);owner.AddToBackpack(steel);Require(NamedGear.Indestructible(steel),"Actual indestructible source item");
        foreach(var row in data.RootElement.GetProperty("wear").EnumerateArray())
        {
            var input=row.GetProperty("input");int kind=input[4].GetInt32();if(kind is not(6 or 7 or 8)){continue;}var item=kind==6?steel:kind==7?spear:javelin;
            item.MaxHitPoints=5;item.HitPoints=input[5].GetInt32();if(kind==8){NamedAmmunition.Quantities[item.Serial]=new(input[6].GetInt32(),DateTime.SpecifyKind(DateTime.MaxValue,DateTimeKind.Utc));}
            AmazonImpale.SpecialWear(item,input[7].GetInt32(),1,new Rolls(input[8].GetInt32()));var output=row.GetProperty("output");
            Require(item.HitPoints==output[5].GetInt32() && (kind!=8 || NamedAmmunition.Remaining(item)==output[6].GetInt32()),"Compiled special wear/durability eligibility/quantity reset: "+row);wear++;
        }
        int[] slow=[11,20,27,33,37,41,44,46,49,51,53,54,56,57,58,60,60,61,62,63];
        for(int rank=1;rank<=20;rank++)
        {var v=new SkillValues(SkillMechanics.For(CombatAbility.Impale),_=>rank,_=>0,99);Require(v.Mana256==768 && v.Eval("calc1")==300+25*(rank-1) && v.Eval("calc3")==1 && -v.Eval("aurastatcalc1")==slow[rank-1] && v.Eval("auralencalc")==300,"Pinned Impale golden");}
        Require(schedules==12 && wear==864,"Incomplete Impale differential");spear.MaxHitPoints=30;spear.HitPoints=30;steel.Delete();javelin.Delete();
        Log.Information("Impale:12 native SQ8 schedules,864 applicable native special-wear comparisons and20 rank goldens passed;16/2592 source cases remain bounded separately");
    }
    private static void Cases(PlayerMobile owner,BaseWeapon spear)
    {
        Require(!ClassCombat.Available(owner,CombatAbility.Impale),"Unlearned Impale");Train(owner,"amazon.impale");Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.Impale) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Impale) && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Impale),"Impale bindings");
        var target=Enemy(owner);int hitRolls=0;AmazonImpale.HitRoll=()=>{hitRolls++;return 1;};
        foreach(int input in new[]{1,2,0})
        {
            Ready(owner);ImpaleSlow.Clear(target);int hp=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(owner);var attack=Begin(owner,target,input);
            Require(attack.Hit && !attack.Basic && hitRolls==0 && ImpaleSlow.Effect(target)==null && SkillHealth.Balance256(target)==hp && NecromancerEffects.ManaBalance256(owner)==mana-768,"Always-hit, delayed impact/slow or start payment");
            Require(AmazonImpale.Locked(owner,Core.TickCount) && !ClassCombatHooks.MovementAllowed(owner) && !HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount),"Paid Impale interrupted by voluntary step/get-hit");ClassCombat.Stop(owner);Require(ClassCombat.For(owner).Impale!=null,"Stop cancelled committed Impale");
            AmazonImpale.Effects=new Rolls(99);target.SetResistance(ResistanceType.Physical,100);Require(Commit(owner,target,attack)==attack.Impact.LifeDamage256 && NecromancerEffects.ManaBalance256(owner)==mana-768 && ImpaleSlow.Effect(target) is{Velocity:11,Attack:11,Other:11},"Captured physical changed, slow missing or repayment");target.SetResistance(ResistanceType.Physical,0);
            Require(AmazonImpale.Locked(owner,attack.Until-1) && !AmazonImpale.Locked(owner,attack.Until) && ClassCombat.ResolveAction(owner,attack.Until) && ClassCombat.For(owner).Impale==null,"Recovery lock/end");
        }
        Ready(owner);NecromancerEffects.SetMana256(owner,17);int life=SkillHealth.Balance256(target);var basic=Begin(owner,target);Require(basic.Basic && !basic.Hit && hitRolls==1 && !AmazonImpale.Locked(owner,Core.TickCount) && ClassCombatHooks.MovementAllowed(owner) && Commit(owner,target,basic)==0 && SkillHealth.Balance256(target)==life && NecromancerEffects.ManaBalance256(owner)==17,"Basic fallback bypassed ordinary miss, lock or mana");
        Ready(owner);AmazonImpale.HitRoll=()=>0;NecromancerEffects.SetMana256(owner,17);ImpaleSlow.Clear(target);basic=Begin(owner,target);Require(basic.Basic && basic.Hit && Commit(owner,target,basic)>0 && ImpaleSlow.Effect(target)==null,"Basic fallback retained skill slow/damage");
        Ready(owner);var prepared=Begin(owner,target);ClassMovement.Moved(owner,Direction.South);Require(ClassCombat.For(owner).Impale==null && !ClassCombat.ResolveAction(owner,prepared.EffectAt),"Accepted external step did not cancel");
        Ready(owner);prepared=Begin(owner,target);owner.AddToBackpack(spear);Require(!ClassCombat.ResolveAction(owner,prepared.EffectAt),"Manual weapon removal retained packet");Require(owner.EquipItem(spear),"Spear restore");ClassGear.Refresh(owner);
        Ready(owner);prepared=Begin(owner,target);target.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);Require(Commit(owner,target,prepared)==0 && ImpaleSlow.Effect(target)!=null,"Source target state must precede separate failed range damage");target.Delete();
        target=Enemy(owner);Ready(owner);prepared=Begin(owner,target);target.Delete();var other=Enemy(owner);life=SkillHealth.Balance256(other);Require(ClassCombat.ResolveAction(owner,prepared.EffectAt) && SkillHealth.Balance256(other)==life && ImpaleSlow.Effect(other)==null,"Dead target redirected strike");other.Delete();
        var origin=owner.Location;Ready(owner);owner.MoveToWorld(new(1432,1697,0),Map.Trammel);target=Enemy(owner);int balance=NecromancerEffects.ManaBalance256(owner);Require(!ClassCombat.Perform(owner,CombatAbility.Impale,target,target.Location,Core.TickCount) && NecromancerEffects.ManaBalance256(owner)==balance,"Town gate/payment");target.Delete();owner.MoveToWorld(origin,Map.Trammel);
    }
    private static void Depletion(PlayerMobile owner,BaseWeapon spear)
    {
        var target=Enemy(owner);Ready(owner);spear.HitPoints=1;AmazonImpale.SpecialWearRoll=new Rolls(0);var attack=Begin(owner,target);
        Require(spear.HitPoints==0 && !spear.Deleted && spear.Parent==owner && attack.OwnDepletion && attack.Impact.Parts.Physical>0 && CombatTiming.Equipped(owner)==null && AmazonImpale.Locked(owner,Core.TickCount),"Start-time break removed packet/lock or kept weapon active");
        Require(Commit(owner,target,attack)==attack.Impact.LifeDamage256 && ImpaleSlow.Effect(target)!=null,"Committed broken-spear strike disappeared");ClassCombat.ResolveAction(owner,attack.Until);Require(!ClassCombat.Available(owner,CombatAbility.Impale),"Broken weapon allowed a fresh attack");spear.HitPoints=spear.MaxHitPoints;ClassGear.Refresh(owner);
        Ready(owner);spear.HitPoints=5;AmazonImpale.SpecialWearRoll=new Rolls(0);AmazonImpale.WearRoll=new Rolls(0);attack=Begin(owner,target);Require(spear.HitPoints==4,"Special start wear missing");Commit(owner,target,attack);Require(spear.HitPoints==3,"Ordinary hit wear was replaced by special wear");
        Ready(owner);owner.AddToBackpack(spear);var titan=(BaseWeapon)Named(owner,"Titan's Revenge");titan.HitPoints=1;NamedAmmunition.Quantities[titan.Serial]=new(1,DateTime.SpecifyKind(DateTime.MaxValue,DateTimeKind.Utc));AmazonImpale.WearRoll=new Rolls(99);attack=Begin(owner,target);
        Require(attack.OwnDepletion && NamedAmmunition.Remaining(titan)==0 && titan.HitPoints==titan.MaxHitPoints && AmazonImpale.Locked(owner,Core.TickCount),"Last javelin quantity/durability/commit lock");Require(Commit(owner,target,attack)==attack.Impact.LifeDamage256,"Last-javelin prepared strike disappeared");ClassCombat.ResolveAction(owner,attack.Until);Require(!ClassCombat.Available(owner,CombatAbility.Impale),"Empty javelin allowed fresh skill");
        owner.AddToBackpack(titan);titan.Delete();Require(owner.EquipItem(spear),"Spear restore after depletion");ClassGear.Refresh(owner);AmazonImpale.SpecialWearRoll=new Rolls(99);AmazonImpale.WearRoll=new Rolls(99);target.Delete();
    }
    private static (BaseWeapon Weapon,Item Helm) Gear(PlayerMobile owner,BaseWeapon spear)
    {
        Ready(owner);owner.AddToBackpack(spear);var weapon=(BaseWeapon)Named(owner,"Titan's Revenge");var helm=Named(owner,"Tarnhelm");Named(owner,"Highlord's Wrath");Named(owner,"Ghoulhide");Train(owner,"amazon.impale",20);Train(owner,"amazon.criticalstrike",20);
        var target=Enemy(owner);Ready(owner);AmazonImpale.Effects=new Rolls(0);var attack=Begin(owner,target);var values=SkillMechanics.Values(owner,CombatAbility.Impale);
        using(WeaponHands.UseMelee(owner,weapon))
        {
            var raw=GearEffects.Damage(owner,0,null,(int)values.Eval("calc1"),_=>0);var expected=GearEffects.PrepareWeaponImpact(owner,target,0,new Rolls(0),raw,projectileCritical:false);
            Require(attack.Impact.Parts==expected.Parts && AmazonCombat.CriticalChance(owner)>0 && ClassGear.Bonus(owner,"deadly")>0,"Impale inherited ordinary critical or lost actual gear ED");
        }
        owner.AddToBackpack(helm);ClassGear.Refresh(owner);AmazonImpale.Effects=new Rolls(99);Require(Commit(owner,target,attack)==attack.Impact.LifeDamage256,"Gear removal changed prepared packet");
        Ready(owner);
        target.Delete();var other=Fresh();Equip(other,new Spear());Train(other,"amazon.impale");target=Enemy(other);Ready(other);var pending=Begin(other,target);ClassCreation.Profiles.TryGet(other.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(other,profile);Require(ClassCombat.For(other).Impale==null && !ClassCombat.ResolveAction(other,pending.EffectAt),"Respec retained Impale");
        Equip(other,new Spear());Train(other,"amazon.impale");Ready(other);pending=Begin(other,target);other.Kill();Require(ClassCombat.For(other).Impale==null && !ClassCombat.ResolveAction(other,pending.EffectAt),"Death retained Impale");other.Delete();target.Delete();return(weapon,helm);
    }
    private static void SlowCases(PlayerMobile owner)
    {
        var target=Enemy(owner);target.SetResistance(ResistanceType.Cold,100);var low=new SkillValues(SkillMechanics.For(CombatAbility.Impale),_=>1,_=>0,99);var high=new SkillValues(SkillMechanics.For(CombatAbility.Impale),_=>20,_=>0,99);var before=TimedEffects.Now;ImpaleSlow.Apply(owner,target,high);
        Require(ImpaleSlow.Effect(target) is{Velocity:63,Attack:63,Other:63} && ImpaleSlow.Effect(target).UntilUtc>=before.AddSeconds(12) && ImpaleSlow.Effect(target).UntilUtc<=TimedEffects.Now.AddSeconds(12) && GearEffects.MovementSlow(target)==63 && GearEffects.AttackSlow(target)==63,"Source slow, cold-immunity independence or duration");
        var other=Fresh();ImpaleSlow.Apply(other,target,low);Require(ImpaleSlow.Effect(target) is{Velocity:11,Attack:11,Other:11} && ImpaleSlow.Effect(target).Source==other.Serial.Value,"Weaker different caster did not replace generic target state");other.Delete();Require(ImpaleSlow.Effect(target)!=null,"Caster removal erased independent target state");
        var curse=ClassSkillChecks.Player(DiabloClass.Necromancer);Require(NecromancerEffects.Apply(curse,target,CombatAbility.Decrepify,1),"Decrepify coexistence");Require(ImpaleSlow.Effect(target).Velocity==11 && GearEffects.MovementSlow(target)>=61 && GearEffects.AttackSlow(target)>=61,"Ordinary curse overwrote independent Impale slow");
        var expiry=ImpaleSlow.Snapshot().Select(s=>s.Target==target.Serial.Value?s with{UntilUtc=TimedEffects.Now.AddMilliseconds(80)}:s).ToArray();ImpaleSlow.Restore(expiry);
        Timer.StartTimer(TimeSpan.FromMilliseconds(250),()=>{try{Require(ImpaleSlow.Effect(target)==null && NecromancerEffects.Curse(target)?.Ability==CombatAbility.Decrepify,"Slow expiry erased curse or persisted");Log.Information("impale-slow-expiry: passed");}catch(Exception ex){Log.Error(ex,"impale-slow-expiry: failed");}finally{target.Delete();curse.Delete();}});
    }
    private static void Persist(PlayerMobile owner,BaseWeapon weapon,Item helm)
    {
        var target=Enemy(owner);Ready(owner);var attack=Begin(owner,target);Commit(owner,target,attack);ClassCombat.ResolveAction(owner,attack.Until);var held=TimedEffects.Now.Add(FixtureSpace.Retention);
        ImpaleSlow.Restore(ImpaleSlow.Snapshot().Select(s=>s.Target==target.Serial.Value?s with{UntilUtc=held}:s).ToArray());Ready(owner);Begin(owner,target);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,weapon.Serial.Value,helm.Serial.Value,target.Serial.Value,owner.Location,NecromancerEffects.ManaBalance256(owner),SkillHealth.Balance256(target),weapon.HitPoints,NamedAmmunition.Remaining(weapon),SkillMechanics.Rank(owner,CombatAbility.Impale),ImpaleSlow.Effect(target))));
    }
    private static void Check(){var owner=Fresh();var spear=Equip(owner,new Spear());Differential(owner,spear);Cases(owner,spear);Depletion(owner,spear);var gear=Gear(owner,spear);SlowCases(owner);Persist(owner,gear.Weapon,gear.Helm);}
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var weapon=World.FindItem((Serial)saved.Weapon) as BaseWeapon;var target=World.FindMobile((Serial)saved.Target) as BaseCreature;
        Require(owner!=null && weapon?.Parent==owner && target!=null && NecromancerEffects.ManaBalance256(owner)==saved.Mana && SkillHealth.Balance256(target)==saved.Life && weapon.HitPoints==saved.Durability && NamedAmmunition.Remaining(weapon)==saved.Quantity && SkillMechanics.Rank(owner,CombatAbility.Impale)==saved.Rank,"Saved Impale gear/resources/target/rank");
        Require(ImpaleSlow.Effect(target)==saved.Slow && GearEffects.MovementSlow(target)>=saved.Slow.Velocity && ClassCombat.Existing(owner)?.Impale==null && ClassCombat.Existing(owner)?.PendingAbility is null or CombatAbility.None,"Slow payload changed or transient Impale replayed");Require(SkillBindings.Instance.Get(owner).Left==CombatAbility.Impale && World.FindItem((Serial)saved.Helm)?.Parent==owner.Backpack,"Saved bindings/soft rank item");
        owner.MoveToWorld(saved.Origin,Map.Trammel);target.Frozen=target.CantWalk=true;target.AIObject.AITimer.Stop();Ready(owner);var attack=Begin(owner,target);Require(Commit(owner,target,attack)>0,"Post-load Impale failed");ClassCombat.ResolveAction(owner,attack.Until);
        var held=TimedEffects.Now.Add(FixtureSpace.Retention);ImpaleSlow.Restore(ImpaleSlow.Snapshot().Select(s=>s.Target==target.Serial.Value?s with{UntilUtc=held}:s).ToArray());File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Mana=NecromancerEffects.ManaBalance256(owner),Life=SkillHealth.Balance256(target),Durability=weapon.HitPoints,Quantity=NamedAmmunition.Remaining(weapon),Slow=ImpaleSlow.Effect(target)}));
    }
}
