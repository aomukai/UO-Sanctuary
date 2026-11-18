// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

internal static class FireFieldVisuals
{
    private readonly record struct Tile(Map Map, Point3D Point, CombatAbility Ability);
    private readonly record struct Lease(DateTime Until, DateTime SeenAt, DateTime RefreshAt);
    private static readonly Dictionary<Tile, Lease> Visible = new();
    private static readonly List<Tile> Retired = new();

    internal static bool IsField(CombatAbility ability) =>
        ability is CombatAbility.Blaze or CombatAbility.FireWall or CombatAbility.ImmolationArrow
            or CombatAbility.FistsOfFire or CombatAbility.PhoenixStrike;
    private static ushort Graphic(CombatAbility ability) => ability switch
    {
        CombatAbility.FireWall => SpellEffectGraphics.FireWallField,
        CombatAbility.ImmolationArrow => SpellEffectGraphics.ImmolationArrowField,
        CombatAbility.FistsOfFire => SpellEffectGraphics.MartialFistsField,
        CombatAbility.PhoenixStrike => SpellEffectGraphics.MartialPhoenixField,
        _ => SpellEffectGraphics.BlazeField
    };

    internal static void Observe(FirePatch patch, Point3D point, Map map, DateTime now)
    {
        if (now >= patch.UntilUtc) { return; }
        var tile = new Tile(map, point, patch.Ability);
        var previous = Visible.GetValueOrDefault(tile);
        var until = patch.UntilUtc > previous.Until ? patch.UntilUtc : previous.Until;
        var refreshAt = previous.RefreshAt;
        if (refreshAt <= now)
        {
            // Several mechanical subtiles can overlap one visible tile. A bounded
            // lease renews the single client effect and reaches new observers.
            var remaining = (until - now).TotalMilliseconds;
            int duration = (int)Math.Ceiling(Math.Min(remaining, 600) / 50);
            Effects.SendLocationEffect(point, map, Graphic(patch.Ability), duration);
            refreshAt = now.AddMilliseconds(Math.Min(remaining, 450));
        }
        Visible[tile] = new Lease(until, now, refreshAt);
        SpellAreaSounds.Pulse(patch.Owner, SpellEffectSounds.GroundFire, point, map, now, 1150);
    }

    internal static void Retire(DateTime now)
    {
        Retired.Clear();
        foreach (var (tile, lease) in Visible)
        {
            if (lease.Until <= now || lease.SeenAt.AddMilliseconds(200) <= now) { Retired.Add(tile); }
        }
        foreach (var tile in Retired)
        {
            var lease = Visible[tile];
            Visible.Remove(tile);
            Effects.SendLocationEffect(tile.Point, tile.Map, Graphic(tile.Ability), 0);
            if (lease.Until <= now && lease.SeenAt.AddMilliseconds(200) > now)
            {
                int fade = tile.Ability == CombatAbility.FireWall ? SpellEffectGraphics.FireWallFade : SpellEffectGraphics.BlazeFade;
                Effects.SendLocationEffect(tile.Point, tile.Map, fade, 6);
            }
        }
    }
}
