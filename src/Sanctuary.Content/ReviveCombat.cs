// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Opt-in native consumers for the source level write and the two supported
// current-row combat bonuses. This is not the complete retained stat graph.
public sealed record ReviveCombatPlan(int Level,int DamagePercent,int VelocityPercent);
internal static class ReviveCombat
{
    public static void Configure()=>ClassCombatHooks.SourceWeaponDamage=actor=>Active(actor)
        &&CreatureProfiles.For(actor) is{Enabled:true,ApplyStats:true}
        &&actor.DamageMin>=0&&actor.DamageMax>=actor.DamageMin;
    internal static int ReducedLevel(int original,int caster)=>original!=0&&caster<original?caster:original;
    internal static ReviveCombatPlan Calculate(ReviveLifePlan life,int rank,Func<string,int> accrued)
    {
        ArgumentNullException.ThrowIfNull(life);ArgumentNullException.ThrowIfNull(accrued);
        var row=SkillMechanics.For(CombatAbility.Revive);
        string[] inherited=["cold","fire","light","poison","damage","magic"];
        if(rank is <1 or >255||life.OriginalLevel<1||life.CasterLevel is <1 or >99||life.MasteryRank is <0 or >255
            ||row.SourceId!=95||row.Text("aurastat1")!="damagepercent"||row.Text("aurastat2")!="0"
            ||row.Text("passivestat1")!="velocitypercent"||row.Text("passivestat8")!="0")
        {throw new NotSupportedException("Unsupported Revive combat stat route");}
        var values=new SkillValues(row,key=>key=="Revive"?rank:key=="Skeleton Mastery"?life.MasteryRank:0,_=>0,life.CasterLevel);
        for(int i=0;i<inherited.Length;i++)
        {
            string name="item_pierce_"+inherited[i]+"_immunity";
            if(row.Text("passivestat"+(i+2))!=name){throw new NotSupportedException("Unknown Revive inherited stat slot");}
            if(values.StatEval("passivecalc"+(i+2),accrued)!=0)
            {throw new NotSupportedException("Revive inherited sunder consumer is not implemented: "+name);}
        }
        return new(ReducedLevel(life.OriginalLevel,life.CasterLevel),checked((int)values.Eval("aurastatcalc1")),checked((int)values.Eval("passivecalc1")));
    }
    internal static ReviveCombatPlan Capture(PlayerMobile owner,ReviveLifePlan life,int rank)
        =>Calculate(life,rank,name=>ClassGear.Bonus(owner,name switch
        {
            "item_pierce_cold_immunity"=>"pierce-immunity-cold","item_pierce_fire_immunity"=>"pierce-immunity-fire",
            "item_pierce_light_immunity"=>"pierce-immunity-light","item_pierce_poison_immunity"=>"pierce-immunity-poison",
            "item_pierce_damage_immunity"=>"pierce-immunity-damage","item_pierce_magic_immunity"=>"pierce-immunity-magic",
            _=>throw new NotSupportedException("Unmapped Revive inherited stat: "+name)
        }));
    internal static void Validate(ReviveNativeSetup setup)
    {
        if(setup.CombatPlan==null){return;}
        if(setup.LifePlan==null||Calculate(setup.LifePlan,setup.Rank,_=>0)!=setup.CombatPlan)
        {throw new InvalidDataException("Revive combat plan does not match its captured source context");}
    }
    private static ReviveCombatPlan Plan(Mobile actor)=>actor is BaseCreature creature?ReviveNativeActivation.State(creature)?.Setup.CombatPlan:null;
    internal static bool Active(Mobile actor)=>Plan(actor)!=null;
    internal static int? Level(Mobile actor)=>Plan(actor)?.Level;
    internal static int DamagePercent(Mobile actor)=>Plan(actor)?.DamagePercent??0;
    internal static int VelocityPercent(Mobile actor)=>Plan(actor)?.VelocityPercent??0;
}
