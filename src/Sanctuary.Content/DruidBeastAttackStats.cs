// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal readonly record struct BeastAttackStats(int Rank,int Minimum,int Maximum,int Attack);

// Source flag1 mode-list values, distinct from birth base/passive stats.
// Spirit Wolf A1entry captures these; Dire Wolf consumers remain separate.
internal static class DruidBeastAttackStats
{
    internal static BeastAttackStats Capture(SkillValues live,MonsterDifficulty difficulty,int sourceMode)
    {
        string key=live.Ability switch
        {
            CombatAbility.SummonSpiritWolf=>"spiritwolf",CombatAbility.SummonDireWolf=>"fenris",
            _=>throw new NotSupportedException("Unmapped beast attack source")
        };
        if(live.Rank is <0 or >255||difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell)
        {throw new ArgumentOutOfRangeException(nameof(live));}
        string lane=sourceMode switch{4=>"A1",5=>"A2",3 or 7 or 8=>"S1",_=>throw new NotSupportedException("Unmapped beast attack mode")};
        var row=SkillMechanics.For(live.Ability);
        if(NecromancerSummons.Source(key,"noRatio")!=1||NecromancerSummons.Source(key,"Align")!=1
            ||row.Number("SrcDam")!=0||row.Text("DmgSymPerCalc")!="0"||row.Text("ToHitCalc")!="0"
            ||row.Number("HitShift")!=8||row.Number("kick")!=0)
        {throw new NotSupportedException("Unsupported beast mode-stat route");}
        // Friendly noRatio tables are literal in every difficulty/game context.
        // Even an absent owner skill retains the getter's physical baseline;
        // the to-hit getter separately returns zero at rank zero.
        return new(live.Rank,checked((live.PhysicalDamage256(false)>>8)+NecromancerSummons.Source(key,lane+"MinD",difficulty)),
            checked((live.PhysicalDamage256(true)>>8)+NecromancerSummons.Source(key,lane+"MaxD",difficulty)),
            checked((live.Rank>0?(int)live.Formula("toht"):0)+NecromancerSummons.Source(key,lane+"TH",difficulty)));
    }
}
