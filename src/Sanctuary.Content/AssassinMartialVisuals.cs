// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

internal static class AssassinMartialVisuals
{
    private readonly record struct OrbLease(ushort Graphic, Map Map, DateTime RefreshAfter);
    private static readonly Dictionary<(uint, CombatAbility), OrbLease> Shown = new();

    private static int Family(CombatAbility ability) => ability switch
    {
        CombatAbility.TigerStrike => 0,
        CombatAbility.CobraStrike => 1,
        CombatAbility.FistsOfFire => 2,
        CombatAbility.ClawsOfThunder => 3,
        CombatAbility.BladesOfIce => 4,
        CombatAbility.PhoenixStrike => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    internal static void Swing(Mobile owner, CombatAbility ability)
    {
        int family = Family(ability);
        owner.FixedEffect(SpellEffectGraphics.MartialSwing + family * SpellEffectGraphics.MartialFrames, 10, 5);
        owner.PlaySound(SpellEffectSounds.MartialTigerSwing + family);
    }

    internal static void Contact(Mobile owner, Point3D point, Map map, CombatAbility ability, DateTime at)
    {
        if (World.Loading || map == null || map == Map.Internal) { return; }
        int family = Family(ability);
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.MartialContact + family * SpellEffectGraphics.MartialFrames, 6);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.MartialTigerContact + family, point, map, at, 120);
    }

    internal static void Gain(Mobile owner, MartialCharge charge, DateTime at, bool incremented)
    {
        Refresh(owner, charge, at, renewed: true);
        if (incremented && CanPresent(owner))
        {
            SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.MartialChargeGain, owner.Location, owner.Map, at, 140);
        }
    }

    internal static void Refresh(Mobile owner, MartialCharge charge, DateTime at, bool renewed = false)
    {
        if (!CanPresent(owner))
        {
            Clear(owner, charge, at, expired: false);
            return;
        }
        var key = (charge.Owner, charge.Ability);
        int variant = Family(charge.Ability) * SpellEffectGraphics.MartialChargeCount + charge.Count - 1;
        ushort graphic = (ushort)(SpellEffectGraphics.MartialOrbs + variant * SpellEffectGraphics.MartialOrbFrames);
        var previous = Shown.GetValueOrDefault(key);
        if (previous.Graphic != graphic && previous.Graphic != 0 && previous.Map == owner.Map)
        {
            PaladinAuraVisuals.Clear(owner, previous.Graphic);
        }
        else if (!renewed && previous.Map == owner.Map && previous.RefreshAfter > at) { return; }

        var refreshEnd = at.AddMilliseconds(1400);
        if (PaladinAuraVisuals.Attach(owner, graphic, charge.UntilUtc < refreshEnd ? charge.UntilUtc : refreshEnd, at))
        {
            Shown[key] = new(graphic, owner.Map, at.AddMilliseconds(700));
        }
    }

    internal static void Clear(Mobile owner, MartialCharge charge, DateTime at, bool expired)
    {
        if (!Shown.Remove((charge.Owner, charge.Ability), out var previous)
            || !PaladinAuraVisuals.InWorld(owner) || previous.Map != owner.Map) { return; }
        PaladinAuraVisuals.Clear(owner, previous.Graphic);
        if (expired && owner is { Alive: true, Hidden: false })
        {
            SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.MartialChargeFade, owner.Location, owner.Map, at, 230);
        }
    }

    private static bool CanPresent(Mobile owner) => owner is { Alive: true, Hidden: false } && PaladinAuraVisuals.InWorld(owner);

    internal static void Ring(Point3D point, Map map, int graphic, int sound, double radius)
    {
        int encoded = (int)Math.Clamp(Math.Round(radius * SpellEffectGraphics.HolyAuraRadiusUnitsPerTile), 1, ushort.MaxValue);
        Effects.SendLocationEffect(point, map, graphic, 8, hue: encoded);
        Effects.PlaySound(point, map, sound);
    }
}
