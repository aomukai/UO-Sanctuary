// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class DragonClawPreparationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DragonClawPreparationChecks));
    private sealed record Saved(uint Owner,uint Primary,uint Secondary,uint Helm,int Mana256,int Life256,int PhysicalPercent,string PrimaryState,string SecondaryState,DragonClawClockPlan Plan,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dragon-claw-preparation-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {
        var mana=Mobile.ManaRegenRateHandler;var life=Mobile.HitsRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>Creating||p.Name=="DragonClawClockFixture"?FixtureSpace.Retention:mana?.Invoke(p)??Mobile.DefaultManaRate;
        Mobile.HitsRegenRateHandler=p=>Creating||p.Name=="DragonClawClockFixture"?FixtureSpace.Retention:life?.Invoke(p)??Mobile.DefaultHitsRate;
    }
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"assassin-claw-modern-rate-probe.json")));int rates=0,unequal=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();if(row[0].GetString()!="DRAGON_CLAW"||V(1)!=1){continue;}
            Require(DragonClawPreparation.Advance(V(2),V(3),V(4)+V(6),V(5)+V(6),V(8),V(7))==V(12)&&V(9)==256&&V(10)==16,"Original dualOnly rate context "+rates);
            if(V(2)!=V(3)||V(4)!=V(5)){unequal++;}rates++;
        }
        using var seq=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"assassin-claw-sequence-source-probe.json")));int clocks=0;
        foreach(var row in seq.RootElement.EnumerateArray())
        {
            int speed=row[1].GetInt32();if(row[0].GetInt32()!=1||speed is <38 or >448){continue;}
            var plan=DragonClawPreparation.Plan(speed);var events=row[9].EnumerateArray().ToArray();
            Require(plan.Profile==DragonClawPreparation.Profile&&plan.Contacts.SequenceEqual(events[..^1].Select(x=>x[1].GetInt32()))&&plan.EndFrame==events[^1][1].GetInt32(),"Original dual sequence event schedule");clocks++;
        }
        Require(rates==1152&&unequal==1128&&clocks==192,"Incomplete dual rate/clock controls");
        var ordinary=DragonClawPreparation.Plan(256);Require(ordinary.Contacts.SequenceEqual(new[]{6,10})&&ordinary.EndFrame==16,"Dual sequence baseline");ordinary.Contacts[0]=999;Require(DragonClawPreparation.Plan(256).Contacts[0]==6,"Prepared plan array alias");
        foreach(int rate in new[]{0,37,449}){bool refused=false;try{DragonClawPreparation.Plan(rate);}catch(NotSupportedException){refused=true;}Require(refused,"Unknown prepared Dragon Claw rate admitted");}
        Log.Information("Dragon Claw prepared clock:1152eligible original-author dualOnly rate controls/{Clocks}complete dual source clocks;single-claw source controls do not authorize eligibility;current executable timing and finisher policy remain unverified",clocks);
    }
    public static void Configure()
    {
        Rates();Register("dragon-claw-preparation-check",Check);Register("dragon-claw-preparation-reload-check",Reload);
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Dragon Claw dual timing/actual stat collectors/original persistence suppliers",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Check()
    {
        Differential();PlayerMobile p;Creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Assassin);}finally{Creating=false;}
        p.Name="DragonClawClockFixture";p.AddItem(new Backpack());
        while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}
        Require(!DragonClawPreparation.WeaponAllowed(p),"Unarmed dual finisher eligible");
        var primary=new Tekagi();bool found=false;
        for(int seed=0;seed<500;seed++){ClassGear.Profiles.Convert(primary,99,GearRarity.Magic,new Random(seed));if(ClassGear.Bonus(primary,"swing1")+ClassGear.Bonus(primary,"swing2")+ClassGear.Bonus(primary,"swing3")>0){found=true;break;}}
        Require(found,"Whole actual IAS affix claw");p.AddToBackpack(primary);Require(p.EquipItem(primary),"Equip actual primary");ClassGear.Refresh(p);Require(!DragonClawPreparation.WeaponAllowed(p),"Single claw finisher eligible");
        var secondary=(BaseWeapon)NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Cutthroat1").Id,99,new Random(173),true);p.AddToBackpack(secondary);Require(p.EquipItem(secondary),"Equip complete Bartuc offhand");ClassGear.Refresh(p);
        Require(WeaponHands.Pair(p) is{} pair&&pair.Primary==primary&&pair.Secondary==secondary&&DragonClawPreparation.WeaponAllowed(p),"Actual dual-claw identity");
        int first=CombatTiming.Ias(p),second;using(WeaponHands.Use(p,secondary)){second=CombatTiming.Ias(p);}
        Require(first>second&&CombatTiming.Weapon(primary).Modifier!=CombatTiming.Weapon(secondary).Modifier,"Actual IAS/WSM pair fails to distinguish hands");
        var plan=DragonClawPreparation.Plan(p);Require(plan.Advance==DragonClawPreparation.Advance(CombatTiming.Weapon(primary).Modifier,CombatTiming.Weapon(secondary).Modifier,first,second,GearEffects.AttackSlowTotal(p),CombatTiming.SkillAttackRate(p)),"Actual selected-hand collectors disagree with dual projection");
        using(WeaponHands.Use(p,secondary)){Require(Json(DragonClawPreparation.Plan(p))==Json(plan)&&CombatTiming.Equipped(p)==secondary,"Outer secondary scope corrupted rate or was not restored");}Require(CombatTiming.Equipped(p)==primary,"Dual rate leaked selected hand");
        int rank=SkillMechanics.Rank(p,CombatAbility.DragonClaw);Require(DragonClawPreparation.PhysicalPercent(p)==50+15*(rank-1)+4,"Current calc1/effective finisher and hard-only mastery synergy");
        ClassSkillChecks.Train(p,"assassin.clawmastery",10);Require(DragonClawPreparation.PhysicalPercent(p)==50+15*(rank-1)+40,"Hard mastery did not affect source damage formula");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Actual +skill helmet");ClassGear.Refresh(p);
        Require(DragonClawPreparation.PhysicalPercent(p)==50+15*rank+40,"Soft mastery incorrectly contributes extra synergy or finisher +skill ignored");
        NecromancerEffects.SetMana256(p,20*256+31);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-257);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(p),wear=primary.HitPoints,other=secondary.HitPoints;
        plan=DragonClawPreparation.Plan(p);Require(AssassinCombat.Implemented(CombatAbility.DragonClaw)&&ClassCombat.Available(p,CombatAbility.DragonClaw)&&SkillBindings.Instance.Assign(p,false,CombatAbility.DragonClaw)&&!ClassCombat.Perform(p,CombatAbility.DragonClaw,null,p.Location,Core.TickCount),"Approved route availability or empty-target refusal");
        Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(p)==life&&primary.HitPoints==wear&&secondary.HitPoints==other,"Preparation paid/healed/wore original items");
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,primary.Serial.Value,secondary.Serial.Value,helm.Serial.Value,mana,life,DragonClawPreparation.PhysicalPercent(p),Json(ClassGear.State(primary)),Json(ClassGear.State(secondary)),plan)));
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(p!=null&&World.FindItem((Serial)saved.Primary)?.Parent==p&&World.FindItem((Serial)saved.Secondary)?.Parent==p&&World.FindItem((Serial)saved.Helm)?.Parent==p,"Original dual context missing on reload");
        Require(WeaponHands.Pair(p)?.Primary.Serial.Value==saved.Primary&&WeaponHands.Pair(p)?.Secondary.Serial.Value==saved.Secondary,"Saved selected pair identity changed");
        Require(Json(ClassGear.State(World.FindItem((Serial)saved.Primary)))==saved.PrimaryState&&Json(ClassGear.State(World.FindItem((Serial)saved.Secondary)))==saved.SecondaryState,"Whole original item rolls changed");
        Require(NecromancerEffects.ManaBalance256(p)==saved.Mana256&&SkillHealth.Balance256(p)==saved.Life256&&DragonClawPreparation.PhysicalPercent(p)==saved.PhysicalPercent&&Json(DragonClawPreparation.Plan(p))==Json(saved.Plan),"Saved wounds/mana/ranks/dual clock changed");
        Require(ClassCombat.For(p).PendingAbility==CombatAbility.None&&AssassinCombat.Implemented(CombatAbility.DragonClaw),"Prepared component replayed or approved handler missing");
        File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
    }
}
