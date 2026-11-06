// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal abstract class SourceAvoidanceServices
{
    internal abstract bool Present{get;}
    internal abstract int Kind{get;}
    internal abstract int Mode{get;}
    internal abstract bool Moving{get;}
    internal abstract int BlockRate{get;}
    internal abstract int Stat(int stat);
    internal abstract IEnumerable<EquipmentStatValue> WeaponBlockStats{get;}
    internal abstract bool HandType(int type);
    internal abstract int WeaponClass{get;}
    internal abstract uint Raw();
}
// Complete older block/dodge orchestration and layered weapon-block reader.
// Native world modes, shield rate, hand facts, stat supply and RNG are services.
internal static class SourceAvoidance
{
    internal const int Block=1,Avoid=2,Dodge=4,Evade=8,WeaponBlock=16;
    internal static int WeaponBlockChance(SourceAvoidanceServices s)
    {
        if(!s.Present){return 0;}int best=0,count=0;
        foreach(var field in s.WeaponBlockStats)
        {
            if(count++==32){break;}int type=(ushort)field.Packed;
            // An untyped entry replaces the current maximum even if lower.
            if(type<=0||s.HandType(type)&&field.Value>best){best=field.Value;}
        }
        return best;
    }
    internal static int ApplyDodge(SourceAvoidanceServices s,bool avoid)
    {
        if(s.Present&&(s.Kind==0&&s.Mode is 2 or 3||s.Kind==1&&s.Mode is 2 or 15))
        {int chance=s.Stat(340);return chance>0&&s.Raw()%100<chance?Evade:0;}
        int weapon=WeaponBlockChance(s);
        if(weapon>0&&s.WeaponClass==13&&s.Raw()%100<weapon){return WeaponBlock;}
        int value=s.Stat(avoid?339:338);
        return value>0&&s.Raw()%100<value?(avoid?Avoid:Dodge):0;
    }
    internal static int Resolve(SourceAvoidanceServices s,bool avoid,bool block)
    {
        ArgumentNullException.ThrowIfNull(s);
        if(!block){return ApplyDodge(s,avoid);}
        int chance=s.BlockRate;if(chance<=0){return ApplyDodge(s,avoid);}
        if(s.Present&&s.Kind==0&&s.Moving&&s.Mode!=2){chance/=3;}
        return s.Raw()%100>=chance?ApplyDodge(s,avoid):Block;
    }
}
