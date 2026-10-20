using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class MouldChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(MouldChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "mould-checks.json");
    private sealed record Saved(uint Item, string Identity, uint Parent);
    private sealed record SaveData(Saved[] Items, uint Creature, uint Corpse, int Count);
    internal sealed class Drops(bool mould, int statTicket = 0, bool gold = false) : Random
    {
        public int ChanceCalls;
        private int _ordinaryCalls;
        public override int Next(int maximum)
        {
            if (maximum == 10000) { ChanceCalls++; return mould ? 9 : 10; }
            if (maximum == MouldRewards.TotalWeight) { return statTicket; }
            return gold && _ordinaryCalls++ == 0 ? 100 : 0;
        }
        public override int Next(int minimum, int maximum) => minimum;
    }
    private sealed class Ticket(int value, int stat = 0) : Random
    {
        private int _calls;
        public int Calls => _calls;
        public override int Next(int maximum) => _calls++ == 0 ? value : stat;
    }
    public static void Configure()
    { Register("mould-check", Check); Register("mould-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Mould bands, frequency tickets, life claims, transfer and persistence", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", name); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    internal static void Require(bool value, string message) => ImbuingChecks.Require(value, message);
    internal static string Identity(RuneMould item) => JsonSerializer.Serialize(new
    { item.Tier, item.StatId, item.EffectCode, item.Baseline, item.StatLabel, item.SourceAffix, item.AffixName,
        item.AffixKind, item.TierName, item.SourceLevel, item.Difficulty, item.Grade, item.Name, item.ItemID,
        item.Hue, item.Weight, item.Amount, item.Origin, item.Stackable });
    private static Saved Snapshot(RuneMould item) => new(item.Serial.Value, Identity(item), (item.Parent as Item)?.Serial.Value ?? 0);
    private static void Check()
    {
        Require(MouldRewards.TotalWeight == 430 && MouldRewards.Settings.Stats.Length == 15, "Incomplete weighted semantic catalogue");
        var observed = new Dictionary<string, int>();
        for (int n = 0; n < MouldRewards.TotalWeight; n++)
        { var stat = MouldRewards.Select(n); observed[stat.Id] = observed.GetValueOrDefault(stat.Id) + 1; }
        foreach (var stat in MouldRewards.Settings.Stats)
        { Require(observed[stat.Id] == stat.Weight, "Source frequency boundary was not exact"); }
        var player = GearDropChecks.Player(); var other = GearDropChecks.Player(); var saved = new List<Saved>();
        for (int level = 0; level <= 100; level++)
        {
            var rolls = new Ticket(9); var item = MouldRewards.Roll(level, MonsterDifficulty.Normal, rolls);
            int tier = level is >= 20 and <= 99 ? (level - 10) / 10 : 0;
            Require(tier == MouldRewards.TierForLevel(level) && (tier == 0 ? item == null && rolls.Calls == 0 : item.Tier == tier),
                "Mould source level band drifted"); item?.Delete();
        }
        int wins = 0;
        for (int ticket = 0; ticket < 10000; ticket++)
        { var item = MouldRewards.Roll(20, MonsterDifficulty.Normal, new Ticket(ticket)); if (item != null) { wins++; item.Delete(); } }
        Require(wins == 10, "Ordinary mould chance is not exactly 0.1%");
        foreach (var tier in MouldRewards.Settings.Tiers)
        {
            foreach (var stat in MouldRewards.Settings.Stats)
            {
                var item = ItemOrigins.Create(ItemOrigin.Drop, () => new RuneMould(tier.Tier, stat.Id, tier.MinimumLevel, MonsterDifficulty.Hell));
                player.Backpack.DropItem(item); string before = Identity(item);
                string statLine = $"Declared rune stat: {stat.Label}";
                Require(item.PropertyList.Buffer.AsSpan().IndexOf(Encoding.Unicode.GetBytes(statLine)) >= 0,
                    "Mould tooltip does not render its declared stat as one client argument");
                Require(item.Percentage(CraftQuality.Average) == stat.Baseline + tier.Tier - 1
                    && item.Percentage(CraftQuality.HighQuality) == stat.Baseline + tier.Tier
                    && item.Percentage(CraftQuality.Superior) == stat.Baseline + tier.Tier + 1
                    && item.Grade == MouldGrade.Ordinary && !item.Stackable && ClassGear.State(item) == null,
                    "Mould has wrong formula, rarity, grade or gear metadata");
                Require(stat.AffixKind == AffixKind.Suffix ? item.Name == $"{tier.Name} Mould {stat.AffixName}"
                    : item.Name == $"{tier.Name} {stat.AffixName} Mould", "Declared source name is not canonical");
                other.Backpack.DropItem(item); Require(Identity(item) == before, "Trade changed known mould identity");
                var copy = new RuneMould(); Item.CopyProperties(item, copy); other.Backpack.DropItem(copy);
                Require(Identity(copy) == before, "Direct native copy lost private mould identity"); copy.Delete();
                copy = new RuneMould(); item.Dupe(copy); other.Backpack.DropItem(copy);
                Require(Identity(copy) == before && !item.CanStackWith(copy), "Dupe lost identity or permitted merging"); copy.Delete();
                Require(EquipmentEconomy.Quote(other, item, EssenceAction.Imbue) == null
                    && EquipmentEconomy.Quote(other, item, EssenceAction.Salvage) == null,
                    "Mould entered gear transformation/salvage"); saved.Add(Snapshot(item));
            }
        }
        var (creature, body) = LifeChecks(player);
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new SaveData(saved.ToArray(), creature.Serial.Value, body.Serial.Value, body.Items.Count)));
    }
    private static (BaseCreature, Corpse) LifeChecks(PlayerMobile player)
    {
        BaseCreature final = null; Corpse finalBody = null;
        foreach (var difficulty in Enum.GetValues<MonsterDifficulty>())
        {
            BaseCreature victim = difficulty == MonsterDifficulty.Normal ? new Dragon() : new Skeleton();
            if (difficulty != MonsterDifficulty.Normal) { MonsterVariants.Assign(victim, difficulty); }
            var body = GearDropChecks.Body(victim, player); var random = new Drops(true);
            bool xp = RewardLife.Process(victim, body, random);
            var mould = body.Items.OfType<RuneMould>().Single();
            Require(!xp && random.ChanceCalls == 1 && mould.Difficulty == difficulty
                && mould.Tier == MouldRewards.TierForLevel(CreatureProfiles.Level(victim))
                && mould.SourceLevel == CreatureProfiles.Level(victim) && body.SanctuaryNoRewards,
                "Mould entitlement depended on XP or lost difficulty/level");
            Require(body.Items.Count == 1, "Empty source treasure roll was rerolled for mould");
            var replay = new Drops(true); Require(!RewardLife.Process(victim, body, replay) && replay.ChanceCalls == 0
                && body.Items.Count == 1, "Spent life generated a second mould");
            body.Delete(); victim.Delete();
        }
        var pet = new Skeleton { Controlled = true, ControlMaster = player };
        var enemy = new Dragon(); var corpse = GearDropChecks.Body(enemy, pet); var hit = new Drops(true);
        RewardLife.Process(enemy, corpse, hit);
        Require(corpse.Items.OfType<RuneMould>().Count() == 1, "Pet owner lost mould entitlement"); corpse.Delete(); enemy.Delete();
        player.MoveToWorld(new Point3D(5600, 1250, 0), Map.Trammel); player.Kill();
        enemy = new Dragon(); corpse = GearDropChecks.Body(enemy, pet); RewardLife.Process(enemy, corpse, new Drops(true));
        Require(corpse.Items.OfType<RuneMould>().Count() == 1, "Distant/dead pet owner lost mould entitlement"); corpse.Delete(); enemy.Delete();
        player.Resurrect(); ClassDeath.Recover(player, (Corpse)player.Corpse); pet.Delete();
        foreach (var excluded in new BaseCreature[] { new Dragon { Controlled = true, ControlMaster = player },
            new Dragon { Summoned = true, SummonMaster = player }, new Dragon { NoKillAwards = true }, new Cow(), new Blacksmith() })
        {
            corpse = GearDropChecks.Body(excluded, player); hit = new Drops(true); RewardLife.Process(excluded, corpse, hit);
            Require(hit.ChanceCalls == 0 && !corpse.Items.OfType<RuneMould>().Any(), "Excluded monster rolled a mould"); corpse.Delete(); excluded.Delete();
        }
        var raised = new GraveyardSkeleton(); MonsterVariants.Assign(raised, MonsterDifficulty.Hell);
        typeof(GraveyardUndead).GetMethod("MarkRaised", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(raised, [true]);
        corpse = GearDropChecks.Body(raised, player); hit = new Drops(true); RewardLife.Process(raised, corpse, hit);
        Require(hit.ChanceCalls == 0 && !corpse.Items.OfType<RuneMould>().Any(), "Raised high-level life rolled a mould");
        corpse.Delete(); raised.Delete();
        var noCredit = new Dragon(); corpse = GearDropChecks.Body(noCredit, null); hit = new Drops(true);
        RewardLife.Process(noCredit, corpse, hit); Require(hit.ChanceCalls == 0, "Uncredited death rolled a mould");
        corpse.Delete(); noCredit.Delete();
        foreach (int find in new[] { 0, 100, int.MaxValue })
        {
            var monster = new Dragon(); var body = GearDropChecks.Body(monster, player);
            MouldRewards.Award(new(monster, body, player, 35, 0, find), new Drops(true));
            Require(body.Items.OfType<RuneMould>().Single().StatId == MouldRewards.Settings.Stats[0].Id,
                "Magic Find altered independent mould chance/selection"); body.Delete(); monster.Delete();
        }
        // Compare the shared treasure outcome with and without the independent mould hit.
        foreach (bool success in new[] { false, true })
        {
            var victim = new Dragon(); var body = GearDropChecks.Body(victim, player);
            var random = new Drops(success, gold: true); RewardLife.Process(victim, body, random);
            Require(body.Items.OfType<Gold>().Any() && body.Items.OfType<RuneMould>().Count() == (success ? 1 : 0),
                "Mould replaced or altered the ordinary gold drop");
            if (success) { final = victim; finalBody = body; } else { body.Delete(); victim.Delete(); }
        }
        player.MoveToWorld(finalBody.Location, finalBody.Map);
        var loot = finalBody.Items.OfType<RuneMould>().Single(); string before = Identity(loot);
        var filler = new Ruby(); player.Backpack.DropItem(filler);
        int limit = player.Backpack.MaxItems; player.Backpack.MaxItems = player.Backpack.TotalItems;
        Require(!ClassLoot.Take(player, loot) && loot.Parent == finalBody, "Full pack consumed or moved mould");
        player.Backpack.MaxItems = limit; filler.Delete();
        Require(ClassLoot.Take(player, loot) && loot.IsChildOf(player.Backpack) && Identity(loot) == before,
            "Owned mould pickup rerolled its declaration"); finalBody.DropItem(loot);
        return (final, finalBody);
    }
    private static void Reload()
    {
        var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(Manifest));
        foreach (var row in data.Items)
        { var item = World.FindItem((Serial)row.Item) as RuneMould; Require(item != null && Snapshot(item) == row, "Mould tier/name/stat/level/ownership changed on reload"); }
        var body = World.FindItem((Serial)data.Corpse) as Corpse; var victim = World.FindMobile((Serial)data.Creature) as BaseCreature;
        var random = new Drops(true); RewardLife.Process(victim, body, random);
        Require(body.Items.Count == data.Count && random.ChanceCalls == 0 && body.Items.OfType<RuneMould>().Count() == 1,
            "Reloaded spent life rewarded mould twice");
    }
}
