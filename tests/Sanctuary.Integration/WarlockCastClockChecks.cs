// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class WarlockCastClockChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(WarlockCastClockChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"warlock-cast-clock-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {string name=reload?"warlock-cast-clock-reload-check":"warlock-cast-clock-check";ConsoleInputHandler.RegisterCommand(name,"Owned Warlock ordinary SC projection over shared source scheduling",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static void Player(bool female,bool boosted)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Female=female;p.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"warlock.miasmabolt",1);
        try
        {
            while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Warlock fixture Strength");}
            FixtureSpace.Corridor(p,6680,20,3480);NecromancerEffects.SetMana256(p,100*256);ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;
            if(boosted){var item=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Magefist").Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Warlock FCR equipment refused");ClassGear.Refresh(p);}
            int fcr=boosted?20:0;Require(CombatTiming.Fcr(p)==fcr,"Actual Warlock/Witch FCR differs");var expected=WarlockCastClock.Calculate(fcr);var world=NpcWorldAllocation.Capture();int mana=NecromancerEffects.ManaBalance256(p);
            Require(!ClassCombat.Available(p,CombatAbility.MiasmaBolt)&&SkillCombat.BeginPrepared(p,CombatAbility.MiasmaBolt,Serial.Zero,new(p.X+3,p.Y,p.Z),Core.TickCount,(_,_,_,_)=>true,(_,a)=>a==CombatAbility.MiasmaBolt,CombatTiming.For(p,CombatAbility.MiasmaBolt)),"Prepared ordinary SC start failed or timing enabled missing spell");
            Require(s.Timing==expected&&s.Animation==16&&s.EffectAt==s.NextAction-expected.DurationMs+expected.EffectMs&&NecromancerEffects.ManaBalance256(p)==mana&&NpcWorldAllocation.Capture()==world,"Warlock prepared windup lost clock or paid/allocated early");
            ClassCombat.Stop(p);Require(s.PendingAbility==CombatAbility.None&&NecromancerEffects.ManaBalance256(p)==mana&&NpcWorldAllocation.Capture()==world,"Prepared clock cancellation emitted or paid");
        }
        finally{ClassCombat.Stop(p);p.Delete();}
    }
    private static void Check(bool reload)
    {
        int generation=reload?int.Parse(File.ReadAllText(Manifest))+1:0;
        using var reference=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"warlock-shared-sc-cases.json")));int controls=0,routes=0;
        foreach(var c in reference.RootElement.EnumerateArray())
        {
            var plan=WarlockCastClock.Calculate(c[1].GetInt32());Require(plan.EffectFrame==c[3][0][1].GetInt32()&&plan.Frames==c[3][1][1].GetInt32(),"Owned identical WKSC clock differs from complete source controls");controls++;
        }
        var skills=SkillMechanics.All.Where(r=>WarlockCastClock.Handles(r.Ability)).ToArray();Require(controls==2072&&skills.Length==22,"Warlock ordinary SC scope differs");
        foreach(var skill in skills)for(int i=0;i<259;i++)
        {
            int fcr=i<256?i:i==256?500:i==257?1000:10000;var expected=WarlockCastClock.Calculate(fcr);
            Require(CombatTiming.Calculate(DiabloClass.Warlock,skill.Ability,new(AttackFamily.Bow,-20),90,fcr,85,70)==expected,"Ordinary Warlock SC inherited weapon/IAS/chill instead of FCR");routes++;
        }
        foreach(var a in new[]{CombatAbility.BindDemon,CombatAbility.Cleave,CombatAbility.MirroredBlades,CombatAbility.MiasmaChain,CombatAbility.DemonicMastery,CombatAbility.BloodOath,CombatAbility.EnhancedEntropy,CombatAbility.LevitationMastery})
        {Require(!WarlockCastClock.Handles(a),"Warlock SC captured separate sequence/attack-rate/passive context");}
        Require(WarlockCastClock.Calculate(0)==new ActionTiming(16,8)&&WarlockCastClock.Calculate(20)==new ActionTiming(14,7),"Owned Warlock baseline/FCR golden differs");
        foreach(bool female in new[]{false,true})foreach(bool boosted in new[]{false,true}){Player(female,boosted);}
        Require(generation<=2&&routes==5698,"Warlock fixture generation or candidate count differs");File.WriteAllText(Manifest,generation.ToString());
        Log.Information("Warlock ordinary SC:8owned16/256/8/1rows,2072unchanged source controls reused,22candidates/5698timing projections and Warlock/Witch actual FCR prepared windup/cancel pass. Chain UseAttackRate,three SQ and passive routes stay separate;no active spell/handler enabled.");
    }
}
