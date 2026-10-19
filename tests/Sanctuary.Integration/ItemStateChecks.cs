using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ItemStateChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(ItemStateChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "item-state-checks.json");
    private sealed record Saved(uint Serial, ItemOrigin Origin, string State, string LegacyState, NativeGearDetails Native);
    public static void Configure()
    {
        Register("item-state-check", Setup);
        Register("item-state-reload-check", () => Reload(false));
        Register("item-state-legacy-reload-check", () => Reload(true));
        Register("item-state-write-legacy", () => File.WriteAllBytes(
            Path.Join(Core.BaseDirectory, "Saves/SanctuaryEquipment/SanctuaryEquipment.bin"), VersionZero()));
    }
    private static void Register(string command, Action check) => ConsoleInputHandler.RegisterCommand(command,
        "Isolated item persistence check", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                check();
                _logger.Information("{Command}: passed", command);
            }
            catch (Exception error)
            {
                _logger.Error(error, "{Command}: failed", command);
            }
        }));
    private static void Require(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
    private static string State(Item item) => JsonSerializer.Serialize(ClassGear.State(item));
    private static GearState Legacy(GearState state) => state with
    {
        Tier = GearTier.Normal, Quality = CraftQuality.Average, NativeSockets = 0, TinkerSockets = 0,
        Identified = true, IdentifiedSaleOffer = null
    };
    private static byte[] Serialize()
    {
        var writer = new BufferWriter(new byte[1024 * 1024], true);
        ClassGear.Profiles.Serialize(writer);
        return writer.Buffer[..(int)writer.Position];
    }
    // Emit the original version-0 layout from the real current serialized records.
    private static byte[] VersionZero()
    {
        var reader = new BufferReader(Serialize());
        var writer = new BufferWriter(new byte[1024 * 1024], true);
        Require(reader.ReadInt() == 1, "Expected gear version 1");
        writer.Write(0);
        int migrations = reader.ReadInt();
        writer.Write(migrations);
        for (int i = 0; i < migrations; i++) { writer.Write(reader.ReadUInt()); }
        int count = reader.ReadInt();
        writer.Write(count);
        for (int i = 0; i < count; i++)
        {
            writer.Write(reader.ReadUInt());
            writer.Write(reader.ReadString());
            writer.Write(reader.ReadInt());
            writer.Write(reader.ReadBool());
            writer.Write(reader.ReadByte());
            writer.Write(reader.ReadInt());
            writer.Write(reader.ReadInt());
            writer.Write(reader.ReadInt());
            int affixes = reader.ReadInt();
            writer.Write(affixes);
            for (int j = 0; j < affixes; j++)
            {
                string id = reader.ReadString();
                writer.Write(id);
                foreach (var modifier in AffixBalance.Find(id).Modifiers) { writer.Write(reader.ReadInt()); }
            }
            reader.ReadByte(); reader.ReadByte(); reader.ReadInt(); reader.ReadInt(); reader.ReadBool(); reader.ReadInt();
        }
        return writer.Buffer[..(int)writer.Position];
    }
    private static void Reject(Item item, GearState state)
    {
        string before = State(item);
        bool rejected = false;
        try { ClassGear.Profiles.Store(item, state); }
        catch (InvalidDataException) { rejected = true; }
        Require(rejected && State(item) == before, "Invalid state was stored or changed the previous record");
    }
    private static void Setup()
    {
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = "ItemState" };
        new Account("item-state-save", Guid.NewGuid().ToString("N"))[0] = player;
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        player.AddItem(new Backpack());
        player.RawStr = 200;
        player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5550, 1150), Map.Trammel);
        var saved = new List<Saved>();
        Func<Item>[] factories = [() => new Longsword(), () => new WoodenShield(), () => new LeatherCap(),
            () => new RingmailChest(), () => new GoldRing(), () => new Necklace(), () => new SkullCap(), () => new BoneChest()];
        GearMaterial[] materials = [GearMaterial.Metal, GearMaterial.Wood, GearMaterial.Leather, GearMaterial.Metal,
            GearMaterial.Metal, GearMaterial.Metal, GearMaterial.Cloth, GearMaterial.Bone];
        for (int i = 0; i < factories.Length; i++)
        {
            var factory = factories[i];
            var item = ItemOrigins.Create(i % 2 == 0 ? ItemOrigin.Crafted : ItemOrigin.Drop, factory);
            player.Backpack.DropItem(item);
            SetNative(item);
            var state = ClassGear.Profiles.Convert(item, 35, GearRarity.Magic, new Random(50 + i));
            state = state with { Quality = CraftQuality.Superior, NativeSockets = GearDetails.Socketable(item) ? 2 : 0,
                TinkerSockets = GearDetails.Socketable(item) ? 3 : 0, Identified = false, IdentifiedSaleOffer = 234 + i };
            ClassGear.Profiles.Store(item, state);
            var details = GearDetails.Read(item);
            Require(details.Material == materials[i] && details.Durability == 17 && details.MaximumDurability == 73,
                "Native material or durability projection failed");
            Require(i is 4 or 5 ? details.Creator == null : details.Creator == "Persistent Maker", "Native maker changed");
            if (i == 0) { Require(details.Resource == CraftResource.Valorite, "Coloured metal subtype was lost"); }
            if (i == 1) { Require(details.Resource == CraftResource.OakWood, "Coloured wood subtype was lost"); }
            if (i == 2) { Require(details.Resource == CraftResource.SpinedLeather, "Coloured leather subtype was lost"); }
            string original = State(item);
            var direct = factory();
            Item.CopyProperties(item, direct);
            Require(State(direct) == original && direct.Origin == item.Origin && GearDetails.Read(direct) == details,
                "Direct property copy lost gear/native state");
            player.Backpack.DropItem(direct);
            var copy = factory();
            item.Dupe(copy);
            player.Backpack.DropItem(copy);
            Require(State(copy) == original && copy.Origin == item.Origin && GearDetails.Read(copy) == details
                && !ReferenceEquals(ClassGear.State(copy).Roll.Affixes, state.Roll.Affixes), "Dupe lost or shared mutable gear state");
            item.Stackable = copy.Stackable = true;
            Require(!item.CanStackWith(copy) && !item.StackWith(player, copy, false) && !copy.Deleted,
                "Equipment stacking erased state");
            item.Amount = 2;
            var split = Mobile.LiftItemDupe(item, 1);
            Require(split != null && split.Amount == 1 && item.Amount == 1 && State(split) == original
                && split.Origin == item.Origin && GearDetails.Read(split) == details, "Split lost gear/native state");
            item.Stackable = copy.Stackable = split.Stackable = false;
            var trader = new Backpack();
            player.Backpack.DropItem(trader);
            trader.DropItem(item);
            player.Backpack.DropItem(item);
            Require(State(item) == original && item.Origin == copy.Origin,
                "Container transfer changed item metadata");
            trader.Delete();
            CheckTrade(player, item);
            foreach (var kept in new[] { item, direct, copy, split })
            {
                saved.Add(new(kept.Serial.Value, kept.Origin, State(kept), JsonSerializer.Serialize(Legacy(state)), GearDetails.Read(kept)));
            }
            Reject(item, state with { Tier = GearTier.Elite });
            Reject(item, state with { Quality = (CraftQuality)255 });
            Reject(item, state with { NativeSockets = -1 });
            Reject(item, state with { NativeSockets = 3 });
            Reject(item, state with { TinkerSockets = 6 });
            Reject(item, state with { IdentifiedSaleOffer = -1 });
            Reject(item, state with { IdentifiedSaleOffer = 501 });
            Reject(item, state with { Quality = CraftQuality.Average, NativeSockets = 1 });
            if (!GearDetails.Socketable(item)) { Reject(item, state with { TinkerSockets = 1 }); }
        }
        CheckImplicitCopy();
        CheckImbuing(player);
        CheckProtection(player);
        byte[] current = Serialize();
        try
        {
            ClassGear.Profiles.Deserialize(new BufferReader(VersionZero()));
            ClassGear.Profiles.PostDeserialize();
            foreach (var row in saved)
            {
                Require(State(World.FindItem((Serial)row.Serial)) == row.LegacyState, "Version-0 defaults or rolls changed");
            }
        }
        finally { ClassGear.Profiles.Deserialize(new BufferReader(current)); }
        ClassGear.Profiles.PostDeserialize();
        CheckMalformedBuffers();
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
        _logger.Information("Item-state coverage: eight bases, native subtypes/wear, 32 saved identities, copy/split/transfer, legacy migration and Imbuing");
    }
    private static void SetNative(Item item)
    {
        switch (item)
        {
            case BaseWeapon weapon:
                weapon.Resource = CraftResource.Valorite; weapon.Crafter = "Persistent Maker";
                weapon.MaxHitPoints = 73; weapon.HitPoints = 17; break;
            case BaseArmor armor:
                if (armor is WoodenShield) { armor.Resource = CraftResource.OakWood; }
                else if (armor is LeatherCap) { armor.Resource = CraftResource.SpinedLeather; }
                armor.Crafter = "Persistent Maker"; armor.MaxHitPoints = 73; armor.HitPoints = 17; break;
            case BaseClothing clothing:
                clothing.Crafter = "Persistent Maker"; clothing.MaxHitPoints = 73; clothing.HitPoints = 17; break;
            case BaseJewel jewel:
                jewel.MaxHitPoints = 73; jewel.HitPoints = 17; break;
        }
    }
    private static void CheckImplicitCopy()
    {
        var item = new RingmailChest();
        int defense = ClassGear.Defense(item);
        var copy = new RingmailChest();
        item.Dupe(copy);
        Require(ClassGear.Defense(copy) == defense && State(copy) == State(item), "Implicit white defense changed on copy");
        item.Delete(); copy.Delete();
    }
    private static void CheckTrade(PlayerMobile player, Item item)
    {
        var receiver = new PlayerMobile { Body = 0x190, Player = true, Name = "TradeReceiver", RawStr = 200 };
        receiver.AddItem(new Backpack());
        receiver.MoveToWorld(player.Location, player.Map);
        var serial = item.Serial;
        string before = State(item);
        var native = GearDetails.Read(item);
        var origin = item.Origin;
        var cancelled = new SecureTrade(player, receiver);
        cancelled.From.Container.DropItem(item);
        cancelled.Cancel();
        Require(item.IsChildOf(player.Backpack) && State(item) == before, "Cancelled secure trade lost state");
        var completed = new SecureTrade(player, receiver);
        completed.From.Container.DropItem(item);
        completed.From.Accepted = completed.To.Accepted = true;
        completed.Update();
        Require(!completed.Valid && item.IsChildOf(receiver.Backpack) && item.Serial == serial
            && item.Origin == origin && State(item) == before && GearDetails.Read(item) == native,
            "Accepted secure trade changed identity or metadata");
        player.Backpack.DropItem(item);
        receiver.Delete();
    }
    private static void CheckMalformedBuffers()
    {
        ClassGear.Profiles.Prune();
        byte[] current = Serialize();
        foreach (int offset in new[] { 0, current.Length - 15, current.Length - 14, current.Length - 9, current.Length - 4 })
        {
            var corrupt = (byte[])current.Clone();
            if (offset == current.Length - 15 || offset == current.Length - 14) { corrupt[offset] = 255; }
            else { BinaryPrimitives.WriteInt32LittleEndian(corrupt.AsSpan(offset), int.MaxValue); }
            bool rejected = false;
            try
            {
                ClassGear.Profiles.Deserialize(new BufferReader(corrupt));
                ClassGear.Profiles.PostDeserialize();
            }
            catch (InvalidDataException) { rejected = true; }
            finally { ClassGear.Profiles.Deserialize(new BufferReader(current)); }
            Require(rejected, "Malformed saved equipment metadata was accepted");
        }
        ClassGear.Profiles.PostDeserialize();
    }
    private static void CheckImbuing(PlayerMobile player)
    {
        var implicitWhite = new Longsword();
        player.Backpack.DropItem(implicitWhite);
        var unpaid = EquipmentEconomy.Quote(player, implicitWhite, EssenceAction.Imbue);
        Require(unpaid != null && !EquipmentEconomy.Commit(player, unpaid, out _, new Random(42))
            && ClassGear.State(implicitWhite) == null, "Unpaid Imbuing changed implicit gear state");
        implicitWhite.Delete();
        var item = new Longsword();
        player.Backpack.DropItem(item);
        var state = ClassGear.Profiles.Convert(item, 35) with
        { Quality = CraftQuality.HighQuality, NativeSockets = 1, IdentifiedSaleOffer = 321 };
        ClassGear.Profiles.Store(item, state with { TinkerSockets = 1 });
        Require(EquipmentEconomy.Quote(player, item, EssenceAction.Imbue) == null, "Tinker socket permitted Imbuing");
        ClassGear.Profiles.Store(item, state);
        player.Backpack.DropItem(new Gold(1000));
        player.Backpack.DropItem(new MagicEssence(100));
        var quote = EquipmentEconomy.Quote(player, item, EssenceAction.Imbue);
        Require(quote != null && EquipmentEconomy.Commit(player, quote, out _, new Random(42)), "Native socket blocked valid Imbuing");
        Require(ClassGear.State(item).NativeSockets == 1 && ClassGear.State(item).Quality == CraftQuality.HighQuality
            && ClassGear.State(item).IdentifiedSaleOffer == 321, "Imbuing erased metadata");
        item.Delete();
    }
    private static void CheckProtection(PlayerMobile player)
    {
        var starter = new Dagger { LootType = LootType.Newbied };
        player.Backpack.DropItem(starter);
        ClassGear.Profiles.MarkStarter(starter);
        var copy = new Dagger();
        starter.Dupe(copy);
        player.Backpack.DropItem(copy);
        Require(ClassGear.State(copy).StarterTeeth && copy.LootType == LootType.Newbied
            && EquipmentEconomy.Quote(player, copy, EssenceAction.Salvage) == null, "Copy laundered starter protection");
        Reject(starter, ClassGear.State(starter) with { TinkerSockets = 1 });
        var vendor = ItemOrigins.Create(ItemOrigin.Vendor, () => new Longsword());
        player.Backpack.DropItem(vendor);
        var state = ClassGear.Profiles.Convert(vendor);
        Reject(vendor, state with { IdentifiedSaleOffer = 234 });
        var vendorCopy = new Longsword();
        vendor.Dupe(vendorCopy);
        player.Backpack.DropItem(vendorCopy);
        Require(vendorCopy.Origin == ItemOrigin.Vendor && EquipmentEconomy.Quote(player, vendorCopy, EssenceAction.Imbue) == null,
            "Copy laundered vendor exclusion");
        starter.Delete(); copy.Delete(); vendor.Delete(); vendorCopy.Delete();
    }
    private static void Reload(bool legacy)
    {
        foreach (var row in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var item = World.FindItem((Serial)row.Serial);
            Require(item is { Deleted: false } && item.Origin == row.Origin && State(item) == (legacy ? row.LegacyState : row.State)
                && GearDetails.Read(item) == row.Native, "Saved identity, random values, provenance or native fields changed on restart");
        }
    }
}
