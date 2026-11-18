// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class AssassinLobVisuals
{
    internal static void Release(Mobile actor, bool shockWeb)
    {
        var (art, cue) = shockWeb
            ? (SpellEffectGraphics.ShockWebThrow, SpellEffectSounds.ShockWebThrow)
            : (SpellEffectGraphics.FireBlastThrow, SpellEffectSounds.FireBlastThrow);
        actor.FixedEffect(art, 9, 6);
        actor.PlaySound(cue);
    }

    internal static void Flight(Map map, FireBomb bomb) =>
        Lob(map, bomb.X, bomb.Y, bomb.Z, bomb.Remaining, SpellEffectGraphics.FireBlastFlight);

    internal static void Flight(Map map, ShockShard shard)
    {
        if (shard.Remaining % 2 != 0) { return; }
        Lob(map, shard.X, shard.Y, shard.Z, shard.Remaining, SpellEffectGraphics.ShockWebFlight, step: 2);
    }

    private static void Lob(Map map, double x, double y, int z, int remaining, int art, int step = 1)
    {
        int frame = (SpellEffectGraphics.AssassinLobFrames - remaining / step % SpellEffectGraphics.AssassinLobFrames)
            % SpellEffectGraphics.AssassinLobFrames;
        int lift = Math.Clamp(16 + remaining * 3, 20, 38);
        GroundSpellVisuals.Sample(map, x, y, z, art + frame, step, lift);
    }

    internal static void Fuse(Map map, FireBomb bomb, bool landed = false)
    {
        int frame = Math.Clamp(5 - bomb.Remaining, 0, SpellEffectGraphics.AssassinLobFrames - 1);
        var point = GroundSpellVisuals.Sample(map, bomb.X, bomb.Y, bomb.Z, SpellEffectGraphics.FireBlastFuse + frame, 1);
        if (landed) { Effects.PlaySound(point, map, SpellEffectSounds.FireBlastFuse); }
    }

    internal static void Detonation(Map map, FireBomb bomb)
    {
        var point = GroundSpellVisuals.Sample(map, bomb.X, bomb.Y, bomb.Z, SpellEffectGraphics.FireBlastBurst, 8);
        Effects.PlaySound(point, map, SpellEffectSounds.FireBlastBurst);
    }

    internal static void FireContact(Map map, Point3D point) =>
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.FireBoltContact, 7);

    internal static void Web(Map map, ShockShard shard, bool landed = false)
    {
        // Renew at the native shard's half-tile center without creating a physical object.
        int duration = Math.Min(5, (shard.Remaining * 40 + 49) / 50);
        var point = GroundSpellVisuals.Sample(map, shard.X, shard.Y, shard.Z, SpellEffectGraphics.ShockWebField, duration);
        if (landed)
        {
            SpellAreaSounds.Pulse(shard.Owner, SpellEffectSounds.ShockWebLand, point, map, shard.NextUtc, 135);
        }
    }

    internal static void WebContact(Map map, Point3D point, ShockShard shard)
    {
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.ShockWebHit, 7);
        SpellAreaSounds.Pulse(shard.Owner, SpellEffectSounds.ShockWebHit, point, map, shard.NextUtc, 155);
    }

    internal static void WebEnd(Map map, ShockShard shard)
    {
        GroundSpellVisuals.Sample(map, shard.X, shard.Y, shard.Z, SpellEffectGraphics.ShockWebField, 0);
        var point = GroundSpellVisuals.Sample(map, shard.X, shard.Y, shard.Z, SpellEffectGraphics.ShockWebFade, 7);
        SpellAreaSounds.Pulse(shard.Owner, SpellEffectSounds.ShockWebFade, point, map, shard.NextUtc, 215);
    }
}
