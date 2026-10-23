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

public static class ChargedStrikeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ChargedStrikeChecks));
    private sealed class Rolls(int value):Random{public override int Next(int maximum)=>Math.Clamp(value,0,Math.Max(0,maximum-1));}
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Target,Point3D Origin,int Mana,int Life,int Durability,int Quantity,int Rank,ChargedStrikeFlight[] Flights);
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"charged-strike-fixtures.json");
    public static void Configure(){Freeze();Register("charged-strike-check",Check);Register("charged-strike-reload-check",Reload);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=a=>_creating || a.Name is "ChargedStrikeOwner" or "ChargedStrikeTarget"?TimeSpan.FromHours(1):hp?.Invoke(a)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=a=>_creating || a.Name=="ChargedStrikeOwner"?TimeSpan.FromHours(1):mp?.Invoke(a)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Charged Strike melee/emission, independent native bolts and reload",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}});
    private static void Controlled(Action action)
    {
        var hit=AmazonMelee.HitRoll;var element=AmazonMelee.ElementRoll;var physical=AmazonMelee.PhysicalRoll;var lightning=AmazonMelee.LightningRoll;var effects=AmazonMelee.Effects;var wear=AmazonMelee.WearRoll;
        try{AmazonMelee.HitRoll=()=>0;AmazonMelee.ElementRoll=()=>0;AmazonMelee.PhysicalRoll=_=>0;AmazonMelee.LightningRoll=_=>0;AmazonMelee.Effects=new Rolls(99);AmazonMelee.WearRoll=new Rolls(99);action();}
        finally{AmazonMelee.HitRoll=hit;AmazonMelee.ElementRoll=element;AmazonMelee.PhysicalRoll=physical;AmazonMelee.LightningRoll=lightning;AmazonMelee.Effects=effects;AmazonMelee.WearRoll=wear;}
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="ChargedStrikeOwner",Player=true};}finally{_creating=false;}
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
        BaseCreature target;_creating=true;try{target=new GraveyardZombie{Name="ChargedStrikeTarget"};}finally{_creating=false;}
        if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(target,difficulty);}
        target.Frozen=target.CantWalk=true;target.NoKillAwards=true;target.AIObject.AITimer.Stop();target.SetHits(50000);target.Hits=target.HitsMax;target.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);return target;
    }
    private static BaseWeapon Equip(PlayerMobile owner,BaseWeapon weapon)
    {if(CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(weapon),"Equip weapon");ClassGear.Refresh(owner);return weapon;}
    private static Item Named(PlayerMobile owner,string source)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);Require(owner.EquipItem(item),"Named "+source);ClassGear.Refresh(owner);return item;}
    private static ChargedStrikeFlight[] Flights(PlayerMobile owner)=>ChargedStrikeBolts.Snapshot().Where(x=>x.Owner==owner.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static void Replace(ChargedStrikeFlight[] flights,PlayerMobile owner)=>ChargedStrikeBolts.Restore(ChargedStrikeBolts.Snapshot().Where(x=>x.Owner!=owner.Serial.Value).Concat(flights).ToArray());
    private static AmazonMelee.Prepared Begin(PlayerMobile owner,Mobile target,int input=0)
    {
        long now=Core.TickCount;bool ok=input==1?ClassCombat.ContinueAttack(owner,CombatAbility.ChargedStrike,target,target.Location,now):input==2?ClassCombat.ContinueCast(owner,CombatAbility.ChargedStrike,target,target.Location,now):ClassCombat.Perform(owner,CombatAbility.ChargedStrike,target,target.Location,now);
        Require(ok && ClassCombat.For(owner).AmazonAttack!=null,"Charged Strike input");ClassCombat.ReleaseCast(owner);return ClassCombat.For(owner).AmazonAttack;
    }
    private static int Commit(PlayerMobile owner,Mobile target,AmazonMelee.Prepared prepared)
    {
        int before=SkillHealth.Balance256(target);Require(!ClassCombat.ResolveAction(owner,prepared.EffectAt-1),"Early strike");Require(ClassCombat.ResolveAction(owner,prepared.EffectAt),"Strike event");Require(!ClassCombat.ResolveAction(owner,prepared.EffectAt),"Duplicate event");return before-SkillHealth.Balance256(target);
    }
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"charged-strike-source-probe.json")));
        var owner=Fresh();owner.MoveToWorld(new(11,19,0),Map.Trammel);var definition=SkillMechanics.For(CombatAbility.ChargedStrike);int emitted=0,packets=0,seeds=0,ed=0;
        foreach(var row in data.RootElement.GetProperty("flow").EnumerateArray())
        {
            var input=row.GetProperty("input");int rank=input[4].GetInt32();var values=new SkillValues(definition,_=>rank,_=>0,99);
            if(input[0].GetInt32()!=1 || input[1].GetInt32()!=1){continue;}
            Require((input[2].GetInt32()==1?AmazonMelee.PhysicalBonus(CombatAbility.ChargedStrike,values,false):0)==row.GetProperty("result")[7].GetInt32(),"Compiled shared start6 calc1/physical ED overlap");ed++;
            if(input[3].GetInt32()!=215){continue;}
            int move=input[5].GetInt32();var origin=new Point3D(17+5*move,23-3*move,owner.Z);ChargedStrikeBolts.Cast(owner,origin,values);var flights=Flights(owner);var expected=row.GetProperty("packets");Require(flights.Length==expected.GetArrayLength(),"Compiled bolt count");
            for(int i=0;i<flights.Length;i++)
            {
                var p=expected[i];var f=flights[i];Require(f.OriginX*2==p[3].GetInt32() && f.OriginY*2==p[4].GetInt32() && f.Rank==p[8].GetInt32()
                    && f.Seed==ChargedBolts.SeedSubtile(p[5].GetInt32(),p[9].GetInt32()) && f.Direction==ChargedBolts.Heading(p[5].GetInt32()-p[3].GetInt32(),p[6].GetInt32()-p[4].GetInt32()),$"Compiled target-origin/reflected aim/rank/ordinal seed: input={input} packet={p} flight={f} owner={owner.Location}");packets++;
            }
            ChargedStrikeBolts.Cancel(owner);emitted++;
        }
        foreach(var row in data.RootElement.GetProperty("init").EnumerateArray())
        {Require(ChargedBolts.SeedSubtile(row[1].GetInt32(),row[2].GetInt32())==row[5].GetUInt32(),"Compiled aim-coordinate seed getter");seeds++;}
        int[] maxima=[30,42,54,66,78,90,102,114,130,146,162,178,194,210,226,242,262,282,302,322];
        for(int rank=1;rank<=20;rank++)
        {var v=new SkillValues(definition,_=>rank,_=>0,99);Require(v.Damage256(false)==256 && v.Damage256(true)==256*maxima[rank-1] && v.Mana256==(16+rank-1)*64 && v.Eval("calc1")==3+rank/5 && v.Formula("toht")==0,"Pinned Charged Strike golden");}
        Require(ed==128 && emitted==32 && packets==136 && seeds==45 && ChargedStrikeBolts.Frames==77,"Incomplete Charged Strike differential");owner.Delete();
        Log.Information("Charged Strike:128 native calc1 cases,32 native emission groups/136 packets,45 seed cases and20 rank goldens passed;512 compiled source flow cases remain bounded source evidence");
    }
    private static void Cases(PlayerMobile owner,BaseWeapon spear)
    {
        Require(!ClassCombat.Available(owner,CombatAbility.ChargedStrike),"Unlearned skill");Train(owner,"amazon.chargedstrike");Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.ChargedStrike) && SkillBindings.Instance.Assign(owner,false,CombatAbility.ChargedStrike) && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.ChargedStrike),"Bindings");
        var target=Enemy(owner);target.SetResistance(ResistanceType.Energy,0);int rolls=0;AmazonMelee.LightningRoll=span=>{rolls++;return span-1;};
        foreach(int input in new[]{1,2,0})
        {
            Ready(owner);int hp=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(owner);var p=Begin(owner,target,input);int expected=p.Impact.LifeDamage256.Value;
            Require(p.Hit && p.Lightning256>0 && SkillHealth.Balance256(target)==hp && Flights(owner).Length==0 && NecromancerEffects.ManaBalance256(owner)==mana-1024,"Start snapshot/payment/early damage");
            target.SetResistance(ResistanceType.Energy,100);Require(Commit(owner,target,p)==expected && Flights(owner).Length==3 && NecromancerEffects.ManaBalance256(owner)==mana-1024,"Melee snapshot, independent emission or repeated payment");target.SetResistance(ResistanceType.Energy,0);ChargedStrikeBolts.Cancel(owner);
        }
        Ready(owner);AmazonMelee.HitRoll=()=>1;int oldRolls=rolls;var miss=Begin(owner,target);Require(!miss.Hit && rolls==oldRolls && Commit(owner,target,miss)==0 && Flights(owner).Length==3,"Miss suppressed bolts or rolled direct lightning");ChargedStrikeBolts.Cancel(owner);AmazonMelee.HitRoll=()=>0;
        Ready(owner);NecromancerEffects.SetMana256(owner,17);var basic=Begin(owner,target);Require(basic.Basic && basic.Lightning256==0 && Commit(owner,target,basic)>0 && Flights(owner).Length==0 && NecromancerEffects.ManaBalance256(owner)==17,"Low-mana ordinary fallback emitted bolts/spent mana");
        Ready(owner);var prepared=Begin(owner,target);Require(ClassCombatHooks.MovementAllowed(owner) && HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount) && !ClassCombat.ResolveAction(owner,prepared.EffectAt) && Flights(owner).Length==0,"Interruptible A1/get-hit");
        Ready(owner);prepared=Begin(owner,target);ClassMovement.Moved(owner,Direction.South);Require(!ClassCombat.ResolveAction(owner,prepared.EffectAt) && Flights(owner).Length==0,"Accepted movement cancellation");
        Ready(owner);prepared=Begin(owner,target);ClassCombat.Stop(owner);Require(!ClassCombat.ResolveAction(owner,prepared.EffectAt),"Stop cancellation");
        Ready(owner);prepared=Begin(owner,target);owner.AddToBackpack(spear);Require(!ClassCombat.ResolveAction(owner,prepared.EffectAt) && Flights(owner).Length==0,"Weapon swap cancellation");Require(owner.EquipItem(spear),"Spear restore");ClassGear.Refresh(owner);
        Ready(owner);prepared=Begin(owner,target);target.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);Require(!ClassCombat.ResolveAction(owner,prepared.EffectAt) && Flights(owner).Length==0,"Range loss");target.Delete();
        target=Enemy(owner);Ready(owner);prepared=Begin(owner,target);target.Delete();var neighbour=Enemy(owner);int untouched=neighbour.Hits;Require(!ClassCombat.ResolveAction(owner,prepared.EffectAt) && neighbour.Hits==untouched && Flights(owner).Length==0,"Invalid target acquired a replacement");
        Ready(owner);SkillHealth.SetBalance256(neighbour,256);prepared=Begin(owner,neighbour);Require(Commit(owner,neighbour,prepared)>0 && !neighbour.Alive && Flights(owner).Length==3,"Same-release lethal melee suppressed source bolt emission");ChargedStrikeBolts.Cancel(owner);neighbour.Delete();
        var origin=owner.Location;Ready(owner);owner.MoveToWorld(new(1432,1697,0),Map.Trammel);target=Enemy(owner);int balance=NecromancerEffects.ManaBalance256(owner);Require(!ClassCombat.Perform(owner,CombatAbility.ChargedStrike,target,target.Location,Core.TickCount) && NecromancerEffects.ManaBalance256(owner)==balance,"Town gate/payment");target.Delete();owner.MoveToWorld(origin,Map.Trammel);AmazonMelee.LightningRoll=_=>0;
    }
    private static (BaseWeapon Weapon,Item Helm) Gear(PlayerMobile owner,BaseWeapon spear)
    {
        Ready(owner);owner.AddToBackpack(spear);var weapon=(BaseWeapon)Named(owner,"Titan's Revenge");var helm=Named(owner,"Griffon's Eye");Named(owner,"Goblin Toe");
        foreach(string id in new[]{"amazon.chargedstrike","amazon.powerstrike","amazon.lightningbolt","amazon.lightningstrike"}){Train(owner,id,20);}Train(owner,"amazon.criticalstrike");
        var target=Enemy(owner);Ready(owner);var values=SkillMechanics.Values(owner,CombatAbility.ChargedStrike);Require(values.Eval("EDmgSymPerCalc")==840 && values.Damage256(false)==256 && NamedElements.MasteryPercent(owner,"ltng")>0,"Actual hard-only synergies and Griffon");
        AmazonMelee.LightningRoll=span=>span-1;AmazonMelee.Effects=new Rolls(0);var prepared=Begin(owner,target);int quantity=NamedAmmunition.Remaining(weapon);
        using(WeaponHands.UseMelee(owner,weapon))
        {
            var raw=GearEffects.Damage(owner,0,target,(int)values.Eval("calc1"),_=>0);var expected=GearEffects.PrepareWeaponImpact(owner,target,0,new Rolls(99),raw,new(0,0,0,prepared.Lightning256/256.0,0,ColdMs:0),projectileCritical:true);
            Require(prepared.Impact.Parts==expected.Parts,"Weapon critical doubled lightning or skipped separate calc1 ED");
        }
        owner.AddToBackpack(helm);ClassGear.Refresh(owner);int before=SkillHealth.Balance256(target);AmazonMelee.Effects=new Rolls(99);Require(Commit(owner,target,prepared)==prepared.Impact.LifeDamage256,"Captured melee changed after gear removal");var flights=Flights(owner);
        var fresh=SkillMechanics.Values(owner,CombatAbility.ChargedStrike,prepared.Rank);Require(flights.Length==(int)fresh.Eval("calc1") && flights.All(f=>f.Rank==prepared.Rank && f.Min256==256 && f.Max256==fresh.Damage256(true)) && NamedAmmunition.Remaining(weapon)==quantity && !WeaponHands.Melee(weapon),"Bolt factory reused melee critical/gear snapshot or javelin ammunition");
        var snapshot=ChargedStrikeBolts.Snapshot();bool rejected=false;try{ChargedStrikeBolts.Restore(snapshot.Append(flights[0] with{Direction=8,Id=long.MaxValue}).ToArray());}catch(InvalidDataException){rejected=true;}Require(rejected && snapshot.SequenceEqual(ChargedStrikeBolts.Snapshot()),"Invalid restore erased valid flights");ChargedStrikeBolts.Cancel(owner);AmazonMelee.LightningRoll=_=>0;target.Delete();
        var other=Fresh();Equip(other,new Spear());Train(other,"amazon.chargedstrike");target=Enemy(other);Ready(other);Begin(other,target);ChargedStrikeBolts.Cast(other,target.Location,SkillMechanics.Values(other,CombatAbility.ChargedStrike));ClassCreation.Profiles.TryGet(other.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(other,profile);Require(!ClassCombat.ResolveAction(other,Core.TickCount+10000) && Flights(other).Length==0 && ClassCombat.For(other).AmazonAttack==null,"Respec retained bolts/attack");
        Equip(other,new Spear());Train(other,"amazon.chargedstrike");Ready(other);Begin(other,target);ChargedStrikeBolts.Cast(other,target.Location,SkillMechanics.Values(other,CombatAbility.ChargedStrike));other.Kill();Require(Flights(other).Length==0 && ClassCombat.For(other).AmazonAttack==null,"Death retained bolts/attack");target.Delete();other.Delete();return(weapon,helm);
    }
    private static void Natural(PlayerMobile owner,BaseWeapon weapon,Item helm)
    {
        var target=Enemy(owner);var behind=Enemy(owner,4);target.SetResistance(ResistanceType.Energy,0);int hp=SkillHealth.Balance256(target),untouched=SkillHealth.Balance256(behind);SkillHealth.SetBalance256(owner,10*256+17);int health=SkillHealth.Balance256(owner),mana=NecromancerEffects.ManaBalance256(owner);
        var caster=ClassSkillChecks.Player(DiabloClass.Necromancer);Require(NecromancerEffects.Apply(caster,target,CombatAbility.LifeTap,1),"Life Tap control");
        var oldRoll=ChargedStrikeBolts.DamageRoll;int rolls=0;ChargedStrikeBolts.DamageRoll=span=>{rolls++;return span-1;};var values=new SkillValues(SkillMechanics.For(CombatAbility.ChargedStrike),_=>1,_=>0,99);ChargedStrikeBolts.Cast(owner,target.Location,values);
        Require(Flights(owner).Length==3 && SkillHealth.Balance256(target)==hp,"Bolts hit synchronously");
        Later(1000,"charged-strike-native-bolts",()=>
        {
            try{Require(hp-SkillHealth.Balance256(target)==3*(30*256-1) && rolls==3 && SkillHealth.Balance256(behind)==untouched && SkillHealth.Balance256(owner)==health && NecromancerEffects.ManaBalance256(owner)==mana && Flights(owner).Length==0,"Independent upper-exclusive spell bolts inherited CB/leech/Life Tap/weapon damage, pierced, repaid or missed native collision");}
            finally{ChargedStrikeBolts.DamageRoll=oldRoll;ChargedStrikeBolts.Cancel(owner);target.Delete();behind.Delete();caster.Delete();}
        });
        Later(1100,"charged-strike-expiry-start",()=>{ChargedStrikeBolts.Cast(owner,new(owner.X+30,owner.Y+25,owner.Z),values);});
        Later(4500,"charged-strike-expiry",()=>{Require(Flights(owner).Length==0,"Natural bolt lifetime did not terminate");Persist(owner,weapon,helm);});
    }
    private static void Persist(PlayerMobile owner,BaseWeapon weapon,Item helm)
    {
        Ready(owner);SkillHealth.SetBalance256(owner,owner.HitsMax*256);var target=Enemy(owner,30);ChargedStrikeBolts.Cast(owner,new(owner.X+10,owner.Y,owner.Z),SkillMechanics.Values(owner,CombatAbility.ChargedStrike));
        Later(160,"charged-strike-result",()=>
        {
            var moving=Flights(owner);Require(moving.Length>0 && moving.All(f=>f.Frame>0 && (f.X!=f.OriginX || f.Y!=f.OriginY)),"No real moving bolt snapshot");var held=TimedEffects.Now.AddMinutes(10);Replace(moving.Select(f=>f with{NextUtc=held,UntilUtc=held.AddSeconds(10)}).ToArray(),owner);
            Controlled(()=>{var melee=Enemy(owner);Ready(owner);Begin(owner,melee);});
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,weapon.Serial.Value,helm.Serial.Value,target.Serial.Value,owner.Location,NecromancerEffects.ManaBalance256(owner),SkillHealth.Balance256(target),weapon.HitPoints,NamedAmmunition.Remaining(weapon),SkillMechanics.Rank(owner,CombatAbility.ChargedStrike),Flights(owner))));
        });
    }
    private static void Check(){Differential();var owner=Fresh();var spear=Equip(owner,new Spear());(BaseWeapon Weapon,Item Helm) gear=default;Controlled(()=>{Cases(owner,spear);gear=Gear(owner,spear);});Natural(owner,gear.Weapon,gear.Helm);}
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var weapon=World.FindItem((Serial)saved.Weapon) as BaseWeapon;var target=World.FindMobile((Serial)saved.Target) as BaseCreature;
        Require(owner!=null && weapon?.Parent==owner && target!=null && NecromancerEffects.ManaBalance256(owner)==saved.Mana && SkillHealth.Balance256(target)==saved.Life && weapon.HitPoints==saved.Durability && NamedAmmunition.Remaining(weapon)==saved.Quantity && SkillMechanics.Rank(owner,CombatAbility.ChargedStrike)==saved.Rank,"Saved resources, health, gear or rank");
        Require(Flights(owner).SequenceEqual(saved.Flights) && ClassCombat.Existing(owner)?.AmazonAttack==null && ClassCombat.Existing(owner)?.PendingAbility is null or CombatAbility.None,"Saved bolt payload changed or transient melee replayed");Require(SkillBindings.Instance.Get(owner).Left==CombatAbility.ChargedStrike && World.FindItem((Serial)saved.Helm)?.Parent==owner.Backpack,"Saved bindings/helm");owner.MoveToWorld(saved.Origin,Map.Trammel);target.Frozen=target.CantWalk=true;target.AIObject.AITimer.Stop();target.SetResistance(ResistanceType.Energy,0);
        var first=saved.Flights[0];target.MoveToWorld(new((int)Math.Round(first.X),(int)Math.Round(first.Y),first.Z),owner.Map);var resumed=first with{NextUtc=TimedEffects.Now,UntilUtc=TimedEffects.Now.AddSeconds(2)};Replace(saved.Flights.Select(f=>f.Id==first.Id?resumed:f).ToArray(),owner);
        Later(500,"charged-strike-reload-result",()=>
        {
            Require(SkillHealth.Balance256(target)<saved.Life && Flights(owner).All(f=>f.Id!=first.Id) && NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Restored moving bolt did not hit, terminate or repaid mana");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Life=SkillHealth.Balance256(target),Flights=Flights(owner)}));
        });
    }
}
