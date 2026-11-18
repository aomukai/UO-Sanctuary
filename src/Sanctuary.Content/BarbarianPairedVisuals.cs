// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

internal static class BarbarianPairedVisuals
{
    private static readonly Dictionary<uint, (Map Map, DateTime After)> FrenzyRefreshAfter = new();

    internal static void Swing(Mobile owner, CombatAbility ability, bool secondary)
    {
        var (graphic, sound) = (ability, secondary) switch
        {
            (CombatAbility.DoubleSwing, false) => (SpellEffectGraphics.DoubleSwingLead, SpellEffectSounds.DoubleSwingLead),
            (CombatAbility.DoubleSwing, true) => (SpellEffectGraphics.DoubleSwingFollow, SpellEffectSounds.DoubleSwingFollow),
            (_, false) => (SpellEffectGraphics.FrenzyLead, SpellEffectSounds.FrenzyLead),
            _ => (SpellEffectGraphics.FrenzyFollow, SpellEffectSounds.FrenzyFollow)
        };
        owner.FixedEffect(graphic, 10, 5);
        owner.PlaySound(sound);
    }

    internal static void Contact(CombatAbility ability, bool secondary, Point3D point, Map map, PreparedMonsterCommitResult commit)
    {
        if (commit is not { Packet.ReactionSettled: false }) { return; }
        var (graphic, sound) = (ability, secondary) switch
        {
            (CombatAbility.DoubleSwing, false) => (SpellEffectGraphics.DoubleSwingLeadImpact, SpellEffectSounds.DoubleSwingLeadImpact),
            (CombatAbility.DoubleSwing, true) => (SpellEffectGraphics.DoubleSwingFollowImpact, SpellEffectSounds.DoubleSwingFollowImpact),
            _ => (SpellEffectGraphics.FrenzyImpact, SpellEffectSounds.FrenzyImpact)
        };
        Effects.SendLocationEffect(point, map, graphic, 6);
        Effects.PlaySound(point, map, sound);
    }

    internal static void RefreshFrenzy(Mobile owner, FrenzySpeedState state, DateTime now, bool renewed = false)
    {
        if (!PaladinAuraVisuals.InWorld(owner))
        {
            FrenzyRefreshAfter.Remove(state.Owner);
            return;
        }
        if (owner.Hidden)
        {
            ClearFrenzy(state.Owner, owner, expired: false);
            return;
        }
        var last = FrenzyRefreshAfter.GetValueOrDefault(state.Owner);
        if (!renewed && last.Map == owner.Map && last.After > now) { return; }

        var refreshEnd = now.AddMilliseconds(1600);
        if (PaladinAuraVisuals.Attach(owner, SpellEffectGraphics.FrenzySpeed,
                state.UntilUtc < refreshEnd ? state.UntilUtc : refreshEnd, now))
        {
            FrenzyRefreshAfter[state.Owner] = (owner.Map, now.AddMilliseconds(800));
        }
    }

    internal static void ClearFrenzy(uint serial, Mobile owner, bool expired)
    {
        if (!FrenzyRefreshAfter.Remove(serial) || !PaladinAuraVisuals.InWorld(owner)) { return; }
        PaladinAuraVisuals.Clear(owner, SpellEffectGraphics.FrenzySpeed);
        if (expired && owner is { Alive: true, Hidden: false })
        {
            owner.FixedEffect(SpellEffectGraphics.FrenzyFade, 10, 8);
            owner.PlaySound(SpellEffectSounds.FrenzyFade);
        }
    }

    internal static void ThrowLaunch(Mobile owner, WeaponFlight flight)
    {
        ThrowFlight(owner.Map, flight);
        owner.PlaySound(flight.Missile switch
        {
            "throwknife" => SpellEffectSounds.DoubleThrowKnife,
            "throwaxe" => SpellEffectSounds.DoubleThrowAxe,
            _ => SpellEffectSounds.DoubleThrowSpear
        });
    }

    internal static void ThrowFlight(Map map, WeaponFlight flight)
    {
        int phase = flight.Frame / 2;
        int graphic = flight.Missile switch
        {
            "throwknife" => SpellEffectGraphics.DoubleThrowKnife + phase % SpellEffectGraphics.BarbarianThrowSpinFrames,
            "throwaxe" => SpellEffectGraphics.DoubleThrowAxe + phase % SpellEffectGraphics.BarbarianThrowSpinFrames,
            "glaive" => Aim(SpellEffectGraphics.DoubleThrowGlaive),
            "pilum" => Aim(SpellEffectGraphics.DoubleThrowPilum),
            _ => Aim(SpellEffectGraphics.DoubleThrowJavelin)
        };
        GroundSpellVisuals.Sample(map, flight.X, flight.Y, flight.Z, graphic, 2, 32);

        int Aim(int basis) => basis + SpellEffectGraphics.ProjectileFacing(flight.Vx, flight.Vy)
            * SpellEffectGraphics.BarbarianPairedFrames + phase % SpellEffectGraphics.BarbarianPairedFrames;
    }

    internal static void ThrowImpact(Point3D point, Map map)
    {
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.DoubleThrowImpact, 6);
        Effects.PlaySound(point, map, SpellEffectSounds.DoubleThrowImpact);
    }
}
