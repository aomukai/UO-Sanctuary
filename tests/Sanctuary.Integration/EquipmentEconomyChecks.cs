using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class EquipmentEconomyChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(EquipmentEconomyChecks));
    private static readonly List<Item> _items = new();
    private static readonly List<Mobile> _mobiles = new();
    private static PlayerMobile _player;
    private sealed record SavedMaterial(uint Serial, int Amount, string Type);
    private sealed record Saved(uint Player, uint Gear, string State, SavedMaterial[] Materials);
    private static string Manifest => Path.Join(Core.BaseDirectory, "economy-checks.json");
    private sealed class RollRandom(int chance = 0, bool upper = false) : Random
    {
        public override int Next(int maxValue) => Math.Min(chance, maxValue - 1);
        public override int Next(int minValue, int maxValue) => upper ? maxValue - 1 : minValue;
    }
    private sealed class SplitRandom : Random
    {
        private int _call;
        public override int Next(int maxValue) => _call++ == 0 ? 99 : 0;
        public override int Next(int minValue, int maxValue) => minValue;
    }
    public static void Configure()
    {
        Register("economy-check", Setup);
        Register("economy-reload-check", Reload);
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable equipment resale, randomized salvage and imbuing checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
    private static PlayerMobile Player(string name)
    {
        var player = new PlayerMobile { Player = true, Body = 0x190, Name = name };
        _mobiles.Add(player);
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        ClassCreation.Profiles.AdvanceLevel(player, 99);
        player.RawStr = 200;
        player.AddItem(new Backpack());
        player.Skills.Imbuing.Cap = 120; player.Skills.Imbuing.Base = 120;
        player.Backpack.DropItem(new ImbuingLadle());
        foreach (var tool in new BaseTool[] { new SmithHammer(), new FletcherTools(), new SewingKit(), new TinkerTools(), new ScribesPen() })
        { player.Backpack.DropItem(tool); }
        player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5550, 1150), Map.Trammel);
        return player;
    }
    private static T Gear<T>(T item, GearRarity rarity = GearRarity.Normal, int level = 30) where T : Item
    {
        var insured = item.Insured;
        _items.Add(item);
        ClassGear.Profiles.Convert(item, level, rarity, new Random(42));
        _player.Backpack.DropItem(item);
        // Install the protected-item fixture after receipt, which cancels inherited insurance.
        item.Insured = insured;
        return item;
    }
    private static int Count<T>() where T : Item => _player.Backpack.GetAmount(typeof(T));
    private static EssenceQuote Quote(Item item, EssenceAction action = EssenceAction.Salvage)
        => EquipmentEconomy.Quote(_player, item, action);
    private static SalvageResult Salvage(Item item, Random random)
    {
        var quote = Quote(item);
        SalvageResult result = null;
        Require(quote != null && EquipmentEconomy.Commit(_player, quote, out result, random) && item.Deleted,
            $"Failed to salvage {item.GetType().Name}");
        Require(!EquipmentEconomy.Commit(_player, quote, out _, random), "A quote was replayed");
        return result;
    }
    private static void Setup()
    {
        _player = Player("EquipmentEconomy");
        var account = new Account("economy-save", Guid.NewGuid().ToString("N"));
        account[0] = _player;
        _player.Backpack.DropItem(new Gold(1000));
        CheckResale();
        CheckRestrictions();
        CheckMaterials();
        CheckRolls();
        CheckFailureAtomicity();
        var dagger = CheckImbuing();
        var materialItems = _player.Backpack.Items.Where(i => i is MagicEssence or MetalScraps or WoodenChips or LeatherStrips or ClothScraps).ToArray();
        Require(materialItems.Select(i => i.GetType()).Distinct().Count() == 5
            && materialItems.All(i => i.Origin == ItemOrigin.Crafted && i.Stackable), "Missing persistent salvage resource or wrong provenance");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(_player.Serial.Value, dagger.Serial.Value,
            JsonSerializer.Serialize(ClassGear.State(dagger)), materialItems.Select(i => new SavedMaterial(i.Serial.Value, i.Amount, i.GetType().FullName)).ToArray())));
        _items.Remove(dagger);
        foreach (var item in _items) { item.Delete(); }
        _items.Clear();
        _mobiles.Remove(_player);
        foreach (var mobile in _mobiles) { mobile.Delete(); }
        _mobiles.Clear();
        ClassCreation.Profiles.PruneDeleted();
        _logger.Information("economy-check: passed");
    }
    private static void CheckResale()
    {
        var vendor = new Blacksmith { Direction = Direction.South };
        _mobiles.Add(vendor);
        vendor.MoveToWorld(_player.Location, _player.Map);
        vendor.AIObject.AITimer.Stop();
        var info = new GenericSellInfo();
        info.Add(typeof(Dagger), 1000);
        info.Add(typeof(Bandage), 20);
        var dagger = Gear(new Dagger(), GearRarity.Magic);
        int raw = info.GetSellPriceFor(dagger), buy = info.GetBuyPriceFor(dagger);
        Require(BaseVendor.GetSellPriceFor(dagger, info) is >= 100 and <= 500
            && info.GetSellPriceFor(dagger) == raw && info.GetBuyPriceFor(dagger) == buy,
            "Persistent resale changed native valuation or missed the Normal band");
        var bandage = new Bandage();
        _items.Add(bandage);
        Require(BaseVendor.GetSellPriceFor(bandage, info) == 20, "Non-equipment profession goods were repriced");
        Require(EquipmentEconomy.SellPrice(dagger, 1) == EquipmentEconomy.SellPrice(dagger, 0),
            "A native quote changed the saved equipment offer");
        var shopInfo = vendor.GetSellInfo().First(i => i.IsSellable(dagger));
        int expected = BaseVendor.GetSellPriceFor(dagger, shopInfo), gold = Count<Gold>();
        Require(vendor.OnSellItems(_player, [new SellItemResponse(dagger, 1)]) && Count<Gold>() == gold + expected,
            "Actual NPC transaction paid a different amount than its quote");
    }
    private static void CheckRestrictions()
    {
        var vendor = Gear(ItemOrigins.Create(ItemOrigin.Vendor, () => new Dagger()), GearRarity.Magic);
        Require(Quote(vendor) == null && Quote(vendor, EssenceAction.Imbue) == null, "Vendor gear entered salvage or imbuing");
        var sold = Gear(new Dagger(), GearRarity.Magic);
        var quote = Quote(sold);
        var foreign = Player("ForeignEconomy");
        foreign.Backpack.DropItem(sold);
        Require(Quote(sold) == null && !EquipmentEconomy.Commit(_player, quote, out _), "Transferred gear used a stale quote");
        var changed = Gear(new Dagger(), GearRarity.Magic);
        quote = Quote(changed);
        ClassGear.Profiles.Convert(changed, 30, GearRarity.Rare, new Random(41));
        Require(!EquipmentEconomy.Commit(_player, quote, out _), "A changed rarity reused the old quote");
        foreach (var item in new Item[] { Gear(new Dagger { LootType = LootType.Blessed }, GearRarity.Magic),
                     Gear(new Dagger { Insured = true }, GearRarity.Magic), Gear(new Dagger { Movable = false }, GearRarity.Magic),
                     Gear(new Dagger { QuestItem = true }, GearRarity.Magic) })
        {
            Require(Quote(item) == null, "Protected equipment was salvageable");
        }
        var equipped = Gear(new Dagger(), GearRarity.Magic);
        Require(_player.EquipItem(equipped) && Quote(equipped) == null, "Equipped gear was salvageable");
        _player.Backpack.DropItem(equipped);
        var starter = Gear(new Dagger());
        ClassGear.Profiles.MarkStarter(starter);
        Require(Quote(starter) == null && Quote(starter, EssenceAction.Imbue) == null, "Starter protection was bypassed");
        var scissors = new Scissors();
        _items.Add(scissors);
        var robe = Gear(ItemOrigins.Create(ItemOrigin.Vendor, () => new Robe()));
        var leather = Gear(ItemOrigins.Create(ItemOrigin.Vendor, () => new LeatherChest()));
        Require(!robe.Scissor(_player, scissors) && !leather.Scissor(_player, scissors) && !robe.Deleted && !leather.Deleted,
            "Native scissors bypassed vendor restrictions");
        var smelt = typeof(Resmelt).GetNestedType("InternalTarget", BindingFlags.NonPublic);
        var method = smelt.GetMethod("Resmelt", BindingFlags.Instance | BindingFlags.NonPublic);
        var target = Activator.CreateInstance(smelt, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, [DefBlacksmithy.CraftSystem, null], null);
        Require((SmeltResult)method.Invoke(target, [_player, vendor, CraftResource.Iron]) == SmeltResult.Invalid && !vendor.Deleted,
            "Native forge resmelting bypassed vendor restrictions");
        var bag = new SalvageBag();
        _items.Add(bag);
        method = typeof(SalvageBag).GetMethod("Resmelt", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(!(bool)method.Invoke(bag, [_player, vendor, CraftResource.Iron]) && !vendor.Deleted,
            "Native salvage bag bypassed vendor restrictions");
    }
    private static void CheckMaterials()
    {
        foreach (var (item, expected) in new (Item, SalvageMaterial)[]
                 { (new Dagger(), SalvageMaterial.Metal), (new ChainChest(), SalvageMaterial.Metal),
                   (new RingmailChest(), SalvageMaterial.Metal), (new PlateArms(), SalvageMaterial.Metal),
                   (new WoodenShield(), SalvageMaterial.Wood), (new WoodlandChest(), SalvageMaterial.Wood),
                   (new Bow(), SalvageMaterial.Wood), (new Club(), SalvageMaterial.Wood),
                   (new QuarterStaff(), SalvageMaterial.Wood), (new Boomerang(), SalvageMaterial.Wood),
                   (new LeatherChest(), SalvageMaterial.Leather), (new LeatherArms(), SalvageMaterial.Leather),
                   (new StuddedChest(), SalvageMaterial.Leather), (new Robe(), SalvageMaterial.Cloth) })
        {
            Gear(item);
            Require(EquipmentEconomy.Material(item) == expected, $"Wrong physical salvage material: {item.GetType().Name}");
            var result = Salvage(item, new RollRandom());
            Require(result.Essence == 0 && result.Material == EquipmentEconomy.MaterialRange(expected).Bounds(30).Min,
                "White equipment yielded essence or ignored its item level");
        }
    }
    private static void CheckRolls()
    {
        var first = Salvage(Gear(new Dagger(), GearRarity.Magic, 1), new RollRandom());
        var last = Salvage(Gear(new Dagger(), GearRarity.Magic, 99), new RollRandom(0, true));
        Require(first.Essence == 1 && last.Essence == 2 && first.Material == 1 && last.Material == 12,
            "Rarity ranges or item-level material bounds were flattened");
        var rare = Salvage(Gear(new Bow(), GearRarity.Rare, 99), new RollRandom(0, true));
        Require(rare.Essence == 5 && rare.Material == 12, "Rare gear did not use its distinct essence range");
        var empty = Salvage(Gear(new Dagger(), GearRarity.Rare), new RollRandom(99));
        Require(empty is { Essence: 0, Material: 0 }, "Failed rolls refunded gear or guaranteed output");
        var partial = Salvage(Gear(new Dagger(), GearRarity.Magic), new SplitRandom());
        Require(partial.Essence == 0 && partial.Material > 0, "Essence and material were not independent rolls");
        var outcomes = new HashSet<int>();
        for (int seed = 0; seed < 256; seed++) { outcomes.Add(EquipmentEconomy.Settings.MagicEssence.Roll(1, new Random(seed))); }
        Require(outcomes.SetEquals([0, 1, 2]), "Ordinary random seeds never produced all configured outcomes");
    }
    private static void CheckFailureAtomicity()
    {
        var full = Player("FullSalvagePack");
        var item = ItemOrigins.Create(ItemOrigin.Drop, () => new Dagger());
        _items.Add(item);
        ClassGear.Profiles.Convert(item, 30, GearRarity.Magic, new Random(42));
        full.Backpack.DropItem(item);
        while (full.Backpack.TotalItems < full.Backpack.MaxItems) { full.Backpack.DropItem(new Item(1) { Weight = 0 }); }
        var quote = EquipmentEconomy.Quote(full, item, EssenceAction.Salvage);
        Require(!EquipmentEconomy.Commit(full, quote, out _, new RollRandom()) && !item.Deleted
            && full.Backpack.GetAmount(typeof(MagicEssence)) == 0, "Full pack consumed gear or leaked salvage output");
        var target = Gear(new Dagger());
        int gold = Count<Gold>(), essence = Count<MagicEssence>();
        var oldEssence = _player.Backpack.Items.OfType<MagicEssence>().ToArray();
        foreach (var resource in oldEssence) { resource.Delete(); }
        quote = Quote(target, EssenceAction.Imbue);
        var state = ClassGear.State(target);
        Require(!EquipmentEconomy.Commit(_player, quote, out _, new RollRandom()) && Count<Gold>() == gold
            && ReferenceEquals(ClassGear.State(target), state), "Missing essence consumed gold or changed gear");
        _player.Backpack.DropItem(ItemOrigins.Create(ItemOrigin.Crafted, () => new MagicEssence(essence)));
        foreach (var money in _player.Backpack.Items.OfType<Gold>().ToArray()) { money.Delete(); }
        quote = Quote(target, EssenceAction.Imbue);
        Require(!EquipmentEconomy.Commit(_player, quote, out _, new RollRandom()) && Count<MagicEssence>() == essence
            && ReferenceEquals(ClassGear.State(target), state), "Missing gold consumed essence or changed gear");
        _player.Backpack.DropItem(new Gold(gold));
    }
    private static Dagger CheckImbuing()
    {
        _player.Backpack.DropItem(ItemOrigins.Create(ItemOrigin.Crafted, () => new MagicEssence(30)));
        var dagger = Gear(ItemOrigins.Create(ItemOrigin.Crafted, () => new Dagger { Name = "Retained crafted base", Resource = CraftResource.Verite,
            HitPoints = 10, Hue = 123 }), level: 30);
        var before = ClassGear.State(dagger);
        int gold = Count<Gold>(), essence = Count<MagicEssence>(), skill = _player.Skills.Imbuing.BaseFixedPoint;
        var quote = Quote(dagger, EssenceAction.Imbue);
        Require(EquipmentEconomy.Commit(_player, quote, out _, new RollRandom())
            && ClassGear.State(dagger).Roll.Rarity == GearRarity.Rare
            && Count<Gold>() == gold - EquipmentEconomy.Settings.ImbueGoldCost
            && Count<MagicEssence>() == essence - EquipmentEconomy.Settings.ImbueEssenceCost,
            "Imbuing did not atomically charge both resources or roll a rare result");
        Require(dagger.Origin == ItemOrigin.Crafted && dagger.Name == "Retained crafted base" && dagger.Resource == CraftResource.Verite
            && dagger.HitPoints == 10 && dagger.Hue == 123 && ClassGear.State(dagger).Defense == before.Defense
            && ClassGear.State(dagger).Roll.ItemLevel == 30 && _player.Skills.Imbuing.BaseFixedPoint == skill,
            "Imbuing reset identity, base, material, durability, item level or granted unconfigured skill gains");
        Require(Quote(dagger, EssenceAction.Imbue) == null && !EquipmentEconomy.Commit(_player, quote, out _), "Affixes could be rerolled or a paid quote replayed");
        var magic = Gear(new Dagger(), level: 30);
        Require(EquipmentEconomy.Commit(_player, Quote(magic, EssenceAction.Imbue), out _, new RollRandom(99))
            && ClassGear.State(magic).Roll.Rarity == GearRarity.Magic, "Imbuing magic branch was unreachable");
        return dagger;
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var player = World.FindMobile((Serial)saved.Player) as PlayerMobile;
        var item = World.FindItem((Serial)saved.Gear) as Dagger;
        Require(player != null && item != null && item.IsChildOf(player.Backpack) && item.Origin == ItemOrigin.Crafted
            && item.Resource == CraftResource.Verite && item.HitPoints == 10 && item.Hue == 123
            && JsonSerializer.Serialize(ClassGear.State(item)) == saved.State, "Imbued identity or affixes did not persist");
        foreach (var resource in saved.Materials)
        {
            var material = World.FindItem((Serial)resource.Serial);
            Require(material is { Deleted: false, Stackable: true, Origin: ItemOrigin.Crafted } && material.IsChildOf(player.Backpack)
                && material.Amount == resource.Amount && material.GetType().FullName == resource.Type,
                "A salvage material failed its save/reload");
        }
        _logger.Information("economy-reload-check: passed");
    }
}
