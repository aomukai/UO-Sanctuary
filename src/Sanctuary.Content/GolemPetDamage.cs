// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Approved golem replacement baselines share the captured Skeleton curve.
// Keep fractional intrinsic endpoints before ordinary item/aura percentages.
internal static class GolemPetDamage
{
    private static (int Min,int Max,int Percent) Curve(int rank,SkillValues values,MonsterDifficulty difficulty)=>SkeletonStats.WeaponBase(rank,
        checked((int)values.Formula("skill('Golem Mastery'.lvl)")),difficulty);
    private static (int Min,int Max,int Percent) Curve(NecromancerMinion actor)=>Curve(actor.Rank,actor.CapturedValues,actor.SummonDifficulty);
    // Prepared Iron stores the same captured skill context in its original-
    // item recipe, rather than the ordinary NecromancerMinion rank snapshot.
    internal static (int Min,int Max,int Percent) Weapon(SkillValues values,MonsterDifficulty difficulty)=>Curve(values.Rank,values,difficulty);
    private static int Ratio(NecromancerMinion actor)=>actor.Kind switch
    {CombatAbility.ClayGolem or CombatAbility.IronGolem=>100,CombatAbility.BloodGolem=>150,_=>throw new NotSupportedException("Unmapped approved golem damage baseline")};
    internal static (int Min256,int Max256,int Percent) Base(NecromancerMinion actor)
    {var curve=Curve(actor);int ratio=Ratio(actor);return(checked(curve.Min*256*ratio)/100,checked(curve.Max*256*ratio)/100,curve.Percent);}
    internal static (int Min,int Max,int Percent) Weapon(NecromancerMinion actor)
    {var basis=Base(actor);return(basis.Min256/256,basis.Max256/256,basis.Percent);}
    internal static NpcPhysicalStats Physical(NecromancerMinion actor)
    {
        var basis=Base(actor);return new(basis.Min256/256,basis.Max256/256,0,
            checked(basis.Percent+CombatAuras.DamagePercent(actor)+NecromancerEffects.DamageStatPercent(actor)),0,0,false,
            MinimumFraction256:basis.Min256%256,MaximumFraction256:basis.Max256%256);
    }
    internal static (int Min,int Max) Damage(NecromancerMinion actor,bool modifiers=true)
    {
        var basis=Base(actor);int percent=Math.Max(-90,checked(basis.Percent+(modifiers?IronGolemUpgrades.DamagePercent(actor)+CombatAuras.DamagePercent(actor)+NecromancerEffects.DamageStatPercent(actor):0)));
        return(checked(basis.Min256+SourcePercentage.Calculate(basis.Min256,percent,100))/256,
            checked(basis.Max256+SourcePercentage.Calculate(basis.Max256,percent,100))/256);
    }
}
