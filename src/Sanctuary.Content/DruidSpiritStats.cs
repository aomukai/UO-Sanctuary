// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// All mapped spirits use complete do119/base/passive composition. Preserve
// the existing OakBirth saved field shape and immutable summon-birth values.
internal static class DruidSpiritStats
{
    internal static string Key(CombatAbility ability)=>ability switch
    {CombatAbility.OakSage=>"oaksage",CombatAbility.HeartOfWolverine=>"heartofwolverine",CombatAbility.SpiritOfBarbs=>"spiritofbarbs",_=>throw new NotSupportedException("Unmapped spirit birth")};
    private static void Route(SkillValues values)
    {
        string key=Key(values.Ability);var row=SkillMechanics.For(values.Ability);
        int id=values.Ability switch{CombatAbility.OakSage=>226,CombatAbility.HeartOfWolverine=>236,CombatAbility.SpiritOfBarbs=>246,_=>throw new NotSupportedException()};
        string aura=values.Ability switch{CombatAbility.OakSage=>"Oak Sage Aura",CombatAbility.HeartOfWolverine=>"Wolverine Aura",CombatAbility.SpiritOfBarbs=>"Barbs Aura",_=>throw new NotSupportedException()};
        if(values.Rank is <1 or >255 || row.SourceId!=id || row.Number("srvdofunc")!=119 || row.Text("summon")!=key
            || row.Text("pettype")!="totem" || row.Text("summode")!="NU" || values.Eval("petmax")!=1 || row.Text("sumskill1")!=aura
            || row.Text("passivestat1")!="poisonresist" || row.Text("passivestat2")!="maxpoisonresist")
        {throw new NotSupportedException("Unsupported spirit creation route");}
    }
    internal static OakSpawn Spawn(CombatAbility ability,MonsterDifficulty difficulty)
    {
        if(difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell){throw new ArgumentOutOfRangeException(nameof(difficulty));}
        string key=Key(ability);int F(string field)=>NecromancerSummons.Source(key,field,difficulty);
        if(NecromancerSummons.Source(key,"noRatio")!=1 || NecromancerSummons.Source(key,"Align")!=1
            || F("minHP")<=0 || F("minHP")!=F("maxHP") || F("A1TH")!=0)
        {throw new NotSupportedException("Unsupported spirit noRatio spawn");}
        int life=checked(F("minHP")*256);
        return new(new(life,F("AC"),0),checked(life*NecromancerSummons.Source(key,"DamageRegen"))>>12);
    }
    internal static OakBirth Birth(SkillValues values,MonsterDifficulty difficulty,bool expansion=true)
        =>Apply(values,Spawn(values.Ability,difficulty),difficulty,expansion);
    internal static OakBirth Apply(SkillValues values,OakSpawn spawn,MonsterDifficulty difficulty,bool expansion)
    {
        Route(values);
        if(spawn?.Stats is not{Life256:>0,Defense:>=0,Attack:>=0} || spawn.Regeneration256<0)
        {throw new ArgumentException("Invalid supplied spirit spawn");}
        int level=Math.Max(1,checked((int)values.Eval("calc2")));
        var basis=ValkyrieCreation.BaseAtLevel(level,(int)difficulty,expansion,spawn.Stats);
        int life=checked(spawn.Stats.Life256+SourcePercentage.Calculate(spawn.Stats.Life256,checked((int)values.Eval("calc1")),100));
        return new(values.Rank,basis.Level,life,basis.Defense,basis.Attack,spawn.Regeneration256,
            checked((int)values.Eval("passivecalc1")),checked((int)values.Eval("passivecalc2")),
            checked((int)values.Eval("sumsk1calc")),Math.Clamp(checked(3*values.Rank),1,checked((int)values.Formula("ulvl"))));
    }
    internal static int EffectiveAuraRank(int birthRank,int commandBonus)
    {
        if(birthRank is <1 or >255 || commandBonus is <0 or >1){throw new NotSupportedException("Unsupported spirit innate skill bonus context");}
        // This research key pins the common NPC getter's current experience maximum.
        int maximum=NecromancerSummons.Source("oaksage-skill-context","MaximumRank");
        if(maximum!=99){throw new NotSupportedException("Unsupported current NPC skill ceiling");}
        return Math.Clamp(checked(birthRank+commandBonus),0,maximum);
    }
    internal static OakBirth Capture(PlayerMobile owner,CombatAbility ability)
    {
        Key(ability);
        if(owner is not{Alive:true,Deleted:false} || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile)
            || profile.Class!=DiabloClass.Druid || SkillMechanics.Rank(owner,ability)<1)
        {throw new InvalidOperationException("Spirit needs a live learned Druid owner");}
        return Birth(SkillMechanics.Values(owner,ability),SummonDifficulties.For(owner.Map));
    }
    internal static OakBirth Rescale(OakBirth birth,MonsterDifficulty difficulty,CombatAbility ability)
    {
        ArgumentNullException.ThrowIfNull(birth);
        if(birth.Level is <1 or >99){throw new ArgumentException("Invalid captured spirit owner level");}
        return Birth(new(SkillMechanics.For(ability),_=>birth.Rank,_=>0,birth.Level),difficulty);
    }
}
