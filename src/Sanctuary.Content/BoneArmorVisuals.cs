// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

internal static class BoneArmorVisuals
{
    private static readonly HashSet<Serial> Armed = new();
    private static readonly List<Serial> Keys = new();

    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(400),
        TimeSpan.FromMilliseconds(400), Tick);

    internal static void Track(Mobile actor, bool active)
    {
        bool changed = active ? Armed.Add(actor.Serial) : Armed.Remove(actor.Serial);
        if (changed && Visible(actor)) { Send(actor, active); }
    }

    private static bool Visible(Mobile actor) =>
        !World.Loading && actor is { Alive: true, Deleted: false, Map: { } map } && map != Map.Internal;

    private static void Send(Mobile actor, bool active) =>
        actor.FixedEffect(SpellEffectGraphics.BoneArmorWard, 10, active ? 13 : 0);

    private static void Tick()
    {
        Keys.Clear();
        Keys.AddRange(Armed);
        foreach (var serial in Keys)
        {
            var actor = World.FindMobile(serial);
            if (NecromancerEffects.Profiles.Find(actor) is not { BoneArmor: > 0 })
            {
                Armed.Remove(serial);
                if (Visible(actor)) { Send(actor, false); }
            }
            else if (Visible(actor))
            {
                Send(actor, true);
            }
        }
    }

    internal static void Absorbed(Mobile target, AbsorptionStep step)
    {
        if (step.Absorbed256 <= 0 || !Visible(target)) { return; }
        if (step.Remaining256 == 0)
        {
            target.FixedEffect(SpellEffectGraphics.BoneArmorBreak, 10, 8);
            target.PlaySound(SpellEffectSounds.BoneArmorBreak);
        }
        else
        {
            target.FixedEffect(SpellEffectGraphics.BoneArmorCatch, 10, 3);
            SpellAreaSounds.Pulse(target.Serial.Value, SpellEffectSounds.BoneArmorCatch,
                target.Location, target.Map, TimedEffects.Now, 170);
        }
    }
}
