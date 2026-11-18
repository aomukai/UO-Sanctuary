// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.CompilerServices;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal static class WarlockFireVisuals
{
    private sealed class ImpactAudio { internal DateTime NextUtc; }
    private static readonly ConditionalWeakTable<PlayerMobile, ImpactAudio> Audio = new();

    internal static void Contact(PlayerMobile owner, Map map, Point3D point, ushort graphic, int cue, DateTime at)
    {
        Effects.SendLocationEffect(point, map, graphic, 7);
        var audio = Audio.GetValue(owner, static _ => new ImpactAudio());
        if (at < audio.NextUtc) { return; }
        audio.NextUtc = at.AddMilliseconds(160);
        Effects.PlaySound(point, map, cue);
    }
}
