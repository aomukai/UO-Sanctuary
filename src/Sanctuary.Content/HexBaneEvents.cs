// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
namespace Sanctuary;
internal static class HexBaneEvents
{
    private static bool Apply(Mobile source,Mobile target)=>WeaponHexDelivery.Apply(source,target,static(owner,npc)=>
        HexBane.Buff(owner) is{} registered&&HexBane.ApplyPrepared(owner,npc,registered,true,TimedEffects.Now,replaceGroup:true));
    // Invoked after ordinary accuracy/block/dodge decisions. The returned
    // result is already fixed;this penalty can affect only later attacks.
    internal static bool MeleeResult(Mobile source,Mobile target)
        =>source is PlayerMobile owner&&!NamedCombat.Ranged(owner)&&target is BaseCreature npc&&PlayerMeleeGeometry.Within(owner,npc,0)&&Apply(source,target);
    internal static bool MissileDamage(Mobile source,Mobile target)=>Apply(source,target);
    internal static bool QualifiedHexTrigger(Mobile source,Mobile target,bool qualified)=>qualified&&Apply(source,target);
}
