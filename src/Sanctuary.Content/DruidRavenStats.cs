// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record RavenBirth(int Rank,int Level,int Life256,int Defense,int Attack,int Pierce,int Blind);
internal readonly record struct RavenAttack(int Rank,int Minimum256,int Maximum256,int AttackBonus);

// Components for do114 and monster SkillDamage only. No learned Raven handler
// or autonomous actor is enabled until its casting/AI/lifecycle acceptance.
internal static class DruidRavenStats
{
    private static void Route(SkillValues values)
    {
        var row=SkillMechanics.For(CombatAbility.Raven);
        if(values.Ability!=CombatAbility.Raven || values.Rank is <1 or >255 || row.SourceId!=221
            || row.Number("srvdofunc")!=114 || row.Text("summon")!="druidhawk" || row.Text("pettype")!="raven"
            || row.Text("summode")!="S1" || values.Mana256!=6*256
            || row.Text("passivestat1")!="item_pierce_damage_immunity")
        {throw new NotSupportedException("Unsupported Raven source route");}
    }
    internal static RavenBirth Birth(SkillValues values,MonsterDifficulty difficulty,bool expansion,Func<string,int> accrued)
    {
        Route(values);ArgumentNullException.ThrowIfNull(accrued);
        int Field(string key)=>NecromancerSummons.Source("druidhawk",key,difficulty);
        if(NecromancerSummons.Source("druidhawk","noRatio")!=1 || NecromancerSummons.Source("druidhawk","Align")!=1
            || Field("minHP")<=0 || Field("minHP")!=Field("maxHP") || Field("A1TH")!=0
            || values.Eval("calc1")!=0)
        {throw new NotSupportedException("Unsupported Raven spawn/base-life initializer");}
        int level=Math.Max(1,checked((int)values.Eval("calc2")));
        var spawn=new SummonSpawnStats(checked(Field("minHP")*256),Field("AC"),Field("A1TH"));
        var basis=ValkyrieCreation.BaseAtLevel(level,(int)difficulty,expansion,spawn);
        int pierce=checked((int)values.StatEval("passivecalc1",accrued));
        if(pierce!=0){throw new NotSupportedException("Raven inherited physical sunder consumer is not implemented");}
        int blind=NecromancerSummons.Source("druidhawk-properties","item_stupidity");
        if(blind!=-1){throw new NotSupportedException("Unsupported Raven signed blind property");}
        return new(values.Rank,basis.Level,spawn.Life256,basis.Defense,basis.Attack,pierce,blind);
    }
    internal static RavenBirth Capture(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false} || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile)
            || profile.Class!=DiabloClass.Druid || SkillMechanics.Rank(owner,CombatAbility.Raven)<1)
        {throw new InvalidOperationException("Raven needs a live learned Druid owner");}
        return Birth(SkillMechanics.Values(owner,CombatAbility.Raven),SummonDifficulties.For(owner.Map),true,
            name=>name=="item_pierce_damage_immunity"?ClassGear.Bonus(owner,"pierce-immunity-damage")
                :throw new NotSupportedException("Unmapped inherited Raven stat: "+name));
    }
    internal static RavenBirth Rescale(RavenBirth birth,MonsterDifficulty difficulty)
    {
        ArgumentNullException.ThrowIfNull(birth);
        int Field(string key)=>NecromancerSummons.Source("druidhawk",key,difficulty);
        var spawn=new SummonSpawnStats(checked(Field("minHP")*256),Field("AC"),Field("A1TH"));
        var basis=ValkyrieCreation.BaseAtLevel(birth.Level,(int)difficulty,true,spawn);
        return birth with{Life256=spawn.Life256,Defense=basis.Defense,Attack=basis.Attack};
    }
    internal static RavenAttack Attack(SkillValues liveValues)
    {
        Route(liveValues);
        // sub14D20 writes whole-life endpoints into monster mode stats. The
        // source monster mode adds its toht result as flat AR, not a percent.
        return new(liveValues.Rank,(liveValues.PhysicalDamage256(false)>>8)<<8,
            (liveValues.PhysicalDamage256(true)>>8)<<8,checked((int)liveValues.Formula("toht")));
    }
    internal static int InitialBudget(SkillValues firstThinkValues)
    {Route(firstThinkValues);return checked((int)firstThinkValues.Eval("calc3"));}
    internal static int Limit(SkillValues values)
    {Route(values);return Math.Max(1,checked((int)values.Eval("petmax")));}
}
