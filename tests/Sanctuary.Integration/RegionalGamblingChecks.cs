using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class RegionalGamblingChecks
{
    private const string Patron = "Regional gambling fixture";
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(RegionalGamblingChecks));
    private static readonly Point3D _point = new(6560, 3610, 0);
    private static string GearManifest => Path.Join(Core.BaseDirectory, "regional-gambling-gear.txt");

    public static void Configure()
    {
        Register("regional-gambling-check", Check);
        Register("regional-gambling-reload-check", Reload);
    }

    private static void Register(string command, Action check) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable regional gambling checks", _ => Core.LoopContext.Post(() =>
        {
            try { check(); _log.Information("{Command}: passed", command); }
            catch (Exception error) { _log.Error(error, "{Command}: failed", command); }
        }));

    private static void Check()
    {
        var player = new PlayerMobile { Player = true, Body = 0x190, Name = Patron };
        player.AddItem(new Backpack());
        player.MoveToWorld(_point, Map.Trammel);
        ClassCreation.Profiles.Assign(player, DiabloClass.Paladin);
        ClassCreation.Profiles.AdvanceLevel(player, 99);
        var jaana = new MayorJaana();
        var queen = new DestardQueen();
        jaana.MoveToWorld(player.Location, player.Map);
        queen.MoveToWorld(player.Location, player.Map);

        try
        {
            foreach (var shop in new[] { RegionalGambling.Cove, RegionalGambling.Destard })
            foreach (var category in Enum.GetValues<BountyGearCategory>())
            foreach (int level in new[] { 1, 17, 30, 44, 60, 99 })
            {
                var stock = Gambling.RegionalStock(shop, category, level, new Random(23));
                var (minimum, maximum) = shop.ItemLevels(level);
                Require(stock.Length is > 0 and <= Gambling.RegionalStockSize
                    && stock.Select(o => o.Code).Distinct().Count() == stock.Length
                    && stock.All(o => o.Variant.Graphic > 0 && o.Price is 10 or 15 or 20
                        && o.ItemLevel >= minimum && o.ItemLevel <= maximum), "Stock escaped its regional range");
            }

            var cove = RegionalGambling.Cove;
            var destard = RegionalGambling.Destard;
            const BountyGearCategory categoryToBuy = BountyGearCategory.Accessories;
            var earOffer = Gambling.RegionalStock(cove, categoryToBuy, 99, new Random(7)).First();
            var hornOffer = Gambling.RegionalStock(destard, categoryToBuy, 99, new Random(15)).First();
            Require(cove.GoldPrice(10) == 500 && cove.GoldPrice(15) == 750 && cove.GoldPrice(20) == 1000
                && destard.GoldPrice(10) == 1000 && destard.GoldPrice(15) == 1500 && destard.GoldPrice(20) == 2000,
                "Gold equivalent differed");
            Require(Buy(jaana, cove, earOffer, BountyPayment.Gold) == GambleResult.Invalid
                && Buy(queen, destard, hornOffer, BountyPayment.Gold) == GambleResult.Invalid, "Shop opened before turn-ins");

            WithDialogueConnection(player, () =>
            {
                PayJaana(49);
                Require(!cove.Unlocked(player) && cove.TurnIns(player) == 49 && Gold() == 2450, "Cove unlocked early");
                PayJaana(1);
                Require(cove.Unlocked(player) && Gold() == 2500 && Page(player).Line.Contains("great service"),
                    "Cove did not unlock at 50 ears");
                DestardBounty.Record(player, 49);
                Require(!destard.Unlocked(player), "Destard unlocked early");
                player.Backpack.DropItem(new DaemonHorn());
                queen.BeginConversation(player);
                Choose("Exchange for Gold", queen);
                Require(destard.Unlocked(player) && Gold() == 2600 && Page(player).Line.Contains("great service"),
                    "Destard did not unlock at 50 lifetime horns");
            });

            Require(Buy(jaana, cove, earOffer with { Price = 1 }, BountyPayment.Gold) == GambleResult.Invalid
                && Buy(queen, destard, hornOffer with { ItemLevel = 61 }, BountyPayment.Gold) == GambleResult.Invalid,
                "Forged price or item level accepted");
            Require(Buy(jaana, cove, earOffer, BountyPayment.Trophies) == GambleResult.NoFunds, "Missing ears accepted");
            player.Backpack.DropItem(new OrcEar { Amount = earOffer.Price + 1 });
            Require(Buy(jaana, cove, earOffer, BountyPayment.Trophies) == GambleResult.Purchased
                && player.Backpack.GetAmount(typeof(OrcEar)) == 1 && Gold() == 2600, "Ears debited incorrectly");
            Require(Buy(queen, destard, hornOffer, BountyPayment.Gold) == GambleResult.Purchased
                && Gold() == 2600 - destard.GoldPrice(hornOffer.Price), "Gold debited incorrectly");
            player.Backpack.DropItem(new DaemonHorn { Amount = hornOffer.Price + 1 });
            Require(Buy(queen, destard, hornOffer, BountyPayment.Trophies) == GambleResult.Purchased
                && player.Backpack.GetAmount(typeof(DaemonHorn)) == 1, "Horns debited incorrectly");
            Require(cove.TurnIns(player) == 50 && destard.TurnIns(player) == 50, "A gear payment changed turn-in credit");
            var bought = player.Backpack.Items.Where(ClassGear.Converted).ToArray();
            VerifyGear(player, bought);
            File.WriteAllLines(GearManifest, bought.Select(item => item.Serial.Value.ToString()));

            void PayJaana(int count)
            {
                player.Backpack.DropItem(new OrcEar { Amount = count });
                jaana.BeginConversation(player);
                Choose("Orc Bounty", jaana);
                Choose("Exchange for Gold", jaana);
            }

            void Choose(string choice, ConversationNpc npc) =>
                Page(player).Replies.Single(r => r.Text == choice).Select(player, npc);

            int Gold() => player.Backpack.GetAmount(typeof(Gold));
            GambleResult Buy(ConversationNpc npc, RegionalGambleShop shop, GambleOffer offer, BountyPayment pay) =>
                Gambling.BuyRegional(player, npc, shop, categoryToBuy, offer, 99, pay, new GamblingChecks.Roll());
        }
        finally
        {
            jaana.Delete();
            queen.Delete();
        }
    }

    private static void Reload()
    {
        var player = World.Mobiles.Values.OfType<PlayerMobile>().Single(p => p.Name == Patron);
        player.MoveToWorld(_point, Map.Trammel);
        Require(RegionalGambling.Cove.TurnIns(player) == 50 && RegionalGambling.Destard.TurnIns(player) == 50
            && RegionalGambling.Cove.Unlocked(player) && RegionalGambling.Destard.Unlocked(player), "Bounty unlock lost on reload");
        VerifyGear(player, File.ReadAllLines(GearManifest)
            .Select(line => World.FindItem((Serial)uint.Parse(line))).ToArray());
    }

    private static void VerifyGear(PlayerMobile player, Item[] gear)
    {
        Require(gear.Length == 3 && gear.All(item => item?.Parent == player.Backpack
            && ClassGear.State(item) is { Gambled: true, Identified: true }),
            "Purchased gear flags missing");
        Require(gear.Count(item => ClassGear.State(item).Roll.ItemLevel is >= 25 and <= 30) == 1
            && gear.Count(item => ClassGear.State(item).Roll.ItemLevel is >= 55 and <= 60) == 2,
            "Purchased gear escaped its region");
        var tool = new TinkerTools();
        player.Backpack.DropItem(tool);
        try
        {
            Require(gear.All(item => TradePrices.CanSell(item)
                && EquipmentEconomy.Quote(player, item, EssenceAction.Salvage, tool) == null),
                "Gambled gear was salvageable or could not be sold");
        }
        finally { tool.Delete(); }
    }

    private static ScriptedDialogue.Node Page(PlayerMobile player)
    {
        var sessions = (IDictionary)Field(typeof(ScriptedDialogue), "_sessions").GetValue(null);
        var session = sessions[player.Serial];
        var script = (System.Collections.Generic.IReadOnlyList<ScriptedDialogue.Node>)
            session.GetType().GetProperty("Script").GetValue(session);
        return script[(byte)session.GetType().GetProperty("Node").GetValue(session)];
    }

    private static void WithDialogueConnection(PlayerMobile player, Action action)
    {
        var state = (NetState)RuntimeHelpers.GetUninitializedObject(typeof(NetState));
        state.Mobile = player;
        var socket = Field(typeof(NetState), "_socket");
        socket.SetValue(state, RuntimeHelpers.GetUninitializedObject(socket.FieldType));
        Field(typeof(NetState), "_running").SetValue(state, true);
        var connection = Field(typeof(Mobile), "m_NetState");
        connection.SetValue(player, state);
        try { action(); }
        finally { connection.SetValue(player, null); }
    }

    private static FieldInfo Field(Type type, string name) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);

    private static void Require(bool value, string message) => RuneChecks.Require(value, message);
}
