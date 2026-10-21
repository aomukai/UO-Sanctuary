using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CorpseEscrowUiChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(CorpseEscrowUiChecks));
    private static PlayerMobile _player;
    private static Banker _banker;
    private static CorpseClaim _recover, _forfeit;
    private static Item _gear, _discarded;
    private static Bag _bag;
    private static int _fee, _balance;
    private static string Manifest => Path.Join(Core.BaseDirectory, "escrow-ui-checks.json");
    private sealed record Saved(uint Owner, uint Bag, uint Gear, uint Discarded, uint Kept, int Balance);
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    public static void Configure()
    {
        Command("setup", () =>
        {
            _player = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_player);
            _player.Target = null; _player.Frozen = _player.Paralyzed = _player.Blessed = false;
            _player.CloseGump<TalentGump>(); _player.CloseGump<WardrobeGump>();
            foreach (var item in EquipmentInsurance.Carried(_player).ToArray()) { item.Delete(); }
            int bank = Banker.GetBalance(_player); if (bank > 0) { Require(Banker.Withdraw(_player, bank), "Could not clear test bank"); }
            _banker = CorpseEscrowChecks.BankerFor(_player);
            _gear = InsuranceChecks.Gear(_player, GearRarity.Rare, GearTier.Elite);
            _bag = new Bag(); _player.Backpack.DropItem(_bag); _bag.AddItem(_gear); _bag.AddItem(new Gold(1001));
            _recover = CorpseEscrowChecks.Expired(CorpseEscrowChecks.Death(_player));
            var second = CorpseEscrowChecks.Death(_player); _discarded = new Item(1) { Name = "Forfeiture test object" }; second.AddItem(_discarded);
            _forfeit = CorpseEscrowChecks.Expired(second); _player.Blessed = true;
            _fee = CorpseEscrow.Quote(_player, _recover).Fee;
            _player.CloseGump<CorpseEscrowGump>();
        });
        Command("opened", () => Require(_player.FindGump<CorpseEscrowGump>() != null, "Actual banker speech did not open escrow"));
        Command("no-funds", () => Require(CorpseEscrow.Claims(_player).Length == 2 && !_gear.Deleted && !_discarded.Deleted
            && Banker.GetBalance(_player) == 0 && _bag.Parent?.Serial == _recover.Storage, "No-funds UI deleted or moved contents"));
        Command("fund", () =>
        {
            Require(Banker.Deposit(_player, 5000), "Could not fund recovery"); _balance = Banker.GetBalance(_player);
            CorpseRecoveryHooks.Open(_player, _banker);
        });
        Command("closed", () => Require(_player.FindGump<CorpseEscrowGump>() == null && CorpseEscrow.Claims(_player).Length == 2
            && Banker.GetBalance(_player) == _balance && !_gear.Deleted, "Closing UI spent gold or forfeited a claim"));
        Command("full", () => _player.BankBox.MaxItems = _player.BankBox.TotalItems);
        Command("full-kept", () => Require(_bag.Parent?.Serial == _recover.Storage && Banker.GetBalance(_player) == _balance,
            "Full-bank UI partially transferred/charged"));
        Command("room", () => _player.BankBox.MaxItems = 125);
        Command("recovered", () => Require(_bag.Parent == _player.BankBox && _gear.Parent == _bag && !_gear.Deleted
            && Banker.GetBalance(_player) == _balance - _fee + 801 && CorpseEscrow.Profiles.Find(_recover.Corpse) == null,
            "Actual recovery UI lost nesting/identity or charged a different fee"));
        Command("confirm-shown", () =>
        {
            var g = _player.FindGump<CorpseEscrowGump>();
            Require(g != null && (bool)typeof(CorpseEscrowGump).GetField("_forfeit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(g)
                && !_discarded.Deleted && CorpseEscrow.Profiles.Find(_forfeit.Corpse) != null, "Forfeiture selection deleted before confirmation");
        });
        Command("kept", () => Require(!_discarded.Deleted && CorpseEscrow.Profiles.Find(_forfeit.Corpse) != null,
            "Backing out of forfeiture deleted belongings"));
        Command("final", () =>
        {
            Require(_discarded.Deleted && CorpseEscrow.Profiles.Find(_forfeit.Corpse) == null && _gear.Parent == _bag
                && _bag.Parent == _player.BankBox && Banker.GetBalance(_player) == _balance - _fee + 801, "Confirmed forfeiture affected bank/recovered claim");
            _player.Blessed = false;
            var third = CorpseEscrowChecks.Death(_player); third.AddItem(new Item(1) { Name = "Deferred escrow object" });
            var kept = CorpseEscrowChecks.Expired(third); _player.Blessed = true;
            File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(_player.Serial.Value, _bag.Serial.Value, _gear.Serial.Value,
                _discarded.Serial.Value, kept.Corpse.Value, Banker.GetBalance(_player))));
        });
        Command("reload", () =>
        {
            var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
            var p = World.FindMobile((Serial)saved.Owner) as PlayerMobile;
            Require(p is { Alive: true } && Banker.GetBalance(p) == saved.Balance && World.FindItem((Serial)saved.Bag)?.Parent == p.BankBox
                && World.FindItem((Serial)saved.Gear)?.Parent?.Serial == (Serial)saved.Bag && World.FindItem((Serial)saved.Discarded) == null
                && CorpseEscrow.Profiles.Find((Serial)saved.Kept) is { Storage.IsItem: true },
                "Client recovery/forfeiture/deferred claim did not survive restart");
        });
    }
    private static void Command(string suffix, Action action)
    {
        string name = "escrow-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Disposable actual-client corpse escrow checks", _ => Core.LoopContext.Post(() =>
        { try { action(); _log.Information("{Command}: passed", name); } catch (Exception e) { _log.Error(e, "{Command}: failed", name); } }));
    }
}
