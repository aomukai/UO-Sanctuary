// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server.Engines.Spawners;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;

namespace Sanctuary;

internal static class DaggerIsleSettlement
{
    internal static readonly Point3D DockLanding = new(3980, 572, 0);
    internal static readonly Point3D CraftHall = new(4003, 562, 0);
    internal static readonly Point3D Well = new(3986, 574, 0);
    internal const int WellGraphic = 0xA300;
    internal const int DoorX = DaggerVillageLayout.DoorX, DoorY = DaggerVillageLayout.DoorY;

    internal static void Install()
    {
        var map = Map.Trammel;
        RegisterArt();
        RetireOriginalVillage(map);
        InstallMapScenery(map);
        foreach (var hallSite in WorldDecorationGraphics.DaggerBuildings.Skip(1))
            Door(map, new(hallSite.X + hallSite.Width / 2, hallSite.Y + hallSite.Depth - 1, 0));
        Door(map, new(4004, 578, 0), gate: true);
        InstallSheepfold(map);
        Vendor(map, "dagger-isle-smith", MarketRole.Smith, new(3987, 561, 0), () => new Blacksmith());
        Vendor(map, "dagger-isle-tailor", MarketRole.Tailor, new(3993, 561, 0), () => new Tailor());
        Vendor(map, "dagger-isle-provisioner", MarketRole.Provisioner, new(3987, 564, 0), () => new Provisioner());

        InstallWorkshop(map);

        Resident(map, ThingRole.Jarl, new(4023, 556, 0));
        Resident(map, ThingRole.Seer, new(4025, 556, 0));
        const int flank = DaggerVillageLayout.DoorHalfWidth + 1;
        Resident(map, ThingRole.Sten, new(DoorX - flank, DoorY + 2, 0), new Point3D(4021, 568, 0));
        Resident(map, ThingRole.Ketil, new(DoorX + flank, DoorY + 2, 0), new Point3D(4025, 568, 0));
        var hall = WorldDecorationGraphics.DaggerBuildings[0];
        new DaggerThingRegion(map, Region.Find(new Point3D(DoorX, DoorY, 0), map),
            new(hall.X, hall.Y, hall.Width, hall.Depth)).Register();
    }

    private static void InstallWorkshop(Map map)
    {
        CraftFacilities.Register(new(SkillName.Blacksmith, map.Name, 4001, 558, 0, 0x0FAF));
        CraftFacilities.Register(new(SkillName.Blacksmith, map.Name, 4002, 558, 0, 0x0FB1));
        CraftFacilities.Register(new(SkillName.Imbuing, map.Name, 4005, 559, 0, CraftFacilities.DefaultGraphic(SkillName.Imbuing)));

        // Put the writing stations along the north wall and leave an open aisle
        // from the south door to the hearth.
        foreach (var (skill, oldX, oldY, x, y) in new (SkillName, int, int, int, int)[]
        {
            (SkillName.Tinkering, 4001, 560, 3999, 558),
            (SkillName.Fletching, 4001, 564, 4000, 564),
            (SkillName.Alchemy, 4005, 558, 4006, 557),
            (SkillName.Cartography, 4005, 561, 4001, 556),
            (SkillName.Inscribe, 4005, 560, 3999, 556)
        })
        {
            int graphic = CraftFacilities.DefaultGraphic(skill);
            foreach (var old in map.GetItemsAt<TownCraftStation>(oldX, oldY))
            {
                if (old.Z == 0 && !old.Movable && old.Skill == skill &&
                    (old.ItemID == graphic || old.ItemID == CraftFacilities.LegacyDesk))
                {
                    old.MoveToWorld(new Point3D(x, y, 0), map);
                    break;
                }
            }
            CraftFacilities.Register(new(skill, map.Name, x, y, 0, graphic));
        }

        RetireStation(map, new(4001, 562, 0), SkillName.Carpentry, CraftFacilities.LegacyDesk);
        RetireStation(map, new(4001, 562, 0), SkillName.Carpentry, CraftFacilities.DefaultGraphic(SkillName.Carpentry));
        RetireStation(map, new(4005, 562, 0), SkillName.Tailoring, 0x1060);
        RetireStation(map, new(4005, 564, 0), SkillName.Cooking, 0x092B);
        CraftFacilities.RegisterAddon<FjellheimWoodworkersBench>(map, new(3999, 561, 0), SkillName.Carpentry);
        CraftFacilities.RegisterAddon<LoomEastAddon>(map, new(4005, 562, 0), SkillName.Tailoring);
        CraftFacilities.RegisterAddon<StoneOvenSouthAddon>(map, new(4004, 556, 0), SkillName.Cooking);
    }

    private static void RetireStation(Map map, Point3D location, SkillName skill, int graphic)
    {
        foreach (var station in map.GetItemsAt<TownCraftStation>(location))
            if (station.Z == location.Z && station.Skill == skill && station.ItemID == graphic && !station.Movable)
            {
                station.Delete();
                return;
            }
    }

    private static void RegisterArt()
    {
        for (int id = WorldDecorationGraphics.DaggerMeadow; id < WorldDecorationGraphics.DaggerMeadow + 256; id++)
            TileData.ItemTable[id] = new ItemData("winter pasture", TileFlag.Background, 255, 0, 0, 0, 0, 0);
        for (int id = WorldDecorationGraphics.DaggerHallRoof; id < WorldDecorationGraphics.DaggerGable + 24; id++)
            if (WorldDecorationGraphics.IsDaggerRoof((ushort)id))
                TileData.ItemTable[id] = new ItemData("timber and thatch", TileFlag.Roof, 255, 0, 0, 0, 0, 0);
        for (int id = WorldDecorationGraphics.DaggerGroundFirst; id <= WorldDecorationGraphics.DaggerMooring; id++)
        {
            var flags = id < WorldDecorationGraphics.DaggerRoofFirst ? TileFlag.Background | TileFlag.Surface
                : id < WorldDecorationGraphics.DaggerLintel ? TileFlag.Roof
                : id == WorldDecorationGraphics.DaggerLintel ? TileFlag.None : TileFlag.Impassable;
            TileData.ItemTable[id] = new ItemData("Dagger Isle timberwork", flags, 255, 0, 0, 0, 0,
                (byte)(flags == TileFlag.Impassable ? 16 : 0));
        }
    }

    private static void RetireOriginalVillage(Map map)
    {
        var old = World.Items.Values.OfType<DaggerSettlementBuilding>().Where(item => item.Map == map).ToArray();
        if (old.Length == 0) return;
        foreach (var item in old) item.Delete();
        foreach (var item in World.Items.Values.Where(item => item.Map == map && (item is DaggerSettlementPiece or DaggerSettlementDoor
            || item is TownCraftStation && item.Z == 8 && item.InRange(CraftHall, 4))).ToArray())
            item.Delete();
        foreach (var resident in World.Mobiles.Values.OfType<DaggerThingResident>().Where(npc => npc.Map == map).ToArray())
            resident.Delete();
    }

    private static void InstallMapScenery(Map map)
    {
        foreach (var group in DaggerVillageLayout.Blocks)
        {
            var (bx, by) = group.Key;
            var original = map.Tiles.GetStaticBlock(bx, by);
            // Blocks can share their empty arrays with other facets.
            var block = original.Select(column => column.ToArray()).ToArray();
            foreach (var cell in group.GroupBy(tile => (X: tile.At.X & 7, Y: tile.At.Y & 7)))
            {
                var (x, y) = cell.Key;
                block[x][y] = [..block[x][y], ..cell.Select(tile =>
                    new StaticTile((ushort)tile.Graphic, (byte)x, (byte)y, (sbyte)tile.At.Z, 0))];
            }
            map.Tiles.SetStaticBlock(bx, by, block);
        }
    }

    private static void InstallSheepfold(Map map)
    {
        var pen = new Point3D(4004, 581, 0);
        foreach (var spawner in map.GetItemsAt<Spawner>(pen))
            if (spawner.Name == "Dagger Isle sheepfold") return;
        new Spawner(2, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4), 0,
            new Rectangle3D(4003, 580, 0, 3, 3, 16), "Sheep")
        { Name = "Dagger Isle sheepfold" }.MoveToWorld(pen, map);
    }

    private static void Door(Map map, Point3D point, bool gate = false)
    {
        if (gate)
        {
            foreach (var existing in map.GetItemsInRange<DarkWoodGate>(point, 1))
                if (!existing.Deleted) return;
            new DarkWoodGate(DoorFacing.WestCW).MoveToWorld(point, map);
            return;
        }
        foreach (var door in map.GetItemsInRange<DaggerSettlementDoor>(point, 1))
        {
            if (door.ClosedLocation == point)
                return;
        }
        new DaggerSettlementDoor().MoveToWorld(point, map);
    }

    private static void Vendor(Map map, string site, MarketRole role, Point3D point, Func<BaseVendor> create)
    {
        if (TownMarkets.Profiles.At(site) is { Deleted: false } existing)
        {
            existing.Home = point;
            if (existing.Location != point || existing.Map != map) existing.MoveToWorld(point, map);
            return;
        }
        var vendor = create();
        vendor.Name = role switch
        {
            MarketRole.Smith => vendor.Female ? "Astrid" : "Gunnar",
            MarketRole.Tailor => vendor.Female ? "Ingrid" : "Ivar",
            _ => vendor.Female ? "Signe" : "Leif"
        };
        for (int i = 0; i < vendor.Skills.Length; i++)
            vendor.Skills[i].Base = 0;
        vendor.CantWalk = true;
        vendor.Home = point;
        vendor.RangeHome = 0;
        vendor.MoveToWorld(point, map);
        TownMarkets.Profiles.Add(vendor, role, site);
    }

    private static void Resident(Map map, ThingRole role, Point3D point, Point3D? previous = null)
    {
        foreach (var site in previous is { } old ? new[] { point, old } : new[] { point })
        {
            foreach (var resident in map.GetMobilesInRange<DaggerThingResident>(site, 0))
            {
                if (resident.Location != site) continue;
                if (resident.Role == ThingRole.Guard && role is ThingRole.Sten or ThingRole.Ketil)
                    resident.AssignGuard(role);
                if (resident.Role != role) continue;
                if (site != point) resident.MoveToWorld(point, map);
                return;
            }
        }
        new DaggerThingResident(role).MoveToWorld(point, map);
    }
}

internal sealed class DaggerThingRegion(Map map, Region parent, Rectangle2D area)
    : BaseRegion("the Thing of Fjellheim", map, parent, parent.Priority + 1,
        new Rectangle3D(area.X, area.Y, MinZ, area.Width, area.Height, MaxZ - MinZ))
{
    public override bool OnMoveInto(Mobile mobile, Direction direction, Point3D next, Point3D previous)
    {
        if (mobile is not PlayerMobile { AccessLevel: AccessLevel.Player } player || Contains(previous))
            return base.OnMoveInto(mobile, direction, next, previous);
        FjellheimGuards.AtDoor(player);
        return false;
    }

    public override bool CheckTravel(Mobile mobile, Point3D destination, TravelCheckType travel, out TextDefinition message)
    {
        if (mobile is PlayerMobile { AccessLevel: AccessLevel.Player } && Contains(destination)
            && travel is TravelCheckType.RecallTo or TravelCheckType.GateTo or TravelCheckType.TeleportTo)
        {
            message = null; // The spell supplies its usual blocked-travel message.
            return false;
        }
        return base.CheckTravel(mobile, destination, travel, out message);
    }
}

[SerializationGenerator(0, false)]
public partial class FjellheimWoodworkersBench : BaseAddon
{
    public FjellheimWoodworkersBench()
    {
        AddComponent(new AddonComponent(0x19F1), 0, 1, 0);
        AddComponent(new AddonComponent(0x19F2), 0, 0, 0);
        AddComponent(new AddonComponent(0x19F3), 0, -1, 0);
    }
}

[SerializationGenerator(0)]
public partial class DaggerSettlementPiece : Static
{
    internal DaggerSettlementPiece(int graphic) : base(graphic) => Movable = false;
}

[SerializationGenerator(0)]
public partial class DaggerSettlementBuilding : BaseMulti
{
    internal DaggerSettlementBuilding(int graphic) : base(graphic) { }
}

[SerializationGenerator(0)]
public partial class DaggerSettlementDoor : StrongWoodDoor
{
    internal DaggerSettlementDoor() : base(DoorFacing.WestCW) { }
    internal Point3D ClosedLocation => Open ? new Point3D(X - Offset.X, Y - Offset.Y, Z - Offset.Z) : Location;
}

public enum ThingRole : byte { Jarl, Seer, Guard, Sten, Ketil } // Guard is the original saved role.

[SerializationGenerator(0)]
public partial class DaggerThingResident : BaseCreature, IScriptedSpeaker
{
    [SerializableField(0)] private ThingRole _role;

    internal DaggerThingResident(ThingRole role) : base(AIType.AI_Vendor, FightMode.None, 10, 1)
    {
        _role = role;
        Name = role switch
        {
            ThingRole.Jarl => "Jarl Asbjorn Isisson",
            ThingRole.Seer => "Seer Herkja",
            _ => "a Thing guard"
        };
        Female = role == ThingRole.Seer;
        Body = Female ? 0x191 : 0x190;
        Hue = 0x83EF;
        CantWalk = Blessed = true;
        Direction = Direction.West;
        AddItem(new Boots());
        AddItem(new FurCape { Hue = role == ThingRole.Seer ? 0x482 : 0x763 });
        if (role == ThingRole.Seer)
        {
            AddItem(new Robe { Hue = 0x455 });
            HairItemID = 0x203C;
            HairHue = 0x465;
        }
        else
        {
            AddItem(new RingmailChest());
            AddItem(new LongPants());
            AddItem(new WoodenShield());
            AddItem(new Axe());
            HairItemID = 0x203B;
            FacialHairItemID = 0x203E;
            HairHue = FacialHairHue = 0x46E;
        }
        if (IsGuard) AssignGuard(role);
    }

    internal bool IsGuard => Role is ThingRole.Sten or ThingRole.Ketil;

    internal void AssignGuard(ThingRole role)
    {
        Role = role;
        bool sten = role == ThingRole.Sten;
        Name = sten ? "Sten" : "Ketil";
        Title = "the Thing guard";
        HairItemID = sten ? 0x203B : 0x2044;
        HairHue = FacialHairHue = sten ? 0x0387 : 0x0461;
    }

    ushort IScriptedSpeaker.Portrait => (ushort)(Role == ThingRole.Sten ? NpcPortrait.Sten : NpcPortrait.Ketil);
    bool IScriptedSpeaker.CanTalkTo(PlayerMobile player) => IsGuard;
    void IScriptedSpeaker.BeginConversation(PlayerMobile player) => FjellheimGuards.Open(player, this);


    public override bool IsInvulnerable => true;

    public override void OnDoubleClick(Mobile from)
    {
        if (IsGuard && from is PlayerMobile player)
            ScriptedDialogue.Open(player, this);
    }
}
