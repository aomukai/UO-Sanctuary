// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record OakSpawn(SummonSpawnStats Stats,int Regeneration256);
internal sealed record OakBirth(int Rank,int Level,int Life256,int Defense,int Attack,int Regeneration256,
    int PoisonResistance,int MaximumPoisonResistance,int AuraRank,int RequestedEquipmentLevel);
internal readonly record struct OakAura(int Rank,int Radius,int LifePercent);

// The legacy OakBirth/OakSpawn record names retain the existing saved shape.
// Shared do119 creation is in DruidSpiritStats; Oak owns only its life aura.
internal static class DruidOakStats
{
    private static SkillDefinition _aura;
    public static void Configure()
    {
        var row=BalanceCatalog.Read<SkillDefinition>("druid-spirit-auras").SingleOrDefault(r=>r.SourceId==298);
        if(row is not{SourceId:298,SourceKey:"Oak Sage Aura",Ability:CombatAbility.OakSage}
            || row.Number("srvdofunc")!=65 || row.Number("aurafilter")!=65795
            || row.Text("aurastat1")!="item_maxhp_percent"
            || row.Text("aurastate")!="oaksagecontrol" || row.Text("auratargetstate")!="oaksage")
        {throw new InvalidDataException("Unsupported Oak Sage aura route");}
        _aura=row;
    }
    internal static OakSpawn Spawn(MonsterDifficulty difficulty)=>DruidSpiritStats.Spawn(CombatAbility.OakSage,difficulty);
    internal static OakBirth Birth(SkillValues values,MonsterDifficulty difficulty,bool expansion=true)
        =>Apply(values,Spawn(difficulty),difficulty,expansion);
    internal static OakBirth Apply(SkillValues values,OakSpawn spawn,MonsterDifficulty difficulty,bool expansion)
    {
        if(values.Ability!=CombatAbility.OakSage){throw new NotSupportedException("Expected Oak Sage birth values");}
        return DruidSpiritStats.Apply(values,spawn,difficulty,expansion);
    }
    internal static OakAura Aura(int rank)
    {
        if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}
        var values=new SkillValues(_aura,_=>rank,_=>0,1);
        if(values.Mana256!=0){throw new NotSupportedException("Unexpected Oak Sage NPC aura cost");}
        return new(rank,checked((int)values.Eval("aurarangecalc")),checked((int)values.Eval("aurastatcalc1")));
    }
    internal static int EffectiveAuraRank(int birthRank,int commandBonus)=>DruidSpiritStats.EffectiveAuraRank(birthRank,commandBonus);
    internal static OakBirth Capture(PlayerMobile owner)=>DruidSpiritStats.Capture(owner,CombatAbility.OakSage);
    internal static OakBirth Rescale(OakBirth birth,MonsterDifficulty difficulty)=>DruidSpiritStats.Rescale(birth,difficulty,CombatAbility.OakSage);
}
