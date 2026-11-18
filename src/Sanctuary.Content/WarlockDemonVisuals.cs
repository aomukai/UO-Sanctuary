// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal static class WarlockDemonVisuals
{
    private readonly record struct Style(ushort Cast, ushort Arrival, ushort Departure,
        int CastCue, int ArrivalCue, int DepartureCue);
    private static readonly HashSet<Serial> Present = new();

    private static Style Art(SkeletonGuardian body) => body switch
    {
        WarlockGoatmanBody => new(SpellEffectGraphics.GoatmanCast, SpellEffectGraphics.GoatmanArrival,
            SpellEffectGraphics.GoatmanDeparture, SpellEffectSounds.GoatmanCast, SpellEffectSounds.GoatmanArrival,
            SpellEffectSounds.GoatmanDeparture),
        WarlockTaintedBody => new(SpellEffectGraphics.TaintedCast, SpellEffectGraphics.TaintedArrival,
            SpellEffectGraphics.TaintedDeparture, SpellEffectSounds.TaintedCast, SpellEffectSounds.TaintedArrival,
            SpellEffectSounds.TaintedDeparture),
        WarlockDefilerBody => new(SpellEffectGraphics.DefilerCast, SpellEffectGraphics.DefilerArrival,
            SpellEffectGraphics.DefilerDeparture, SpellEffectSounds.DefilerCast, SpellEffectSounds.DefilerArrival,
            SpellEffectSounds.DefilerDeparture),
        _ => throw new ArgumentOutOfRangeException(nameof(body))
    };

    private static bool Visible(Mobile actor) => PaladinAuraVisuals.InWorld(actor)
        && actor is { Alive: true, Hidden: false };

    internal static void Release(PlayerMobile caster, SkeletonGuardian body)
    {
        var style = Art(body);
        if (Visible(caster))
        {
            caster.FixedEffect(style.Cast, 10, 6);
            caster.PlaySound(style.CastCue);
        }
        Arrive(body);
    }

    internal static void Arrive(BaseCreature actor)
    {
        if (actor is not SkeletonGuardian body || !DemonNativeState.Learned(body) || !Visible(body)) { return; }
        if (!Present.Add(body.Serial)) { return; }
        var style = Art(body);
        Effects.SendLocationEffect(body, style.Arrival, 10);
        Effects.PlaySound(body.Location, body.Map, style.ArrivalCue);
    }

    internal static void Track(SkeletonGuardian body)
    {
        if (DemonNativeState.Learned(body) && Visible(body)) { Present.Add(body.Serial); }
    }

    internal static void Depart(BaseCreature actor, bool show = true)
    {
        if (!Present.Remove(actor.Serial) || actor is not SkeletonGuardian body || !PaladinAuraVisuals.InWorld(body))
        { return; }
        if (body is WarlockTaintedBody) { PaladinAuraVisuals.Clear(body, SpellEffectGraphics.TaintedFireWard); }
        if (body is WarlockGoatmanBody) { BarbarianStrikeVisuals.ClearBerserk(body.Serial.Value, body); }
        if (!show || body.Hidden) { return; }
        var style = Art(body);
        Effects.SendLocationEffect(body, style.Departure, 10);
        Effects.PlaySound(body.Location, body.Map, style.DepartureCue);
    }

    internal static void GoatmanHit(WarlockGoatmanBody body, Mobile target, Point3D point, Map map,
        MonsterContactResult result, DateTime at)
    {
        if (!DemonNativeState.Learned(body) || result.Commit == null
            || (result.Packet.ResultFlags & SourceMeleeResult.Successful) == 0) { return; }
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.GoatmanContact, 6);
        SpellAreaSounds.Pulse(target.Serial.Value, SpellEffectSounds.GoatmanContact, point, map, at, 220);
    }

    internal static void TaintedWard(WarlockTaintedBody body, DateTime now, DateTime until)
    {
        if (DemonNativeState.Learned(body) && Visible(body))
        { PaladinAuraVisuals.Attach(body, SpellEffectGraphics.TaintedFireWard, until, now); }
    }

    internal static void DefilerLinkRelease(WarlockDefilerBody body)
    {
        if (!DemonNativeState.Learned(body) || !Visible(body)) { return; }
        body.FixedEffect(SpellEffectGraphics.DefilerCast, 10, 6);
        body.PlaySound(SpellEffectSounds.DefilerLinkCast);
    }

    internal static void DefilerLinkImpact(WarlockDefilerBody body, Mobile target, Point3D point, Map map, DateTime at)
    {
        if (!DemonNativeState.Learned(body)) { return; }
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.DefilerLinkContact, 6);
        SpellAreaSounds.Pulse(target.Serial.Value, SpellEffectSounds.DefilerLinkContact, point, map, at, 230);
    }
}
