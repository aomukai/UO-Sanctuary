using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CorpseEscrowChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(CorpseEscrowChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "escrow-checks.json");
    private sealed record Saved(uint Owner, uint Banker, uint Corpse, uint Storage, uint Bag, uint Gear, uint Waiting,
        DateTime WaitingDeath, string GearState, int Balance, uint Overdue, uint OverdueItem);
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    public static void Configure()
    { Register("escrow-check", Check); Register("escrow-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Disposable corpse escrow deadline/payment/forfeiture/persistence checks", _ => Core.LoopContext.Post(() =>
        { try { action(); _log.Information("{Command}: passed", name); } catch (Exception e) { _log.Error(e, "{Command}: failed", name); } }));
    internal static Banker BankerFor(PlayerMobile p)
    {
        var b = new Banker(); b.MoveToWorld(p.Location, p.Map); return b;
    }
    internal static Corpse Death(PlayerMobile p)
    {
        p.Kill(); var corpse = p.Corpse as Corpse;
        Require(corpse != null && CorpseEscrow.Profiles.Find(corpse.Serial) != null, "Real death did not register a corpse deadline");
        p.Resurrect(); return corpse;
    }
    internal static CorpseClaim Expired(Corpse corpse)
    {
        var claim = CorpseEscrow.Profiles.Find(corpse.Serial);
        CorpseEscrow.Expire(corpse.Serial, claim.Death + CorpseEscrow.Lifetime);
        Require(corpse.Deleted, "Expiry did not remove the ground corpse");
        return CorpseEscrow.Profiles.Find(corpse.Serial);
    }
    private static void Check()
    {
        var p = InsuranceChecks.Player(); var banker = BankerFor(p);
        var plain = new Item(1); var units = new Item(1) { Stackable = true, Amount = 15 };
        var scroll = new TownPortalScroll(); var hammer = new SewingKit();
        Require(EscrowValuation.Unit(new Gold(1000)) == 1 && EscrowValuation.Unit(plain) == 100
            && EscrowValuation.Unit(units) == 10 && EscrowValuation.Unit(scroll) == 100 && EscrowValuation.Unit(hammer) == 1,
            $"Valuations differ: gold={EscrowValuation.Unit(new Gold(1000))}, plain={EscrowValuation.Unit(plain)}, units={EscrowValuation.Unit(units)}, scroll={EscrowValuation.Unit(scroll)}, hammer={EscrowValuation.Unit(hammer)}");
        var corpse = Death(p); corpse.AddItem(new Gold(1000)); corpse.AddItem(plain); corpse.AddItem(units);
        var initial = CorpseEscrow.Profiles.Find(corpse.Serial);
        CorpseEscrow.Expire(corpse.Serial, initial.Death + CorpseEscrow.Lifetime - TimeSpan.FromTicks(1));
        Require(!corpse.Deleted && plain.Parent == corpse, "Corpse expired before the exact24hour boundary");
        corpse.TurnToBones(); corpse.BeginDecay(TimeSpan.Zero);
        Require(ReferenceEquals(CorpseEscrow.Profiles.Find(corpse.Serial), initial), "Native bone/decay reset the saved deadline");
        var claim = Expired(corpse); var quote = CorpseEscrow.Quote(p, claim);
        Require(quote is { Fee: 125 } && quote.Items.Length == 3 && CorpseEscrow.Quote(InsuranceChecks.Player(), claim) == null,
            "Aggregate content fee, ownership or real escrow transfer differs");
        Require(!CorpseEscrow.Recover(p, banker, quote) && !plain.Deleted && CorpseEscrow.Current(p, quote),
            "Insufficient funds deleted belongings or changed the claim");
        Require(Banker.Deposit(p, 1000), "Recovery fixture funding failed");
        var bank = p.BankBox; bank.MaxItems = bank.TotalItems;
        Require(!CorpseEscrow.Recover(p, banker, quote) && Banker.GetBalance(p) == 1000 && !plain.Deleted,
            "Full bank partially charged, transferred or forfeited a claim");
        bank.MaxItems = 125;
        var foreign = InsuranceChecks.Player(); var foreignBanker = BankerFor(foreign);
        Require(!CorpseEscrow.Recover(foreign, foreignBanker, quote) && !CorpseEscrow.Forfeit(foreign, foreignBanker, quote),
            "Another owner could recover or forfeit this claim");
        int balance = Banker.GetBalance(p); banker.Internalize();
        Require(!CorpseEscrow.Recover(p, banker, quote) && Banker.GetBalance(p) == balance, "Remote banker accepted payment");
        banker.MoveToWorld(p.Location, p.Map);
        units.Amount = 16;
        Require(!CorpseEscrow.Recover(p, banker, quote) && !CorpseEscrow.Forfeit(p, banker, quote), "Changed contents reused a stale quote");
        units.Amount = 15; quote = CorpseEscrow.Quote(p, claim);
        Require(CorpseEscrow.Recover(p, banker, quote) && Banker.GetBalance(p) == 1875 && plain.Parent == p.BankBox
            && units.Parent == p.BankBox && CorpseEscrow.Profiles.Find(claim.Corpse) == null,
            "Recovery lost actual items or charged the wrong amount");
        Require(!CorpseEscrow.Recover(p, banker, quote) && !CorpseEscrow.Forfeit(p, banker, quote) && Banker.GetBalance(p) == 1875,
            "Replayed recovery/forfeiture charged or deleted recovered property");

        var tiny = Death(p); var token = new Item(1); tiny.AddItem(token); var tinyClaim = Expired(tiny);
        Require(CorpseEscrow.Quote(p, tinyClaim).Fee == 100, "Minimum fee is not applied per corpse");
        var split = Death(p); split.AddItem(new Item(1) { Stackable = true, Amount = 750 }); var splitClaim = Expired(split);
        var splitQuote = CorpseEscrow.Quote(p, splitClaim); var splitStore = (CorpseEscrowStorage)World.FindItem(splitClaim.Storage);
        splitStore.Items[0].Amount = 375; splitStore.AddItem(new Item(1) { Stackable = true, Amount = 375 });
        Require(splitQuote.Fee == 750 && CorpseEscrow.Quote(p, splitClaim).Fee == 750 && !CorpseEscrow.Current(p, splitQuote),
            "Splitting stacks changed the aggregate fee or left an old quote usable");
        var toForfeit = CorpseEscrow.Quote(p, tinyClaim); balance = Banker.GetBalance(p);
        Require(CorpseEscrow.Forfeit(p, banker, toForfeit) && token.Deleted && Banker.GetBalance(p) == balance
            && CorpseEscrow.Profiles.Find(splitClaim.Corpse) != null, "Explicit forfeiture affected another claim or bank");
        var empty = Death(p); Expired(empty);
        Require(CorpseEscrow.Profiles.Find(empty.Serial) == null, "Empty corpse created a paid claim");
        var access = Death(p); var accessed = new Item(1); access.AddItem(accessed);
        var accessClaim = CorpseEscrow.Profiles.Find(access.Serial);
        CorpseEscrow.Profiles.Store(accessClaim with { Death = Core.Now - CorpseEscrow.Lifetime });
        Require(!access.CheckItemUse(p, accessed) && access.Deleted && accessed.Parent is CorpseEscrowStorage,
            "An existing corpse window could use expired contents before timer settlement");

        var physical = InsuranceChecks.Player(); var physicalItem = InsuranceChecks.Gear(physical);
        var physicalCorpse = Death(physical);
        Require(ClassDeath.Recover(physical, physicalCorpse) && physicalItem.IsChildOf(physical.Backpack)
            && CorpseEscrow.Profiles.Find(physicalCorpse.Serial) is { Storage.Value: 0 },
            "Free physical recovery before expiry no longer returns the actual item");

        var insuredPlayer = InsuranceChecks.Player(); var insured = InsuranceChecks.Gear(insuredPlayer); var uninsured = InsuranceChecks.Gear(insuredPlayer);
        Require(Banker.Deposit(insuredPlayer, 1000) && EquipmentInsurance.Apply(insuredPlayer,
            [EquipmentInsurance.Quote(insured)], [true]), "Could not insure death fixture");
        var insuredCorpse = Death(insuredPlayer); var insuredClaim = Expired(insuredCorpse);
        Require(insured.Parent == insuredPlayer.Backpack && uninsured.Parent?.Serial == insuredClaim.Storage,
            "Escrow captured insured equipment or lost uninsured equipment");

        var saved = InsuranceChecks.Player(); var savedBanker = BankerFor(saved); var gear = InsuranceChecks.Gear(saved, GearRarity.Rare, GearTier.Elite);
        var bag = new Bag(); saved.Backpack.DropItem(bag); bag.AddItem(gear); bag.AddItem(new Gold(1001));
        Require(Banker.Deposit(saved, 5000), "Persistent fixture funding failed");
        var savedCorpse = Death(saved); var savedClaim = Expired(savedCorpse);
        Require(CorpseEscrow.Quote(saved, savedClaim) != null, "Could not quote persistent fixture");
        string state = JsonSerializer.Serialize(ClassGear.State(gear));
        Require(bag.Parent?.Serial == savedClaim.Storage && gear.Parent == bag && bag.GetAmount(typeof(Gold)) == 801,
            "Nested escrow flattened containers or charged death gold twice");
        var waiting = Death(saved); waiting.AddItem(new Item(1)); var waitingClaim = CorpseEscrow.Profiles.Find(waiting.Serial);
        var overdue = Death(saved); var overdueItem = new Item(1); overdue.AddItem(overdueItem);
        var overdueClaim = CorpseEscrow.Profiles.Find(overdue.Serial);
        CorpseEscrow.Profiles.Store(overdueClaim with { Death = Core.Now - CorpseEscrow.Lifetime - TimeSpan.FromMinutes(1) });
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(saved.Serial.Value, savedBanker.Serial.Value,
            savedClaim.Corpse.Value, savedClaim.Storage.Value, bag.Serial.Value, gear.Serial.Value, waiting.Serial.Value,
            waitingClaim.Death, state, Banker.GetBalance(saved), overdue.Serial.Value, overdueItem.Serial.Value)));
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var p = World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        var claim = CorpseEscrow.Profiles.Find((Serial)saved.Corpse);
        var bag = World.FindItem((Serial)saved.Bag) as Bag; var gear = World.FindItem((Serial)saved.Gear);
        var waiting = CorpseEscrow.Profiles.Find((Serial)saved.Waiting);
        Require(p is { Alive: true } && Banker.GetBalance(p) == saved.Balance && claim?.Storage == (Serial)saved.Storage
            && World.FindItem(claim.Storage) is CorpseEscrowStorage && bag.Parent?.Serial == claim.Storage && gear.Parent == bag
            && bag.GetAmount(typeof(Gold)) == 801 && JsonSerializer.Serialize(ClassGear.State(gear)) == saved.GearState
            && waiting is { Storage.Value: 0 } && waiting.Death == saved.WaitingDeath && World.FindItem(waiting.Corpse) is Corpse { Deleted: false },
            $"Reload differs: alive={p?.Alive}, bank={Banker.GetBalance(p)}/{saved.Balance}, storage={claim?.Storage}/{saved.Storage}, bag={bag?.Parent?.Serial}, gear={gear?.Parent?.Serial}, gold={bag?.GetAmount(typeof(Gold))}, state={JsonSerializer.Serialize(ClassGear.State(gear)) == saved.GearState}, waiting={waiting?.Storage}, death={waiting?.Death.Ticks}/{saved.WaitingDeath.Ticks}, body={World.FindItem((Serial)saved.Waiting)?.Deleted}");
        var quote = CorpseEscrow.Quote(p, claim);
        Require(quote != null && CorpseEscrow.Current(p, quote), "Persisted claim is not recoverable");
        Require(World.FindItem((Serial)saved.Overdue) == null
            && CorpseEscrow.Profiles.Find((Serial)saved.Overdue) is { Storage.IsItem: true } expired
            && World.FindItem((Serial)saved.OverdueItem)?.Parent?.Serial == expired.Storage,
            "A corpse whose deadline passed during downtime did not automatically enter escrow with its actual item");
    }
}
