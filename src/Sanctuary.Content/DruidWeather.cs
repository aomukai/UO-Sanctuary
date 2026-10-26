// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal readonly record struct DruidWeatherStart(ulong Seed, ushort Param, DateTime NextUtc, DateTime UntilUtc);

// Shared do124 setup only. Hurricane and Armageddon keep distinct active
// callbacks, damage capture and emitted-actor lifetimes.
internal static class DruidWeather
{
    internal static bool Outdoors(PlayerMobile p) => p is { Alive: true, Deleted: false } && p.Map != null && p.Map != Map.Internal && !SpellHelper.IsTown(p.Location, p);
    internal static DruidWeatherStart Prepare(PlayerMobile owner, SkillValues values, DateTime at, Func<int> seedLow)
    {
        if (owner is not { Alive: true, Deleted: false } || !ClassCreation.Profiles.TryGet(owner.Serial, out var profile) || profile.Class != DiabloClass.Druid
            || values.Ability is not (CombatAbility.Hurricane or CombatAbility.Armageddon) || values.Rank is < 1 or > 255 || at.Kind != DateTimeKind.Utc)
        { throw new NotSupportedException("Unsupported Druid weather supplier/clock"); }
        int duration = Math.Max(1, (int)values.Eval("auralencalc")), interval = values.Param(4);
        if (duration is < 250 or > 1250 || (duration - 250) % 50 != 0 || interval <= 0)
        { throw new NotSupportedException("Unsupported Druid weather duration/event interval"); }
        // Declared scoped caster stream adapter; source do124 stores low16
        // bits of one draw. Complete cross-action RNG interleaving is open.
        ulong seed = SourceRandom.InitLow(seedLow()); ushort param = unchecked((ushort)SourceRandom.Roll(ref seed));
        return new(seed, param, at.AddMilliseconds(interval * 40), at.AddMilliseconds(duration * 40));
    }
}

// Shared supported living-monster projection of source area filters0x8583,
// 0xA583 and0xA783. Optional NoAura/NPC filtering is absent in these callers.
internal static class SourceDamageAreas
{
    internal static bool Recipient(PlayerMobile owner, BaseCreature target, int map, int x, int y, int z, int radius, bool lineOfSight = false)
    {
        bool attackable = target is EncounterCreature ? target.CanBeDamaged() : SourceAuraTargets.Rule(CreatureProfiles.Definition(target).Reference)?.Attackable == true;
        return attackable && owner.Map?.MapID == map && target.Map == owner.Map && ClassCombat.IsHostile(owner, target)
            && Math.Abs(target.Z - z) < 16 && DruidTornado.InPulse(x, y, target.X * 2, target.Y * 2, radius)
            && !SpellHelper.IsTown(target.Location, owner)
            && (!lineOfSight || owner.Map.LineOfSight(new Point3D(owner.X, owner.Y, owner.Z + 12), new Point3D(target.X, target.Y, target.Z + 12)));
    }
}
