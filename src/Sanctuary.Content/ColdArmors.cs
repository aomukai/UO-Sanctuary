// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// do18 shares state-group replacement; event02 is a distinct freeze-only
// incoming melee reaction. Shiver/event03 and Chilling/event01 keep their
// existing producers until their packet/flight contracts are migrated.
internal static class ColdArmors
{
    internal static ColdArmorState Prepare(SkillValues values, DateTime at)
    {
        var row = SkillMechanics.For(values.Ability);
        var (source, state, reaction, mana) = values.Ability switch
        {
            CombatAbility.FrozenArmor => (40, "frozenarmor", 2, 7),
            CombatAbility.ShiverArmor => (50, "shiverarmor", 3, 11),
            CombatAbility.ChillingArmor => (60, "chillingarmor", 1, 17),
            _ => throw new NotSupportedException("Unmapped cold armor")
        };
        if (values.Rank is < 1 or > 255 || at.Kind != DateTimeKind.Utc || row.SourceId != source
            || row.Number("srvstfunc") != 0 || row.Number("srvdofunc") != 18 || row.Text("aurastate") != state
            || row.Text("aurastat1") != "skill_armor_percent" || row.Number("auraeventfunc1") != reaction
            || values.Mana256 != mana * 256 || values.Eval("auralencalc") <= 0)
        { throw new NotSupportedException("Unsupported pinned cold armor route"); }
        return SorceressEffects.PrepareColdArmor(values.Ability, values, at.AddMilliseconds(values.Eval("auralencalc") * 40));
    }
    internal static bool Release(PlayerMobile owner, SkillValues values)
    {
        var prepared = Prepare(values, TimedEffects.Now);
        if (!NecromancerEffects.Pay(owner, values.Mana256)) { return false; }
        // do18 clears the state group including the same member before list
        // allocation. A weaker recast replaces its earlier stronger instance.
        SorceressEffects.ApplyArmor(owner, prepared);
        owner.PlaySound(0x1E5); return true; // Identified cast placeholder.
    }
    internal static int FreezeFrames(PlayerMobile owner, ColdArmorState armor)
    {
        if (armor.Ability != CombatAbility.FrozenArmor) { throw new ArgumentException("Not Frozen Armor"); }
        return armor.Rank == 0 ? armor.FreezeFrames
            : checked((int)SkillMechanics.Values(owner, CombatAbility.FrozenArmor, armor.Rank).Eval("calc1"));
    }
    internal static bool PhysicalMelee(Mobile attacker, Mobile defender, int physical256, bool ranged)
    {
        if (physical256 <= 0 || ranged || defender is not PlayerMobile { Alive: true, Deleted: false } owner
            || attacker is not { Alive: true, Deleted: false } || attacker.Map != owner.Map
            || !ClassCombat.IsHostile(owner, attacker)
            || SorceressEffects.Armor(owner) is not { Ability: CombatAbility.FrozenArmor } armor) { return false; }
        return FreezeStates.Apply(owner, attacker, FreezeFrames(owner, armor), Core.TickCount);
    }
}
