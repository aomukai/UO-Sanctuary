// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal static class SigilVisuals
{
    private readonly record struct Presentation(ushort Cast, ushort Field, ushort Fade, int CastCue);

    private static Presentation For(CombatAbility ability) => ability switch
    {
        CombatAbility.SigilLethargy => new(SpellEffectGraphics.LethargySigilCast, SpellEffectGraphics.LethargySigilField,
            SpellEffectGraphics.LethargySigilFade, SpellEffectSounds.LethargySigilCast),
        CombatAbility.SigilRancor => new(SpellEffectGraphics.RancorSigilCast, SpellEffectGraphics.RancorSigilField,
            SpellEffectGraphics.RancorSigilFade, SpellEffectSounds.RancorSigilCast),
        CombatAbility.SigilDeath => new(SpellEffectGraphics.DeathSigilCast, SpellEffectGraphics.DeathSigilField,
            SpellEffectGraphics.DeathSigilFade, SpellEffectSounds.DeathSigilCast),
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    internal static int Radius(SigilActor actor) => checked((int)SigilActors.Values(actor).Eval("aurarangecalc"));

    private static void Boundary(SigilActor actor, int duration) =>
        Effects.SendLocationEffect(actor.Origin, Map.Maps[actor.Map], For(actor.Ability).Field, duration,
            hue: Radius(actor) * SpellEffectGraphics.HolyAuraRadiusUnitsPerTile / 2);

    internal static void Cast(PlayerMobile owner, SigilActor actor, DateTime at)
    {
        var art = For(actor.Ability);
        owner.FixedEffect(art.Cast, 10, 8);
        owner.PlaySound(art.CastCue);
        Renew(actor, at);
    }

    internal static void Renew(SigilActor actor, DateTime at)
    {
        // A live sigil pulses every 240 ms. The short lease also expires during suspension.
        int duration = Math.Min(6, (int)Math.Ceiling((actor.ExpiresUtc - at).TotalMilliseconds / 50));
        if (duration > 0) { Boundary(actor, duration); }
    }

    internal static void Remove(SigilActor actor, bool clearBoundary, bool fade)
    {
        if (World.Loading) { return; }
        if (clearBoundary) { Boundary(actor, 0); }
        if (fade)
        {
            var map = Map.Maps[actor.Map];
            Effects.SendLocationEffect(actor.Origin, map, For(actor.Ability).Fade, 8);
            Effects.PlaySound(actor.Origin, map, SpellEffectSounds.SigilFade);
        }
    }

    internal static void Mark(BaseCreature target, ushort graphic, DateTime until, DateTime at)
    {
        if (!target.Hidden) { PaladinAuraVisuals.Attach(target, graphic, until, at); }
    }
}
