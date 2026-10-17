using System;
using Server;
using Server.Mobiles;

namespace Sanctuary.Integration;

// Existing outcome checks step directly to impact; timing checks exercise each frame separately.
public static class CombatTestActions
{
    public static int HitDamage(PlayerMobile player, Mobile target) =>
        Math.Max(1, ClassStats.DamageRange(player).Min * (100 - target.PhysicalResistance) / 100);
    public static bool Execute(PlayerMobile from, CombatAbility ability, Mobile target, Point3D point, long now)
    {
        var started = ClassCombat.Perform(from, ability, target, point, now);
        if (started)
        {
            ClassCombat.ResolveAction(from, ClassCombat.For(from).EffectAt);
        }
        return started;
    }
    public static void Step(PlayerMobile from, long now)
    {
        ClassCombat.UpdateCombat(from, now);
        ClassCombat.ResolveAction(from, ClassCombat.For(from).EffectAt);
    }
}
