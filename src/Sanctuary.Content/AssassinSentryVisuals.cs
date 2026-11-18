// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal static class AssassinSentryVisuals
{
    private const int Frames = SpellEffectGraphics.SentryFrames;

    private static int ModelBase(CombatAbility ability)
    {
        int type = ability switch
        {
            CombatAbility.WakeOfFire => 0,
            CombatAbility.WakeOfInferno => 1,
            CombatAbility.ChargedBoltSentry => 2,
            CombatAbility.LightningSentry => 3,
            CombatAbility.DeathSentry => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(ability))
        };
        return SpellEffectGraphics.SentryModel + type * SpellEffectGraphics.SentryModelFrames;
    }

    internal static ushort Model(SentryBodyState state)
    {
        int mode = state.Control?.Mode ?? state.WakeControl?.Mode ?? state.InfernoControl?.Mode ?? state.DeathControl.Mode;
        int frame = state.Control?.Frame ?? state.WakeControl?.Frame ?? state.InfernoControl?.Frame ?? state.DeathControl.Frame;
        int phase = mode == 8 ? frame * Frames / AssassinSentryEmergence.EndDelay() : Frames + frame / 4 % Frames;
        return (ushort)(ModelBase(state.Creation.Ability) + phase);
    }

    internal static void Collapse(AssassinSentryBody body, SentryRetirementState retirement)
    {
        if (!PaladinAuraVisuals.InWorld(body) || body.Hidden) { return; }
        DateTime visibleFrom = TimedEffects.Now;
        if (visibleFrom < retirement.EnteredUtc) { visibleFrom = retirement.EnteredUtc; }
        int duration = (int)Math.Ceiling((retirement.CompletesUtc - visibleFrom).TotalMilliseconds / 50);
        // The source actor can become invisible as soon as retirement starts.
        // Show its debris on the tile while the native corpse transition completes.
        body.FixedEffect(SpellEffectGraphics.SentryModelGone, 64, 64);
        if (duration <= 0) { return; }
        Effects.SendLocationEffect(body, ModelBase(body.Prepared.Creation.Ability) + 2 * Frames, duration);
        Effects.PlaySound(body.Location, body.Map, SpellEffectSounds.SentryCollapse);
    }

    private static bool IsFire(CombatAbility ability) => ability is CombatAbility.WakeOfFire or CombatAbility.WakeOfInferno;

    internal static void Placed(Mobile caster, AssassinSentryBody body, DateTime at)
    {
        if (PaladinAuraVisuals.InWorld(caster) && !caster.Hidden)
        {
            caster.FixedEffect(IsFire(body.Prepared.Creation.Ability) ? SpellEffectGraphics.FireBlastThrow
                : SpellEffectGraphics.ShockWebThrow, 10, 6);
        }
        if (PaladinAuraVisuals.InWorld(body))
        {
            body.PresentFrame(at);
            body.PlaySound(SpellEffectSounds.SentryPlacement);
        }
    }

    internal static void Release(AssassinSentryBody body, DateTime at)
    {
        if (!PaladinAuraVisuals.InWorld(body) || body.Hidden) { return; }
        var ability = body.Prepared.Creation.Ability;
        int cue = ability switch
        {
            CombatAbility.WakeOfFire => SpellEffectSounds.SentryWakeRelease,
            CombatAbility.WakeOfInferno => SpellEffectSounds.SentryInfernoRelease,
            CombatAbility.ChargedBoltSentry => SpellEffectSounds.SentryChargedRelease,
            CombatAbility.LightningSentry => SpellEffectSounds.SentryLightningRelease,
            CombatAbility.DeathSentry => SpellEffectSounds.DeathSentryRelease,
            _ => throw new ArgumentOutOfRangeException(nameof(body))
        };
        body.Flash(IsFire(ability) ? SpellEffectGraphics.SentryFireFlash : SpellEffectGraphics.SentryLightningFlash, at);
        SpellAreaSounds.Pulse(body.Serial.Value, cue, body.Location, body.Map, at, 260);
    }

    internal static void Flight(CombatAbility ability, Map map, double x, double y, int z, double vx, double vy, int phase)
    {
        int graphic = ability switch
        {
            CombatAbility.WakeOfFire => SorceressVisuals.DirectionalFrame(SpellEffectGraphics.SentryWakeFlight, vx, vy, phase),
            CombatAbility.WakeOfInferno => SorceressVisuals.DirectionalFrame(SpellEffectGraphics.SentryInfernoFlight, vx, vy, phase),
            CombatAbility.ChargedBoltSentry => SpellEffectGraphics.SentryChargedFlight + phase % Frames,
            _ => SorceressVisuals.DirectionalFrame(SpellEffectGraphics.SentryLightningFlight, vx, vy, phase)
        };
        GroundSpellVisuals.Sample(map, x, y, z, graphic, 2);
    }

    internal static void Impact(string missile, Mobile target)
    {
        if (!PaladinAuraVisuals.InWorld(target) || target.Hidden) { return; }
        var (graphic, cue) = missile switch
        {
            "inferno sentry 1" or "wake of destruction" => (SpellEffectGraphics.FireBoltContact, SpellEffectSounds.FireBoltContact),
            "sentrychargedbolt" => (SpellEffectGraphics.ChargedBoltContact, SpellEffectSounds.ChargedBoltContact),
            _ => (SpellEffectGraphics.LightningContact, SpellEffectSounds.LightningContact)
        };
        target.FixedEffect(graphic, 10, 6);
        SpellAreaSounds.Pulse(target.Serial.Value, cue, target.Location, target.Map, TimedEffects.Now, 210);
    }

    internal static void Explode(BaseCreature actor, Map map, Point3D point, int range)
    {
        if (actor is AssassinSentryBody body) { Release(body, TimedEffects.Now); }
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.DeathSentryBurst, 8,
            hue: range * SpellEffectGraphics.HolyAuraRadiusUnitsPerTile / 2);
        Effects.PlaySound(point, map, SpellEffectSounds.DeathSentryBurst);
    }
}
