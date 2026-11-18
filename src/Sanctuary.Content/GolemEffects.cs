// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class GolemEffects
{
    internal static void Contact(Mobile actor, CombatAbility ability, Map map, Point3D point,
        PreparedMonsterDamagePacket packet, SpectralHitRoll spectral)
    {
        var (graphic, sound) = ability switch
        {
            CombatAbility.ClayGolem => (SpellEffectGraphics.ClayGolemContact, SpellEffectSounds.ClayGolemContact),
            CombatAbility.IronGolem => (SpellEffectGraphics.IronGolemSpark, SpellEffectSounds.IronGolemSpark),
            CombatAbility.FireGolem => (SpellEffectGraphics.FireBoltContact, SpellEffectSounds.FireBoltContact),
            _ => (SpellEffectGraphics.BoneSpearContact, SpellEffectSounds.SpearContact)
        };
        if (ability == CombatAbility.IronGolem)
        {
            int channel = spectral is { Amount: > 0 } ? spectral.Channel : Element(packet);
            (graphic, sound) = channel switch
            {
                0 => (SpellEffectGraphics.MagicArrowContact, SpellEffectSounds.MagicArrowContact),
                1 => (SpellEffectGraphics.FireBoltContact, SpellEffectSounds.FireBoltContact),
                2 => (SpellEffectGraphics.LightningContact, SpellEffectSounds.LightningContact),
                3 => (SpellEffectGraphics.IceBoltContact, SpellEffectSounds.IceBoltContact),
                4 => (SpellEffectGraphics.PoisonDaggerContact, SpellEffectSounds.PoisonDaggerContact),
                _ => (graphic, sound)
            };
        }

        Effects.SendLocationEffect(point, map, graphic, SpellEffectGraphics.IsGolemEffect(graphic) ? 7 : 5);
        SpellAreaSounds.Pulse(actor.Serial.Value, sound, point, map, TimedEffects.Now, 130);
        if (packet is { Overlay: > 0 } && graphic != SpellEffectGraphics.IronGolemSpark)
        {
            Effects.SendLocationEffect(point, map, SpellEffectGraphics.IronGolemSpark, 7);
        }
    }

    private static int Element(PreparedMonsterDamagePacket packet)
    {
        if (packet == null) { return -1; }
        int largest = Math.Max(Math.Max(packet.Magic256, packet.Fire256),
            Math.Max(Math.Max(packet.Lightning256, packet.Cold256), packet.Poison256));
        if (largest <= 0) { return -1; }
        if (largest == packet.Magic256) { return 0; }
        if (largest == packet.Fire256) { return 1; }
        if (largest == packet.Lightning256) { return 2; }
        return largest == packet.Cold256 ? 3 : 4;
    }

    internal static void Heal(Mobile target)
    {
        target.FixedEffect(SpellEffectGraphics.BloodGolemHeal, 10, 10);
        SpellAreaSounds.Pulse(target.Serial.Value, SpellEffectSounds.BloodGolemHeal,
            target.Location, target.Map, TimedEffects.Now, 260);
    }

    internal static void FirePulse(Mobile actor)
    {
        actor.FixedEffect(SpellEffectGraphics.GolemFirePulse, 10, 10);
        actor.PlaySound(SpellEffectSounds.GolemFirePulse);
    }

    internal static void FireContact(Point3D point, Map map) =>
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.FireBoltContact, 5);

    internal static void FireDeath(Point3D point, Map map)
    {
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.GolemFireDeath, 12);
        Effects.PlaySound(point, map, SpellEffectSounds.MeteorImpact);
    }

    internal static void IronThorns(Mobile defender, Point3D point, Map map)
    {
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.IronGolemSpark, 7);
        SpellAreaSounds.Pulse(defender.Serial.Value, SpellEffectSounds.IronGolemSpark,
            point, map, TimedEffects.Now, 170);
    }

    private static bool Visible(Mobile target) =>
        !World.Loading && !target.Deleted && target.Map is { } map && map != Map.Internal;

    internal static void RefreshSlow(Mobile target, TimedActor actor, DateTime now)
    {
        if (actor.SlowAttackPercent == null || actor.SlowUntil <= now)
        {
            ClearSlow(target, actor);
            return;
        }
        if (actor.SlowVisualRefresh > now || !Visible(target)) { return; }
        int remaining = (int)Math.Min(700, Math.Ceiling((actor.SlowUntil - now).TotalMilliseconds));
        target.FixedEffect(SpellEffectGraphics.GolemSlow, 10, (remaining + 49) / 50);
        actor.SlowVisualRefresh = now.AddMilliseconds(Math.Min(remaining, 450));
    }

    internal static void ClearSlow(Mobile target, TimedActor actor)
    {
        if (actor.SlowVisualRefresh == default) { return; }
        actor.SlowVisualRefresh = default;
        if (Visible(target)) { target.FixedEffect(SpellEffectGraphics.GolemSlow, 10, 0); }
    }
}
