// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;

namespace Sanctuary;

internal static class BladeFuryLaunch
{
    internal static WeaponMissileDamage FromSource(SkillValues values,int weaponMin256,int weaponMax256,
        bool twoHanded,int percent,int demon,int undead,bool critical,MissileSourceStats elements)
    {
        ArgumentNullException.ThrowIfNull(values);ArgumentNullException.ThrowIfNull(elements);
        if(values.Ability!=CombatAbility.BladeFury||values.Rank is <1 or >255||weaponMin256<0||weaponMax256<weaponMin256)
        {throw new ArgumentException("Invalid Blade Fury player packet context");}
        var row=SkillMechanics.For(values.Ability);
        if(row.SourceId!=266||row.Number("SrcDam")!=96||row.Text("ToHitCalc")!="lvl*10")
        {throw new NotSupportedException("Blade Fury packet differs from pinned skill266");}
        return BowLaunch.FromSource(weaponMin256,weaponMax256,values.PhysicalDamage256(false),values.PhysicalDamage256(true),
            twoHanded?48:96,percent,0,demon,undead,critical,elements)
            with{SourcePoisonFrames=true,SkillAttackPercent=checked((int)values.Eval("ToHitCalc"))};
    }

    internal static WeaponMissileDamage Capture(PlayerMobile owner,int rank,Random random)
    {
        ArgumentNullException.ThrowIfNull(owner);ArgumentNullException.ThrowIfNull(random);
        if(owner is not{Alive:true,Deleted:false}||rank is <1 or >255
            ||ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)!=true||profile.Class!=DiabloClass.Assassin)
        {throw new InvalidOperationException("Invalid Blade Fury player source");}
        if(ClassGear.Bonus(owner,"dmg-ac")>0)
        {throw new NotSupportedException("Positive target-defense adjustment has no native consumer");}
        var weapon=CombatTiming.Equipped(owner);bool two=weapon!=null&&WeaponHands.TwoHanded(weapon);
        var raw=ClassStats.WeaponBaseDamage(owner);
        int percent=ClassStats.PhysicalDamageBonus(owner,weapon)-(weapon==null?owner.Str:0);
        bool critical=NamedCombat.ProjectileCritical(AmazonCombat.CriticalChance(owner),
            Math.Clamp(ClassGear.Bonus(owner,"deadly"),0,100),WeaponMasteries.Bonus(owner,weapon,3),random);
        int undead=ClassGear.Bonus(owner,"dmg-undead%");
        if(weapon!=null&&ClassGear.Base(weapon) is{} basis&&ClassItemTypes.Is(basis.Type,"blun")){undead+=50;}
        return FromSource(SkillMechanics.Values(owner,CombatAbility.BladeFury,rank),checked(raw.Min*256),checked(raw.Max*256),
            two,percent,ClassGear.Bonus(owner,"dmg-demon%"),undead,critical,BowLaunch.WeaponElements(owner,weapon))
            with{PhysicalSupplement=BowLaunch.PhysicalSupplement(owner,weapon,two?48:96)};
    }
}
