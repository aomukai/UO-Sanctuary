// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal readonly record struct PreparedPoison(int Rate256,int Frames)
{
    internal bool Commit(Mobile source,Mobile target,DateTime now)=>target is{Alive:true,Deleted:false,Blessed:false,IsDeadBondedPet:false}
        && source!=null && !ClassCombat.Friendly(source,target) && TimedEffects.PoisonFrames(source,target,Rate256,Frames,now);
}

// Prepared source rates and lengths are independent integer lanes. The caller
// supplies raw stat pools and explicit game difficulty, before item pierce.
// Existing aggregate-total producers keep their original pipeline.
internal static class PoisonPackets
{
    internal static PreparedPoison Mitigate(int rate256,int frames,bool player,int resistance,int lengthResistance,
        int pierce,int maximumBonus,int difficultyPenalty,bool shrine=false)
    {
        if(rate256<0 || frames<0 || pierce<0 || maximumBonus<0 || difficultyPenalty>0){throw new ArgumentOutOfRangeException();}
        int Effective(int raw,int maximum)
        {
            long value=(long)raw-(player || raw<100?pierce:0)+(player?difficultyPenalty:0);
            if(player && value>0){value=Math.Min(value,maximum);}
            return (int)Math.Clamp(value,-100,100);
        }
        int rateResist=Effective(resistance,(int)Math.Min(95,75L+maximumBonus)),lengthResist=Effective(lengthResistance,75);
        // Zero resistance bypasses the percentage helper. For large values
        // that bypass differs from its deliberate divide-before-multiply path.
        int rate=rateResist==0?rate256:SourcePercentage.Calculate(rate256,100-rateResist,100);
        int length=shrine?0:lengthResist==0?frames:SourcePercentage.Calculate(frames,100-lengthResist,100);
        return new(rate,length);
    }
}
