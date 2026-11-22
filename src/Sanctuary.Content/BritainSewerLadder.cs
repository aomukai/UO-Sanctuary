// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

[SerializationGenerator(0)]
public sealed partial class BritainSewerLadder : InteractionTeleporter
{
    public BritainSewerLadder(Point3D destination, Map map) : base(destination, map)
    {
        ItemID = 0x375A;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!Active || from.Map != Map)
            return;
        if (!from.InRange(Location, 3) || Math.Abs(from.Z - Z) >= 16 || !from.InLOS(this))
        {
            from.SendMessage("You need to be closer to use the ladder.");
            return;
        }
        if (CanTeleport(from))
            StartTeleport(from);
    }
}
