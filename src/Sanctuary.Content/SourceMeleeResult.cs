// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Complete GetResultFlags decision order. Actor/world/stat/avoidance services
// are explicit; this does not allocate damage or dispatch contact/death events.
internal abstract class SourceMeleeResultServices
{
    internal abstract int? AttackerKind{get;}
    internal abstract int? DefenderKind{get;}
    internal abstract bool Hostile();
    internal abstract bool DefenderRunning();
    internal abstract int MeleeReach();
    internal abstract bool InRange(int bonus);
    internal abstract bool HitSuccessful(int attackPercent,bool missile);
    internal abstract int BlockOrDodge(bool avoid,bool block);
    internal abstract bool Uninterruptible();
}
internal static class SourceMeleeResult
{
    internal const int Successful=1,GetHit=4,Block=16,Dodge=128,Avoid=256,WeaponBlock=32768;
    internal static int Resolve(SourceMeleeResultServices services,int attackPercent=0,int rangeOffset=0)
    {
        ArgumentNullException.ThrowIfNull(services);
        int? attacker=services.AttackerKind,defender=services.DefenderKind;
        if(attacker is not(0 or 1)||defender is not(0 or 1)||!services.Hostile()){return 0;}
        int result=defender==0&&services.DefenderRunning()?Successful:0;
        int range=attacker==1?3:1;
        if(rangeOffset!=0){range=checked(range-rangeOffset-services.MeleeReach());}
        if(!services.InRange(range)){return 0;}
        if((result&Successful)==0&&services.HitSuccessful(attackPercent,false)){result|=Successful;}
        if((result&Successful)!=0)
        {
            int flags=services.BlockOrDodge(false,true);
            if((flags&2)!=0){result|=Avoid;}
            if((flags&4)!=0){result|=Dodge;}
            if((flags&16)!=0){result|=WeaponBlock;}
            if((flags&1)!=0){result|=Block;}
            // Evade and unknown nonzero action flags also clear success; the
            // older routine does not copy them into a result bit here.
            if(flags!=0){result&=~Successful;}
        }
        if((result&Block)!=0){result&=~Successful;}
        if((result&Successful)!=0&&!services.Uninterruptible()){result|=GetHit;}
        return result;
    }
}
