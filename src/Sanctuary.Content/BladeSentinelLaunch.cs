// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal sealed record BladeMissileCapture(AssassinTrapCreationStats Creation,WeaponMissileDamage Damage,int TargetDefenseDelta);

// The first-missile player stage. Later impact uses the blade's inherited
// context; this record contains no live weapon or owner stat references.
internal static class BladeSentinelLaunch
{
    internal static BladeMissileCapture Capture(PlayerMobile owner,AssassinTrapCreationStats creation,Random random)
    {
        ArgumentNullException.ThrowIfNull(owner);ArgumentNullException.ThrowIfNull(creation);ArgumentNullException.ThrowIfNull(random);
        if(owner.Deleted || !owner.Alive || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile)
            || profile.Class!=DiabloClass.Assassin || creation.Rank is <1 or >255 || creation.MissileArmed)
        {throw new InvalidOperationException("Invalid Blade Sentinel first-missile owner or repeated inheritance");}
        var weapon=CombatTiming.Equipped(owner);
        int targetDefense=ClassGear.Bonus(owner,"dmg-ac");
        if(targetDefense>0){throw new NotSupportedException("Positive blade target-defense adjustment has no native consumer");}
        var values=SkillMechanics.Values(owner,CombatAbility.BladeSentinel,creation.Rank);
        var raw=ClassStats.WeaponBaseDamage(owner);
        bool twoHanded=weapon!=null && WeaponHands.TwoHanded(weapon);
        int sourceDamage=SkillMechanics.For(CombatAbility.BladeSentinel).Number("SrcDam")/(twoHanded?2:1);
        int percent=ClassStats.PhysicalDamageBonus(owner,weapon)-(weapon==null?owner.Str:0);
        // The source's generic/deadly/mastery order applies to the actual
        // selected item. The arrow helper's throwing-only selector is narrower.
        bool critical=NamedCombat.ProjectileCritical(AmazonCombat.CriticalChance(owner),
            Math.Clamp(ClassGear.Bonus(owner,"deadly"),0,100),WeaponMasteries.Bonus(owner,weapon,3),random);
        int undead=ClassGear.Bonus(owner,"dmg-undead%");
        if(weapon!=null && ClassGear.Base(weapon) is{} basis && ClassItemTypes.Is(basis.Type,"blun")){undead+=50;}
        var packet=BladeSentinelPackets.FromSource(values,checked(raw.Min*256),checked(raw.Max*256),twoHanded,
            percent,ClassGear.Bonus(owner,"dmg-demon%"),undead,critical,BowLaunch.WeaponElements(owner,weapon))
            with{PhysicalSupplement=BowLaunch.PhysicalSupplement(owner,weapon,sourceDamage)};
        var armed=AssassinTrapCreation.Arm(creation,ClassStats.PlayerAttackBase(owner),
            ClassStats.PlayerItemAttackPercent(owner),WeaponMasteries.Bonus(owner,weapon,1));
        return new(armed,packet,targetDefense);
    }
}
