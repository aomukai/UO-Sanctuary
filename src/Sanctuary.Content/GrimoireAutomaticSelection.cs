// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
namespace Sanctuary;

// Normal-quality group306 selection only,using a caller-supplied item seed
// at the automatic-affix phase. No seed allocation,staffmods or item factory.
internal static class GrimoireAutomaticSelection
{
    internal static GrimoireAutomatic[] Pool(string code,int itemLevel)
    {
        var basis=GrimoirePrepared.Base(code);
        if(itemLevel is<1 or>99){throw new ArgumentOutOfRangeException(nameof(itemLevel));}
        int level=AffixBalance.Level(itemLevel,basis.QualityLevel,0);
        // All eight owned rows are group306/version100,have valid non-socket
        // first properties and include armor. Do not apply the rare-only gate
        // to this NORMAL-quality branch or multiply zero-magic-level weights.
        return GrimoirePrepared.AllAutomatic.Where(row=>row.AffixLevel<=level).ToArray();
    }
    internal static GrimoireAutomatic Select(string code,int itemLevel,ref ulong seed)
    {
        // The prepared API rejects invalid context before touching caller RNG;
        // invalid game pointers/table services are outside its source contract.
        _=GrimoirePrepared.Base(code);if(itemLevel is<1 or>99){throw new ArgumentOutOfRangeException(nameof(itemLevel));}
        return SourceAffixPicker.Select<GrimoireAutomatic>(()=>Pool(code,itemLevel),row=>row.Frequency,ref seed);
    }
}
