// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;

internal static class SourceRandom
{
    internal static ulong InitLow(int value)=>((ulong)666<<32)|unchecked((uint)value);
    internal static uint Roll(ref ulong seed)
    {seed=0x6AC690C5UL*(uint)seed+(seed>>32);return (uint)seed;}
    internal static uint Limited(ref ulong seed,int bound)=>bound<=0?0:Roll(ref seed)%(uint)bound;
}
