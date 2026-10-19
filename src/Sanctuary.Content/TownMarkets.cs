// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary;

public static partial class TownMarkets
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(TownMarkets));
    public static MarketSettings Settings { get; private set; }
    public static MarketSite[] Sites { get; private set; } = [];
    public static MarketSupply[] Supplies { get; private set; } = [];
    public static MarketProfiles Profiles { get; private set; }
    private static readonly Dictionary<string, Type> _itemTypes = typeof(BaseArmor).Assembly.GetTypes()
        .Concat(typeof(TownMarkets).Assembly.GetTypes()).Where(t => typeof(Item).IsAssignableFrom(t))
        .GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.FirstOrDefault(t => t.Namespace == "Server.Items") ?? g.First(), StringComparer.Ordinal);
    private static readonly Dictionary<MarketRole, IShopSellInfo[]> _sellInfos = new();
    private static readonly Dictionary<string, MarketSupply> _supplies = new(StringComparer.Ordinal);
    private static readonly Dictionary<(MarketRole, int, GearRarity), EquipmentRecipe[]> _candidates = new();
    public static string SupplyId(MarketSupply row) => $"{row.Type}/{row.Kind}/{row.Tier}";
    public static SkillName? Skill(MarketRole role) => role switch
    {
        MarketRole.Smith => SkillName.Blacksmith, MarketRole.Tailor => SkillName.Tailoring,
        MarketRole.Bowyer => SkillName.Fletching, MarketRole.Carpenter => SkillName.Carpentry,
        MarketRole.Alchemist => SkillName.Alchemy, MarketRole.Cook => SkillName.Cooking,
        MarketRole.Cartographer => SkillName.Cartography, MarketRole.Scribe => SkillName.Inscribe,
        MarketRole.Tinker => SkillName.Tinkering, MarketRole.Imbuer => SkillName.Imbuing,
        MarketRole.Miner => SkillName.Mining, MarketRole.Lumberjack => SkillName.Lumberjacking,
        MarketRole.Fisher => SkillName.Fishing, _ => null
    };
    public static void Load()
    {
        var settings = BalanceCatalog.Read<MarketSettings>("market-settings");
        if (settings.Length != 1) { throw new InvalidDataException("One market settings row is required."); }
        Settings = settings[0];
        if (Settings.OrdinaryItemLevel is < 1 or > 99 || Settings.BritainItemLevel < Settings.OrdinaryItemLevel
            || Settings.BritainItemLevel > 99 || Settings.WhiteOffers is < 1 or > 100 || Settings.MagicOffers is < 1 or > 100
            || Settings.RefreshMinutes is < 1 or > 1440 || Settings.WhitePriceMultiplier < 2
            || Settings.MagicPriceMultiplier < Settings.WhitePriceMultiplier || Settings.VendorResalePercent is < 0 or > 25)
        { throw new InvalidDataException("Invalid market settings."); }
        foreach (var tier in Enum.GetValues<GearTier>())
        {
            var (min, max) = TradePrices.Band(tier);
            if (min < 100 || max < min || (long)max * Settings.MagicPriceMultiplier > 60000)
            { throw new InvalidDataException("Invalid market price band."); }
        }
        Sites = BalanceCatalog.Read<MarketSite>("town-markets");
        var ids = new HashSet<string>();
        foreach (var site in Sites)
        {
            if (!ids.Add(site.Id) || !Enum.IsDefined(site.Role) || site.Map is not ("Felucca" or "Trammel")
                || string.IsNullOrWhiteSpace(site.Town) || site.X < 0 || site.Y < 0 || string.IsNullOrWhiteSpace(site.Source))
            { throw new InvalidDataException("Invalid market site."); }
        }
        _supplies.Clear(); _candidates.Clear(); _sellInfos.Clear();
        Supplies = BalanceCatalog.Read<MarketSupply>("market-supplies");
        foreach (var row in Supplies)
        {
            var type = EquipmentCrafting.Resolve(row.Type);
            if (!MaterialProcessing.StockAllowed(type) || row.Price is < 1 or > 60000 || row.SellPrice < 0
                || row.SellPrice >= row.Price || row.Roles.Length == 0 || row.Roles.Any(r => !Enum.IsDefined(r))
                || row.Kind != null && (type != typeof(RestorationPotion) || row.Tier is < 1 or > 4)
                || row.Kind == null && row.Tier != 0)
            { throw new InvalidDataException("Invalid merchant supply."); }
            _supplies.Add(SupplyId(row), row);
        }
    }
    public static void Configure()
    {
        Profiles = new MarketProfiles();
        ClassEconomyHooks.Stock = Stock;
        ClassEconomyHooks.SellInfo = SellInfo;
        ClassEconomyHooks.BuyItems = Buy;
        ClassEconomyHooks.SellItems = Sell;
        ClassEconomyHooks.SellAllowed = TradePrices.CanSell;
        ClassEconomyHooks.ResaleAllowed = _ => false;
        EventSink.WorldSave += Profiles.Prune;
    }
    public static MarketRole? Role(BaseVendor vendor)
    {
        if (Profiles?.Find(vendor.Serial) is { } state) { return state.Role; }
        string name = vendor.GetType().Name.Replace("Guildmaster", "", StringComparison.Ordinal);
        return name switch
        {
            "Blacksmith" or "Weaponsmith" or "Armorer" => MarketRole.Smith,
            "Tailor" or "Cobbler" or "Weaver" or "Furtrader" => MarketRole.Tailor,
            "Bowyer" or "Ranger" => MarketRole.Bowyer, "Carpenter" => MarketRole.Carpenter,
            "Alchemist" or "Herbalist" => MarketRole.Alchemist, "Cook" or "Baker" or "InnKeeper" => MarketRole.Cook,
            "Mapmaker" => MarketRole.Cartographer, "Scribe" or "Mage" => MarketRole.Scribe,
            "Tinker" => MarketRole.Tinker, "Jeweler" => MarketRole.Jeweller,
            "Miner" => MarketRole.Miner, "Lumberjack" => MarketRole.Lumberjack,
            "Fisherman" or "Fisher" => MarketRole.Fisher, "Provisioner" => MarketRole.Provisioner, _ => null
        };
    }
    public static int ItemLevel(BaseVendor vendor) => vendor.Region.IsPartOf("Britain")
        || Profiles.Find(vendor.Serial)?.Site is { } id && Sites.Any(s => s.Id == id && s.Town == "Britain")
            ? Settings.BritainItemLevel : Settings.OrdinaryItemLevel;
    internal static Item Supply(MarketSupply row)
    {
        var item = ItemOrigins.Create(ItemOrigin.Vendor, () => row.Kind is { } kind
            ? new RestorationPotion(kind, row.Tier) : EquipmentCrafting.Resolve(row.Type).CreateInstance<Item>());
        if (item is BaseTool tool) { CraftTransactions.Supply(tool); }
        if (ClassGear.Converted(item)) { ClassGear.Profiles.Ensure(item); }
        return item;
    }
    internal static EquipmentRecipe[] Candidates(MarketRole role, int level, GearRarity rarity)
    {
        var key = (role, level, rarity);
        if (_candidates.TryGetValue(key, out var cached)) { return cached; }
        var rows = new List<EquipmentRecipe>();
        foreach (var row in EquipmentCrafting.Recipes)
        {
            if (row.Tier == GearTier.Elite || row.Tier == GearTier.Exceptional && level < 70
                || EquipmentBalance.Base(row.BaseCode).QualityLevel > level) { continue; }
            bool include = role switch
            {
                MarketRole.Smith => row.Skill == SkillName.Blacksmith,
                MarketRole.Tailor => row.Skill == SkillName.Tailoring,
                MarketRole.Bowyer or MarketRole.Carpenter => row.Skill == SkillName.Fletching,
                MarketRole.Provisioner => true, _ => false
            };
            if (include && (rarity == GearRarity.Normal || MagicCapable(row))) { rows.Add(row); }
        }
        if (role is MarketRole.Jeweller or MarketRole.Tinker or MarketRole.Provisioner)
        {
            foreach (var gear in EquipmentBalance.All)
            {
                if (!gear.NormalDrop || gear.Slot is not (GearSlot.Ring or GearSlot.Amulet) || gear.BaseCode == null) { continue; }
                var row = new EquipmentRecipe("shop/" + gear.Type, gear.Type, EquipmentBalance.Base(gear.BaseCode).Name,
                    SkillName.Tinkering, GearTier.Normal, gear.BaseCode, 0, 0, []);
                if (rarity == GearRarity.Normal || MagicCapable(row)) { rows.Add(row); }
            }
        }
        _candidates.Add(key, cached = rows.ToArray());
        return cached;
    }
    private static bool MagicCapable(EquipmentRecipe row)
    {
        var type = EquipmentCrafting.Resolve(row.Type);
        EquipmentBalance.TryGet(type, out var gear);
        return gear.AllowsAffixes && (gear.Category != "Clothing" || gear.Slot == GearSlot.Belt
            || EquipmentCrafting.PhysicalMaterial(type) == GearMaterial.Leather);
    }
    internal static Item Equipment(EquipmentRecipe row, int level, GearRarity rarity, Random random)
    {
        var item = ItemOrigins.Create(ItemOrigin.Vendor, () => EquipmentCrafting.Resolve(row.Type).CreateInstance<Item>());
        item.PlayerConstructed = false;
        var basis = EquipmentBalance.Base(row.BaseCode);
        var roll = AffixBalance.Roll(basis, level, rarity, random, ClassGear.Implemented);
        roll = roll with { RequiredLevel = Math.Max(EquipmentCrafting.EquipFloor(row.Tier), roll.RequiredLevel) };
        var state = new GearState(row.BaseCode, random.Next(basis.MinDefense, basis.MaxDefense + 1), false, roll) { Tier = row.Tier };
        ClassGear.Profiles.Store(item, state);
        item.Name = $"{row.Tier} {row.Name}";
        return item;
    }
    public static MarketState Market(BaseVendor vendor)
    {
        if (Role(vendor) is not { } role) { return null; }
        var state = Profiles.Find(vendor.Serial) ?? Profiles.Add(vendor, role);
        if (World.FindItem(state.Shelf) is not MarketShelf shelf)
        {
            shelf = new MarketShelf(); vendor.AddItem(shelf); state.Shelf = shelf.Serial;
        }
        // Newly authored supplies join existing shops without rerolling gear.
        {
            foreach (var row in Supplies)
            {
                if (!row.Roles.Contains(role) && !(role == MarketRole.Provisioner && row.Type is "Bottle" or "Apple")) { continue; }
                if (state.Offers.Exists(o => !o.Equipment && o.Supply == SupplyId(row))) { continue; }
                var item = Supply(row); shelf.DropItem(item);
                state.Offers.Add(new(item.Serial, row.Price, false, SupplyId(row)));
            }
        }
        Refresh(state, vendor, Core.Now);
        return state;
    }
    internal static void Refresh(MarketState state, BaseVendor vendor, DateTime now, Random random = null)
    {
        if (now < state.NextRefresh) { return; }
        random ??= Random.Shared;
        var shelf = (MarketShelf)World.FindItem(state.Shelf);
        for (int i = state.Offers.Count - 1; i >= 0; i--)
        {
            var offer = state.Offers[i];
            if (!offer.Equipment) { continue; }
            if (World.FindItem(offer.Item) is { } item && item.Parent == shelf) { item.Delete(); }
            state.Offers.RemoveAt(i);
        }
        int level = ItemLevel(vendor);
        foreach (var rarity in new[] { GearRarity.Normal, GearRarity.Magic })
        {
            var pool = Candidates(state.Role, level, rarity);
            if (pool.Length == 0) { continue; }
            int count = rarity == GearRarity.Normal ? Settings.WhiteOffers : Settings.MagicOffers;
            for (int i = 0; i < count; i++)
            {
                var row = pool[random.Next(pool.Length)];
                var item = Equipment(row, level, rarity, random); shelf.DropItem(item);
                int price = TradePrices.Valuation(row.Tier, random) * (rarity == GearRarity.Normal
                    ? Settings.WhitePriceMultiplier : Settings.MagicPriceMultiplier);
                state.Offers.Add(new(item.Serial, price, true, null));
            }
        }
        var period = TimeSpan.FromMinutes(Settings.RefreshMinutes);
        state.NextRefresh = state.NextRefresh == DateTime.MinValue ? now + period
            : state.NextRefresh + TimeSpan.FromTicks(((now - state.NextRefresh).Ticks / period.Ticks + 1) * period.Ticks);
    }
    private sealed class OfferInfo(MarketOffer offer, Item item) : GenericBuyInfo(item.Name, item.GetType(), offer.Price,
        offer.Equipment ? 1 : 999, item.ItemID, item.Hue)
    {
        public override IEntity GetDisplayEntity() => item;
        public override IEntity GetEntity() => throw new InvalidOperationException("Market purchases require the common transaction.");
    }
    private static IBuyItemInfo[] Stock(BaseVendor vendor)
    {
        var state = Market(vendor);
        return state?.Offers.Select(offer => (IBuyItemInfo)new OfferInfo(offer, World.FindItem(offer.Item))).ToArray();
    }
    private sealed class MarketSellInfo(MarketRole role) : IShopSellInfo
    {
        public Type[] Types { get; } = EquipmentBalance.All.Where(g => g.Category != "Unarmed").Select(g => _itemTypes[g.Type])
            .Concat(Supplies.Where(s => s.Roles.Contains(role)).Select(s => EquipmentCrafting.Resolve(s.Type))).Distinct().ToArray();
        public bool IsSellable(Item item) => TradePrices.CanSell(item) && (ClassGear.Converted(item)
            || Supplies.Any(s => s.SellPrice > 0 && s.Roles.Contains(role) && s.Type == item.GetType().Name && s.Kind == null));
        public bool IsResellable(Item item) => false;
        public string GetNameFor(Item item) => item.Name ?? item.LabelNumber.ToString();
        public int GetSellPriceFor(Item item) => ClassGear.Converted(item) ? TradePrices.Quote(item) : Supplies.FirstOrDefault(s =>
            s.Roles.Contains(role) && s.Type == item.GetType().Name && s.Kind == null)?.SellPrice ?? 0;
        public int GetBuyPriceFor(Item item) => 0;
    }
    private static IShopSellInfo[] SellInfo(BaseVendor vendor, IShopSellInfo[] native)
    {
        if (Role(vendor) is not { } role) { return null; }
        if (!_sellInfos.TryGetValue(role, out var result)) { _sellInfos.Add(role, result = [new MarketSellInfo(role)]); }
        return native.Length == 0 ? result : [result[0], .. native.Select(info => (IShopSellInfo)new LegacySupplySales(info))];
    }
    private sealed class LegacySupplySales(IShopSellInfo native) : IShopSellInfo
    {
        public Type[] Types => native.Types;
        public bool IsSellable(Item item) => !ClassGear.Converted(item) && TradePrices.CanSell(item) && native.IsSellable(item);
        public bool IsResellable(Item item) => false;
        public int GetSellPriceFor(Item item) => native.GetSellPriceFor(item);
        public int GetBuyPriceFor(Item item) => 0;
        public string GetNameFor(Item item) => native.GetNameFor(item);
    }
}
