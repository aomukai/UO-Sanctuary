// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Collections;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal static class WindTown
{
    internal static bool Contains(Point3D point, Map map) =>
        map == Map.Trammel && Region.Find(point, map).IsPartOf("Wind");

    internal static void Install()
    {
        RetireFeluccaResidents();
        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is not BaseVendor { Deleted: false } vendor)
                continue;

            var home = vendor.Home == Point3D.Zero ? vendor.Location : vendor.Home;
            StyleResident(vendor, home, vendor.Map);
        }
    }

    private static void RetireFeluccaResidents()
    {
        using var spawners = PooledRefList<BaseSpawner>.Create();
        foreach (var item in World.Items.Values)
            if (item is BaseSpawner { Name: "Britannia town resident" } spawner && spawner.Map == Map.Felucca
                && Region.Find(spawner.Location, Map.Felucca).IsPartOf("Wind"))
                spawners.Add(spawner);
        foreach (var spawner in spawners)
            spawner.Delete();

        bool removedMarkets = false;
        foreach (var market in TownMarkets.Profiles.All)
        {
            if (market.Site?.StartsWith("Felucca/Wind/", StringComparison.Ordinal) != true)
                continue;

            if (World.FindItem(market.Station) is TownCraftStation station)
                station.Delete();
            if (World.FindItem(market.Forge) is TownCraftStation forge)
                forge.Delete();
            if (World.FindItem(market.Shelf) is MarketShelf shelf)
                shelf.Delete();
            if (World.FindMobile(market.Vendor) is BaseVendor vendor)
                vendor.Delete();
            removedMarkets = true;
        }
        if (removedMarkets)
            TownMarkets.Profiles.Prune();
    }

    internal static void StyleResident(BaseCreature creature, Point3D home, Map map)
    {
        if (creature is not BaseVendor vendor || vendor.Race == Race.Gargoyle || !Contains(home, map))
            return;

        for (int i = vendor.Items.Count - 1; i >= 0; i--)
        {
            if (vendor.Items[i] is BaseClothing or BaseArmor or BaseWeapon or BaseTool)
                vendor.Items[i].Delete();
        }

        vendor.Race = Race.Gargoyle;
        vendor.Name = NameList.RandomName("gargoyle vendor");
        vendor.Hue = vendor.Race.RandomSkinHue();
        var hornHue = vendor.Race.RandomHairHue();
        Utility.AssignRandomHair(vendor, hornHue);
        Utility.AssignRandomFacialHair(vendor, hornHue);

        var clothHue = Utility.RandomNeutralHue();
        vendor.AddItem(new GargishClothChestType1 { Hue = clothHue, LootType = LootType.Blessed });
        vendor.AddItem(new GargishClothLegsType1 { Hue = clothHue, LootType = LootType.Blessed });
    }
}
