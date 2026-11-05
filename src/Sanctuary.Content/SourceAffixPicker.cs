// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

// Shared complete older picker contract. Resolve the pool lazily AFTER the
// parity draw,including forced attempts; retain the final-row endpoint bias.
internal static class SourceAffixPicker
{
    internal static T Select<T>(Func<IReadOnlyList<T>> resolvePool,Func<T,int> weight,ref ulong seed,bool force=true,Func<T,bool> preferred=null)where T:class
    {
        ulong current=seed;
        try{return Select(resolvePool,weight,bound=>SourceRandom.Limited(ref current,bound),force,preferred);}
        finally{seed=current;}
    }
    internal static T Select<T>(Func<IReadOnlyList<T>> resolvePool,Func<T,int> weight,Func<int,uint> roll,bool force=true,Func<T,bool> preferred=null)where T:class
    {
        if(roll(2)==0&&!force){return null;}
        var pool=resolvePool();if(pool.Count==0){return null;}
        if(preferred!=null){foreach(var row in pool){if(preferred(row)){return row;}}}
        int total=0;foreach(var row in pool){total=checked(total+weight(row));}
        int ticket=(int)roll(checked(total+1));
        int count=Math.Min(pool.Count,511);
        for(int i=0;i<count;i++){var row=pool[i];ticket-=weight(row);if(ticket<0){return row;}}
        return pool[count-1];
    }
}
