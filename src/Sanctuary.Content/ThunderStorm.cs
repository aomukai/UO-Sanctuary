// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record ThunderStormState(DateTime UntilUtc, DateTime NextStrikeUtc, int IntervalFrames,
    int RadiusSubtiles, int Rank, uint LastTarget = 0);

public static class ThunderStorm
{
    private static readonly Dictionary<Serial, TimerExecutionToken> _timers = new();
    internal static bool Valid(ThunderStormState state) => state.IntervalFrames >= 5 && state.RadiusSubtiles > 0 && state.Rank > 0;
    public static ThunderStormState Active(Mobile owner) => SorceressEffects.Profiles.Find(owner)?.Storm is { } storm
        && storm.UntilUtc > TimedEffects.Now ? storm : null;
    public static void Cast(PlayerMobile owner, SkillValues values)
    {
        var now = TimedEffects.Now;
        int frames = Math.Max(5, (int)values.Eval("perdelay"));
        var storm = new ThunderStormState(now.AddMilliseconds(values.Eval("auralencalc") * 40),
            now.AddMilliseconds(frames * 40), frames, values.Param(7), values.Rank);
        SorceressEffects.Profiles.Store(owner, (SorceressEffects.Profiles.Find(owner) ?? new()) with { Storm = storm });
        Schedule(owner.Serial, storm);
        owner.FixedEffect(0x375A, 10, 15, 0x482, 0); // Placeholder storm aura.
    }
    internal static void Cancel(Serial serial)
    { if (_timers.Remove(serial, out var timer)) { timer.Cancel(); } }
    internal static void Schedule(Serial serial, ThunderStormState storm)
    {
        Cancel(serial);
        var next = storm.NextStrikeUtc < storm.UntilUtc ? storm.NextStrikeUtc : storm.UntilUtc;
        Timer.StartTimer(TimeSpan.FromMilliseconds(Math.Max(1, (next - TimedEffects.Now).TotalMilliseconds)),
            () => Tick(serial), out var timer);
        _timers.Add(serial, timer);
    }
    private static void Tick(Serial serial)
    {
        _timers.Remove(serial);
        if (World.FindMobile(serial) is not PlayerMobile owner || SorceressEffects.Profiles.Find(owner) is not { Storm: { } storm } state)
        { return; }
        var now = TimedEffects.Now;
        if (storm.UntilUtc <= now || !ClassCreation.Profiles.TryGet(serial, out var profile) || profile.Class != DiabloClass.Sorceress
            || SkillMechanics.Rank(owner, CombatAbility.ThunderStorm) == 0)
        { SorceressEffects.Profiles.Store(owner, state with { Storm = null }); return; }
        if (storm.NextStrikeUtc > now) { Schedule(serial, storm); return; }
        Mobile selected = null;
        if (owner.Map != null && owner.Map != Map.Internal)
        {
            // The source picks the next greater unit ID, wrapping to the smallest; a sole target may repeat.
            var targets = SkillCombat.Targets(owner, owner.Location, SkillGeometry.Tiles(storm.RadiusSubtiles));
            foreach (var candidate in targets)
            {
                selected ??= candidate;
                if (candidate.Serial.Value > storm.LastTarget) { selected = candidate; break; }
            }
        }
        storm = storm with { NextStrikeUtc = now.AddMilliseconds(storm.IntervalFrames * 40), LastTarget = selected?.Serial.Value ?? 0 };
        SorceressEffects.Profiles.Store(owner, state with { Storm = storm });
        Schedule(serial, storm); // Missed/offline periods never replay a burst of strikes.
        if (selected == null || SpellHelper.IsTown(owner.Location, owner) || SpellHelper.IsTown(selected.Location, owner)) { return; }
        var values = SkillMechanics.Values(owner, CombatAbility.ThunderStorm, storm.Rank);
        double damage = values.Damage(false) + Utility.RandomDouble() * (values.Damage(true) - values.Damage(false));
        SorceressEffects.MissileHit(owner, selected, "thunderstorm1");
        SkillCombat.Hit(owner, selected, damage, "ltng",missileDefense:true);
        selected.BoltEffect(0x482); // Placeholder lightning strike.
    }
}
