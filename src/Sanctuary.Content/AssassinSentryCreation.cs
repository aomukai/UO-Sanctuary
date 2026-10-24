// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server.Mobiles;

namespace Sanctuary;

internal sealed record SentryCreationStats(int Rank,int CasterLevel,int Level,int Life256,int Defense,int Attack,
    MonsterDifficulty Difficulty,SummonStat[] Passives,SummonStat[] Skills)
{
    internal int Passive(string name)=>Passives.Where(x=>x.Name==name).Sum(x=>x.Value);
    internal int Skill(string name)=>Skills.Where(x=>x.Name==name).Sum(x=>x.Value);
}

// Stationary elemental sentry context. calc4 is a remaining-shot formula; this
// deliberately does not inherit Blade Sentinel's duration or weapon snapshot.
internal static class AssassinSentryCreation
{
    internal static SummonSpawnStats SourceSpawn(MonsterDifficulty difficulty)
    {
        if(difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell){throw new ArgumentOutOfRangeException();}
        const string source="chargeboltsentry";
        int Field(string field)=>NecromancerSummons.Source(source,field,difficulty);
        if(NecromancerSummons.Source(source,"noRatio")!=1 || NecromancerSummons.Source(source,"Align")!=1
            || Field("minHP")<=0 || Field("minHP")!=Field("maxHP") || Field("A1TH")!=0)
        {throw new NotSupportedException("Unsupported sentry spawn HP/ratio/alignment/attack initializer");}
        return new(checked(Field("minHP")*256),Field("AC"),0);
    }
    internal static SentryCreationStats Prepare(SkillValues values,int casterLevel,MonsterDifficulty difficulty,Func<string,int> accrued)
    {
        ArgumentNullException.ThrowIfNull(values);ArgumentNullException.ThrowIfNull(accrued);
        if(values.Ability!=CombatAbility.ChargedBoltSentry){throw new ArgumentException("Expected Charged Bolt Sentry",nameof(values));}
        var spawn=SourceSpawn(difficulty);var basis=ValkyrieCreation.Base(casterLevel,values.Rank,(int)difficulty,true,spawn);
        var row=SkillMechanics.For(values.Ability);
        var passives=Enumerable.Range(1,3).Select(i=>new SummonStat(row.Text("passivestat"+i),
            AssassinTrapStats.InheritedValue(row.Text("passivestat"+i),checked((int)values.StatEval("passivecalc"+i,accrued))))).ToArray();
        var skills=Enumerable.Range(1,5).Select(i=>new SummonStat(row.Text("sumskill"+i),checked((int)values.Eval("sumsk"+i+"calc"))))
            .Where(x=>x.Value>0).ToArray();
        // do45 keeps the base helper's level. Only Blade's separate do44
        // overwrites that level with the caster's without recomputing AC/TH.
        return new(values.Rank,casterLevel,basis.Level,spawn.Life256,basis.Defense,basis.Attack,difficulty,passives,skills);
    }
    internal static SentryCreationStats Capture(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false} || owner.Map==null || owner.Map==Server.Map.Internal
            || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Assassin
            || SkillMechanics.Rank(owner,CombatAbility.ChargedBoltSentry) is <1 or >255)
        {throw new InvalidOperationException("Sentry needs a living learned Assassin world owner");}
        return Prepare(SkillMechanics.Values(owner,CombatAbility.ChargedBoltSentry),ClassStats.Level(owner),SummonDifficulties.For(owner.Map),
            name=>AssassinTrapStats.Accrued(owner,name));
    }
    internal static SentryCreationStats Rescale(SentryCreationStats old,MonsterDifficulty difficulty)
    {
        var next=ValkyrieCreation.Base(old.CasterLevel,old.Rank,(int)difficulty,true,SourceSpawn(difficulty));
        if(next.Level!=old.Level){throw new NotSupportedException("Retained sentry base level is not mapped");}
        return old with{Life256=SourceSpawn(difficulty).Life256,Defense=next.Defense,Attack=next.Attack,Difficulty=difficulty};
    }
    internal static SkillValues Values(SentryCreationStats stats)=>new(SkillMechanics.For(CombatAbility.ChargedBoltSentry),
        key=>key=="Charged Bolt Sentry"?stats.Rank:stats.Skill(key),key=>stats.Skill(key),stats.Level);
    internal static int Shots(SentryCreationStats stats)=>checked((int)Values(stats).Eval("calc4"));
    internal static int Bolts(SentryCreationStats stats)
    {
        var values=Values(stats);
        // These player-row parameters explicitly mirror BoltSentry's ln12.
        return checked(values.Linear(3,4)+stats.Skill("Shock Field")/values.Param(6));
    }
    internal static (int Min,int Max) Lightning(SentryCreationStats stats)
    {
        var values=Values(stats);int mastery=stats.Passive("passive_ltng_mastery");
        return(AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery),AmazonMelee.EnhanceEndpoint(values.Damage256(true),mastery));
    }
}
