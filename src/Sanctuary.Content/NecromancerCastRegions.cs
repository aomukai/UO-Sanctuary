// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Ordinary learned-player casting. Use the same native town/safe-zone
// projection as the existing spell guards and approved summon parking.
internal static class NecromancerCastRegions
{
    internal static bool Allowed(PlayerMobile owner,CombatAbility ability)
    {
        if(owner is not{Alive:true,Deleted:false,Map:not null}||owner.Map==Map.Internal
            ||ClassAbilities.Find(ability)?.Class!=DiabloClass.Necromancer){return false;}
        int permission=SkillMechanics.For(ability).Number("InTown");
        if(permission is not(0 or 1)){throw new NotSupportedException("Unmapped Necromancer InTown flag");}
        return permission==1||!SpellHelper.IsTown(owner.Location,owner);
    }
}
