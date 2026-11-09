// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Current event names distinguish melee attempts from landed missile damage.
// Reuse the existing registered-rank recipient model;protected callback36
// sibling order/overrides remain explicitly unqualified in the checkpoint.
internal static class HexSiphonEvents
{
    private static bool Apply(Mobile source,Mobile target)=>WeaponHexDelivery.Apply(source,target,static(owner,npc)=>
        HexSiphon.Buff(owner) is{} registered&&HexSiphon.ApplyPrepared(owner,npc,registered,true,TimedEffects.Now,replaceGroup:true));
    internal static bool MeleeAttempt(Mobile source,Mobile target)
        =>source is PlayerMobile owner&&!NamedCombat.Ranged(owner)&&target is BaseCreature npc&&PlayerMeleeGeometry.Within(owner,npc,0)&&Apply(source,target);
    // Called only by the existing successful weapon-damage event path.
    // Full mitigation does not turn a landed projectile into a miss.
    internal static bool MissileDamage(Mobile source,Mobile target)=>Apply(source,target);
    // Advanced Warlock producers must explicitly qualify their hextrigger
    // event. Spells and ordinary physical retaliation do not call this.
    internal static bool QualifiedHexTrigger(Mobile source,Mobile target,bool qualified)
        =>qualified&&Apply(source,target);
}
