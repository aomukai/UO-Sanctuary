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
using Server.Network;

namespace Sanctuary.Integration;

public static class GamblingUiChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(GamblingUiChecks));
    private static PlayerMobile _p;
    private static TavernGambler _npc;
    private static GamblingGump _old;
    private static Item _bought, _named, _filler;
    private static int _balance, _refundBalance, _namedBalance;
    private static GamblingChecks.Roll _refundRandom;
    private static string Manifest => Path.Join(Core.BaseDirectory, "gambling-ui-checks.json");
    private sealed record Saved(uint Owner, uint Item, int Balance, string State, uint NamedItem, string NamedState);
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    public static void Configure()
    {
        Command("setup", () =>
        {
            _p = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_p);
            _p.Target = null; _p.Frozen = _p.Paralyzed = false; _p.Blessed = true;
            _p.CloseGump<TalentGump>(); _p.CloseGump<WardrobeGump>();
            foreach (var item in _p.Backpack.Items.ToArray()) { item.Delete(); }
            int balance = Banker.GetBalance(_p); if (balance > 0) { Banker.Withdraw(_p, balance); }
            ClassCreation.Profiles.AdvanceLevel(_p, 99);
            _p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            _npc = GamblingChecks.Npc(_p);
        });
        Command("opened", () =>
        {
            _old = _p.FindGump<GamblingGump>();
            Require(_old != null, "Vendor speech did not open gambling stock");
            typeof(GamblingGump).GetField("_random", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_old, new GamblingChecks.Roll());
            Require(_p.Backpack.Items.Where(ClassGear.Converted).All(i => i == _bought), "Opening stock generated or changed gear");
        });
        Command("next-page", () => Require((int)typeof(GamblingGump).GetField("_page", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(_p.FindGump<GamblingGump>()) == 1, "Next page did not open"));
        Command("previous-page", () => Require((int)typeof(GamblingGump).GetField("_page", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(_p.FindGump<GamblingGump>()) == 0, "Previous page did not open"));
        Command("refund-prepare", () =>
        {
            _refundBalance = Banker.GetBalance(_p);
            _refundRandom = new GamblingChecks.Roll(50); Stock("clw", 99, _refundRandom);
        });
        Command("refunded", () => Require(_refundRandom.QualityCalls == 1 && Banker.GetBalance(_p) == _refundBalance && _p.Backpack.TotalItems == 1
            && _bought.Parent == _p.Backpack, "Unavailable UI gamble charged, replaced or changed loot"));
        Command("named-prepare", () => { _namedBalance = Banker.GetBalance(_p); Stock("rin", 99, new GamblingChecks.Roll(0, other: 0)); });
        Command("named-bought", () =>
        {
            _named = _p.Backpack.Items.Single(i => i != _bought);
            Require(ClassGear.State(_named) is { NamedItemId: not null, Identified: true, Roll.Rarity: GearRarity.Unique }
                && _named.Origin == ItemOrigin.Drop && Banker.GetBalance(_p) == _namedBalance - 50000,
                "Actual client Unique purchase failed to deliver/reveal exactly one paid named item");
        });
        Command("no-funds", () => Require(Banker.GetBalance(_p) == 0 && _p.Backpack.TotalItems == 0,
            "No-funds button generated or charged gear"));
        Command("closed", () => Require(_p.FindGump<GamblingGump>() == null && _p.Backpack.TotalItems == 0,
            "Closing gambling UI purchased gear"));
        Command("fund", () => { Require(Banker.Deposit(_p, 200000), "Could not fund gamble"); _balance = Banker.GetBalance(_p); });
        Command("full", () => { _p.Backpack.MaxItems = 1; _filler = new Item(1); _p.Backpack.DropItem(_filler); });
        Command("full-kept", () => Require(Banker.GetBalance(_p) == _balance && _p.Backpack.TotalItems == 1 && _filler.Parent == _p.Backpack,
            "Full-pack gambling charged or delivered"));
        Command("room", () =>
        {
            _p.Backpack.MaxItems = 125; _filler.Delete();
            typeof(GamblingGump).GetField("_random", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_p.FindGump<GamblingGump>(), new GamblingChecks.Roll());
        });
        Command("bought", () =>
        {
            _bought = _p.Backpack.Items.Single();
            Require(_bought.Origin == ItemOrigin.Drop && ClassGear.State(_bought) is { Identified: true }
                && Banker.GetBalance(_p) == _balance - 50000, "Actual purchase did not reveal the paid loot item");
            // The obsolete callback must stay harmless even while a newer menu is open.
            _old.OnResponse(_p.NetState, new RelayInfo(100, [], [], [], []));
            Require(Banker.GetBalance(_p) == _balance - 50000 && _p.Backpack.TotalItems == 1, "Replayed old menu purchased twice");
        });
        Command("stale", () =>
        {
            var stale = _p.FindGump<GamblingGump>();
            _p.MoveToWorld(new Point3D(_p.X + 20, _p.Y, _p.Z), _p.Map);
            stale.OnResponse(_p.NetState, new RelayInfo(102, [], [], [], []));
            Require(Banker.GetBalance(_p) == _balance - 50000 && _p.Backpack.TotalItems == 1, "Distant menu purchased gear");
            _p.MoveToWorld(_npc.Location, _npc.Map);
        });
        Command("final", () =>
        {
            Require(_bought?.Parent == _p.Backpack && ClassGear.State(_bought).Identified, "Purchased loot lost after refresh/close");
            _p.CloseGump<GamblingGump>();
            File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(_p.Serial.Value, _bought.Serial.Value,
                Banker.GetBalance(_p), JsonSerializer.Serialize(ClassGear.State(_bought)), _named.Serial.Value,
                JsonSerializer.Serialize(ClassGear.State(_named)))));
        });
        Command("reload", () =>
        {
            var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
            var p = World.FindMobile((Serial)saved.Owner) as PlayerMobile; var item = World.FindItem((Serial)saved.Item);
            var named = World.FindItem((Serial)saved.NamedItem);
            Require(p != null && item?.Parent == p.Backpack && Banker.GetBalance(p) == saved.Balance
                && item.Origin == ItemOrigin.Drop && JsonSerializer.Serialize(ClassGear.State(item)) == saved.State,
                "Actual client gamble did not survive restart");
            Require(named?.Parent == p.Backpack && JsonSerializer.Serialize(ClassGear.State(named)) == saved.NamedState,
                "Actual client named gamble did not survive restart");
        });
    }
    private static void Stock(string code, int level, Random random)
    {
        var current = _p.FindGump<GamblingGump>();
        var stock = (GambleOffer[])((GambleOffer[])typeof(GamblingGump).GetField("_stock", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(current)).Clone();
        stock[1] = GamblingChecks.Offer(code, level);
        var gump = (GamblingGump)typeof(GamblingGump).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single()
            .Invoke([_p, _npc, stock, 99, 0]);
        typeof(GamblingGump).GetField("_random", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gump, random);
        _p.SendGump(gump);
    }
    private static void Command(string suffix, Action action)
    {
        string name = "gambling-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Disposable actual-client gambling checks", _ => Core.LoopContext.Post(() =>
        { try { action(); _log.Information("{Command}: passed", name); } catch (Exception e) { _log.Error(e, "{Command}: failed", name); } }));
    }
}
