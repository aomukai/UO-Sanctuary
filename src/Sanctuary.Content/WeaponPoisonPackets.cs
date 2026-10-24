// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

// Optional saved weapon packet: preserve source rate and frame length instead
// of recovering either from a rounded aggregate total after windup.
public sealed record WeaponPoisonRoll(int Rate256,int Frames)
{
    public double Total=>(long)Rate256*Frames/256.0;
    internal bool Commit(PlayerMobile owner,Mobile target,DateTime at)=>target is BaseCreature creature
        && PoisonJavelins.Poison(owner,creature,Rate256,Frames,at);
}

internal static class WeaponPoisonPackets
{
    internal static MissileSourceStats AddVenom(MissileSourceStats raw,VenomState venom)
        =>venom==null?raw:raw with{PoisonMin=checked(raw.PoisonMin+venom.Minimum256),PoisonMax=checked(raw.PoisonMax+venom.Maximum256),PoisonFrames=venom.Frames,PoisonCount=0};
    internal static WeaponPoisonRoll Melee(MissileSourceStats raw,int mastery,int sourceDamage,Func<int,int> random,int initialRate=0,int initialFrames=0,int overrideFrames=0)
    {
        if(sourceDamage is <1 or >128 || raw.PoisonCount<0 || initialRate<0 || initialFrames<0 || overrideFrames<0 || mastery<0){throw new ArgumentOutOfRangeException();}
        int minimum=checked(raw.PoisonMin+SourcePercentage.Calculate(raw.PoisonMin,mastery,100)),maximum=checked(raw.PoisonMax+SourcePercentage.Calculate(raw.PoisonMax,mastery,100));
        int rolled=0;if(raw.PoisonMax>0){int span=Math.Max(0,maximum-minimum);rolled=minimum+(span>0?Math.Clamp(random(span),0,span-1):0);}
        int rate=checked((int)((long)Math.Max(0,checked(initialRate+rolled))*sourceDamage/128));
        int frames=initialFrames;
        if(rate>0){frames=checked(frames+(overrideFrames>0?overrideFrames:raw.PoisonFrames));if(overrideFrames<=0 && raw.PoisonCount>1){frames/=raw.PoisonCount;}}
        return new(rate,frames);
    }
    internal static WeaponPoisonRoll Capture(PlayerMobile owner,BaseWeapon weapon,VenomState venom,Func<int,int> random)
    {
        var raw=BowLaunch.RawPoison(owner,weapon);raw=raw with{PoisonMin=checked(raw.PoisonMin+venom.Minimum256),PoisonMax=checked(raw.PoisonMax+venom.Maximum256)};
        if(CombatTiming.Weapon(weapon).Family is AttackFamily.Bow or AttackFamily.Crossbow or AttackFamily.Throw)
        {
            // Older source missile collection adds owner stats without live
            // poison mastery. Current ranged Venom mastery remains acceptance.
            int span=Math.Max(0,raw.PoisonMax-raw.PoisonMin);
            return new(raw.PoisonMax>0?raw.PoisonMin+(span>0?Math.Clamp(random(span),0,span-1):0):0,venom.Frames);
        }
        return Melee(raw,NamedElements.MasteryPercent(owner,"pois"),128,random,overrideFrames:venom.Frames);
    }
}
