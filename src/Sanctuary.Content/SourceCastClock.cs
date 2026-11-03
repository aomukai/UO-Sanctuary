// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record SourceCastRow(string WeaponClass,int Frames,int Speed,int Action,int Flag);
// Shared literal SC/FCR writer and action/end scheduling projection. Source
// frame-origin/current-client acceptance remains separate from these offsets.
internal static class SourceCastClock
{
    internal static ActionTiming Calculate(int frames,int speed,int action,int fcr)
    {
        if(frames is <1 or >144||speed is <1 or >32767||action<1||action>=frames||fcr is <0 or >10000)
        {throw new NotSupportedException("Unsupported source cast clock context");}
        int effective=120*fcr/(120+fcr);
        int advance=Math.Clamp(speed*Math.Min(175,100+effective)/100,0,32767);
        return new((frames*256+advance-1)/advance,(action*256+advance-1)/advance);
    }
}
