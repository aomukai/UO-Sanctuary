// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

[Flags]
internal enum HedgeMazeProgress : byte
{
    PassageOpened = 1,
    DaemonBanished = 2,
    DaemonMet = 4,
    DaemonDefeated = 8
}

internal static class HedgeMazeStory
{
    internal static readonly Point3D Woodpile = new(1168, 2256, 4);
    private static readonly Point3D TobinFormerLocation = new(1166, 2242, 40);
    private static readonly Rectangle2D MazeWalls = new(1032, 2159, 225, 145);
    internal static Point3D Point((int X, int Y, int Z) at) => new(at.X, at.Y, at.Z);

    internal static void Install(Map map)
    {
        // The maze hedges have movement collision but their native height stops below eye level.
        map.AdditionalSightBlockHeight = static (x, y, itemID) =>
            itemID is 0x0DB8 or 0x0DB9 && MazeWalls.Contains(x, y) ? 20 : -1;

        var residents = World.Mobiles.Values.OfType<HedgeHouseGhost>().Where(ghost => ghost.Map == map)
            .ToLookup(ghost => ghost.Part);
        for (int i = 0; i < HedgeMazeLayout.Ghosts.Length; i++)
        {
            var part = (HedgeGhostPart)i;
            var location = Point(HedgeMazeLayout.Ghosts[i]);
            var ghost = residents[part].FirstOrDefault();
            if (ghost == null)
                new HedgeHouseGhost(part).MoveToWorld(location, map);
            else if (part == HedgeGhostPart.YoungerSon && ghost.Location == TobinFormerLocation)
                ghost.MoveToWorld(location, map);
        }

        var fowl = World.Mobiles.Values.OfType<HedgeFowl>().Where(bird => bird.Map == map)
            .Select(bird => bird.Location).ToHashSet();
        for (int i = 0; i < HedgeMazeLayout.Fowl.Length; i++)
        {
            var at = Point(HedgeMazeLayout.Fowl[i]);
            if (!fowl.Contains(at))
                new HedgeFowl(i == 2).MoveToWorld(at, map);
        }

        if (!World.Items.Values.OfType<Static>().Any(item => item.Map == map && item.Location == Woodpile && item.ItemID == 0x1BDF))
            new Static(0x1BDF) { Name = "a stack of firewood", Movable = false }.MoveToWorld(Woodpile, map);

        if (!World.Items.Values.OfType<HedgeRitualLever>().Any(item => item.Map == map && item.Location == Point(HedgeMazeLayout.Lever)))
            new HedgeRitualLever().MoveToWorld(Point(HedgeMazeLayout.Lever), map);

        var entry = Point(HedgeMazeLayout.Entry);
        var exit = Point(HedgeMazeLayout.Exit);
        PlaceSparkle(map, entry, Point(HedgeMazeLayout.RitualRoom), locked: true, new(1163, 2205, 0));
        PlaceSparkle(map, exit, entry, locked: false, new(1171, 2202, 0));

        var path = new Point3D(1156, 2235, 40);
        NewHavenPopulation.PlacePassage(map, new(1160, 2246, 40), path);
        NewHavenPopulation.PlacePassage(map, new(1161, 2246, 40), path);
        GraveyardPopulation.InstallSpawners("hedge-maze-spawns.json");
    }

    private static void PlaceSparkle(Map map, Point3D at, Point3D destination, bool locked, Point3D oldDestination)
    {
        var obsolete = new List<Item>();
        bool installed = false;
        foreach (var item in map.GetItemsInRange<Item>(at, 0))
        {
            if (item is HedgeRitualSparkle && item.Z == at.Z)
                installed = true;
            else if (item is Static && item.ItemID == HedgeMazeLayout.SparkleGraphic && (item.Z == at.Z || item.Z == at.Z + 2)
                || item is Teleporter teleporter && item.GetType() == typeof(Teleporter) && item.Z == at.Z
                    && teleporter.PointDest == oldDestination)
                obsolete.Add(item);
        }
        foreach (var item in obsolete)
            item.Delete();
        if (!installed)
            new HedgeRitualSparkle(destination, map, locked).MoveToWorld(at, map);
    }
}

[SerializationGenerator(0)]
public partial class HedgeRitualLever : Item
{
    [Constructible]
    public HedgeRitualLever() : base(0x1095)
    {
        Movable = false;
        Name = "an alchemist's lever";
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile { Alive: true } player || player.Map != Map
            || !player.InRange(this, 2) || Math.Abs(player.Z - Z) > 4 || !player.InLOS(this))
            return;

        if (ClassCreation.Profiles.AdvanceHedgeMaze(player.Serial, HedgeMazeProgress.PassageOpened))
        {
            player.PlaySound(0x1F3);
            player.SendMessage("You hear a hum from the direction of the other house.");
        }
        else if (ClassCreation.Profiles.HasHedgeMazeProgress(player.Serial, HedgeMazeProgress.PassageOpened))
            player.SendMessage("The lever is already humming.");
    }
}

[SerializationGenerator(0)]
public partial class HedgeRitualSparkle : Teleporter
{
    [SerializableField(0)]
    private bool _locked;

    public HedgeRitualSparkle(Point3D destination, Map map, bool locked) : base(destination, map)
    {
        _locked = locked;
        ItemID = HedgeMazeLayout.SparkleGraphic;
        Visible = true;
        Name = "a magical sparkle";
    }

    public override bool CanTeleport(Mobile m)
    {
        if (m is not PlayerMobile player || player.Map != Map || Math.Abs(player.Z - Z) > 4 || !base.CanTeleport(m))
            return false;
        if (!_locked || ClassCreation.Profiles.HasHedgeMazeProgress(player.Serial, HedgeMazeProgress.PassageOpened))
            return true;

        player.SendMessage("This teleporter is not active yet.");
        return false;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (CanUse(from))
            StartTeleport(from);
    }

    public override void StartTeleport(Mobile from)
    {
        if (!_locked)
            base.StartTeleport(from);
        else if (from is PlayerMobile { NetState.Running: true } player && !player.HasGump<HedgePassageGump>())
            player.SendGump(new HedgePassageGump(player, this));
    }

    internal void Confirm(PlayerMobile player)
    {
        if (CanUse(player))
            base.StartTeleport(player);
    }

    private bool CanUse(Mobile from) =>
        !Deleted && Active && from.Map == Map && from.InRange(this, 2) && from.InLOS(this) && CanTeleport(from);

    public override void OnDoubleClickDead(Mobile from) => OnDoubleClick(from);
    public override void AddProperties(IPropertyList list) { }
}

internal sealed class HedgePassageGump(PlayerMobile viewer, HedgeRitualSparkle passage) : DynamicGump(100, 90)
{
    public override bool Singleton => true;

    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage();
        b.AddBackground(0, 0, 420, 210, 5054);
        b.AddLabel(22, 18, 0x481, "The sparkle");
        b.AddHtml(22, 52, 375, 102,
            "<BASEFONT COLOR=#FFFFFF>This looks like a teleporter. Whatever awaits on the other side may be more than thou canst face. After what thou hast seen here, wilt thou risk it?</BASEFONT>");
        b.AddButton(22, 163, 4005, 4007, 1);
        b.AddLabel(57, 163, 0x481, "Take the risk");
        b.AddButton(240, 163, 4017, 4019, 0);
        b.AddLabel(275, 163, 0x481, "Stay here");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 1 && sender.Mobile == viewer)
            passage.Confirm(viewer);
    }
}
