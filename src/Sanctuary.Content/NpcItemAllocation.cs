// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed class SourceSeedStream(ulong value){internal ulong Value=value;}

internal sealed record NpcItemAllocation(ulong GameBefore,ulong GameAfter,uint UnitStart,uint ItemStart);
// Supplied game-stream boundary. This does not choose a Sanctuary world seed
// or consume the pet's independent MonEquip/AI stream.
internal static class NpcItemAllocations
{
    internal const uint InitialFlags=0x00082010; // INIT | INSTORE | IDENTIFIED
    internal static NpcItemAllocation Take(ref ulong game)
    {
        ulong before=game;uint unit=SourceRandom.Roll(ref game),item=SourceRandom.Roll(ref game);
        return new(before,game,unit,item);
    }
    internal static PreparedItemRequest Request(string code,int level,int quality,int difficulty,NpcItemAllocation a)
        =>new(code,level,quality,difficulty,SourceRandom.InitLow(unchecked((int)a.UnitStart)),SourceRandom.InitLow(unchecked((int)a.ItemStart)),a.ItemStart);
    internal static void Validate(NpcItemAllocation a,PreparedItemRequest request)
    {
        if(a==null){throw new ArgumentException("Missing source allocation context");}
        ulong game=a.GameBefore;var expected=Take(ref game);
        if(expected!=a||Request(request.Code,request.Level,request.Quality,request.Difficulty,a)!=request)
        {throw new ArgumentException("Allocated item lost its independent source seed contexts");}
    }
}
