// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal static class MiasmaVisuals
{
    private readonly record struct CloudCell(int Map, int Z, int X2, int Y2, bool Chain);
    private readonly record struct CloudAppearance(int Radius, DateTime Until);
    private sealed class CueClock { internal DateTime Contact, Stamp, Fade; }

    private static readonly ConditionalWeakTable<PlayerMobile, CueClock> CueClocks = new();
    private static Dictionary<CloudCell, CloudAppearance> _clouds = new();
    private static DateTime _nextCloudRefresh;

    internal static void Cast(PlayerMobile owner, bool chain)
    {
        owner.FixedEffect(chain ? SpellEffectGraphics.MiasmaChainCast : SpellEffectGraphics.MiasmaBoltCast, 10, 8);
        owner.PlaySound(chain ? SpellEffectSounds.MiasmaChainCast : SpellEffectSounds.MiasmaBoltCast);
    }

    internal static void Travel(Map map, double x, double y, int z, double vx, double vy, int phase, bool chain)
    {
        if (vx == 0 && vy == 0) { return; }
        ushort first = chain ? SpellEffectGraphics.MiasmaChainLink : SpellEffectGraphics.MiasmaBoltFlight;
        int graphic = SorceressVisuals.DirectionalFrame(first, vx, vy, phase);
        GroundSpellVisuals.Sample(map, x, y, z, graphic, 1, chain ? 15 : 18);
    }

    internal static void Burst(Map map, double x, double y, int z, bool chain)
    {
        int graphic = chain ? SpellEffectGraphics.MiasmaChainLatch : SpellEffectGraphics.MiasmaBoltBurst;
        var point = GroundSpellVisuals.Sample(map, x, y, z, graphic, 8, chain ? 12 : 9);
        Effects.PlaySound(point, map, chain ? SpellEffectSounds.MiasmaChainLatch : SpellEffectSounds.MiasmaBoltBurst);
    }

    private static int Samples(double dx, double dy, double spacing, int limit)
        => Math.Clamp((int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / spacing), 1, limit);

    internal static void Stamp(PlayerMobile owner, MiasmaChainStamp stamp)
    {
        double dx = stamp.EndX - stamp.X, dy = stamp.EndY - stamp.Y;
        if (dx == 0 && dy == 0) { return; }
        var map = Map.Maps[stamp.Map];
        int count = Samples(dx, dy, 1.13, 18);
        for (int i = 0; i < count; i++)
        {
            double t = (i + .5) / count;
            int graphic = SorceressVisuals.DirectionalFrame(SpellEffectGraphics.MiasmaChainLink, dx, dy, i);
            GroundSpellVisuals.Sample(map, stamp.X + dx * t, stamp.Y + dy * t, stamp.Z, graphic, 8, 13);
        }
        var clock = CueClocks.GetValue(owner, static _ => new CueClock());
        if (stamp.CreatedUtc < clock.Stamp) { return; }
        clock.Stamp = stamp.CreatedUtc.AddMilliseconds(460);
        Effects.PlaySound(new Point3D((int)Math.Round(stamp.EndX), (int)Math.Round(stamp.EndY), stamp.Z),
            map, SpellEffectSounds.MiasmaChainStamp);
    }

    internal static void EndChain(PlayerMobile owner, int z, double x, double y, DateTime at)
    {
        var map = owner.Map;
        var point = GroundSpellVisuals.Sample(map, x, y, z, SpellEffectGraphics.MiasmaChainFade, 7, 12);
        var clock = CueClocks.GetValue(owner, static _ => new CueClock());
        if (at < clock.Fade) { return; }
        clock.Fade = at.AddMilliseconds(250);
        Effects.PlaySound(point, map, SpellEffectSounds.MiasmaChainFade);
    }

    internal static void Contact(PlayerMobile owner, Map map, Point3D where, DateTime at)
    {
        Effects.SendLocationEffect(where, map, SpellEffectGraphics.MiasmaContact, 7);
        var clock = CueClocks.GetValue(owner, static _ => new CueClock());
        if (at < clock.Contact) { return; }
        clock.Contact = at.AddMilliseconds(160);
        Effects.PlaySound(where, map, SpellEffectSounds.MiasmaContact);
    }

    private static int Graphic(CloudCell cell, int radius) =>
        (cell.Chain ? SpellEffectGraphics.MiasmaChainCloud : SpellEffectGraphics.MiasmaBoltCloud)
        + (radius - 2) * SpellEffectGraphics.MiasmaFrames;

    private static void Sample(CloudCell cell, int graphic, int duration) =>
        GroundSpellVisuals.Sample(Map.Maps[cell.Map], cell.X2 / 2.0, cell.Y2 / 2.0, cell.Z, graphic, duration);

    internal static void RefreshClouds(IEnumerable<MiasmaNode> nodes, DateTime at)
    {
        if (at < _nextCloudRefresh) { return; }
        _nextCloudRefresh = at.AddMilliseconds(200);
        var visible = new Dictionary<CloudCell, CloudAppearance>();
        foreach (var node in nodes)
        {
            if (node.Cloud is not { } cloud || node.CreatedUtc > at || node.UntilUtc <= at
                || !MiasmaWorld.Fighting(World.FindMobile((Serial)node.Owner) as PlayerMobile, node.Map))
            { continue; }
            double dx = 0, dy = 0;
            if (cloud.Link is { } line) { dx = line.EndX - node.X; dy = line.EndY - node.Y; }
            int count = cloud.Link == null ? 1 : Samples(dx, dy, 1.75, 12) + 1;
            for (int i = 0; i < count; i++)
            {
                double t = count == 1 ? 0 : i / (count - 1.0);
                // The damage query also projects along the frozen path and rounds to this half-tile grid.
                var cell = new CloudCell(node.Map, node.Z, (int)Math.Round(2 * (node.X + dx * t)),
                    (int)Math.Round(2 * (node.Y + dy * t)), cloud.Link != null);
                var prior = visible.GetValueOrDefault(cell);
                if (prior.Radius < cloud.Shape.Radius || prior.Radius == cloud.Shape.Radius && prior.Until < node.UntilUtc)
                { visible[cell] = new(cloud.Shape.Radius, node.UntilUtc); }
            }
        }

        foreach (var (cell, old) in _clouds)
        {
            if (visible.TryGetValue(cell, out var current) && current.Radius == old.Radius) { continue; }
            Sample(cell, Graphic(cell, old.Radius), 0);
            if (current.Radius == 0 && old.Until <= at)
            { Sample(cell, cell.Chain ? SpellEffectGraphics.MiasmaChainMistFade : SpellEffectGraphics.MiasmaBoltFade, 7); }
        }
        foreach (var (cell, appearance) in visible)
        {
            int duration = Math.Min(6, (int)Math.Ceiling((appearance.Until - at).TotalMilliseconds / 50));
            Sample(cell, Graphic(cell, appearance.Radius), duration);
        }
        _clouds = visible;
    }
}
