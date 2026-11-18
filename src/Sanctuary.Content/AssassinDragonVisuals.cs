// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;

namespace Sanctuary;

internal static class AssassinDragonVisuals
{
    private static int Style(CombatAbility ability, bool secondary) => (ability, secondary) switch
    {
        (CombatAbility.DragonTalon, _) => 0,
        (CombatAbility.DragonClaw, false) => 1,
        (CombatAbility.DragonClaw, true) => 2,
        (CombatAbility.DragonTail, _) => 3,
        (CombatAbility.DragonFlight, _) => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };

    internal static void Sweep(Mobile owner, Mobile target, CombatAbility ability, bool secondary = false)
    {
        var aim = target is { Deleted: false } && target.Map == owner.Map ? target.Location : owner.Location;
        if (aim.X == owner.X && aim.Y == owner.Y)
        { Server.Movement.Movement.Offset(owner.Direction & Direction.Mask, ref aim); }
        int facing = SpellEffectGraphics.ProjectileFacing(aim.X - owner.X, aim.Y - owner.Y);
        int style = Style(ability, secondary);
        int graphic = SpellEffectGraphics.DragonTalonSweep + (style * 8 + facing) * SpellEffectGraphics.DragonFrames;
        owner.FixedEffect(graphic, 10, 5);
        owner.PlaySound(SpellEffectSounds.DragonTalonSweep + style);
    }

    internal static void Contact(Point3D point, Map map, CombatAbility ability, MonsterContactResult result, bool secondary = false)
    {
        if (result?.Commit is { Packet.ReactionSettled: false })
        {
            int style = Style(ability, secondary);
            Burst(point, map, SpellEffectGraphics.DragonTalonImpact + style * SpellEffectGraphics.DragonFrames,
                SpellEffectSounds.DragonTalonImpact + style);
        }
        else if (result is { Exit: MonsterContactExit.Finished, Packet: { } packet }
            && (packet.ResultFlags & (SourceMeleeResult.Block | SourceMeleeResult.WeaponBlock)) != 0)
        {
            Burst(point, map, SpellEffectGraphics.DragonDeflect, SpellEffectSounds.DragonDeflect);
        }
    }

    internal static void Displaced(Mobile target, Point3D from, CombatAbility ability, DateTime at)
    {
        bool fire = ability == CombatAbility.DragonTail;
        Effects.SendLocationEffect(from, target.Map, fire ? SpellEffectGraphics.DragonTailEmbers : SpellEffectGraphics.DragonTalonSkid, 6);
        SpellAreaSounds.Pulse(target.Serial.Value, fire ? SpellEffectSounds.DragonTailEmbers : SpellEffectSounds.DragonTalonSkid,
            from, target.Map, at, 155);
    }

    internal static void Transfer(Mobile owner, Point3D origin, Map map)
    {
        Burst(origin, map, SpellEffectGraphics.DragonFlightDepart, SpellEffectSounds.DragonFlightDepart);
        Burst(owner.Location, owner.Map, SpellEffectGraphics.DragonFlightArrive, SpellEffectSounds.DragonFlightArrive);
    }

    private static void Burst(Point3D point, Map map, int graphic, int sound)
    {
        if (World.Loading || map == null || map == Map.Internal) { return; }
        Effects.SendLocationEffect(point, map, graphic, 7);
        Effects.PlaySound(point, map, sound);
    }
}
