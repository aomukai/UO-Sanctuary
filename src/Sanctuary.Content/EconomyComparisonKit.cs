// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Commands;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class EconomyComparisonKit
{
    public static void Configure() => CommandSystem.Register("EconomyTest", AccessLevel.GameMaster, TestKit);
    private static void TestKit(CommandEventArgs args)
    {
        if (args.Mobile is not PlayerMobile player || !EquipmentEconomy.Ready(player))
        {
            return;
        }
        Add(player, ItemOrigins.Create(ItemOrigin.Drop, () => new Dagger()), GearRarity.Magic, 20);
        Add(player, ItemOrigins.Create(ItemOrigin.Drop, () => new Bow()), GearRarity.Rare, 40);
        Add(player, ItemOrigins.Create(ItemOrigin.Crafted, () => new Dagger()), GearRarity.Normal, 20);
        Add(player, ItemOrigins.Create(ItemOrigin.Drop, () => new ChainChest()), GearRarity.Normal, 30);
        Add(player, ItemOrigins.Create(ItemOrigin.Drop, () => new WoodenShield()), GearRarity.Normal, 30);
        Add(player, ItemOrigins.Create(ItemOrigin.Crafted, () => new LeatherChest()), GearRarity.Normal, 30);
        Add(player, ItemOrigins.Create(ItemOrigin.Crafted, () => new Robe()), GearRarity.Normal, 30);
        Add(player, ItemOrigins.Create(ItemOrigin.Vendor, () => new Dagger { Name = "Vendor restriction comparison" }), GearRarity.Magic, 20);
        Store(player, ItemOrigins.Create(ItemOrigin.Crafted, () => new MagicEssence(20)));
        Store(player, new Gold(500));
        player.SendMessage("Economy comparison kit added where space allowed. Use Imbuing or [Essence. Vendor gear is the rejected comparison.");
    }
    private static void Add(PlayerMobile player, Item item, GearRarity rarity, int level)
    {
        ClassGear.Profiles.Convert(item, level, rarity, Random.Shared);
        Store(player, item);
    }
    private static void Store(PlayerMobile player, Item item)
    {
        if (!player.Backpack.TryDropItem(player, item, true))
        {
            item.Delete();
        }
    }
}
