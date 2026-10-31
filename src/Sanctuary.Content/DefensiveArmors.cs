// SPDX-License-Identifier: GPL-3.0-or-later
using Server.Mobiles;
namespace Sanctuary;
// Implemented members of current states group1. justhit/mindbarrier have no
// native persistent armor projection; this does not reset generic recovery.
internal static class DefensiveArmors
{
    internal static void ClearGroup(PlayerMobile owner)
    {
        if(NecromancerEffects.Profiles?.Find(owner) is{BoneArmor:>0} bone)
        {NecromancerEffects.ClearBoneArmor(owner);}
        if(SorceressEffects.Profiles?.Find(owner) is{Armor:not null} cold)
        {SorceressEffects.Profiles.Store(owner,cold with{Armor=null});}
        PsychicWard.Clear(owner);
    }
}
