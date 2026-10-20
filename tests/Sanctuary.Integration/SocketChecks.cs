using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class SocketChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(SocketChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "socket-checks.json");
    private sealed record Saved(uint Item, string State, NativeGearDetails Native, string Name, ItemOrigin Origin);
    private sealed record SaveData(Saved[] Items, uint Tool, int Uses, CraftToolState ToolState);
    internal sealed class Rolls(params int[] values) : Random
    {
        private int _index;
        public int Calls => _index;
        public override int Next(int maximum)
        {
            int value = _index < values.Length ? values[_index] : 99; _index++;
            ImbuingChecks.Require(value >= 0 && value < maximum, "Socket fixture random bound changed"); return value;
        }
    }
    public static void Configure()
    { Register("socket-check", Check); Register("socket-reload-check", Reload); }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable socket gates, destructive attempts, intuition, provenance and persistence", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", command); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    private static void Require(bool value, string message) => ImbuingChecks.Require(value, message);
    private static string State(Item item) => JsonSerializer.Serialize(ClassGear.State(item));
    private static Saved Snapshot(Item item) => new(item.Serial.Value, State(item), GearDetails.Read(item), item.Name, item.Origin);
    internal static T Put<T>(PlayerMobile player, T item) where T : Item { player.Backpack.DropItem(item); return item; }
    internal static Longsword Gear(PlayerMobile player, GearTier tier = GearTier.Normal, int sockets = 0, GearRarity rarity = GearRarity.Normal)
    {
        var item = Put(player, new Longsword());
        ClassGear.Profiles.FromMonster(item, 99, rarity, EquipmentCraftingChecks.Row("Longsword", tier).BaseCode, tier, new Random(42));
        var state = ClassGear.State(item); ClassGear.Profiles.Store(item, state with { TinkerSockets = sockets, Identified = true });
        return item;
    }
    internal static void Ready(PlayerMobile player, double skill = 120)
    { player.Skills.Tinkering.Base = skill; player.NextSkillTime = Core.TickCount; }
    internal static SocketRequest Quote(PlayerMobile player, TinkerTools tool, Item item)
    { player.NextSkillTime = Core.TickCount; return TinkeringSockets.Quote(player, tool, item); }
    private static SocketResult Act(PlayerMobile player, SocketRequest quote, params int[] rolls)
    {
        Require(quote != null, "Valid socket quote rejected");
        Require(TinkeringSockets.Commit(player, quote, out var result, new Rolls(rolls)), "Valid socket attempt rejected");
        return result;
    }
    private static void Check()
    {
        var player = ImbuingChecks.Player(SkillName.Tinkering); player.Skills.Tinkering.SetLockNoRelay(SkillLock.Locked);
        var tool = Put(player, new TinkerTools(1000)); var saved = new List<Saved>();
        foreach (var tier in Enum.GetValues<GearTier>())
        {
            for (int socket = 1; socket <= 5; socket++)
            {
                var item = Gear(player, tier, socket - 1);
                int required = Math.Max(new[] { 30, 80, 100 }[(int)tier], new[] { 30, 50, 70, 90, 110 }[socket - 1]);
                int chance = new[] { 100, 50, 25, 5, 5 }[socket - 1];
                Ready(player, required - .1); int uses = tool.UsesRemaining; var before = ClassGear.State(item); var native = GearDetails.Read(item);
                Require(Quote(player, tool, item) == null && tool.UsesRemaining == uses && ClassGear.State(item) == before,
                    "Below socket/tier skill gate mutated work");
                Ready(player, required); var quote = Quote(player, tool, item);
                Require(quote is { } && quote.Chance == chance && quote.RequiredSkill == required, "Wrong combined socket gate or odds");
                TinkeringSockets.Cancel(player, quote);
                Require(!TinkeringSockets.Commit(player, quote, out _) && ClassGear.State(item) == before && tool.UsesRemaining == uses,
                    "Cancel changed item or spent a charge");
                quote = Quote(player, tool, item); var result = Act(player, quote, chance - 1);
                Require(result.Outcome == SocketOutcome.Added && result.UsesSpent == 1 && tool.UsesRemaining == uses - 1
                    && ClassGear.State(item) == (before with { TinkerSockets = before.TinkerSockets + 1 })
                    && GearDetails.Read(item) == native && player.Skills.Tinkering.Base == required,
                    "Successful socket changed identity/native details/rolls or wrong charge/skill");
                Require(!TinkeringSockets.Commit(player, quote, out _), "Committed socket quote replayed");
                if (socket == 5) { Require(Quote(player, tool, item) == null, "Sixth socket allowed"); }
                saved.Add(Snapshot(item));
                if (socket > 1)
                {
                    var failure = Gear(player, tier, socket - 1); var ruby = new Ruby(); failure.AddItem(ruby);
                    var bag = new Bag(); var emerald = new Emerald(); bag.DropItem(emerald); failure.AddItem(bag);
                    result = Act(player, Quote(player, tool, failure), chance);
                    Require(result.Outcome == SocketOutcome.Destroyed && failure.Deleted && ruby.Deleted && bag.Deleted && emerald.Deleted,
                        "Destructive failure kept item or installed contents");
                }
            }
        }
        SlotsAndTraining(player, tool, saved);
        ToolQualities(player);
        Intuition(player, tool);
        Rejections(player, tool);
        NativeProvenance(player, tool, saved);
        var legacy = Put(player, new Longsword()); Ready(player, 30);
        var cancel = Quote(player, tool, legacy); TinkeringSockets.Cancel(player, cancel);
        Require(ClassGear.State(legacy) == null, "Cancelled legacy quote materialized or rerolled metadata");
        Act(player, Quote(player, tool, legacy), 99); saved.Add(Snapshot(legacy));
        var persistedTool = Put(player, ItemOrigins.Create(ItemOrigin.Crafted, () => new TinkerTools()));
        CraftTransactions.FinishTool(persistedTool, player, CraftQuality.Superior);
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new SaveData(saved.ToArray(), persistedTool.Serial.Value,
            persistedTool.UsesRemaining, CraftTransactions.Profiles.Find(persistedTool))));
    }
    private static void SlotsAndTraining(PlayerMobile player, TinkerTools tool, List<Saved> saved)
    {
        Ready(player, 30);
        var original = Mobile.SkillCheckLocationHandler;
        int checks = 0;
        Mobile.SkillCheckLocationHandler = (from, skill, minimum, maximum) =>
        {
            Require(from == player && skill == SkillName.Tinkering && minimum == 30 && maximum == 80,
                "Socket gain check used wrong player/skill/window");
            checks++;
            return original(from, skill, minimum, maximum);
        };
        try
        {
            foreach (var rarity in Enum.GetValues<GearRarity>())
            {
                foreach (var candidate in new Item[] { new ChainChest(), new Helmet(), new WoodenShield(),
                    new Bow(), new QuarterStaff(), new MagicArrowWand(), new Spellbook { LootType = LootType.Regular, Content = 7 } })
                {
                    var item = SalvageChecks.Gear(player, candidate, rarity: rarity);
                    var state = ClassGear.State(item); ClassGear.Profiles.Store(item, state with { Identified = true });
                    state = ClassGear.State(item); var native = GearDetails.Read(item);
                    Act(player, Quote(player, tool, item), 99);
                    Require(ClassGear.State(item) == (state with { TinkerSockets = 1 }) && GearDetails.Read(item) == native
                        && (item is not Spellbook book || book.Content == 7), "Allowed slot/rarity changed properties on socketing");
                    saved.Add(Snapshot(item));
                }
            }
            Require(checks == Enum.GetValues<GearRarity>().Length * 7 && player.Skills.Tinkering.Base == 30,
                "Success omitted native gain check or ignored locked skill");
        }
        finally { Mobile.SkillCheckLocationHandler = original; }
    }
    private static void ToolQualities(PlayerMobile player)
    {
        Ready(player);
        foreach (var quality in Enum.GetValues<CraftQuality>())
        {
            var tool = Put(player, ItemOrigins.Create(ItemOrigin.Crafted, () => new TinkerTools()));
            CraftTransactions.FinishTool(tool, player, quality);
            Require(TinkeringSockets.ToolBonus(tool) == (int)quality + 1 && tool.UsesRemaining == new[] { 100, 110, 120 }[(int)quality],
                "Crafted tool quality bonus/charge capacity mismatch");
            var item = Gear(player, sockets: 1); var quote = Quote(player, tool, item);
            Require(quote.Chance == 51 + (int)quality && TinkeringSockets.Chance(tool, 1) == 100, "Tool bonus not additive/capped");
            int uses = tool.UsesRemaining; var result = Act(player, quote, 0, 0);
            Require(result.Outcome == SocketOutcome.Added && result.UsesSpent == 0 && tool.UsesRemaining == uses, "Crafted socket charge saving missing");
            item.Delete(); tool.Delete();
        }
        var vendor = Put(player, ItemOrigins.Create(ItemOrigin.Vendor, () => new TinkerTools(20)));
        CraftTransactions.Profiles.Store(vendor, new(CraftQuality.Superior, true, 120));
        Require(TinkeringSockets.ToolBonus(vendor) == 0, "Vendor origin acquired crafted success bonus"); vendor.Delete();
    }
    private static void Intuition(PlayerMobile player, TinkerTools tool)
    {
        Ready(player, 120); var item = Gear(player, sockets: 3); var original = Snapshot(item); int uses = tool.UsesRemaining;
        var result = Act(player, Quote(player, tool, item), 99, 0);
        Require(result.Outcome == SocketOutcome.Intuition && result.UsesSpent == 0 && result.Retry is { Retry: true }
            && tool.UsesRemaining == uses && Snapshot(item) == original, "Intuition acted or charged before the retry choice");
        var retry = result.Retry; TinkeringSockets.Cancel(player, retry);
        Require(Snapshot(item) == original && tool.UsesRemaining == uses && !TinkeringSockets.Commit(player, retry, out _),
            "Intuition abort changed work or remained replayable");
        result = Act(player, Quote(player, tool, item), 99, 0);
        result = Act(player, result.Retry, 0);
        Require(result.Outcome == SocketOutcome.Added && result.UsesSpent == 1 && tool.UsesRemaining == uses - 1,
            "Chosen retry did not spend exactly its own tool use");
        item.Delete(); item = Gear(player, sockets: 3); result = Act(player, Quote(player, tool, item), 99, 0);
        var random = new Rolls(99, 0, 0); retry = result.Retry;
        Require(TinkeringSockets.Commit(player, retry, out result, random) && result.Outcome == SocketOutcome.Destroyed
            && item.Deleted && random.Calls == 1, "Retry recursively offered intuition or did not destroy failed work");
        var last = Put(player, new TinkerTools(1)); item = Gear(player, sockets: 3); result = Act(player, Quote(player, last, item), 99, 0);
        Require(!last.Deleted && last.UsesRemaining == 1 && !item.Deleted, "Free intuition depleted the last charge");
        result = Act(player, result.Retry, 0);
        Require(result.Outcome == SocketOutcome.Added && last.Deleted && !item.Deleted, "One-use tool could not perform its paid retry"); item.Delete();
        var crafted = Put(player, ItemOrigins.Create(ItemOrigin.Crafted, () => new TinkerTools()));
        CraftTransactions.FinishTool(crafted, player, CraftQuality.Superior);
        item = Gear(player, sockets: 3); int craftedUses = crafted.UsesRemaining;
        result = Act(player, Quote(player, crafted, item), 99, 0);
        result = Act(player, result.Retry, 0, 0);
        Require(result.Outcome == SocketOutcome.Added && result.UsesSpent == 0 && crafted.UsesRemaining == craftedUses,
            "Paid intuition retry lost ordinary crafted-tool saving"); item.Delete(); crafted.Delete();
        Ready(player, 119.9); item = Gear(player, sockets: 3); result = Act(player, Quote(player, tool, item), 99);
        Require(result.Outcome == SocketOutcome.Destroyed, "Intuition enabled below 120");
    }
    private static void Rejections(PlayerMobile player, TinkerTools tool)
    {
        Ready(player); var item = Gear(player); int uses = tool.UsesRemaining;
        var quote = Quote(player, tool, item); tool.UsesRemaining--;
        Require(!TinkeringSockets.Commit(player, quote, out _) && !item.Deleted, "Stale tool uses accepted"); tool.UsesRemaining++;
        quote = Quote(player, tool, item); item.HitPoints--;
        Require(!TinkeringSockets.Commit(player, quote, out _) && tool.UsesRemaining == uses, "Stale native item wear accepted");
        quote = Quote(player, tool, item); var gem = new Ruby(); item.AddItem(gem);
        Require(!TinkeringSockets.Commit(player, quote, out _) && !gem.Deleted, "Changed installed contents accepted");
        quote = Quote(player, tool, item); ClassProfessions.Profiles.Select(player, SkillName.Alchemy); ClassProfessions.Profiles.Select(player, SkillName.Tinkering);
        Require(!TinkeringSockets.Commit(player, quote, out _), "Away/back profession quote remained valid");
        quote = Quote(player, tool, item); EventSink.InvokeDisconnected(player); EventSink.InvokeConnected(player);
        Require(!TinkeringSockets.Commit(player, quote, out _) && !item.Deleted && tool.UsesRemaining == uses,
            "Disconnected/reconnected socket session remained usable");
        var older = Quote(player, tool, item); var newer = Quote(player, tool, item);
        Require(!TinkeringSockets.Commit(player, older, out _), "Old socket session remained usable");
        TinkeringSockets.Cancel(player, newer); item.Delete();
        foreach (var candidate in new Item[] { new GoldRing(), new GoldNecklace(), new LeatherGloves(), new Boots(), new Bandana(), new BodySash() })
        { Put(player, candidate); Require(Quote(player, tool, candidate) == null, "Non-socket slot accepted"); candidate.Delete(); }
        item = Gear(player); item.Insured = true; Require(Quote(player, tool, item) == null, "Insured gear accepted"); item.Insured = false;
        item.LootType = LootType.Blessed; Require(Quote(player, tool, item) == null, "Protected gear accepted"); item.LootType = LootType.Regular;
        var state = ClassGear.State(item); ClassGear.Profiles.Store(item, state with { Identified = false });
        Require(Quote(player, tool, item) == null, "Unknown gear accepted"); ClassGear.Profiles.Store(item, state);
        item.MoveToWorld(player.Location, player.Map); Require(Quote(player, tool, item) == null, "Ground gear accepted"); item.Delete();
        var vendor = Put(player, ItemOrigins.Create(ItemOrigin.Vendor, () => new Longsword())); Require(Quote(player, tool, vendor) == null, "Vendor gear accepted"); vendor.Delete();
        item = Gear(player); quote = Quote(player, tool, item); Ready(player, 119.9);
        Require(!TinkeringSockets.Commit(player, quote, out _), "Changed quoted skill accepted"); Ready(player);
        quote = Quote(player, tool, item); var home=player.Location; player.MoveToWorld(new Point3D(5100, 1000, 0), Map.Trammel);
        Require(!TinkeringSockets.Commit(player, quote, out _), "Remote workbench commit accepted"); item.Delete();
        player.MoveToWorld(home, Map.Trammel);
    }
    private static void NativeProvenance(PlayerMobile player, TinkerTools tool, List<Saved> saved)
    {
        Ready(player); player.Skills.Blacksmith.Cap = 120; player.Skills.Blacksmith.Base = 100;
        var item = Put(player, ItemOrigins.Create(ItemOrigin.Crafted, () => new Longsword()));
        EquipmentCrafting.Finish(item, player, EquipmentCraftingChecks.Row("Longsword"), CraftResource.Iron, 0,
            CraftQuality.Superior, new Random(42));
        var state = ClassGear.State(item); Require(state.NativeSockets == 2 && state.TinkerSockets == 0, "Native socket fixture invalid");
        var imbuer = ImbuingChecks.Player(SkillName.Imbuing); Put(imbuer, new ImbuingLadle()); imbuer.Backpack.DropItem(item);
        Require(ImbuingChecks.Quote(imbuer, item) != null, "Native quality sockets wrongly block Imbuing");
        player.Backpack.DropItem(item); Act(player, Quote(player, tool, item), 0);
        Require(ClassGear.State(item) == (state with { TinkerSockets = 1 }), "Native sockets were replaced instead of preserved");
        imbuer.Backpack.DropItem(item); Require(ImbuingChecks.Quote(imbuer, item) == null, "Tinker-added socket did not permanently block Imbuing");
        player.Backpack.DropItem(item); saved.Add(Snapshot(item));
    }
    private static void Reload()
    {
        var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(Manifest));
        foreach (var row in data.Items)
        { var item = World.FindItem((Serial)row.Item); Require(item != null && Snapshot(item) == row, "Socket identity/provenance/native state changed on reload"); }
        var tool = World.FindItem((Serial)data.Tool) as TinkerTools;
        Require(tool is { Deleted: false } && tool.UsesRemaining == data.Uses && CraftTransactions.Profiles.Find(tool) == data.ToolState
            && TinkeringSockets.ToolBonus(tool) == 3, "Socket tool bonus/uses changed on reload");
    }
}
