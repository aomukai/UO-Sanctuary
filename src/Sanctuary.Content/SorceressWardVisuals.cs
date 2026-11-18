// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Movement;

namespace Sanctuary;

internal static class SorceressWardVisuals
{
    private readonly record struct Lease(ushort Graphic, DateTime Until, DateTime Refresh);
    private sealed class WardEffects
    {
        internal Lease Armor, Shield, Enchantment;
        internal DateTime CatchSoundAfter;
    }

    private static readonly Dictionary<Serial, WardEffects> Active = new();
    private static readonly List<Serial> Keys = new();

    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(200), Tick);

    internal static void Track(Mobile actor, SorceressState state)
    {
        if (!Active.TryGetValue(actor.Serial, out var effects))
        {
            if (state?.Armor == null && state?.Shield == null && state?.Enchantment == null
                && MagePetBuffs.Armor(actor) == null) { return; }
            Active.Add(actor.Serial, effects = new WardEffects());
        }
        Refresh(actor, state, effects, TimedEffects.Now);
    }

    internal static void Clear(Mobile actor)
    {
        if (Active.Remove(actor.Serial, out var effects) && Visible(actor))
        {
            if (effects.Armor.Graphic != 0) { actor.FixedEffect(effects.Armor.Graphic, 10, 0); }
            if (effects.Shield.Graphic != 0) { actor.FixedEffect(effects.Shield.Graphic, 10, 0); }
            if (effects.Enchantment.Graphic != 0) { actor.FixedEffect(effects.Enchantment.Graphic, 10, 0); }
        }
    }

    private static bool Visible(Mobile actor) => !World.Loading && !actor.Deleted && actor.Map != null && actor.Map != Map.Internal;

    private static void Tick()
    {
        var now = TimedEffects.Now;
        Keys.Clear();
        Keys.AddRange(Active.Keys);
        foreach (var serial in Keys)
        {
            if (!Active.TryGetValue(serial, out var effects)) { continue; }
            var actor = World.FindMobile(serial);
            if (actor is not { Alive: true, Deleted: false })
            {
                if (actor == null) { Active.Remove(serial); }
                else { Clear(actor); }
                continue;
            }
            Refresh(actor, SorceressEffects.Profiles.Find(actor), effects, now);
        }
    }

    private static void Refresh(Mobile actor, SorceressState state, WardEffects effects, DateTime now)
    {
        var activeArmor = MagePetBuffs.Armor(actor) ?? state?.Armor;
        ushort armor = activeArmor?.Ability switch
        {
            CombatAbility.FrozenArmor => SpellEffectGraphics.FrozenArmorWard,
            CombatAbility.ShiverArmor => SpellEffectGraphics.ShiverArmorWard,
            CombatAbility.ChillingArmor => SpellEffectGraphics.ChillingArmorWard,
            _ => 0
        };
        Update(actor, ref effects.Armor, armor, activeArmor?.UntilUtc ?? default, now);
        Update(actor, ref effects.Shield, SpellEffectGraphics.EnergyShieldWard, state?.Shield?.UntilUtc ?? default, now);
        Update(actor, ref effects.Enchantment, SpellEffectGraphics.EnchantWard, state?.Enchantment?.UntilUtc ?? default, now);
        if (effects.Armor.Graphic == 0 && effects.Shield.Graphic == 0 && effects.Enchantment.Graphic == 0)
        {
            Active.Remove(actor.Serial);
        }
    }

    private static void Update(Mobile actor, ref Lease lease, ushort graphic, DateTime until, DateTime now)
    {
        if (until <= now) { graphic = 0; }
        bool visible = Visible(actor) && actor.Alive;
        bool changed = graphic != lease.Graphic;
        if (changed && lease.Graphic != 0 && visible)
        {
            actor.FixedEffect(lease.Graphic, 10, 0);
            if (graphic == 0 && lease.Until <= now) { Fade(actor, lease.Graphic); }
        }
        if (graphic == 0 || !visible)
        {
            // An internalized caster can still have an active mechanical ward.
            lease = new Lease(graphic, until, default);
            return;
        }
        if (!changed && lease.Until == until && lease.Refresh > now) { return; }

        double remaining = (until - now).TotalMilliseconds;
        actor.FixedEffect(graphic, 10, (int)Math.Ceiling(Math.Min(remaining, 650) / 50));
        lease = new Lease(graphic, until, now.AddMilliseconds(Math.Min(remaining, 400)));
    }

    private static void Fade(Mobile actor, ushort ward)
    {
        int graphic = ward switch
        {
            SpellEffectGraphics.FrozenArmorWard => SpellEffectGraphics.FrozenArmorFade,
            SpellEffectGraphics.ShiverArmorWard => SpellEffectGraphics.ShiverArmorFade,
            SpellEffectGraphics.ChillingArmorWard => SpellEffectGraphics.ChillingArmorFade,
            SpellEffectGraphics.EnchantWard => SpellEffectGraphics.EnchantFade,
            _ => SpellEffectGraphics.EnergyShieldFade
        };
        actor.FixedEffect(graphic, 10, SpellEffectGraphics.WardOneShot);
        actor.PlaySound(ward switch
        {
            SpellEffectGraphics.EnergyShieldWard => SpellEffectSounds.EnergyShieldExpiry,
            SpellEffectGraphics.EnchantWard => SpellEffectSounds.EnchantExpiry,
            _ => SpellEffectSounds.ColdWardExpiry
        });
    }

    internal static void Catch(PlayerMobile owner, Mobile source)
    {
        int dx = source != null && source.Map == owner.Map ? source.X - owner.X : 0;
        int dy = source != null && source.Map == owner.Map ? source.Y - owner.Y : 0;
        if (dx == 0 && dy == 0) { Movement.Offset(owner.Direction, ref dx, ref dy); }
        int facing = SpellEffectGraphics.ProjectileFacing(dx, dy);
        owner.FixedEffect(SpellEffectGraphics.EnergyShieldCatch + facing * SpellEffectGraphics.WardFrames,
            10, SpellEffectGraphics.WardFrames);

        var now = TimedEffects.Now;
        if (Active.TryGetValue(owner.Serial, out var effects) && effects.CatchSoundAfter > now) { return; }
        owner.PlaySound(SpellEffectSounds.EnergyShieldCatch);
        if (effects != null) { effects.CatchSoundAfter = now.AddMilliseconds(180); }
    }

    internal static void Break(PlayerMobile owner)
    {
        owner.FixedEffect(SpellEffectGraphics.EnergyShieldFade, 10, SpellEffectGraphics.WardOneShot);
        owner.PlaySound(SpellEffectSounds.EnergyShieldBreak);
    }
}
