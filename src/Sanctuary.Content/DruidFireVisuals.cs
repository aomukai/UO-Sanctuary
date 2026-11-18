// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using static Sanctuary.GroundSpellVisuals;

namespace Sanctuary;

internal static class DruidFireVisuals
{
    private static readonly Dictionary<(uint Owner, uint Target, int Art), DateTime> ContactAfter = new();
    private static readonly List<(uint Owner, uint Target, int Art)> ExpiredContacts = new();
    private static readonly HashSet<(Map Map, int X, int Y, int Z)> PaintedFire = new();
    private static DateTime _cleanupAt;

    internal static void BeginFrame(DateTime at)
    {
        PaintedFire.Clear();
        if (_cleanupAt > at) { return; }

        _cleanupAt = at.AddSeconds(1);
        ExpiredContacts.Clear();
        foreach (var (contact, until) in ContactAfter)
        {
            if (until <= at) { ExpiredContacts.Add(contact); }
        }
        foreach (var contact in ExpiredContacts) { ContactAfter.Remove(contact); }
    }

    internal static void FirestormRelease(Mobile owner)
    {
        Effects.SendLocationEffect(owner.Location, owner.Map, SpellEffectGraphics.FirestormRelease, 6);
        owner.PlaySound(SpellEffectSounds.FirestormRelease);
    }

    internal static void FirestormTravel(FirestormEmitter emitter, Map map, double dx, double dy)
    {
        int art = SpellEffectGraphics.FirestormHead
            + SpellEffectGraphics.ProjectileFacing(dx, dy) * SpellEffectGraphics.DruidGroundFireFrames
            + (emitter.Frame / 2 + emitter.Ordinal) % SpellEffectGraphics.DruidGroundFireFrames;
        Sample(map, emitter.X, emitter.Y, emitter.Z, art, 1);
    }

    internal static void FireBed(uint owner, Map map, int x, int y, int z, int age, int framesLeft, DateTime at)
    {
        if ((age & 1) == 0 || !PaintedFire.Add((map, x, y, z))) { return; }

        int art = SpellEffectGraphics.DruidFireBed + ((age / 4 + x + y) & 3);
        Sample(map, x / 2.0, y / 2.0, z, art, framesLeft >= 3 ? 2 : 1);
        SpellAreaSounds.Pulse(owner, SpellEffectSounds.GroundFire, new((x + 1) / 2, (y + 1) / 2, z), map, at, 1050);
    }

    internal static void FissureOpening(Point3D point, Map map)
    {
        Effects.SendLocationEffect(point, map, SpellEffectGraphics.FissureOpening, 10);
        Effects.PlaySound(point, map, SpellEffectSounds.FissureRelease);
    }

    internal static void FissureCrack(FissureCrack crack, Map map)
    {
        if ((crack.Frame & 1) != 0) { return; }

        int art = ((crack.X + crack.Y) & 2) == 0 ? SpellEffectGraphics.FissureCrack : SpellEffectGraphics.FissureCrackMirrored;
        int phase = crack.Frame < 8 ? crack.Frame / 2 : 1 + (crack.Frame / 4) % 3;
        int duration = crack.UntilUtc - crack.NextUtc >= TimeSpan.FromMilliseconds(100) ? 2 : 1;
        Sample(map, crack.X / 2.0, crack.Y / 2.0, crack.Z, art + phase, duration);
        SpellAreaSounds.Pulse(crack.Owner, SpellEffectSounds.GroundFire, new((crack.X + 1) / 2, (crack.Y + 1) / 2, crack.Z),
            map, crack.NextUtc, 1050);
    }

    internal static void Boulder(BoulderMissile missile, Map map, DateTime at)
    {
        switch (missile.Missile)
        {
            case 453:
                Sample(map, missile.X, missile.Y, missile.Z, SpellEffectGraphics.BoulderFormation + Math.Min(5 - missile.Remaining, 3), 1);
                break;
            case 452:
                var (x, y) = BoulderCenter(missile);
                int phase = (50 - missile.Remaining) / 2 & 3;
                var point = Sample(map, x, y, missile.Z, SpellEffectGraphics.BoulderRolling + phase, 1);
                SpellAreaSounds.Pulse(missile.Owner, SpellEffectSounds.BoulderRoll, point, map, at, 720);
                break;
            case 455:
                FireBed(missile.Owner, map, missile.CellX, missile.CellY, missile.Z, 37 - missile.Remaining, missile.Remaining, at);
                break;
        }
    }

    private static (double X, double Y) BoulderCenter(BoulderMissile missile)
    {
        // The mechanical point sweeps ahead of the visible ball's center.
        // Ease the offset in after emergence so the forming rock flows into the roll.
        double velocity = Math.Sqrt(missile.Vx * missile.Vx + missile.Vy * missile.Vy);
        double setback = velocity > 0 ? Math.Min((50 - missile.Remaining) * 0.14, 0.77) / velocity : 0;
        return (missile.X - missile.Vx * setback, missile.Y - missile.Vy * setback);
    }

    internal static void BoulderBurst(BoulderMissile missile, Map map)
    {
        var (x, y) = BoulderCenter(missile);
        var point = Sample(map, x, y, missile.Z, SpellEffectGraphics.BoulderBurst, 9);
        Effects.PlaySound(point, map, SpellEffectSounds.BoulderBurst);
    }

    internal static void Contact(uint owner, uint target, Point3D point, Map map, DateTime at, bool boulder = false)
    {
        int art = boulder ? SpellEffectGraphics.BoulderContact : SpellEffectGraphics.FireBoltContact;
        var key = (owner, target, art);
        if (ContactAfter.GetValueOrDefault(key) > at) { return; }

        ContactAfter[key] = at.AddMilliseconds(200);
        Effects.SendLocationEffect(point, map, art, 6);
        SpellAreaSounds.Pulse(owner, boulder ? SpellEffectSounds.BoulderContact : SpellEffectSounds.FireBoltContact, point, map, at, 180);
    }
}
