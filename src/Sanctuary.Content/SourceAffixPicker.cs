// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

// Shared complete older picker contract. Resolve the pool lazily AFTER the
// parity draw,including forced attempts; retain the final-row endpoint bias.
internal static class SourceAffixPicker
{
    internal static T Select<T>(Func<IReadOnlyList<T>> resolvePool,Func<T,int> weight,ref ulong seed,bool force=true)where T:class
    {
        if(SourceRandom.Limited(ref seed,2)==0&&!force){return null;}
        var pool=resolvePool();if(pool.Count==0){return null;}
        int total=0;foreach(var row in pool){total=checked(total+weight(row));}
        int ticket=(int)SourceRandom.Limited(ref seed,checked(total+1));
        foreach(var row in pool){ticket-=weight(row);if(ticket<0){return row;}}
        return pool[pool.Count-1];
    }
}
