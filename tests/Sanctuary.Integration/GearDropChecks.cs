using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class GearDropChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(GearDropChecks));
    private static readonly Point3D _point = new(5530, 1150, 0);
    private static string Manifest => Path.Join(Core.BaseDirectory, "gear-drop-checks.json");
    private sealed record SavedItem(uint Item, string State, NativeGearDetails Native, ItemOrigin Origin, string Name);
    private sealed record Saved(uint Player, int MagicFind, SavedItem[] Items, uint Creature, uint Corpse, uint Loot);
    internal sealed class PathRoll(params int[] values) : Random
    {
        private int _index;
        public List<int> Bounds { get; } = new();
        public override int Next(int maximum)
        {
            Bounds.Add(maximum);
            int value = _index < values.Length ? values[_index++] : -1;
            Require(value < maximum, "Unexpected treasure random bound"); return value < 0 ? maximum - 1 : value;
        }
        public override int Next(int minimum, int maximum) => minimum;
    }
    private sealed class QualityRoll(DropQuality quality, int variant = 0) : Random
    {
        private int _call;
        private bool _done;
        public override int Next(int max)
        {
            if (_call++ == 0) { return Math.Min(variant, max - 1); }
            if (_done) { return 0; }
            if (_call - 1 == (quality switch { DropQuality.Unique => 1, DropQuality.Set => 2, DropQuality.Rare => 3, DropQuality.Magic => 4, _ => 5 }))
            { _done = true; return 0; }
            if (_call >= 5) { _done = true; }
            return max - 1;
        }
        public override int Next(int min, int max) => min;
    }
    public static void Configure()
    { Register("gear-drop-check", Check); Register("gear-drop-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,
        "Disposable shared treasure/gear/MF generation, consumers and persistence checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", name); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    internal static void Require(bool value, string message) => ImbuingChecks.Require(value, message);
    private static string Json(object value) => JsonSerializer.Serialize(value);
    private static SavedItem Snapshot(Item item) => new(item.Serial.Value, Json(ClassGear.State(item)), GearDetails.Read(item), item.Origin, item.Name);
    internal static PlayerMobile Player()
    {
        var p = new PlayerMobile { Body = 0x190, Player = true, Name = "GearDropCheck" }; p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p, DiabloClass.Necromancer); ClassCreation.Profiles.AdvanceLevel(p, 99);
        p.RawStr = p.RawDex = 1000; FixtureSpace.Place(p, _point, Map.Trammel); return p;
    }
    private static T Put<T>(PlayerMobile p, T item) where T : Item { p.Backpack.DropItem(item); return item; }
    internal static Corpse Body(BaseCreature victim, Mobile killer, Point3D? point = null, Map map = null)
    {
        var location = point ?? _point; map ??= Map.Trammel;
        victim.MoveToWorld(location, map); victim.AIObject?.AITimer.Stop(); victim.Frozen = true; victim.LastKiller = killer;
        var body = new Corpse(victim, new List<Item>()); body.MoveToWorld(location, map);
        if (killer is PlayerMobile p) { body.Aggressors.Add(p); }
        else if (killer is BaseCreature pet && pet.GetMaster() is PlayerMobile owner) { body.Aggressors.Add(owner); }
        return body;
    }
    private static Item Generated(PlayerMobile p, DropQuality quality = DropQuality.Magic, string code = "hax", int level = 35)
    {
        var item = GearDrops.Generate(GearDrops.Base(code), level, 0, new QualityRoll(quality));
        Require(item != null, "Supported direct drop did not generate"); return Put(p, item);
    }
    private static void Check()
    {
        Ratios(); Catalogue();
        var p = Player(); var mfItems = FindEquipment(p); int find = GearDrops.MagicFind(p);
        Require(find > 0 && ClassStats.Snapshot(p)[(int)StatField.MagicFind] == find, "MF snapshot/status missing");
        var victim = new Skeleton(); var body = Body(victim, p);
        var native = new Gold(999); body.DropItem(native);
        // Skeleton: equipment branch -> weap3 -> hax -> UO variant -> Magic.
        Require(!RewardLife.Process(victim, body, new PathRoll(121, 0, 0, 0, -1, -1, -1, 0)) && native.Deleted,
            "Level99/native-cleanup behavior changed");
        var loot = body.Items.Single(); var initial = ClassGear.State(loot);
        Require(loot.Origin == ItemOrigin.Drop && initial is { Identified: false } && initial.Roll.Rarity == GearRarity.Magic
            && initial.Roll.ItemLevel == CreatureProfiles.Level(victim) && body.SanctuaryNoRewards && body.Items.All(i => i is not Gold),
            "Shared terminal roll duplicated gold, lost ilvl/origin or exposed affixes");
        IdentificationChecks.Hidden(loot);
        RewardLife.Process(victim, body, new PathRoll(100, 0));
        Require(body.Items.Count == 1 && body.Items[0] == loot, "Replayed reward life changed gear");
        var filler = Put(p, new Ruby());
        int limit = p.Backpack.MaxItems; p.Backpack.MaxItems = p.Backpack.TotalItems;
        Require(!ClassLoot.Take(p, loot) && loot.Parent == body, "Full backpack lost generated gear");
        p.Backpack.MaxItems = limit; filler.Delete(); Require(ClassLoot.Take(p, loot) && !ClassGear.State(loot).Identified, "Loot transfer identified/changed gear");
        var scroll = Put(p, new IdentifyScroll()); int hiddenPrice = TradePrices.Quote(loot); var before = ClassGear.State(loot);
        Require(Identification.Reveal(p, scroll, loot) && ClassGear.State(loot) == (before with { Identified = true })
            && TradePrices.Quote(loot) == before.IdentifiedSaleOffer && hiddenPrice == before.IdentifiedSaleOffer / 10,
            "Identify rerolled or repriced actual drop");
        Require(p.EquipItem(loot) && ClassGear.Active(p, loot), "Actual drop cannot equip"); p.Backpack.DropItem(loot);
        TradeAndSalvage(p); Exclusions(p); NativeKills(p);
        // Keep one paid corpse through all restarts; XP level/cap is irrelevant.
        var savedBody = Body(victim, p); RewardLife.Process(victim, savedBody, new PathRoll(121, 0, 0, 0, -1, -1, 0));
        var savedLoot = savedBody.Items.Single();
        var saved = mfItems.Append(loot).Append(savedLoot).Select(Snapshot).ToArray();
        File.WriteAllText(Manifest, Json(new Saved(p.Serial.Value, find, saved, victim.Serial.Value, savedBody.Serial.Value, savedLoot.Serial.Value)));
    }
    private static void Ratios()
    {
        var basis = GearDrops.Base("hax");
        Require(GearDrops.Denominator(basis, 2, 0, DropQuality.Unique) == 51328
            && GearDrops.Denominator(basis, 2, 0, DropQuality.Rare) == 12800, "Negative level difference rounding drifted");
        Require(GearDrops.EffectiveFind(10, DropQuality.Unique) == 10 && GearDrops.EffectiveFind(100, DropQuality.Unique) == 71
            && GearDrops.EffectiveFind(100, DropQuality.Set) == 83 && GearDrops.EffectiveFind(100, DropQuality.Rare) == 85
            && GearDrops.EffectiveFind(100, DropQuality.Magic) == 100, "MF diminishing returns differ from source");
        int u = GearDrops.Denominator(basis, 35, 0, DropQuality.Unique), s = GearDrops.Denominator(basis, 35, 0, DropQuality.Set);
        int r = GearDrops.Denominator(basis, 35, 0, DropQuality.Rare), m = GearDrops.Denominator(basis, 35, 0, DropQuality.Magic);
        Require(GearDrops.Quality(basis, 35, 0, new PathRoll(127)) == DropQuality.Unique
            && GearDrops.Quality(basis, 35, 0, new PathRoll(128, 127)) == DropQuality.Set
            && GearDrops.Quality(basis, 35, 0, new PathRoll(u - 1, s - 1, 127)) == DropQuality.Rare
            && GearDrops.Quality(basis, 35, 0, new PathRoll(u - 1, s - 1, r - 1, 127)) == DropQuality.Magic
            && GearDrops.Quality(basis, 35, 0, new PathRoll(u - 1, s - 1, r - 1, m - 1)) == DropQuality.Normal,
            "Quality order or 128-ticket threshold drifted");
        Require(GearDrops.Generate(basis, 35, 500, new QualityRoll(DropQuality.Unique)) == null
            && GearDrops.Generate(basis, 35, 500, new QualityRoll(DropQuality.Set)) == null, "Reserved rarity was replaced/rerolled");
        foreach (int find in new[] { 0, 100, 500, int.MaxValue })
        {
            foreach (var q in new[] { DropQuality.Unique, DropQuality.Set, DropQuality.Rare, DropQuality.Magic })
            { Require(GearDrops.Denominator(basis, 99, find, q) > 0, "MF overflow/zero denominator"); }
        }
        // Fixed seeded distributions are checked against sequential probabilities,
        // including the deliberately empty Unique/Set mass, not just an MF slogan.
        foreach (int find in new[] { 0, 100, 500 })
        {
            int samples = 100000; var count = new int[5]; var random = new Random(8192 + find);
            for (int i = 0; i < samples; i++) { count[(int)GearDrops.Quality(basis, 35, find, random)]++; }
            double remaining = 1;
            foreach (var q in new[] { DropQuality.Unique, DropQuality.Set, DropQuality.Rare, DropQuality.Magic })
            {
                double probability = remaining * Math.Min(1, 128.0 / GearDrops.Denominator(basis, 35, find, q));
                double expected = samples * probability;
                Require(Math.Abs(count[(int)q] - expected) < Math.Max(12, 6 * Math.Sqrt(samples * probability * (1 - probability))), "Seeded quality distribution differs from source");
                remaining -= probability;
            }
            Require(Math.Abs(count[0] - samples * remaining) < Math.Max(12, 6 * Math.Sqrt(samples * remaining * (1 - remaining))), "White distribution drifted");
        }
    }
    private static void Catalogue()
    {
        foreach (var pool in GearDrops.Pools)
        {
            int ticket = 0;
            foreach (var entry in pool.Entries)
            {
                Require(GearDrops.SelectBase(pool.Name, new PathRoll(ticket)).Code == entry.Code
                    && GearDrops.SelectBase(pool.Name, new PathRoll(ticket + entry.Weight - 1)).Code == entry.Code, "Base weights/boundaries drifted");
                ticket += entry.Weight;
            }
            if (ticket == 0) { Require(GearDrops.SelectBase(pool.Name, new PathRoll()) == null, "Empty source class rerolled"); }
        }
        int checkedItems = 0;
        foreach (var basis in GearDrops.Bases)
        {
            if (basis.Variants.Length == 0) { Require(GearDrops.Generate(basis, 99, 0, new PathRoll()) == null, "Unmapped base was replaced"); continue; }
            for (int variant = 0; variant < basis.Variants.Length; variant++)
            {
                foreach (int level in new[] { 1, 10, 35, 70, 99 })
                {
                    foreach (var quality in new[] { DropQuality.Normal, DropQuality.Magic, DropQuality.Rare })
                    {
                        Item item;
                        try { item = GearDrops.Generate(basis, level, 0, new QualityRoll(quality, variant)); }
                        catch (Exception error) { throw new InvalidOperationException($"Drop combination {basis.Code}/{basis.Variants[variant].Type}/{level}/{quality}", error); }
                        var state = ClassGear.State(item);
                        Require(state != null && state.BaseCode == basis.Code && state.Tier == basis.Tier && state.Roll.ItemLevel == level
                            && item.GetType().Name == basis.Variants[variant].Type && item.Origin == ItemOrigin.Drop && !item.PlayerConstructed
                            && !state.CraftedEquipment && state.Quality == CraftQuality.Average && state.SocketCount == 0
                            && state.MaterialRolls.Length == 0 && state.Roll.Affixes.All(a => ClassGear.Implemented(a.Definition))
                            && state.Identified == (state.Roll.Rarity == GearRarity.Normal), "Drop identity/state/filter wrong");
                        GearProfiles.Validate(item, state); item.Delete(); checkedItems++;
                    }
                }
            }
        }
        _logger.Information("Gear drop catalogue: {Count} generated variant/level/quality combinations passed", checkedItems);
        var rolledValues = new HashSet<int>();
        foreach (string code in new[] { "hax", "rin", "amu" })
        {
            for (int seed = 0; seed < 512; seed++)
            {
                var item = GearDrops.Generate(GearDrops.Base(code), 99, 100, new Random(seed));
                if (item == null) { continue; }
                foreach (var affix in ClassGear.State(item).Roll.Affixes)
                { foreach (var mod in affix.Modifiers) { if (mod.Max > mod.Min) { rolledValues.Add(mod.Value.Value); } } }
                item.Delete();
            }
        }
        Require(rolledValues.Count > 20, "Actual generated affix values were flattened to a baseline");
    }
    internal static Item[] FindEquipment(PlayerMobile p)
    {
        var ring = Put(p, new GoldRing());
        for (int seed = 0; seed < 10000 && ClassGear.Bonus(ring, "mag%") == 0; seed++)
        { ClassGear.Profiles.Convert(ring, 99, GearRarity.Magic, new Random(seed)); }
        int affix = ClassGear.Bonus(ring, "mag%"); Require(affix > 0, "MF affix not generated");
        var armor = Put(p, new RingmailChest());
        var recipe = EquipmentCraftingChecks.Row(nameof(RingmailChest)); p.Skills.Blacksmith.Cap = 120; p.Skills.Blacksmith.Base = 120;
        EquipmentCrafting.Finish(armor, p, recipe, CraftResource.Gold, 0, CraftQuality.Average, new Random(42));
        int material = ClassGear.Material(armor, "magic-find"); Require(material is >= 1 and <= 2, "Saved gold-armour MF absent");
        Require(GearDrops.MagicFind(p) == 0 && p.EquipItem(ring) && p.EquipItem(armor) && GearDrops.MagicFind(p) == affix + material,
            "Equipped affix/material MF aggregation failed");
        ring.MaxHitPoints = ring.HitPoints = 100; ring.HitPoints = 0;
        Require(GearDrops.MagicFind(p) == material, "Broken MF remained active"); ring.HitPoints = 100;
        var state = ClassGear.State(ring); ClassGear.Profiles.Store(ring, state with { Identified = false });
        Require(GearDrops.MagicFind(p) == material, "Unidentified MF was active"); ClassGear.Profiles.Store(ring, state);
        p.Backpack.DropItem(armor); Require(GearDrops.MagicFind(p) == affix, "Carried material MF remained active"); p.EquipItem(armor);
        return [ring, armor];
    }
    private static void TradeAndSalvage(PlayerMobile p)
    {
        var item = Generated(p, DropQuality.Rare, "lsd"); int price = TradePrices.Quote(item); string roll = Json(ClassGear.State(item).Roll);
        var smith = TownMarkets.Profiles.At("Felucca/Britain/Smith"); p.MoveToWorld(smith.Location, smith.Map);
        int gold = p.Backpack.GetAmount(typeof(Gold));
        Require(smith.OnSellItems(p, [new SellItemResponse(item, 1)]) && item.Deleted && p.Backpack.GetAmount(typeof(Gold)) == gold + price,
            "Unknown drop sale failed or paid identified price");
        var salvage = Generated(p, DropQuality.Rare, "lsd"); var hammer = Put(p, new SmithHammer(20));
        var quote = EquipmentEconomy.Quote(p, salvage, EssenceAction.Salvage, hammer); int uses = hammer.UsesRemaining;
        Require(quote != null && EquipmentEconomy.Commit(p, quote, out var result, new PathRoll(0, 0, 0, 0)) && salvage.Deleted
            && result.Essence > 0 && hammer.UsesRemaining == uses && !EquipmentEconomy.Commit(p, quote, out _), "Actual drop salvage failed/replayed");
        FixtureSpace.Place(p, _point, Map.Trammel);
    }
    private static void Exclusions(PlayerMobile p)
    {
        foreach (var victim in new BaseCreature[] { new Skeleton { Controlled = true, ControlMaster = p }, new Skeleton { Summoned = true, SummonMaster = p },
            new Skeleton { NoKillAwards = true }, new Cow(), new Blacksmith() })
        {
            var body = Body(victim, p); RewardLife.Process(victim, body, new PathRoll(121, 0, 0, 0, -1, -1, -1, 0));
            Require(body.Items.All(i => !ClassGear.Converted(i)), "Excluded victim generated gear"); body.Delete(); victim.Delete();
        }
        var raised = new GraveyardSkeleton(); typeof(GraveyardUndead).GetMethod("MarkRaised", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(raised, [true]);
        var raisedBody = Body(raised, p); RewardLife.Process(raised, raisedBody, new PathRoll(121));
        Require(raisedBody.Items.Count == 0, "Revived life generated gear"); raisedBody.Delete(); raised.Delete();
        foreach (Mobile credit in new Mobile[] { null, new PlayerMobile { Body = 0x190 } })
        {
            var victim = new Skeleton(); var body = Body(victim, credit); RewardLife.Process(victim, body, new PathRoll(121));
            Require(body.Items.Count == 0, "Uncredited/nonclass reward generated"); body.Delete(); victim.Delete(); credit?.Delete();
        }
        var pet = new Skeleton { Controlled = true, ControlMaster = p }; var enemy = new Skeleton();
        var corpse = Body(enemy, pet); var petRoll = new PathRoll(121, 0, 0, 0, -1, -1, -1, 0);
        RewardLife.Process(enemy, corpse, petRoll);
        Require(corpse.Items.Count == 1 && ClassGear.State(corpse.Items[0]) != null, "Pet credit lost gear"); corpse.Delete(); enemy.Delete();
        Require(petRoll.Bounds[4] == GearDrops.Denominator(GearDrops.Base("hax"), CreatureProfiles.Level(enemy), GearDrops.MagicFind(p), DropQuality.Unique),
            "Pet kill did not use owner MF");
        p.MoveToWorld(new Point3D(_point.X + 30, _point.Y, 0), Map.Trammel);
        enemy = new Skeleton(); corpse = Body(enemy, pet); RewardLife.Process(enemy, corpse, new PathRoll(121, 0, 0, 0, -1, -1, -1, 0));
        Require(corpse.Items.Count == 1, "Distant pet owner lost gear"); corpse.Delete(); enemy.Delete(); FixtureSpace.Place(p, _point, Map.Trammel);
        p.Kill(); enemy = new Skeleton(); corpse = Body(enemy, pet);
        RewardLife.Process(enemy, corpse, new PathRoll(121, 0, 0, 0, -1, -1, -1, 0));
        Require(corpse.Items.Count == 1 && GearDrops.MagicFind(p) == 0, "Dead pet owner lost gear or retained corpse MF");
        corpse.Delete(); enemy.Delete(); p.Resurrect(); Require(ClassDeath.Recover(p, (Corpse)p.Corpse), "MF fixture recovery failed");
        pet.Delete();
        // Every existing Normal reward policy keeps its opt-in and base selection.
        foreach (var policy in CreatureGold.Policies)
        {
            var type = typeof(BaseCreature).Assembly.GetType(policy.Types[0]) ?? typeof(GraveyardUndead).Assembly.GetType(policy.Types[0]);
            Require(type != null, "Reward policy type missing");
            for (int seed = 0; seed < 100; seed++)
            {
                string leaf = CreatureGold.Select(type, new Random(seed));
                Require(policy.Eligible || leaf == null, "Excluded policy selected treasure");
                GearDrops.SelectBase(leaf, new Random(seed));
            }
        }
    }
    private static void NativeKills(PlayerMobile p)
    {
        var original = ClassLootHooks.CreatureDied; var enemy = new Skeleton();
        try
        {
            ClassLootHooks.CreatureDied = (victim, body) =>
            { if (victim == enemy) { RewardLife.Process(victim, body, new PathRoll(121, 0, 0, 0, -1, -1, -1, 0)); } else { original(victim, body); } };
            enemy.MoveToWorld(_point, Map.Trammel); enemy.AIObject.AITimer.Stop(); enemy.Damage(100000, p);
            Require(enemy.Corpse is Corpse { SanctuaryNoRewards: true } corpse && corpse.Items.Count == 1
                && ClassGear.State(corpse.Items[0]) is { Identified: false }, "Native death did not produce hidden equipment");
        }
        finally { ClassLootHooks.CreatureDied = original; enemy.Corpse?.Delete(); enemy.Delete(); }
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var p = (PlayerMobile)World.FindMobile((Serial)saved.Player);
        Require(GearDrops.MagicFind(p) == saved.MagicFind, "Saved equipped MF changed");
        foreach (var row in saved.Items)
        {
            var item = World.FindItem((Serial)row.Item);
            Require(item != null && Json(ClassGear.State(item)) == row.State && GearDetails.Read(item) == row.Native
                && item.Origin == row.Origin && item.Name == row.Name, "Generated gear/MF/identification/native state changed");
        }
        var corpse = (Corpse)World.FindItem((Serial)saved.Corpse); var creature = (BaseCreature)World.FindMobile((Serial)saved.Creature);
        RewardLife.Process(creature, corpse, new PathRoll(121));
        Require(corpse.SanctuaryNoRewards && corpse.Items.Count == 1 && corpse.Items[0].Serial.Value == saved.Loot, "Saved paid corpse rerolled gear");
    }
}
