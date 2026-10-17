// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class CombatPractice
{
    private const string TargetName = "Practice Skeleton";
    private static readonly Point3D _origin = new(1382, 1495, 10);
    private static readonly Dictionary<Serial, (Point3D Location, Map Map)> _returns = new();

    public static void Configure()
    {
        CommandSystem.Register("CombatTest", AccessLevel.Player, Run);
        EventSink.Disconnected += mobile => _returns.Remove(mobile.Serial);
    }
    private static void Run(CommandEventArgs args)
    {
        if (args.Mobile is not PlayerMobile from || !ClassCombat.Enabled(from) || !from.Alive)
        {
            args.Mobile.SendMessage("Use a living class character for the combat practice encounter.");
            return;
        }
        ClassCombat.Stop(from);
        if (args.GetString(0).Equals("return", StringComparison.OrdinalIgnoreCase))
        {
            if (_returns.Remove(from.Serial, out var origin))
            {
                from.MoveToWorld(origin.Location, origin.Map);
            }
            return;
        }
        _returns.TryAdd(from.Serial, (from.Location, from.Map));
        var map = Map.Felucca;
        // This development encounter only replaces its own named targets in one bounded area.
        var old = new List<Mobile>();
        foreach (var mobile in map.GetMobilesInRange<Mobile>(_origin, 12))
        {
            if (mobile is BaseCreature { Frozen: true } && mobile.Name == TargetName)
            {
                old.Add(mobile);
            }
        }
        foreach (var mobile in old)
        {
            mobile.Delete();
        }
        var point = FindFloor(map, _origin.X, _origin.Y);
        from.MoveToWorld(point, map);
        for (var i = 0; i < 2; i++)
        {
            var target = new GraveyardSkeleton
            {
                Name = TargetName, Frozen = true, CantWalk = true,
                Home = point, RangeHome = 0
            };
            target.SetHits(60);
            target.Hits = target.HitsMax;
            target.MoveToWorld(FindFloor(map, point.X + 4 + i * 4, point.Y), map);
        }
        if (ClassCreation.Profiles.TryGet(from.Serial, out var profile) && profile.Class == DiabloClass.Necromancer
            && from.FindItemOnLayer(Layer.OneHanded) == null)
        {
            var dagger = from.Backpack?.FindItemByType<Dagger>() ?? new Dagger();
            if (!from.EquipItem(dagger))
            {
                from.AddToBackpack(dagger);
            }
        }
        ClassEquipment.Prepare(from);
        from.SendMessage("Practice: left-click a skeleton to approach and attack; ground cancels. Right-click casts Teeth with a Necromancer's dagger. Shift holds position. [CombatTest return takes you back.");
    }
    public static Point3D FindFloor(Map map, int x, int y)
    {
        for (var radius = 0; radius <= 4; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    var z = map.GetAverageZ(x + dx, y + dy);
                    if (map.CanSpawnMobile(x + dx, y + dy, z))
                    {
                        return new Point3D(x + dx, y + dy, z);
                    }
                }
            }
        }
        throw new InvalidOperationException("No walkable floor near the combat practice point.");
    }
}
