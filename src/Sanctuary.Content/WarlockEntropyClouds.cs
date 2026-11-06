// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record EntropyCloudShape(int EffectiveRank,int HardRank,int LifetimeFrames,int Radius,int? ServerCalculation);

// These are separate pinned missile formula outputs, not an interpretation of
// do41's layer geometry. In particular, Chains' SrvCalc1 uses hard points while
// Radius and Range use effective rank. Abyss' sksrc context stays unresolved.
internal static class WarlockEntropyClouds
{
    internal static EntropyCloudShape Capture(SkillValues values)
    {
        if(values?.Ability is not(CombatAbility.MiasmaBolt or CombatAbility.MiasmaChain)||values.Rank is <1 or >255)
        {throw new NotSupportedException("Entropy cloud needs a captured Miasma skill context");}
        bool chain=values.Ability==CombatAbility.MiasmaChain;
        string key=chain?"miasmachainscloud":"miasmaboltcloud";
        var cloud=new MissileValues(key,values);
        if(cloud.Number("pSrvDoFunc")!=41||cloud.Number("NextDelay")!=25||cloud.Text("EType")!="mag"
            ||cloud.Number(chain?"Param2":"Param4")!=50||cloud.Number(chain?"Param3":"Param5")!=5)
        {throw new NotSupportedException("Unmapped current entropy cloud route");}
        int effective=checked((int)values.Reference("skill","Enhanced Entropy","lvl"));
        int hard=checked((int)values.Reference("skill","Enhanced Entropy","blvl"));
        if(effective is <0 or >255||hard is <0 or >20)
        {throw new NotSupportedException("Unsupported captured Enhanced Entropy ranks");}
        return new(effective,hard,checked((int)cloud.Eval("Range")),checked((int)cloud.Eval("Radius")),
            chain?checked((int)cloud.Eval("SrvCalc1")):null);
    }
}
