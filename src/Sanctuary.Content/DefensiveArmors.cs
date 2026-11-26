// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
namespace Sanctuary;
// Implemented members of current states group1. justhit/mindbarrier have no
// native persistent armor projection; this does not reset generic recovery.
internal static class DefensiveArmors
{
    // Shared pre-resistance shield entry. Preserve the existing class-damage
    // order; this is not a complete source event registry/priority model.
    internal static GearDamage Absorb(Mobile target,GearDamage damage,Mobile source, bool melee = false)
    {
        var raw=SorceressEffects.Absorb(target,damage,source);
        double physical = PsychicWard.AbsorbPhysical(target, NecromancerEffects.AbsorbPhysical(target, raw.Physical));
        if (target is WrongEncounter { HasWard: true } merchant)
            physical = merchant.AbsorbWard(physical, source, melee);
        return raw with { Physical = physical };
    }
    internal static void ClearGroup(PlayerMobile owner)
    {
        if(NecromancerEffects.Profiles?.Find(owner) is{BoneArmor:>0} bone)
        {NecromancerEffects.ClearBoneArmor(owner);}
        if(SorceressEffects.Profiles?.Find(owner) is{Armor:not null} cold)
        {SorceressEffects.Profiles.Store(owner,cold with{Armor=null});}
        PsychicWard.Clear(owner);
    }
}
