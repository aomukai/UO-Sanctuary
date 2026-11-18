// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal static class AmazonJavelinEffects
{
    private readonly record struct CloudTile(Map Map, Point3D Point, ushort Graphic);
    private readonly record struct CloudLease(DateTime LastSeen, DateTime RefreshAt);
    private static readonly Dictionary<CloudTile, CloudLease> Visible = new();
    private static readonly List<CloudTile> Retired = new();

    internal static int Flight(WeaponFlight flight)
    {
        if (AmazonLightningFury.IsChild(flight))
        {
            return SorceressVisuals.DirectionalFrame(SpellEffectGraphics.ChargedBoltFlight, flight.Vx, flight.Vy, flight.Frame / 2);
        }
        int graphic = flight.Ability switch
        {
            CombatAbility.LightningBolt => SpellEffectGraphics.LightningJavelin,
            CombatAbility.LightningFury => SpellEffectGraphics.FuryJavelin,
            CombatAbility.PoisonJavelin => SpellEffectGraphics.PoisonJavelin,
            CombatAbility.PlagueJavelin => SpellEffectGraphics.PlagueJavelin,
            _ => 0
        };
        return graphic + SpellEffectGraphics.ProjectileFacing(flight.Vx, flight.Vy);
    }

    internal static void Release(PlayerMobile owner, CombatAbility ability)
    {
        owner.PlaySound(SpellEffectSounds.JavelinThrow);
        if (ability == CombatAbility.LightningBolt) { owner.PlaySound(SpellEffectSounds.ChargedBoltRelease); }
        if (ability == CombatAbility.LightningFury) { owner.PlaySound(SpellEffectSounds.LightningRelease); }
    }

    internal static void Contact(PlayerMobile owner, BaseCreature target, CombatAbility ability)
    {
        var (graphic, sound) = ability switch
        {
            CombatAbility.LightningBolt => (SpellEffectGraphics.LightningContact, SpellEffectSounds.LightningContact),
            CombatAbility.LightningFury => (SpellEffectGraphics.ArrowContact, SpellEffectSounds.AmazonArrowContact),
            _ => (SpellEffectGraphics.JavelinVenomContact, SpellEffectSounds.JavelinVenomContact)
        };
        Present(owner, target.Location, graphic, sound, SpellEffectGraphics.JavelinEffectFrames);
    }

    internal static void FurySplit(PlayerMobile owner, Point3D point) =>
        Present(owner, point, SpellEffectGraphics.NovaContact, SpellEffectSounds.NovaRelease, SpellEffectGraphics.JavelinBurstDuration);

    internal static void PlagueBurst(PlayerMobile owner, Point3D point) =>
        Present(owner, point, SpellEffectGraphics.PlagueJavelinBurst, SpellEffectSounds.PlagueJavelinBurst, SpellEffectGraphics.JavelinBurstDuration);

    private static void Present(PlayerMobile owner, Point3D point, int graphic, int sound, int duration)
    {
        Effects.SendLocationEffect(point, owner.Map, graphic, duration);
        SpellAreaSounds.Pulse(owner.Serial.Value, sound, point, owner.Map, TimedEffects.Now, 80);
    }

    internal static void ObserveCloud(PlayerMobile owner, Point3D point, ushort graphic, DateTime at)
    {
        // Co-located half-tile clouds share one visual lease. A moving cloud
        // renews its current tile and lets the previous tile retire.
        var tile = new CloudTile(owner.Map, point, graphic);
        var lease = Visible.GetValueOrDefault(tile);
        if (at < lease.LastSeen) { return; }
        var refresh = lease.RefreshAt;
        if (refresh <= at)
        {
            Effects.SendLocationEffect(point, owner.Map, graphic, 8);
            refresh = at.AddMilliseconds(300);
        }
        Visible[tile] = new CloudLease(at, refresh);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.JavelinCloud, point, owner.Map, TimedEffects.Now, 1150);
    }

    internal static void RetireClouds(DateTime at)
    {
        Retired.Clear();
        foreach (var (tile, lease) in Visible)
        {
            if (lease.LastSeen.AddMilliseconds(110) <= at) { Retired.Add(tile); }
        }
        foreach (var tile in Retired)
        {
            Visible.Remove(tile);
            Effects.SendLocationEffect(tile.Point, tile.Map, tile.Graphic, 0);
        }
    }
}
