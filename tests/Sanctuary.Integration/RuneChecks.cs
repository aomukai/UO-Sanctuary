using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class RuneChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(RuneChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "rune-checks.json");
    private sealed record Saved(uint Item, string Identity, uint Parent);
    internal sealed class Rolls(double success = 0, int quality = 99, bool save = false) : Random
    {
        private int _calls;
        public override double NextDouble() => success;
        public override int Next(int maximum) => save && _calls++ == 0 ? 0 : quality;
    }
    public static void Configure() { Register("rune-check", Check); Register("rune-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Declared rune crafting, quality, costs, invalidation and persistence", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", name); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    internal static void Require(bool value, string message) => ImbuingChecks.Require(value, message);
    internal static string Identity(InscribedRune rune) => JsonSerializer.Serialize(new
    { rune.Pattern, rune.Quality, rune.Percentage, rune.Name, rune.Hue, rune.ItemID, rune.Weight, rune.Amount, rune.Stackable, rune.Origin, rune.PlayerConstructed });
    private static Saved Snapshot(InscribedRune rune) => new(rune.Serial.Value, Identity(rune), (rune.Parent as Item)?.Serial.Value ?? 0);
    internal static RuneMould Mould(PlayerMobile owner, int tier, string stat = "cast-rate")
    {
        var mould = ItemOrigins.Create(ItemOrigin.Drop, () => new RuneMould(tier, stat, MouldRewards.Tier(tier).MinimumLevel, MonsterDifficulty.Hell));
        owner.Backpack.DropItem(mould); return mould;
    }
    private static CraftTransactions.Request Ticket(RuneRequest q) => new(q.Owner, DefInscription.CraftSystem, q.Recipe, null, q.Tool, 0) { Rune = q };
    private static bool Act(RuneRequest q, Random random = null) => q != null && CraftTransactions.Complete(Ticket(q), random ?? new Rolls());
    private static void QualityChecks()
    {
        foreach (var row in RuneCrafting.Recipes)
        {
            foreach (double offset in new[] { 0, 9.9, 10, 19.9, 20 })
            {
                int[] counts = [0, 0, 0];
                for (int roll = 0; roll < 100; roll++) { counts[(int)RuneCrafting.Quality(row.RequiredSkill + offset, row.RequiredSkill, roll)]++; }
                Require(counts.SequenceEqual(offset < 10 ? new[] { 100, 0, 0 } : offset < 20 ? new[] { 90, 10, 0 } : new[] { 89, 10, 1 }), "Rune quality gates or exclusive odds drift");
            }
        }
    }
    private static void Check()
    {
        QualityChecks(); var player = ImbuingChecks.Player(SkillName.Inscribe); var other = ImbuingChecks.Player(SkillName.Alchemy);
        player.Backpack.MaxItems = other.Backpack.MaxItems = 0;
        player.Skills.Inscribe.SetLockNoRelay(SkillLock.Locked);
        var pen = new ScribesPen { UsesRemaining = 5000 }; player.Backpack.DropItem(pen);
        var bark = ItemOrigins.Create(ItemOrigin.Drop, () => new Runebark(8, 5000)); player.Backpack.DropItem(bark);
        player.Backpack.DropItem(new Gold(456)); var saved = new List<Saved>();
        foreach (var row in RuneCrafting.Recipes)
        {
            foreach (var stat in MouldRewards.Settings.Stats)
            {
                foreach (var quality in Enum.GetValues<CraftQuality>())
                {
                    player.Skills.Inscribe.Base = row.RequiredSkill + 20;
                    var mould = Mould(player, row.Tier, stat.Id); var pattern = RunePattern.From(mould);
                    int expected = mould.Percentage(quality), amount = bark.Amount, uses = pen.UsesRemaining;
                    var quote = RuneCrafting.Quote(player, pen, mould); Require(quote != null, "Valid rune quote missing"); var ticket = Ticket(quote);
                    Require(CraftTransactions.Complete(ticket, new Rolls(0, quality == CraftQuality.Superior ? 0 : quality == CraftQuality.HighQuality ? 1 : 99)), "Declared rune rejected");
                    var rune = player.Backpack.Items.OfType<InscribedRune>().Last();
                    Require(mould.Deleted && bark.Amount == amount - row.Bark && pen.UsesRemaining == uses - 1 && player.Backpack.GetAmount(typeof(Gold)) == 456, "Rune costs wrong");
                    Require(rune.Pattern == pattern && rune.Quality == quality && rune.Percentage == expected && rune.Name == pattern.Name
                        && rune.Origin == ItemOrigin.Crafted && rune.PlayerConstructed && !rune.Stackable && ClassGear.State(rune) == null, "Rune identity rerolled");
                    Require(!CraftTransactions.Complete(ticket, new Rolls()), "Rune replayed");
                    Require(rune.PropertyList.Buffer.AsSpan().IndexOf(Encoding.Unicode.GetBytes($"{rune.StatLabel}: +{expected}%")) >= 0, "Tooltip hides percentage");
                    var copy = new InscribedRune(); rune.Dupe(copy); other.Backpack.DropItem(copy); other.Backpack.DropItem(rune);
                    Require(Identity(copy) == Identity(rune) && !rune.CanStackWith(copy) && !rune.StackWith(other, copy, false), "Rune copy/transfer/stack drift");
                    saved.Add(Snapshot(rune)); saved.Add(Snapshot(copy));
                }
            }
        }
        player.Skills.Inscribe.Base = 120;
        Failures(player, pen, bark); MixedBark(player); Rejections(player, other); GainChecks(player);
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved)); player.Delete();
        _logger.Information("Rune coverage: 360 tier/stat/quality outputs, 720 saved identities, mixed bark, failure costs, capacity, replay and native gain boundaries passed");
    }
    private static void Failures(PlayerMobile player, ScribesPen pen, Runebark bark)
    {
        foreach (var row in RuneCrafting.Recipes)
        {
            var mould = Mould(player, row.Tier); int before = bark.Amount, uses = pen.UsesRemaining;
            int count = player.Backpack.Items.OfType<InscribedRune>().Count();
            Require(Act(RuneCrafting.Quote(player, pen, mould), new Rolls(1)), "Failed attempt did not commit");
            Require(mould.Deleted && bark.Amount == before - (row.Bark + 1) / 2 && pen.UsesRemaining == uses - 1
                && player.Backpack.Items.OfType<InscribedRune>().Count() == count, "Wrong failure loss");
        }
        var crafted = ItemOrigins.Create(ItemOrigin.Crafted, () => new ScribesPen());
        CraftTransactions.FinishTool(crafted, player, CraftQuality.Superior); player.Backpack.DropItem(crafted);
        var m = Mould(player, 8); int charges = crafted.UsesRemaining;
        Require(Act(RuneCrafting.Quote(player, crafted, m), new Rolls(1, 99, true)) && crafted.UsesRemaining == charges && m.Deleted, "Failure charge saving failed");
        Require(Act(RuneCrafting.Quote(player, crafted, Mould(player, 8)), new Rolls(0, 0, true)) && crafted.UsesRemaining == charges, "Success charge saving failed");
        pen.UsesRemaining = 1; m = Mould(player, 1);
        Require(Act(RuneCrafting.Quote(player, pen, m)) && pen.Deleted && m.Deleted, "Last pen use did not complete/break");
    }
    private static void RemoveBark(PlayerMobile player)
    {
        var items = new List<Runebark>();
        foreach (var bark in player.Backpack.FindItemsByType<Runebark>(true)) { items.Add(bark); }
        foreach (var bark in items) { bark.Delete(); }
    }
    private static void MixedBark(PlayerMobile player)
    {
        RemoveBark(player);
        var pen = new ScribesPen { UsesRemaining = 50 }; player.Backpack.DropItem(pen);
        var bag = new Bag(); player.Backpack.DropItem(bag);
        var low = new Runebark(2, 8); var exact = new Runebark(3, 1); var higher = new Runebark(4, 1); var perfect = new Runebark(8, 5);
        player.Backpack.DropItem(low); player.Backpack.DropItem(exact); bag.DropItem(higher); player.Backpack.DropItem(perfect);
        var quote = RuneCrafting.Quote(player, pen, Mould(player, 3));
        Require(quote.Bark.Select(i => i.Tier).SequenceEqual(new[] { 3, 4, 8 }), "Lowest suitable bark not selected first");
        Require(Act(quote) && exact.Deleted && higher.Deleted && perfect.Amount == 4 && low.Amount == 8, "Mixed substitution costs wrong");
        var rune = player.Backpack.Items.OfType<InscribedRune>().Last(); Require(rune.Tier == 3, "Higher bark raised output tier"); rune.Delete();
    }
    private static void Rejections(PlayerMobile player, PlayerMobile other)
    {
        var pen = new ScribesPen { UsesRemaining = 100 }; player.Backpack.DropItem(pen);
        RemoveBark(player);
        var bark = new Runebark(8, 100); player.Backpack.DropItem(bark); var m = Mould(player, 8);
        player.Skills.Inscribe.Base = 99.9; Require(RuneCrafting.Quote(player, pen, m) == null, "Below trained gate permitted");
        var bonus = new DefaultSkillMod(SkillName.Inscribe, "RuneTests", true, 20); player.AddSkillMod(bonus);
        Require(RuneCrafting.Quote(player, pen, m) == null, "Bonus bypassed Base gate"); player.RemoveSkillMod(bonus);
        player.Skills.Inscribe.Base = 100; Require(RuneCrafting.Quote(player, pen, m) == null, "Zero chance boundary changed"); player.Skills.Inscribe.Base = 120;
        void Reject(Action mutate, Action restore, string label)
        {
            var quote = RuneCrafting.Quote(player, pen, m); Require(quote != null, "Missing quote: " + label);
            mutate(); Require(!Act(quote), "Invalid rune request: " + label); restore();
        }
        Reject(() => bark.Amount--, () => bark.Amount++, "bark amount");
        Reject(() => bark.Hue++, () => bark.Hue--, "bark hue");
        Reject(() => other.Backpack.DropItem(bark), () => player.Backpack.DropItem(bark), "foreign bark");
        Reject(() => other.Backpack.DropItem(m), () => player.Backpack.DropItem(m), "foreign mould");
        Reject(() => m.Name += "!", () => m.Name = m.Name[..^1], "mould changed");
        Reject(() => pen.UsesRemaining--, () => pen.UsesRemaining++, "pen uses");
        Reject(() => pen.Internalize(), () => player.Backpack.DropItem(pen), "remote pen");
        Reject(() => player.Skills.Inscribe.Base = 119.9, () => player.Skills.Inscribe.Base = 120, "skill changed");
        Reject(() => ClassProfessions.Profiles.Select(player, SkillName.Alchemy), () => ClassProfessions.Profiles.Select(player, SkillName.Inscribe), "craft changed");
        Reject(() => player.Frozen = true, () => player.Frozen = false, "frozen");
        Reject(() => player.Body = 0x192, () => player.Body = 0x190, "dead");
        var location = player.Location;
        Reject(() => player.MoveToWorld(new Point3D(5400, 1100, 0), Map.Trammel), () => player.MoveToWorld(location, Map.Trammel), "remote desk");
        Reject(() => player.Backpack.MaxItems = player.Backpack.TotalItems - 3, () => player.Backpack.MaxItems = 0, "overfull pack");
        var stale = RuneCrafting.Quote(player, pen, m); var fresh = RuneCrafting.Quote(player, pen, m);
        Require(!Act(stale), "Old confirmation valid"); RuneCrafting.End(player, fresh.Session); Require(!Act(fresh), "Cancelled confirmation valid");
        var forge = new CraftItem(typeof(InscribedRune), "forged", "rune"); forge.AddSkill(SkillName.Inscribe, 0, 1);
        var forged = (CraftTransactions.Request)CraftTransactions.Begin(player, DefInscription.CraftSystem, forge, null, pen, 0);
        Require(!CraftTransactions.Complete(forged), "Direct native recipe bypassed mould");
        Require(!m.Deleted && bark.Amount == 100 && pen.UsesRemaining == 100, "Rejection consumed inputs");
        player.Backpack.MaxItems = player.Backpack.TotalItems;
        Require(Act(RuneCrafting.Quote(player, pen, m)), "Full pack cannot replace consumed mould"); player.Backpack.MaxItems = 0;
        m = Mould(player, 8); bark.Delete(); Require(RuneCrafting.Quote(player, pen, m) == null, "Missing bark accepted"); m.Delete();
    }
    private static void GainChecks(PlayerMobile player)
    {
        var pen = new ScribesPen { UsesRemaining = 30 }; player.Backpack.DropItem(pen); player.Backpack.DropItem(new Runebark(8, 30));
        var original = Mobile.SkillCheckLocationHandler; int checks = 0;
        try
        {
            Mobile.SkillCheckLocationHandler = (from, skill, min, max) =>
            { if (from == player && skill == SkillName.Inscribe) { checks++; Require(min == 30 && max == 80, "Wrong gain band"); } return original?.Invoke(from, skill, min, max) == true; };
            player.Skills.Inscribe.Base = 60; player.Skills.Inscribe.SetLockNoRelay(SkillLock.Locked);
            Require(Act(RuneCrafting.Quote(player, pen, Mould(player, 1))) && checks == 1 && player.Skills.Inscribe.Base == 60, "Locked gain changed skill");
            player.Skills.Inscribe.SetLockNoRelay(SkillLock.Up); player.Skills.Inscribe.Cap = 60;
            Require(Act(RuneCrafting.Quote(player, pen, Mould(player, 1)), new Rolls(1)) && checks == 2 && player.Skills.Inscribe.Base == 60, "Failure gain ignored cap");
            player.Skills.Inscribe.Cap = 120; Require(Act(RuneCrafting.Quote(player, pen, Mould(player, 1))) && checks == 3, "Success missed gain check");
        }
        finally { Mobile.SkillCheckLocationHandler = original; }
    }
    private static void Reload()
    {
        foreach (var row in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        { Require(World.FindItem((Serial)row.Item) is InscribedRune rune && Snapshot(rune) == row, "Saved rune identity changed"); }
    }
}
