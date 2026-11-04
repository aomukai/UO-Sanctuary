// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Ordinary learned-player casting. Use the same native town/safe-zone
// projection as the existing spell guards and approved summon parking.
internal static class NecromancerCastRegions
{
    // Source starts15/17 check the corpse's room independently of InTown.
    // Revive has a different start21; do not add this rule to its shared
    // material/access predicate by analogy.
    internal static bool CorpseAllowed(PlayerMobile owner,CombatAbility ability,Corpse corpse)
        =>ability is (CombatAbility.RaiseSkeleton or CombatAbility.RaiseSkeletalMage or CombatAbility.CorpseExplosion or CombatAbility.PoisonExplosion)
            &&Allowed(owner,ability)&&corpse is{Deleted:false}&&corpse.Map==owner.Map&&!SpellHelper.IsTown(corpse.Location,owner);
    internal static bool Allowed(PlayerMobile owner,CombatAbility ability)
    {
        if(owner is not{Alive:true,Deleted:false,Map:not null}||owner.Map==Map.Internal
            ||ClassAbilities.Find(ability)?.Class!=DiabloClass.Necromancer){return false;}
        int permission=SkillMechanics.For(ability).Number("InTown");
        if(permission is not(0 or 1)){throw new NotSupportedException("Unmapped Necromancer InTown flag");}
        return permission==1||!SpellHelper.IsTown(owner.Location,owner);
    }
}
