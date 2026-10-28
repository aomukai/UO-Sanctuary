// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;
internal readonly record struct AbsorptionStep(int Remaining256,int Damage256,int Absorbed256);
// Shared nonnegative FP256 pool operation; channel/event priority is supplied
// by each caller, not inferred from this arithmetic helper.
internal static class SourceAbsorptionPool
{
    internal static AbsorptionStep Take(int remaining256,int damage256)
    {
        if(remaining256<0||damage256<0){throw new NotSupportedException("Negative absorption pool or damage");}
        int used=Math.Min(remaining256,damage256);
        return new(remaining256-used,damage256-used,used);
    }
}
