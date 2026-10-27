// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record OakSpawn(SummonSpawnStats Stats,int Regeneration256);
internal sealed record OakBirth(int Rank,int Level,int Life256,int Defense,int Attack,int Regeneration256,
    int PoisonResistance,int MaximumPoisonResistance,int AuraRank,int RequestedEquipmentLevel);
internal readonly record struct OakAura(int Rank,int Radius,int LifePercent);

// Creation/aura arithmetic only. No learned handler, recipient lease or actor
// is enabled by this component. Birth life excludes the later do65 self aura.
internal static class DruidOakStats
{
    private static SkillDefinition _aura;
    public static void Configure()
    {
        var rows=BalanceCatalog.Read<SkillDefinition>("druid-spirit-auras");
        if(rows is not{Length:1} || rows[0] is not{SourceId:298,SourceKey:"Oak Sage Aura",Ability:CombatAbility.OakSage}
            || rows[0].Number("srvdofunc")!=65 || rows[0].Number("aurafilter")!=65795
            || rows[0].Text("aurastat1")!="item_maxhp_percent"
            || rows[0].Text("aurastate")!="oaksagecontrol" || rows[0].Text("auratargetstate")!="oaksage")
        {throw new InvalidDataException("Unsupported Oak Sage aura route");}
        _aura=rows[0];
    }
    private static void Route(SkillValues values)
    {
        var row=SkillMechanics.For(CombatAbility.OakSage);
        if(values.Ability!=CombatAbility.OakSage || values.Rank is <1 or >255 || row.SourceId!=226
            || row.Number("srvdofunc")!=119 || row.Text("summon")!="oaksage" || row.Text("pettype")!="totem"
            || row.Text("summode")!="NU" || values.Eval("petmax")!=1 || row.Text("sumskill1")!="Oak Sage Aura"
            || row.Text("passivestat1")!="poisonresist" || row.Text("passivestat2")!="maxpoisonresist")
        {throw new NotSupportedException("Unsupported Oak Sage creation route");}
    }
    internal static OakSpawn Spawn(MonsterDifficulty difficulty)
    {
        if(difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell){throw new ArgumentOutOfRangeException(nameof(difficulty));}
        int F(string key)=>NecromancerSummons.Source("oaksage",key,difficulty);
        if(NecromancerSummons.Source("oaksage","noRatio")!=1 || NecromancerSummons.Source("oaksage","Align")!=1
            || F("minHP")<=0 || F("minHP")!=F("maxHP") || F("A1TH")!=0)
        {throw new NotSupportedException("Unsupported Oak Sage noRatio spawn");}
        int life=checked(F("minHP")*256);
        return new(new(life,F("AC"),0),checked(life*NecromancerSummons.Source("oaksage","DamageRegen"))>>12);
    }
    internal static OakBirth Birth(SkillValues values,MonsterDifficulty difficulty,bool expansion=true)
        =>Apply(values,Spawn(difficulty),difficulty,expansion);
    internal static OakBirth Apply(SkillValues values,OakSpawn spawn,MonsterDifficulty difficulty,bool expansion)
    {
        Route(values);
        if(spawn?.Stats is not{Life256:>0,Defense:>=0,Attack:>=0} || spawn.Regeneration256<0)
        {throw new ArgumentException("Invalid supplied Oak Sage spawn");}
        int level=Math.Max(1,checked((int)values.Eval("calc2")));
        // do119 calls the explicit-level base helper, unlike Creeper's do115.
        var basis=ValkyrieCreation.BaseAtLevel(level,(int)difficulty,expansion,spawn.Stats);
        int life=checked(spawn.Stats.Life256+SourcePercentage.Calculate(spawn.Stats.Life256,checked((int)values.Eval("calc1")),100));
        return new(values.Rank,basis.Level,life,basis.Defense,basis.Attack,spawn.Regeneration256,
            checked((int)values.Eval("passivecalc1")),checked((int)values.Eval("passivecalc2")),
            checked((int)values.Eval("sumsk1calc")),Math.Clamp(checked(3*values.Rank),1,checked((int)values.Formula("ulvl"))));
    }
    internal static OakAura Aura(int rank)
    {
        if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}
        var values=new SkillValues(_aura,_=>rank,_=>0,1);
        if(values.Mana256!=0){throw new NotSupportedException("Unexpected Oak Sage NPC aura cost");}
        return new(rank,checked((int)values.Eval("aurarangecalc")),checked((int)values.Eval("aurastatcalc1")));
    }
    internal static OakBirth Capture(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false} || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile)
            || profile.Class!=DiabloClass.Druid || SkillMechanics.Rank(owner,CombatAbility.OakSage)<1)
        {throw new InvalidOperationException("Oak Sage needs a live learned Druid owner");}
        return Birth(SkillMechanics.Values(owner,CombatAbility.OakSage),SummonDifficulties.For(owner.Map));
    }
    internal static OakBirth Rescale(OakBirth birth,MonsterDifficulty difficulty)
    {
        ArgumentNullException.ThrowIfNull(birth);
        if(birth.Level is <1 or >99){throw new ArgumentException("Invalid captured Oak Sage owner level");}
        return Birth(new(SkillMechanics.For(CombatAbility.OakSage),_=>birth.Rank,_=>0,birth.Level),difficulty);
    }
}
