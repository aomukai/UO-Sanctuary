// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;

namespace Sanctuary;

internal static class SpellAreaSounds
{
    private static readonly Dictionary<(uint Owner, int Cue), DateTime> CueAfter = new();
    private static readonly List<(uint Owner, int Cue)> ExpiredCues = new();
    private static DateTime _cleanupAt;

    internal static void Pulse(uint owner, int cue, Point3D point, Map map, DateTime now, int spacing)
    {
        var key = (owner, cue);
        if (CueAfter.GetValueOrDefault(key) > now) { return; }
        Effects.PlaySound(point, map, cue);
        CueAfter[key] = now.AddMilliseconds(spacing);
    }

    internal static void Prune(DateTime now)
    {
        if (_cleanupAt > now) { return; }
        _cleanupAt = now.AddSeconds(1);
        ExpiredCues.Clear();
        foreach (var (cue, until) in CueAfter)
        {
            if (until <= now) { ExpiredCues.Add(cue); }
        }
        foreach (var cue in ExpiredCues) { CueAfter.Remove(cue); }
    }
}
