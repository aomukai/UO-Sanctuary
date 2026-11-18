// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

internal static class BarbarianStrikeVisuals
{
    private readonly record struct Lease(DateTime Until, DateTime RenewAt);
    private static readonly Dictionary<uint, Lease> BerserkWards = new();

    private static (int Swing, int SwingCue, int Impact, int ImpactCue) Style(CombatAbility ability) => ability switch
    {
        CombatAbility.Bash => (SpellEffectGraphics.BashSwing, SpellEffectSounds.BashSwing,
            SpellEffectGraphics.BashImpact, SpellEffectSounds.BashImpact),
        CombatAbility.Stun => (SpellEffectGraphics.StunSwing, SpellEffectSounds.StunSwing,
            SpellEffectGraphics.StunImpact, SpellEffectSounds.StunImpact),
        CombatAbility.Concentrate => (SpellEffectGraphics.ConcentrateSwing, SpellEffectSounds.ConcentrateSwing,
            SpellEffectGraphics.ConcentrateImpact, SpellEffectSounds.ConcentrateImpact),
        CombatAbility.Berserk => (SpellEffectGraphics.BerserkSwing, SpellEffectSounds.BerserkSwing,
            SpellEffectGraphics.BerserkImpact, SpellEffectSounds.BerserkImpact),
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    internal static void Swing(Mobile owner, CombatAbility ability)
    {
        if (!PaladinAuraVisuals.InWorld(owner)) { return; }
        var style = Style(ability);
        owner.FixedEffect(style.Swing, 10, 6);
        owner.PlaySound(style.SwingCue);
    }

    internal static void Impact(Mobile target, Point3D point, Map map, CombatAbility ability, PreparedMonsterCommitResult commit)
    {
        if (World.Loading || map == null || map == Map.Internal || commit is not { Packet.ReactionSettled: false }) { return; }
        var style = Style(ability);
        Effects.SendLocationEffect(point, map, style.Impact, 7);
        Effects.PlaySound(point, map, style.ImpactCue);
        if (ability == CombatAbility.Stun && commit.StunFrames > 0 && target.Alive)
        {
            target.FixedEffect(SpellEffectGraphics.StunConcussion, 10, 7);
            target.PlaySound(SpellEffectSounds.StunConcussion);
        }
    }

    internal static void BashStep(Mobile target, Point3D from, DateTime now)
    {
        if (!PaladinAuraVisuals.InWorld(target)) { return; }
        Effects.SendLocationEffect(from, target.Map, SpellEffectGraphics.BashSkid, 6);
        SpellAreaSounds.Pulse(target.Serial.Value, SpellEffectSounds.BashSkid, from, target.Map, now, 160);
    }

    internal static void Concentrate(Mobile owner, long end)
    {
        if (PaladinAuraVisuals.Attach(owner, SpellEffectGraphics.ConcentrateFocus, TimedEffects.TimeAt(end), TimedEffects.Now))
        { owner.PlaySound(SpellEffectSounds.ConcentrateFocus); }
    }

    internal static void Berserk(Mobile owner, DateTime until, DateTime now)
    {
        uint serial = owner.Serial.Value;
        if (until <= now) { ClearBerserk(serial, owner); return; }
        var previous = BerserkWards.GetValueOrDefault(serial);
        if (previous.Until == until && previous.RenewAt > now) { return; }
        var leaseEnd = now.AddMilliseconds(1500);
        if (!PaladinAuraVisuals.Attach(owner, SpellEffectGraphics.BerserkState, until < leaseEnd ? until : leaseEnd, now)) { return; }
        BerserkWards[serial] = new(until, now.AddMilliseconds(680));
        if (previous.Until <= now) { owner.PlaySound(SpellEffectSounds.BerserkState); }
    }

    internal static void ClearBerserk(uint serial, Mobile owner)
    {
        if (!BerserkWards.Remove(serial) || !PaladinAuraVisuals.InWorld(owner)) { return; }
        PaladinAuraVisuals.Clear(owner, SpellEffectGraphics.BerserkState);
        if (!owner.Alive) { return; }
        owner.FixedEffect(SpellEffectGraphics.BerserkFade, 10, 8);
        owner.PlaySound(SpellEffectSounds.BarbarianWardFade);
    }
}
