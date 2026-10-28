// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal readonly record struct DruidFormRateInput(CombatAbility Form,int HumanFrames,int HumanSpeed,
    int WeaponModifier,int WeaponIas,int GearIas,int SkillAttackRate,int Slowing);
internal readonly record struct DruidFormRateResult(int ItemEias,long Eias,int LimitedEias,int Progress256,int CycleFrames);

// Independently written arithmetic for the pinned3.3author-calculator model.
// Standard A1and Hunger/Rabies S3share this single-weapon normalized progress.
// Human A1metadata is a caller input; action/end schedules are separate.
// CycleFrames remains the standard A1author cycle,not the S3end deadline.
internal static class DruidFormRates
{
    internal static DruidFormRateResult Calculate(DruidFormRateInput input)
    {
        if(input.Form is not(CombatAbility.Werewolf or CombatAbility.Werebear))
        {throw new ArgumentOutOfRangeException(nameof(input));}
        if(input.HumanSpeed!=256||input.HumanFrames is not(16 or 17 or 19 or 20 or 21 or 23))
        {throw new NotSupportedException("Unmapped current Druid human A1metadata");}
        if(input.WeaponIas<0||input.GearIas<0||input.SkillAttackRate<0||input.Slowing<0)
        {throw new ArgumentOutOfRangeException(nameof(input));}
        long item=(long)input.WeaponIas+input.GearIas;
        int itemEias=checked((int)(120*item/(120+item)));
        long eias=(long)itemEias+input.SkillAttackRate-input.Slowing-input.WeaponModifier;
        int capped=checked((int)Math.Clamp(eias,-85L,150L));
        // Truncate the percentage before normalizing to the human weapon
        // length. The reference model's floating ratio is retained explicitly;
        // this comparison does not assert the current executable's numeric ABI.
        int scaled=checked(input.HumanSpeed+input.HumanSpeed*capped/100);
        int frames=input.Form==CombatAbility.Werewolf?13:12;
        int progress=checked((int)Math.Truncate(scaled*(frames/(double)input.HumanFrames)));
        if(progress<=0){throw new InvalidOperationException("Form animation cannot advance");}
        int cycle=checked((frames*256+progress-1)/progress-1);
        return new(itemEias,eias,capped,progress,cycle);
    }
}
