using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class GamblingChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(GamblingChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "gambling-checks.json");
    private sealed record Saved(uint Owner, uint Item, string State, int Balance, uint[] Gamblers);
    internal sealed class Roll(int quality = 10150, int firstTier = 9999, int secondTier = 9999, int? other = null) : Random(77)
    {
        private int _tier;
        public int QualityCalls { get; private set; }
        public override int Next(int max)
        {
            if (max == 100000) { QualityCalls++; return quality; }
            if (max == 10000) { return _tier++ == 0 ? firstTier : secondTier; }
            return other ?? base.Next(max);
        }
    }
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    public static void Configure()
    { Register("gambling-check", Check); Register("gambling-reload-check", Reload); }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable gambling purchase/refund/eligibility/persistence checks", _ => Core.LoopContext.Post(() =>
        { try { action(); _log.Information("{Command}: passed", command); } catch (Exception e) { _log.Error(e, "{Command}: failed", command); } }));
    internal static GambleResult Buy(PlayerMobile p, TavernGambler npc, GambleOffer offer, Random random, int level = 99)
        => (GambleResult)typeof(Gambling).GetMethod("Buy", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { p, npc, offer, level, random });
    internal static Item Generate(GambleOffer offer, Random random) => (Item)typeof(Gambling)
        .GetMethod("Generate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { offer, random });
    internal static GambleOffer Offer(string code = "rin", int level = 99)
    {
        var basis = Gambling.Bases.Single(b => b.Code == code);
        return new(code, basis.Variants[0], Math.Clamp(level, 5, 99), Gambling.Price(code, level));
    }
    internal static TavernGambler Npc(PlayerMobile p)
    { var npc = new TavernGambler(); npc.MoveToWorld(p.Location, p.Map); return npc; }
    private static void Check()
    {
        Require(Gambling.Sites.Length == 36 && Gambling.Sites.All(s => World.FindMobile(Gambling.Profiles.At(s.Id)) is TavernGambler { Deleted: false }),
            "A source tavern is missing its gambler");
        Require(Gambling.Price("rin", 1) == 50000 && Gambling.Price("rin", 99) == 50000
            && Gambling.Price("amu", 1) == 63000 && Gambling.Price("amu", 99) == 63000, "Fixed jewellery prices differ");
        foreach (int level in new[] { 1, 5, 20, 48, 70, 99 })
        {
            for (int run = 0; run < 40; run++)
            {
                var stock = Gambling.Stock(level, new Random(run));
                Require(stock.Length == 14 && stock[0].Code == "rin" && stock[1].Code == "amu"
                    && stock.All(o => o.Price > 0 && o.ItemLevel >= Math.Clamp(level - 5, 5, 99)
                        && o.ItemLevel <= Math.Clamp(level + 4, 5, 99) && EquipmentBalance.Base(o.Code).QualityLevel <= o.ItemLevel),
                    "Stock/range/unlock bounds differ");
            }
        }
        Require(Gambling.Price("hax", 5) == 771 && Gambling.Price("hax", 20) == 3856
            && Gambling.Price("hax", 99) == 57986 && Gambling.Price("cap", 99) == 105906,
            "Source-derived integer price examples differ");
        Require(Gambling.Quality(new Roll(0)) == GambleQuality.Unique && Gambling.Quality(new Roll(49)) == GambleQuality.Unique
            && Gambling.Quality(new Roll(50)) == GambleQuality.Set && Gambling.Quality(new Roll(149)) == GambleQuality.Set
            && Gambling.Quality(new Roll(150)) == GambleQuality.Rare && Gambling.Quality(new Roll(10149)) == GambleQuality.Rare
            && Gambling.Quality(new Roll(10150)) == GambleQuality.Magic, "Fixed gamble quality boundaries differ");
        Require(Gambling.Upgrade("hax", 99, new Roll(firstTier: 0)) == "9ha"
            && Gambling.Upgrade("hax", 99, new Roll(secondTier: 0)) == "7ha"
            && Gambling.Upgrade("hax", 5, new Roll(firstTier: 0, secondTier: 0)) == "hax", "Base tier upgrades/low-level bounds differ");
        var family = InsuranceChecks.Player();
        foreach (BaseTool tool in new BaseTool[] { new SmithHammer(), new FletcherTools(), new SewingKit(), new TinkerTools(), new ScribesPen() })
        { family.Backpack.DropItem(tool); }
        foreach (var basis in Gambling.Bases)
        {
            foreach (var variant in basis.Variants)
            {
                foreach (int tier in new[] { 0, 1, 2 })
                {
                    var random = new Roll(firstTier: tier == 1 ? 0 : 9999, secondTier: tier == 2 ? 0 : 9999);
                    string code = Gambling.Upgrade(basis.Code, 99, new Roll(firstTier: tier == 1 ? 0 : 9999, secondTier: tier == 2 ? 0 : 9999));
                    Require(EquipmentCrafting.Identity(EquipmentCrafting.Resolve(variant.Type), Enum.Parse<GearTier>(EquipmentBalance.Base(code).Tier),
                        code, basis.Code), "Missing gamble tier identity: " + variant.Type + "/" + code);
                    var item = Generate(new(basis.Code, variant, 99, Gambling.Price(basis.Code, 99)), random);
                    family.Backpack.DropItem(item);
                    Require(item is { Origin: ItemOrigin.Drop } && ClassGear.State(item) is { Identified: true, Roll.Rarity: GearRarity.Magic }
                        && TradePrices.CanSell(item) && EquipmentEconomy.Quote(family, item, EssenceAction.Salvage) != null,
                        "Gambled family/tier is restricted or missing: " + variant.Type + "/" + tier);
                    item.Delete();
                }
            }
        }
        var p = InsuranceChecks.Player(); var npc = Npc(p); var offer = Offer();
        var roll = new Roll();
        Require(Buy(p, npc, offer, roll) == GambleResult.NoFunds && roll.QualityCalls == 0, "Unfunded purchase rolled or succeeded");
        Require(Banker.Deposit(p, 200000), "Cannot fund test bank");
        int balance = Banker.GetBalance(p), count = p.Backpack.TotalItems;
        // There is no source Set/Unique for a normal-tier claw. Real named
        // rings are tested separately and must no longer be treated as empty.
        var absent = Offer("clw");
        var unavailable = new Roll(50);
        Require(Buy(p, npc, absent, unavailable) == GambleResult.Refunded && unavailable.QualityCalls == 1
            && Banker.GetBalance(p) == balance && p.Backpack.TotalItems == count, "Deferred result charged, created an item or rerolled");
        Require(Buy(p, npc, absent, new Roll(0)) == GambleResult.Refunded && Banker.GetBalance(p) == balance,
            "Unique outcome failed to refund");
        p.Backpack.MaxItems = 1; p.Backpack.DropItem(new Item(1));
        Require(Buy(p, npc, offer, new Roll()) == GambleResult.NoCapacity && Banker.GetBalance(p) == balance,
            "Full pack purchase charged or partially delivered");
        p.Backpack.Items[0].Delete(); p.Backpack.MaxItems = 125;
        Require(Buy(p, npc, offer, new Roll(150)) == GambleResult.Purchased && Banker.GetBalance(p) == balance - offer.Price,
            "Bank purchase charged incorrectly");
        var bought = p.Backpack.Items.Single();
        Require(p.EquipItem(bought) && ClassGear.Active(p, bought), "Purchased item could not equip"); p.Backpack.DropItem(bought);
        int sale = TradePrices.Quote(bought);
        Require(sale is >= 100 and <= 500 && TradePrices.Quote(bought) == sale
            && ClassGear.State(bought).VendorPurchasePrice == null, "Purchased item inherited vendor resale restrictions");
        var other = InsuranceChecks.Player(); other.MoveToWorld(new Point3D(p.X + 40, p.Y, p.Z), p.Map);
        Require(Buy(other, npc, offer, new Roll()) == GambleResult.Invalid, "Remote player purchased from gambler");
        Require(Buy(p, npc, offer with { Price = 1 }, new Roll()) == GambleResult.Invalid
            && Buy(p, npc, offer, new Roll(), 1) == GambleResult.Invalid, "Forged price or stale level accepted");
        Require(Buy(p, npc, offer, new Roll()) == GambleResult.Purchased, "Cannot buy resale sample");
        var sold = p.Backpack.Items.Single(i => i != bought);
        var merchant = new Jeweler(); merchant.MoveToWorld(p.Location, p.Map);
        int payout = TradePrices.Quote(sold);
        Require(merchant.OnSellItems(p, [new SellItemResponse(sold, 1)]) && sold.Deleted
            && p.Backpack.Items.OfType<Gold>().Sum(g => g.Amount) == payout,
            "Actual native sale of gambled loot failed or used vendor refund pricing");
        Require(Buy(p, npc, offer, new Roll()) == GambleResult.Purchased, "Cannot buy salvage sample");
        var salvaged = p.Backpack.Items.Single(i => i != bought && i is not Gold);
        var salvageTool = new TinkerTools(); p.Backpack.DropItem(salvageTool);
        var quote = EquipmentEconomy.Quote(p, salvaged, EssenceAction.Salvage, salvageTool);
        object[] commit = { p, quote, null, new Random(43) };
        Require(quote != null && (bool)typeof(EquipmentEconomy).GetMethod("Commit", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, commit) && salvaged.Deleted && commit[2] is SalvageResult,
            "Actual salvage of gambled loot failed");
        var purse = InsuranceChecks.Player(); var purseNpc = Npc(purse);
        purse.Backpack.DropItem(new Gold(50000)); Require(Banker.Deposit(purse, 10000), "Cannot fund purse test");
        Require(Buy(purse, purseNpc, offer, new Roll()) == GambleResult.Purchased && Banker.GetBalance(purse) == 10000
            && purse.Backpack.Items.Count == 1 && purse.Backpack.Items[0] is not Gold, "Backpack-first payment touched bank or lost delivery");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(p.Serial.Value, bought.Serial.Value,
            JsonSerializer.Serialize(ClassGear.State(bought)), Banker.GetBalance(p), Gambling.Sites.Select(s => Gambling.Profiles.At(s.Id).Value).ToArray())));
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var p = World.FindMobile((Serial)saved.Owner) as PlayerMobile; var item = World.FindItem((Serial)saved.Item);
        Require(p != null && item?.Parent == p.Backpack && item.Origin == ItemOrigin.Drop
            && JsonSerializer.Serialize(ClassGear.State(item)) == saved.State && Banker.GetBalance(p) == saved.Balance
            && Gambling.Sites.Select(s => Gambling.Profiles.At(s.Id).Value).SequenceEqual(saved.Gamblers),
            "Gambled gear/payment/tavern identities did not survive restart");
    }
}
