// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

// CE and Poison Explosion share the pinned SelectProc1/cltst21 target path.
// Two native tiles and serial ties are Sanctuary policy; only CE ground
// acquisition was directly observed in the pinned running game.
internal static class CorpseCastTargets
{
    internal const int NearClickTiles=2;
    internal static Corpse Acquire(PlayerMobile owner,CombatAbility ability,Point3D cursor)
    {
        if(ability is not(CombatAbility.CorpseExplosion or CombatAbility.PoisonExplosion)
            ||!NecromancerCastRegions.Allowed(owner,ability)){return null;}
        return NearClickTargets.AcquireCorpse(owner,cursor,NearClickTiles,
            corpse=>ability==CombatAbility.CorpseExplosion
                ?CorpseExplosionCasts.Supported(corpse)&&CorpseSkillRules.Usable(owner,corpse,SkeletonPets.Range)
                :SkeletonPets.Usable(owner,corpse),stableTies:true);
    }
}
