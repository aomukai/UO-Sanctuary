// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sanctuary;

// Shared sub_6FCF9580 selection contract. Caller provides one monster's valid
// MonEquip segment with bCheckOnInit=0. Creation remains an explicit service;
// do not filter unsupported rolled properties or invent a generic owner base.
internal static class SummonEquipment
{
    internal static void Select(IReadOnlyList<SummonEquipRow> rows,int rank,int itemLevel,ref ulong petSeed,
        Func<SummonEquipmentRequest,bool> create,IReadOnlyCollection<int> occupied=null,
        Func<int,string> ownerBase=null,bool ownerPresent=true,bool ownerInventory=true,Func<int,uint> boundedDraw=null)
    {
        ArgumentNullException.ThrowIfNull(rows);ArgumentNullException.ThrowIfNull(create);
        if(rank is <0 or >255||itemLevel<1||rows.Any(r=>r.Rank<0||r.Choices is not{Length:>=1 and <=3}
            ||r.Choices.Any(c=>c.Slot is <1 or >10||c.Code==null)))
        {throw new ArgumentOutOfRangeException();}
        var filled=new HashSet<int>(occupied??Array.Empty<int>());
        if(filled.Any(slot=>slot is <1 or >10)){throw new ArgumentOutOfRangeException(nameof(occupied));}
        int start=0;while(start<rows.Count&&rows[start].Rank>rank){start++;}
        for(int i=start;i<rows.Count;i++)
        {
            var row=rows[i];uint index=boundedDraw?.Invoke(row.Choices.Length)??SourceRandom.Limited(ref petSeed,row.Choices.Length);
            if(index>=row.Choices.Length){throw new InvalidOperationException("Invalid bounded equipment draw");}
            var choice=row.Choices[index];if(filled.Contains(choice.Slot)){continue;}
            int quality=choice.Quality is >=0 and <8?choice.Quality:0;string code=choice.Code;
            if(code.Length==0&&ownerPresent)
            {
                if(!ownerInventory){continue;}code=ownerBase?.Invoke(choice.Slot);if(code==null){continue;}
            }
            if(create(new(choice.Slot,code,quality,itemLevel))){filled.Add(choice.Slot);}
        }
    }
}
