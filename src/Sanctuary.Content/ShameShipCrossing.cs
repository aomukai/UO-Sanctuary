// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal static class ShameShipCrossing
{
    private sealed record Dock(int HullArt, Point3D Hull, int PlankArt, Point3D Plank, Point3D Shore);

    private static readonly Dock _west = new(0x4, new(5549, 79, -5), 0x3ED4, new(5552, 80, -2), new(5553, 80, 0));
    private static readonly Dock _east = new(0x5, new(5596, 84, -5), 0x3E84, new(5596, 86, -2), new(5596, 87, 0));

    internal static void Install(Map map)
    {
        Place(_west, eastern: false);
        Place(_east, eastern: true);

        // The western plank spans two tiles between the deck and the bank.
        ShamePopulation.Ensure(map, new(5551, 80, -2), () => new Static(_west.PlankArt),
            item => item.ItemID == _west.PlankArt);

        void Place(Dock dock, bool eastern)
        {
            ShamePopulation.Ensure(map, dock.Hull, () => new ShameBoat(dock.HullArt));
            ShamePopulation.Ensure(map, dock.Plank, () => new ShameCrossingPlank(dock.PlankArt, eastern));
        }
    }

    internal static bool Travel(PlayerMobile player, bool fromEast)
    {
        var map = player.Map;
        var shore = fromEast ? _west.Shore : _east.Shore;
        var landing = shore;
        if (!Teleportation.Space(map, landing, player, allowAllies: true)
            && !PersistentMinions.FindPosition(map, shore, OnBank, out landing))
            return false;

        return BritanniaCampaign.Sail(player, landing);

        bool OnBank(Point3D point) => Utility.InRange(point, shore, 2) && Math.Abs(point.Z - shore.Z) <= 3
            && (fromEast ? point.X >= shore.X : point.Y >= shore.Y)
            && Teleportation.Space(map, point, player, allowAllies: true);
    }
}

[SerializationGenerator(0)]
public partial class ShameCrossingPlank : Item
{
    [SerializableField(0)] private bool _eastern;

    internal ShameCrossingPlank(int art, bool eastern) : base(art)
    {
        _eastern = eastern;
        Movable = false;
        Name = "the pirate ship’s gangplank";
    }

    public override void OnDoubleClick(Mobile from) => TryUse(from);

    public override bool OnMoveOver(Mobile from) => !TryUse(from);

    private bool TryUse(Mobile from)
    {
        if (from is not PlayerMobile { Alive: true } player || Map != Map.Trammel || player.Map != Map
            || !player.InRange(this, 2) || Math.Abs(player.Z - Z) >= 8)
            return false;

        if (ShameShipCrossing.Travel(player, fromEast: _eastern))
            return true;

        player.SendMessage("There’s no room to land on the far shore.");
        return false;
    }
}
