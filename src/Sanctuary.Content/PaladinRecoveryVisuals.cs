// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using static Sanctuary.PaladinAuraVisuals;

namespace Sanctuary;

internal static class PaladinRecoveryVisuals
{
    public static void Configure() => Mobile.ManaRegenerated += ManaRegenerated;

    internal static bool Handles(CombatAbility ability) => ability is CombatAbility.Prayer or CombatAbility.Cleansing
        or CombatAbility.Meditation or CombatAbility.Redemption;

    internal static bool Active(PaladinAuraLease lease) => lease != null && Handles(lease.Ability)
        && (lease.Percent > 0 || lease.Ability == CombatAbility.Redemption);

    private static int AuraGraphic(CombatAbility ability) => ability switch
    {
        CombatAbility.Prayer => SpellEffectGraphics.PrayerAura,
        CombatAbility.Cleansing => SpellEffectGraphics.CleansingAura,
        CombatAbility.Meditation => SpellEffectGraphics.MeditationAura,
        CombatAbility.Redemption => SpellEffectGraphics.RedemptionAura,
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    internal static void Clear(Mobile target, PaladinAuraLease lease)
    {
        if (Active(lease)) { PaladinAuraVisuals.Clear(target, AuraGraphic(lease.Ability)); }
    }

    internal static void Pulse(Mobile target, PaladinAuraLease lease, PaladinAuraLease previous,
        bool healed, bool purified, DateTime now)
    {
        if (!Active(lease)) { Clear(target, previous); return; }
        if (!target.Alive || !Attach(target, AuraGraphic(lease.Ability), lease.UntilUtc, now)) { return; }

        if (lease.Ability == CombatAbility.Meditation && (!Active(previous) || previous.UntilUtc <= now))
        { SpellAreaSounds.Pulse(lease.Source, SpellEffectSounds.MeditationAura, target.Location, target.Map, now, 300); }
        if (healed)
        {
            target.FixedEffect(SpellEffectGraphics.PrayerHeal, 10, 8);
            if (!purified) { SpellAreaSounds.Pulse(lease.Source, SpellEffectSounds.PrayerHeal, target.Location, target.Map, now, 300); }
        }
        if (purified)
        {
            target.FixedEffect(SpellEffectGraphics.CleansingPurify, 10, 8);
            SpellAreaSounds.Pulse(lease.Source, SpellEffectSounds.CleansingPurify, target.Location, target.Map, now, 300);
        }
    }

    private static void ManaRegenerated(Mobile actor)
    {
        if (PaladinAuras.ManaRecoveryPercent(actor) > 0 && InWorld(actor))
        { actor.FixedEffect(SpellEffectGraphics.MeditationMana, 10, 6); }
    }

    internal static void Redeemed(Mobile owner, Point3D corpse, Map map, bool life, bool mana)
    {
        var now = TimedEffects.Now;
        Effects.SendLocationEffect(corpse, map, SpellEffectGraphics.RedemptionCorpse, 8);
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.RedemptionCorpse, corpse, map, now, 180);
        if (!life && !mana) { return; }

        var destination = owner.Location;
        double dx = destination.X - corpse.X, dy = destination.Y - corpse.Y;
        int samples = Math.Clamp((int)Math.Round(Math.Sqrt(dx * dx + dy * dy) / 1.4), 0, 6);
        for (int i = 1; i <= samples; i++)
        {
            double part = i / (samples + 1.0);
            int art = SorceressVisuals.DirectionalFrame(SpellEffectGraphics.RedemptionThread, dx, dy, i);
            GroundSpellVisuals.Sample(map, corpse.X + dx * part, corpse.Y + dy * part,
                (int)Math.Round(corpse.Z + (destination.Z - corpse.Z) * part), art, 5, lift: 8 + (int)(part * 14));
        }
        if (life) { owner.FixedEffect(SpellEffectGraphics.RedemptionLife, 10, 7); }
        if (mana) { owner.FixedEffect(SpellEffectGraphics.RedemptionMana, 10, 7); }
        SpellAreaSounds.Pulse(owner.Serial.Value, SpellEffectSounds.RedemptionReceipt, destination, map, now, 180);
    }
}
