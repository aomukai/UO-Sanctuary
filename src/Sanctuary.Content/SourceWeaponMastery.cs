// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

// Complete typed-stat choice/max semantics of the two source mastery
// readers. CopyStats has a 32-entry limit BEFORE item-type matching.
internal static class SourceWeaponMastery
{
    internal static int Read(IEnumerable<EquipmentStatValue> full,bool actor,bool item,bool extended,bool throwing,int kind,Func<int,bool> matches)
    {
        if(!actor||!item||!extended||kind is <0 or >2){return 0;}
        ArgumentNullException.ThrowIfNull(matches);
        int stat=(throwing?345:342)+kind,count=0,best=0;
        foreach(var field in full)
        {
            if(field.Stat!=stat){continue;}
            if(count++==32){break;}
            if(matches((ushort)field.Packed)&&field.Value>best){best=field.Value;}
        }
        return best;
    }
}
