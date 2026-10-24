// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

// Packet construction only. The eventual actor must capture the original
// player's source stats at first missile creation and then transfer ownership
// to its own inherited stat context. This is not a live player-gear collector.
public static class BladeSentinelPackets
{
    internal static WeaponMissileDamage FromSource(SkillValues values,int weaponMin256,int weaponMax256,
        bool twoHanded,int ownerPercent,int demonPercent,int undeadPercent,bool critical,MissileSourceStats elements)
    {
        ArgumentNullException.ThrowIfNull(values);ArgumentNullException.ThrowIfNull(elements);
        if(values.Ability!=CombatAbility.BladeSentinel || values.Rank is <1 or >255
            || weaponMin256<0 || weaponMax256<weaponMin256){throw new ArgumentException("Invalid Blade Sentinel source context");}
        int sourceDamage=SkillMechanics.For(values.Ability).Number("SrcDam");
        if(twoHanded){sourceDamage/=2;} // Pinned blade creeper Half2HSrc1.
        return BowLaunch.FromSource(weaponMin256,weaponMax256,values.PhysicalDamage256(false),values.PhysicalDamage256(true),
            sourceDamage,ownerPercent,0,demonPercent,undeadPercent,critical,elements) with{SourcePoisonFrames=true};
    }
}
