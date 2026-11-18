// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;

namespace Sanctuary;

internal static class AssassinShadowVisuals
{
    private readonly record struct Lease(DateTime Until, DateTime Refresh, Map Map, bool Shadow);
    private static readonly Dictionary<(uint Actor, CombatAbility Skill), Lease> Leases = new();
    private static readonly HashSet<(uint Actor, CombatAbility Skill)> ShadowBuffs = new();

    private static (int Cast, int Loop, int End, int CastSound, int EndSound) Buff(CombatAbility ability) => ability switch
    {
        CombatAbility.BurstOfSpeed => (SpellEffectGraphics.BurstSpeedCast, SpellEffectGraphics.BurstSpeedWard,
            SpellEffectGraphics.BurstSpeedEnd, SpellEffectSounds.BurstSpeedCast, SpellEffectSounds.BurstSpeedEnd),
        CombatAbility.Fade => (SpellEffectGraphics.FadeCast, SpellEffectGraphics.FadeWard,
            SpellEffectGraphics.FadeEnd, SpellEffectSounds.FadeCast, SpellEffectSounds.FadeEnd),
        CombatAbility.Venom => (SpellEffectGraphics.VenomCast, SpellEffectGraphics.VenomCoat,
            SpellEffectGraphics.VenomEnd, SpellEffectSounds.VenomCast, SpellEffectSounds.VenomEnd),
        CombatAbility.CloakOfShadows => (0, SpellEffectGraphics.CloakMantle,
            SpellEffectGraphics.CloakEnd, 0, SpellEffectSounds.CloakEnd),
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    private static bool Visible(Mobile actor) =>
        PaladinAuraVisuals.InWorld(actor) && actor is { Alive: true, Hidden: false };

    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(250), RefreshShadows);

    internal static void Cast(Mobile actor, CombatAbility ability, DateTime until, DateTime now)
    {
        if (ability == CombatAbility.Fade) { End(actor.Serial.Value, actor, CombatAbility.BurstOfSpeed, false); }
        if (ability == CombatAbility.BurstOfSpeed) { End(actor.Serial.Value, actor, CombatAbility.Fade, false); }
        var style = Buff(ability);
        if (style.Cast != 0) { Attached(actor, style.Cast, style.CastSound); }
        Refresh(actor, ability, until, now);
    }

    internal static void Refresh(Mobile actor, CombatAbility ability, DateTime until, DateTime now)
    {
        var key = (Actor: actor.Serial.Value, Skill: ability);
        if (!Visible(actor))
        {
            End(key.Actor, actor, ability, false);
            return;
        }
        if (Leases.TryGetValue(key, out var lease) && lease.Until == until && lease.Map == actor.Map && lease.Refresh > now) { return; }
        var cap = now.AddMilliseconds(850);
        if (PaladinAuraVisuals.Attach(actor, Buff(ability).Loop, until < cap ? until : cap, now))
        { Leases[key] = new(until, now.AddMilliseconds(400), actor.Map, actor is AssassinShadowBody); }
    }

    internal static void End(uint actorId, Mobile actor, CombatAbility ability, bool expired)
    {
        if (!Leases.Remove((actorId, ability), out var lease) || actor?.Map != lease.Map) { return; }
        var style = Buff(ability);
        PaladinAuraVisuals.Clear(actor, style.Loop);
        if (expired) { Attached(actor, style.End, style.EndSound); }
    }

    private static void RefreshShadows()
    {
        var now = TimedEffects.Now;
        ShadowBuffs.Clear();
        foreach (var (actor, ability, until) in GeneratedNpcLiveResources.AssassinBuffVisuals(now))
        {
            ShadowBuffs.Add((actor.Serial.Value, ability));
            Refresh(actor, ability, until, now);
        }
        foreach (var row in Leases.Where(row => row.Value.Shadow && !ShadowBuffs.Contains(row.Key)).ToArray())
        {
            var (id, ability) = row.Key;
            End(id, World.FindMobile((Serial)id), ability, row.Value.Until <= now);
        }
    }

    internal static void Cloak(Mobile actor, int radius)
    {
        Ring(actor.Location, actor.Map, SpellEffectGraphics.CloakWave, radius);
        actor.PlaySound(SpellEffectSounds.CloakWave);
    }

    internal static void Hammer(Mobile actor, Point3D point, Map map)
    {
        Attached(actor, SpellEffectGraphics.PsychicHammerCast, SpellEffectSounds.PsychicHammerCast);
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.PsychicHammerHit, 7);
        Effects.PlaySound(point, map, SpellEffectSounds.PsychicHammerHit);
    }

    internal static void MindBlast(Mobile actor, Point3D point, int radius)
    {
        Attached(actor, SpellEffectGraphics.MindBlastCast, SpellEffectSounds.MindBlastCast);
        Ring(point, actor.Map, SpellEffectGraphics.MindBlastWave, radius);
    }

    internal static void MindImpact(Mobile actor, Point3D point, Map map)
    {
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.MindBlastHit, 7);
        SpellAreaSounds.Pulse(actor.Serial.Value, SpellEffectSounds.MindBlastHit, point, map, TimedEffects.Now, 125);
    }

    private static void Ring(Point3D point, Map map, int graphic, int radius)
    {
        int hue = (int)Math.Round(SkillGeometry.Tiles(radius) * SpellEffectGraphics.HolyAuraRadiusUnitsPerTile);
        Effects.SendLocationEffect(point, map, graphic, 8, hue: hue);
    }

    private static void Attached(Mobile actor, int graphic, int sound)
    {
        if (!Visible(actor)) { return; }
        actor.FixedEffect(graphic, 9, 7);
        actor.PlaySound(sound);
    }
}
