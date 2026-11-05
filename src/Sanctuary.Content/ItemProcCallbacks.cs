// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal sealed record ItemProcSkill(bool ItemTargetDo);
internal abstract class ItemProcServices
{
    internal abstract bool HasSource{get;}
    internal abstract bool HasTarget{get;}
    internal abstract bool HasPacket{get;}
    internal abstract uint HitFlags{get;}
    internal abstract ushort ResultFlags{get;}
    internal abstract int LevelBits{get;}
    internal abstract int Chance(int stat,int layer);
    internal abstract uint Roll();
    internal abstract ItemProcSkill Skill(int id);
    internal abstract (int X,int Y) FirstPathPoint();
    internal abstract void CastTarget(bool targetAsCaster,int skill,int level,int flag);
    internal abstract void CastPosition(int skill,int level,int x,int y,int flag);
}

// Complete older callbacks20/21/30. Cast services are mandatory;callback1
// reports an accepted attempt even when a nested cast service fails.
// No NPC event subscription or generic spell implementation is implied.
internal static class ItemProcCallbacks
{
    internal static bool OutgoingGate(int chance,bool packet,uint hitFlags)=>chance>0&&(!packet||(hitFlags&32)!=0);
    internal static bool Chance(int chance,bool packet,uint hitFlags,uint roll)=>OutgoingGate(chance,packet,hitFlags)&&roll%100<(uint)chance;
    internal static int Run(int function,uint packed,ItemProcServices s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if(function is not(20 or 21 or 30)){throw new NotSupportedException("Unmapped item proc callback");}
        if(!s.HasSource){return 0;}
        int chance=s.Chance((int)(packed>>16),(ushort)packed);
        if(chance<=0){return 0;}
        if(function==20&&s.HasPacket&&(s.HitFlags&32)==0){return 0;}
        int level=0,skill=0;
        void Decode()
        {
            int bits=s.LevelBits;if(bits is <1 or >8){throw new NotSupportedException("Unmapped source skill-level bit context");}
            level=(ushort)packed&((1<<bits)-1);skill=(ushort)packed>>bits;
        }
        if(function!=20){Decode();}
        if(function==21&&(!s.HasPacket||(s.ResultFlags&4)==0)){return 0;}
        if(s.Roll()%100>=(uint)chance){return 0;}
        if(function==20){Decode();}
        var info=s.Skill(skill);if(info==null){return 0;}
        if(function==20&&info.ItemTargetDo)
        {
            if(s.HasTarget){s.CastTarget(true,skill,level,0);}
            return 1;
        }
        int flag=function==20?1:0;
        if(s.HasTarget){s.CastTarget(false,skill,level,flag);}
        else{var p=s.FirstPathPoint();s.CastPosition(skill,level,p.X,p.Y,flag);}
        return 1;
    }
}
