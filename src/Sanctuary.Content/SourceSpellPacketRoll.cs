// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;

internal readonly record struct SourcePhysicalFire(int Physical256,int Fire256);
internal static class SourceSpellPacketRoll
{
    // Current3aef10/3aeda0 use signed width,exclusive maximum and no draw for
    // nonpositive width. Preserve their unchecked32bit arithmetic. The weapon
    // roller has a different minimum/range normalization contract.
    internal static int Range(ref ulong seed,int minimum,int maximum)
        =>unchecked(minimum+(int)SourceRandom.Limited(ref seed,unchecked(maximum-minimum)));
    internal static SourcePhysicalFire PhysicalFire(ref ulong seed,int physicalMinimum,int physicalMaximum,int fireMinimum,int fireMaximum,int physical=0,int fire=0)
        =>new(unchecked(physical+Range(ref seed,physicalMinimum,physicalMaximum)),unchecked(fire+Range(ref seed,fireMinimum,fireMaximum)));
}
