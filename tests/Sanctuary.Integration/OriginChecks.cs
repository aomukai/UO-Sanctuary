using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class OriginChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(OriginChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "origin-checks.json");
    private sealed record Saved(uint Serial, ItemOrigin Origin);
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
    public static void Check()
    {
        CheckSerialization();
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = "OriginCheck" };
        var account = new Account("origin-save", Guid.NewGuid().ToString("N"));
        account[0] = player;
        player.AddItem(new Backpack());
        player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 1440, 1695), Map.Trammel);
        player.Str = player.Dex = player.Int = 100;
        var saved = new List<Saved>();
        foreach (var origin in Enum.GetValues<ItemOrigin>())
        {
            var plain = ItemOrigins.Create(origin, () => new Item(0x1F13));
            var dagger = ItemOrigins.Create(origin, () => new Dagger());
            var cloth = ItemOrigins.Create(origin, () => new Cloth(20));
            player.Backpack.DropItem(plain);
            player.Backpack.DropItem(dagger);
            player.Backpack.DropItem(cloth);
            ClassGear.Profiles.Convert(dagger, 40, GearRarity.Magic, new Random(42));
            Require(dagger.Origin == origin && plain.Origin == origin && cloth.Origin == origin,
                "Item origin was lost on creation, transfer or gear conversion");
            dagger.Name = "Renamed item";
            player.EquipItem(dagger);
            player.Backpack.DropItem(dagger);
            Require(dagger.Origin == origin, "Equipping or renaming changed item origin");
            var split = Mobile.LiftItemDupe(cloth, 7);
            Require(split is { Amount: 13 } && split.Origin == origin && cloth.Amount == 7,
                "Splitting a stack lost its origin or changed its total");
            var copy = new Dagger();
            dagger.Dupe(copy);
            Require(copy.Origin == origin, "Duplicating an item lost its origin");
            copy.Delete();
            Require(cloth.StackWith(player, split, false) && cloth.Amount == 20 && split.Deleted,
                "Stacks of the same origin could not merge");
            foreach (var item in new[] { plain, dagger, cloth })
            {
                saved.Add(new(item.Serial.Value, origin));
            }
        }
        var vendorCloth = ItemOrigins.Create(ItemOrigin.Vendor, () => new Cloth(4));
        var craftedCloth = ItemOrigins.Create(ItemOrigin.Crafted, () => new Cloth(4));
        try
        {
            Require(ItemOrigins.Create(ItemOrigin.Crafted, () => vendorCloth).Origin == ItemOrigin.Vendor,
                "Returning an existing item from a factory changed its origin");
            Require(!vendorCloth.CanStackWith(craftedCloth) && !vendorCloth.StackWith(player, craftedCloth, false)
                && vendorCloth.Amount == 4 && craftedCloth.Amount == 4 && !craftedCloth.Deleted,
                "Mixed-origin stacks erased provenance");
        }
        finally
        {
            vendorCloth.Delete();
            craftedCloth.Delete();
        }
        CheckSources(player, saved);
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
        _logger.Information("origin-check: passed; all item families, vendor purchases, crafting, split/merge, copying and serialization");
    }
    private static void CheckSerialization()
    {
        foreach (var origin in Enum.GetValues<ItemOrigin>())
        {
            var original = ItemOrigins.Create(origin, () => new Item(0x1F13));
            var restored = new Item();
            var legacy = new Item();
            try
            {
                var writer = new BufferWriter(new byte[1024], true);
                original.Serialize(writer);
                var bytes = writer.Buffer[..(int)writer.Position];
                restored.Deserialize(new BufferReader(bytes));
                Require(restored.Origin == origin, "Serialized item origin did not round-trip");
                var oldBytes = new byte[bytes.Length - 1];
                bytes.AsSpan(0, 4).CopyTo(oldBytes);
                bytes.AsSpan(5).CopyTo(oldBytes.AsSpan(4));
                BinaryPrimitives.WriteInt32LittleEndian(oldBytes, 11);
                legacy.Deserialize(new BufferReader(oldBytes));
                Require(legacy.Origin == ItemOrigin.Drop, "Existing test items did not default to Drop");
                bytes[4] = 255;
                bool rejected = false;
                try
                {
                    restored.Deserialize(new BufferReader(bytes));
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }
                Require(rejected, "Invalid saved origin was accepted");
            }
            finally
            {
                original.Delete();
                restored.Delete();
                legacy.Delete();
            }
        }
        try
        {
            ItemOrigins.Create<int>(ItemOrigin.Vendor, () => throw new InvalidOperationException());
        }
        catch (InvalidOperationException)
        {
        }
        var afterFailure = new Item();
        Require(afterFailure.Origin == ItemOrigin.Drop, "Failed item factory leaked its origin into later creation");
        afterFailure.Delete();
    }
    private static void CheckSources(PlayerMobile player, List<Saved> saved)
    {
        var vendor = new Blacksmith();
        vendor.MoveToWorld(player.Location, player.Map);
        vendor.AIObject.AITimer.Stop();
        try
        {
            player.Backpack.DropItem(new Gold(1000));
            var offer = vendor.GetBuyInfo().OfType<GenericBuyInfo>().First(g => typeof(Item).IsAssignableFrom(g.Type));
            var display = (Item)offer.GetDisplayEntity();
            Require(display.Origin == ItemOrigin.Vendor, "Vendor display item has no origin");
            var before = player.Backpack.Items.ToHashSet();
            Require(vendor.OnBuyItems(player, [new BuyItemResponse(display.Serial, 2)]), "Actual vendor purchase failed");
            var bought = player.Backpack.Items.Where(i => !before.Contains(i) && i.GetType() == offer.Type).ToArray();
            Require(bought.Length > 0 && bought.All(i => i.Origin == ItemOrigin.Vendor),
                "Vendor purchase did not tag every output item");
            saved.AddRange(bought.Select(i => new Saved(i.Serial.Value, i.Origin)));
        }
        finally
        {
            vendor.Delete();
        }
        player.Skills.Tailoring.Base = 120;
        var tool = new SewingKit(100);
        player.Backpack.DropItem(tool);
        player.Backpack.DropItem(new Cloth(100));
        var system = DefTailoring.CraftSystem;
        var recipe = system.CraftItems.Single(i => i.ItemType == typeof(Shirt));
        recipe.CompleteCraft(1, false, player, system, null, tool, null);
        recipe.CompleteCraft(1, false, player, system, null, tool, null, 0);
        var crafted = player.Backpack.Items.OfType<Shirt>().ToArray();
        Require(crafted.Length == 2 && crafted.All(i => i.Origin == ItemOrigin.Crafted && i.PlayerConstructed),
            "Both crafting completion paths must tag their outputs");
        saved.AddRange(crafted.Select(i => new Saved(i.Serial.Value, i.Origin)));
    }
    public static void Reload()
    {
        foreach (var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            Require(World.FindItem((Serial)saved.Serial) is { Deleted: false } item && item.Origin == saved.Origin,
                "Item origin changed or disappeared after a world save/reload");
        }
        _logger.Information("origin-reload-check: passed");
    }
}
