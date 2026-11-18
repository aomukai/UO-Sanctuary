// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class ShadowSummonVisuals
{
    private readonly record struct Art(ushort Cast, ushort Arrival, ushort Veil, ushort Departure,
        int CastCue, int ArrivalCue, int DepartureCue);

    private static Art For(CombatAbility ability) => ability switch
    {
        CombatAbility.ShadowWarrior => new(SpellEffectGraphics.ShadowWarriorCast,
            SpellEffectGraphics.ShadowWarriorArrival, SpellEffectGraphics.ShadowWarriorVeil,
            SpellEffectGraphics.ShadowWarriorDeparture, SpellEffectSounds.ShadowWarriorCast,
            SpellEffectSounds.ShadowWarriorArrival, SpellEffectSounds.ShadowWarriorDeparture),
        CombatAbility.ShadowMaster => new(SpellEffectGraphics.ShadowMasterCast,
            SpellEffectGraphics.ShadowMasterArrival, SpellEffectGraphics.ShadowMasterVeil,
            SpellEffectGraphics.ShadowMasterDeparture, SpellEffectSounds.ShadowMasterCast,
            SpellEffectSounds.ShadowMasterArrival, SpellEffectSounds.ShadowMasterDeparture),
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    internal static void Release(Mobile caster, CombatAbility ability)
    {
        if (!PaladinAuraVisuals.InWorld(caster) || caster.Hidden) { return; }
        var art = For(ability);
        caster.FixedEffect(art.Cast, 10, 6);
        caster.PlaySound(art.CastCue);
    }

    internal static void Arrival(AssassinShadowBody body, CombatAbility ability)
    {
        var art = For(ability);
        Effects.SendLocationEffect(body, art.Arrival, 10);
        Effects.PlaySound(body.Location, body.Map, art.ArrivalCue);
    }

    internal static void RenewVeil(AssassinShadowBody body, CombatAbility ability, DateTime now)
        => PaladinAuraVisuals.Attach(body, For(ability).Veil, now.AddMilliseconds(900), now);

    internal static void Departure(AssassinShadowBody body, CombatAbility ability)
    {
        var art = For(ability);
        PaladinAuraVisuals.Clear(body, art.Veil);
        if (body.Hidden) { return; }
        Effects.SendLocationEffect(body, art.Departure, 10);
        Effects.PlaySound(body.Location, body.Map, art.DepartureCue);
    }
}
