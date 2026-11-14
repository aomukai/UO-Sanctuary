// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Settled direct components at the pre-life boundary. Poison remains on its
// own scheduler and must not be reconstructed from a scalar damage total.
internal readonly record struct DirectDamageParts(int Physical256,int Magic256,int Fire256,int Cold256,int Lightning256)
{
    internal long Total=>(long)Physical256+Magic256+Fire256+Cold256+Lightning256;
    internal void Validate(int total)
    {
        if(Physical256<0||Magic256<0||Fire256<0||Cold256<0||Lightning256<0||Total>total)
        {throw new NotSupportedException("Direct typed components do not fit the settled damage");}
    }
    internal DirectDamageParts Fraction(int percent)
    {
        if(percent is<0 or>60){throw new NotSupportedException("Unapproved Health Link share");}
        int Part(int value)=>checked((int)((long)value*percent/100));
        return new(Part(Physical256),Part(Magic256),Part(Fire256),Part(Cold256),Part(Lightning256));
    }
    internal GearDamage Gear=>new(Physical256/256.0,Fire256/256.0,Cold256/256.0,Lightning256/256.0,0,Magic:Magic256/256.0);
    internal static DirectDamageParts From(PreparedMonsterDamagePacket p)=>new(p.Physical256,p.Magic256,p.Fire256,p.Cold256,p.Lightning256);
    internal static DirectDamageParts From(GearDamage p)=>new(NamedElements.To256(p.Physical),NamedElements.To256(p.Magic),NamedElements.To256(p.Fire),NamedElements.To256(p.Cold),NamedElements.To256(p.Lightning));
}
