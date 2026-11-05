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
        if(sourceDamage is <1 or >255){throw new ArgumentOutOfRangeException(nameof(sourceDamage));}
        int rolled=SourceElementRoll.Roll256(raw.PoisonMin,raw.PoisonMax,mastery,mastery,initialRate,random);
        int rate=checked((int)((long)rolled*sourceDamage/128));
        return new(rate,rate>0?Length(initialFrames,raw.PoisonFrames,raw.PoisonCount,overrideFrames):initialFrames);
    }
    internal static int Length(int initial,int duration,int count,int overrideFrames)
    {
        int frames=checked(initial+(overrideFrames>0?overrideFrames:duration));
        return overrideFrames<=0&&count>1?frames/count:frames;
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
