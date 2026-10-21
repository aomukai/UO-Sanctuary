using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class TreasureChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(TreasureChecks));
    private static string Pathname => Path.Join(Core.BaseDirectory, "treasure-checks.json");
    private sealed record Saved(uint Chart, uint Owner, uint Chest, uint Hurt, int Hits, string Layout, string Items, uint Expiring, uint RecoveryOwner, uint Ground, int ReturnX, int ReturnY, string ReturnMap);
    internal static void LearnAll(Mobile owner) { foreach (var row in RareRecipes.All) { RareRecipes.Profiles.Learn(owner, row.Id); } }
    private static void Require(bool condition, string why) => GearDropChecks.Require(condition, why);
    private static T Put<T>(PlayerMobile owner, T item) where T : Item { owner.Backpack.DropItem(item); return item; }
    internal static PlayerMobile Player(SkillName craft = SkillName.Cartography)
    {
        var owner = new PlayerMobile { Body = 0x190, Player = true, Name = "TreasureHunter", Blessed = true };
        new Account("treasure-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"))[0] = owner;
        owner.AddItem(new Backpack()); ClassCreation.Profiles.Assign(owner, DiabloClass.Necromancer); ClassCreation.Profiles.AdvanceLevel(owner, 99);
        owner.RawStr = owner.RawDex = 1000; ClassProfessions.Profiles.Confirm(owner, craft);
        foreach (var skill in new[] { SkillName.Cartography, SkillName.Tinkering, SkillName.Cooking, SkillName.Alchemy, SkillName.Imbuing })
        { owner.Skills[skill].Cap = 120; owner.Skills[skill].Base = 120; owner.Skills[skill].SetLockNoRelay(SkillLock.Locked); }
        FixtureSpace.Place(owner, new Point3D(5350, 1220, 0), Map.Felucca);
        CraftFacilities.Register(new(craft, owner.Map.Name, owner.X + 1, owner.Y, owner.Z, 0xB4A));
        return owner;
    }
    private sealed class Roll(int value = 0, double chance = 0) : Random
    { public override int Next(int max) => Math.Min(value, max - 1); public override double NextDouble() => chance; }
    public static void Configure()
    {
        Command("treasure-check", Check, true); Command("treasure-reload-check", Reload); Command("treasure-expiry-prepare", PrepareExpiry);
    }
    private static void Command(string name, Action callback, bool timed = false) => ConsoleInputHandler.RegisterCommand(name, "Treasure dungeons and recipe acquisition", _ => Core.LoopContext.Post(() =>
    { try { callback(); if (!timed) { _log.Information("{Command}: passed", name); } } catch (Exception e) { _log.Error(e, "{Command}: failed", name); } }));
    private static void Layouts()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            var plan = TreasureLayout.Generate(seed); var floor = new HashSet<Point2D>(plan.Floor);
            Require(plan.Rooms.Length is >= 8 and <= 12 && floor.Count > 3000 && !plan.Walls.Any(floor.Contains), "Dungeon too small or walls overlap floors");
            var reached = new HashSet<Point2D> { plan.Rooms[0].Center }; var pending = new Queue<Point2D>(); pending.Enqueue(plan.Rooms[0].Center);
            while (pending.TryDequeue(out var point))
            { foreach (var n in TreasureLayout.Neighbors(point)) { if (floor.Contains(n) && reached.Add(n)) { pending.Enqueue(n); } } }
            Require(reached.Count == floor.Count, "Disconnected room or corridor");
            for (int a = 0; a < plan.Rooms.Length; a++)
            { for (int b = a + 1; b < plan.Rooms.Length; b++) { var p = plan.Rooms[a].Center; var q = plan.Rooms[b].Center;
                Require(Math.Abs(p.X - q.X) + Math.Abs(p.Y - q.Y) >= 48, "Pack anchors too close"); } }
            Require(JsonSerializer.Serialize(plan) == JsonSerializer.Serialize(TreasureLayout.Generate(seed)), "Seed rerolled layout");
        }
    }
    private static void DropsAndRecipes()
    {
        var p = Player();
        foreach (int level in new[] { 1, 20, 21, 40, 41, 60, 61, 80, 81, 99 })
        {
            foreach (int ticket in new[] { 0, 99, 100, 9999 })
            {
                var victim = new Skeleton(); var corpse = GearDropChecks.Body(victim, p);
                TreasureHunts.Award(new(victim, corpse, p, level, 0, 5000), new Roll(ticket));
                var maps = corpse.Items.OfType<TreasureChart>().ToArray();
                Require(maps.Length == (ticket < 100 ? 1 : 0) && (maps.Length == 0 || maps[0].Tier == TreasureHunts.TierFor(level)), "Map drop boundaries/bands/MF wrong");
                corpse.Delete(); victim.Delete();
            }
        }
        foreach (var row in TreasureHunts.Settings.Tiers)
        {
            var chest = new ExpeditionChest(); TreasureHunts.Fill(chest, row, 0, new Roll());
            Require(chest.Items.OfType<Gold>().Sum(i => i.Amount) == row.Gold && chest.Items.OfType<SocketGem>().Single().Quality == (GemQuality)row.GemQuality
                && chest.Items.OfType<RareRecipeScroll>().Count() == 1 && chest.Items.OfType<IronIngot>().Single().Amount == row.Resources,
                "Chest rewards or quality tiers wrong");
            Require(chest.Items.Where(ClassGear.Converted).All(i => ClassGear.State(i) is { NamedItemId: not null, Roll.Rarity: GearRarity.Unique }),
                "Unique chest outcome was replaced with an ordinary quality"); chest.Delete();
        }
        Require(RareRecipes.All.Count > 50 && !RareRecipes.Profiles.Knows(p, "key/splendid"), "Fresh character knew rare recipes");
        var scroll = Put(p, new RareRecipeScroll("key/splendid")); Require(RareRecipes.Learn(p, scroll) && scroll.Deleted, "Recipe could not be learned");
        scroll = Put(p, new RareRecipeScroll("key/splendid")); Require(!RareRecipes.Learn(p, scroll) && !scroll.Deleted, "Duplicate consumed");
        var other = Player(); Require(!RareRecipes.Profiles.Knows(other, "key/splendid"), "Recipe shared between characters"); other.Delete();
        var native = Put(p, new TreasureMap(6, Map.Trammel)); p.Skills.Cartography.Base = 0; native.OnDoubleClick(p);
        Require(native.Deleted && p.Backpack.Items.OfType<TreasureChart>().Any(c => c.Tier == 4), "Legacy six-level conversion missing");
        Require(Server.Engines.Harvest.Fishing.System.Construct(typeof(TreasureMap), p) is TreasureChart, "Fishing map route missing");
        p.Delete();
    }
    private static void DifficultyBands()
    {
        foreach (var row in TreasureHunts.Settings.Tiers)
        {
            for (int seed = 0; seed < 5; seed++)
            {
                var defense = TreasureHunts.Defenses(row.Tier, seed);
                Require(defense == TreasureHunts.Defenses(row.Tier, seed), "Map defenses rerolled");
                foreach (var kind in Enum.GetValues<MonsterArchetype>())
                {
                    var profile = TreasureHunts.Profile($"treasure-{row.Tier}-{kind}");
                    foreach (var grade in new[] { EncounterGrade.Ordinary, EncounterGrade.Unique })
                    {
                        var mob = new TreasureMonster(); mob.Bind(row.Tier, defense.First, defense.Second, defense.High, row.Creatures[0].Type);
                        mob.Assign(profile, row.MonsterMaximum - (grade == EncounterGrade.Unique ? 3 : 0), row.Difficulty, grade,
                            ChampionKind.Champion, [EliteModifier.FireEnchanted, EliteModifier.ColdEnchanted, EliteModifier.LightningEnchanted], EliteAura.None, null, new Random(seed));
                        var r = mob.EncounterProfile.Resistances; var values = new[] { r.Physical, r.Magic, r.Fire, r.Lightning, r.Cold, r.Poison };
                        Require(mob.Difficulty == row.Difficulty && CreatureProfiles.Level(mob) >= row.MonsterMinimum
                            && CreatureProfiles.Level(mob) <= row.MonsterMaximum && values.Count(n => n >= 100) == Math.Max(0, row.Tier - 3),
                            "Difficulty, level band or immunity count changed through elite modifiers");
                        if (defense.First >= 0) { Require(values[defense.First] >= 100, "First shared immunity absent"); }
                        if (defense.Second >= 0) { Require(values[defense.Second] >= 100, "Second shared immunity absent"); }
                        if (defense.High >= 0) { Require(values[defense.High] is >= 75 and < 100, "High elemental resistance became immunity"); }
                        mob.Delete();
                    }
                }
            }
        }
    }
    private static void RecipeGatesAndFishing()
    {
        var cook = Player(SkillName.Cooking); var meal = Meals.Recipes.First(r => r.Tier == 3);
        var skillet = Put(cook, new Skillet(10)); MealChecks.Inputs(cook, meal);
        Require(MealCooking.Quote(cook, skillet, meal) == null, "Unlearned rare meal allowed");
        RareRecipes.Profiles.Learn(cook, RareRecipes.Meal(meal)); var mealQuote = MealCooking.Quote(cook, skillet, meal);
        Require(mealQuote != null, "Learned rare meal rejected"); MealCooking.End(mealQuote); cook.Delete();
        var alchemist = Player(SkillName.Alchemy); var protection = AlchemyRestoratives.Recipes.Single(r => r.Skill == 120);
        var mortar = Put(alchemist, new MortarPestle(10)); AlchemyChecks.Inputs(alchemist, protection);
        Require(AlchemyRestoratives.Quote(alchemist, mortar, protection) == null, "Unlearned Protection allowed");
        RareRecipes.Profiles.Learn(alchemist, RareRecipes.Alchemy(protection)); var potionQuote = AlchemyRestoratives.Quote(alchemist, mortar, protection);
        Require(potionQuote != null && potionQuote.Chance == 1, "Learned Protection/120 guarantee rejected"); AlchemyRestoratives.End(potionQuote); alchemist.Delete();
        var imbuer = Player(SkillName.Imbuing); var sword = Put(imbuer, new Longsword());
        EquipmentCrafting.Finish(sword, imbuer, EquipmentCraftingChecks.Row("Longsword", GearTier.Elite), CraftResource.Iron, 0, CraftQuality.Average, new Random(1));
        var ladle = Put(imbuer, new ImbuingLadle { UsesRemaining = 10 });
        Require(!TieredImbuing.Ready(imbuer, sword, ladle), "Unlearned Elite Imbuing allowed");
        RareRecipes.Profiles.Learn(imbuer, "imbuing/elite"); Require(TieredImbuing.Ready(imbuer, sword, ladle), "Learned Elite Imbuing rejected"); imbuer.Delete();
        var fisher = Player();
        foreach (int state in new[] { 0, 1, 2 })
        {
            bool controlled = state == 1, summoned = state == 2;
            var serpent = new SeaSerpent { Summoned = summoned }; if (controlled) { serpent.SetControlMaster(fisher); }
            var corpse = GearDropChecks.Body(serpent, fisher); var map = new TreasureChart(); var bottle = new MessageInABottle(); var net = new SpecialFishingNet(); var nativeGold = new Gold(777);
            foreach (var item in new Item[] { map, bottle, net, nativeGold }) { corpse.DropItem(item); }
            RewardLife.Process(serpent, corpse, new Roll(9999, .999));
            Require((controlled || summoned ? !nativeGold.Deleted : nativeGold.Deleted) && (summoned ? map.Deleted && bottle.Deleted && net.Deleted
                : map.Parent == corpse && bottle.Parent == corpse && net.Parent == corpse), "Fishing reward retention/exclusion leaked native loot");
            int count = corpse.Items.Count; Require(!RewardLife.Process(serpent, corpse, new Roll()) && corpse.Items.Count == count, "Fishing reward replayed");
            corpse.Delete(); serpent.Delete();
        }
        foreach (var cargo in new Item[] { new TreasureChart(), new MessageInABottle(), new SpecialFishingNet() })
        {
            Require(Server.Engines.Harvest.Fishing.System.Give(fisher, cargo, false)
                && !cargo.Deleted && cargo.RootParent is BaseCreature, "Actual native fishing carrier deleted its catch");
            var carrier = (BaseCreature)cargo.RootParent; carrier.AIObject?.AITimer.Stop(); carrier.Frozen = true; carrier.LastKiller = fisher;
            carrier.Kill();
            Require(!cargo.Deleted && cargo.Parent is Corpse, "Actual native carrier death lost the catch");
            ((Corpse)cargo.Parent).Delete(); carrier.Delete();
        }
        var kraken = new Kraken();
        Require(!Core.UOR || kraken.Backpack?.FindItemByType<SpecialFishingNet>() != null, "Native Kraken's fishing-net supply was suppressed"); kraken.Delete();
        fisher.Delete();
    }
    private static void Keys()
    {
        var p = Player(SkillName.Tinkering); var tool = Put(p, new TinkerTools(100));
        foreach (var row in TreasureHunts.Settings.Tiers)
        {
            var input = EquipmentCrafting.Resolve(row.KeyMaterial).CreateInstance<Item>(); input.Amount = 4; Put(p, input);
            var q = TreasureKeys.Quote(p, tool, row.Tier);
            if (row.Tier == 5) { Require(q == null, "Unlearned Splendid recipe allowed"); RareRecipes.Profiles.Learn(p, "key/splendid"); q = TreasureKeys.Quote(p, tool, 5); }
            Require(q != null && (row.Tier != 5 || q.Chance == 1), "Key recipe/120 guarantee missing");
            var t = new CraftTransactions.Request(p, DefTinkering.CraftSystem, q.Recipe, null, tool, 0) { TreasureKey = q };
            int before = tool.UsesRemaining;
            Require(CraftTransactions.Complete(t, new Roll()) && input.Amount == 3 && tool.UsesRemaining == before - 1
                && p.Backpack.Items.OfType<TreasureSkeletonKey>().Any(k => k.Tier == row.Tier) && !CraftTransactions.Complete(t), "Key craft cost/output/replay wrong");
            input.Delete();
        }
        p.Delete();
    }
    private static ExpeditionSession Begin(PlayerMobile p, int tier, int seed)
    {
        var chart = Put(p, new TreasureChart(tier, p.Map, new(p.X + 2, p.Y), seed)); chart.Decoder = p; Put(p, new Shovel());
        var hunt = TreasureHunts.Profiles.Begin(p, chart); Require(hunt != null && chart.Spent, "Valid hunt did not reserve slot/map"); return hunt;
    }
    private static void Check()
    {
        Layouts(); DifficultyBands(); DropsAndRecipes(); RecipeGatesAndFishing(); Keys();
        var p = Player(); RareRecipes.Profiles.Learn(p, "key/splendid");
        var hunt = Begin(p, 5, 173); var recovery = Player(); var expiring = Begin(recovery, 4, 731);
        Timer.StartTimer(TimeSpan.FromSeconds(10), () =>
        {
            try
            {
                Require(hunt.Active && expiring.Active, "Chunked dungeon did not finish");
                foreach (var serial in hunt.Creatures)
                {
                    var mob = World.FindMobile(serial) as TreasureMonster; Require(mob != null && mob.Difficulty == MonsterDifficulty.Hell
                        && CreatureProfiles.Level(mob) is >= 85 and <= 99, "Wrong highest-tier level/difficulty");
                    var r = mob.EncounterProfile.Resistances;
                    Require(new[] { r.Physical, r.Magic, r.Fire, r.Lightning, r.Cold, r.Poison }.Count(n => n >= 100) == 2,
                        "Highest-tier monster did not inherit exactly two immunities"); mob.Frozen = true;
                }
                foreach (var serial in expiring.Creatures)
                {
                    var mob = (TreasureMonster)World.FindMobile(serial); var r = mob.EncounterProfile.Resistances;
                    Require(mob.Difficulty == MonsterDifficulty.Hell && CreatureProfiles.Level(mob) is >= 70 and <= 80
                        && new[] { r.Physical, r.Magic, r.Fire, r.Lightning, r.Cold, r.Poison }.Count(n => n >= 100) == 1,
                        "Hoard difficulty or immunity count wrong"); mob.Frozen = true;
                }
                var portals = hunt.Items.Select(serial => World.FindItem(serial)).OfType<ExpeditionPortal>().ToArray();
                var intruder = Player(); Require(!TreasureHunts.Profiles.Travel(intruder, portals.Single(i => !i.Exit)), "Another player entered"); intruder.Delete();
                var entrance = portals.Single(i => !i.Exit); p.MoveToWorld(new(entrance.X + 1, entrance.Y, entrance.Z), entrance.Map);
                Require(TreasureHunts.Profiles.Travel(p, entrance) && hunt.Contains(p) && MapSurveys.Dungeon(p) != null, "Owner entrance or dungeon surveys failed");
                Require(!TreasureHunts.CanLand(p, p.Map, hunt.Origin) && hunt.Region.Accepts(p, hunt.Entry), "Teleport escaped dungeon floor");
                var chest = hunt.Items.Select(serial => World.FindItem(serial)).OfType<ExpeditionChest>().Single(); var key = Put(p, new TreasureSkeletonKey(5));
                p.MoveToWorld(new(chest.X + 1, chest.Y, chest.Z), chest.Map);
                Require(!TreasureHunts.Unlock(p, key, chest) && !key.Deleted, "Live guardians allowed chest/key consumption");
                foreach (var serial in hunt.Guardians) { World.FindMobile(serial)?.Kill(); }
                var wrong = Put(p, new TreasureSkeletonKey(4)); Require(!TreasureHunts.Unlock(p, wrong, chest) && !wrong.Deleted, "Wrong-tier key consumed/unlocked"); wrong.Delete();
                Require(TreasureHunts.Unlock(p, key, chest) && key.Deleted && chest.Unlocked && !TreasureHunts.Unlock(p, key, chest), "Matching key unlock failed/replayed");
                var hurt = (TreasureMonster)World.FindMobile(hunt.Creatures[0]); hurt.Hits /= 2;
                var exit = portals.Single(i => i.Exit); p.MoveToWorld(new(exit.X + 1, exit.Y, exit.Z), exit.Map);
                Require(TreasureHunts.Profiles.Travel(p, exit) && !hunt.Contains(p), "Exit did not return owner");
                var secondEntrance = expiring.Items.Select(serial => World.FindItem(serial)).OfType<ExpeditionPortal>().Single(i => !i.Exit);
                recovery.MoveToWorld(new(secondEntrance.X + 1, secondEntrance.Y, secondEntrance.Z), secondEntrance.Map);
                Require(TreasureHunts.Profiles.Travel(recovery, secondEntrance), "Recovery fixture could not enter");
                var ground = new Gold(777); ground.MoveToWorld(expiring.Entry, expiring.Map);
                File.WriteAllText(Pathname, JsonSerializer.Serialize(new Saved(hunt.Chart.Value, p.Serial.Value, chest.Serial.Value, hurt.Serial.Value, hurt.Hits,
                    JsonSerializer.Serialize(hunt.Plan), JsonSerializer.Serialize(hunt.Items), expiring.Chart.Value, recovery.Serial.Value, ground.Serial.Value, expiring.Return.X, expiring.Return.Y, expiring.ReturnMap.Name)));
                _log.Information("treasure-check: passed");
            }
            catch (Exception e) { _log.Error(e, "treasure-check: failed"); }
        });
    }
    private static void PrepareExpiry()
    {
        var m = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Pathname));
        var hunt = TreasureHunts.Profiles.Find((Serial)m.Expiring); if (hunt != null) { hunt.Expires = DateTime.UtcNow.AddSeconds(2); }
    }
    private static void Reload()
    {
        var m = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Pathname)); var h = TreasureHunts.Profiles.Find((Serial)m.Chart);
        Require(h is { Active: true } && JsonSerializer.Serialize(h.Plan) == m.Layout && JsonSerializer.Serialize(h.Items) == m.Items
            && World.FindMobile((Serial)m.Hurt)?.Hits == m.Hits && World.FindItem((Serial)m.Chest) is ExpeditionChest { Unlocked: true }
            && RareRecipes.Profiles.Knows(World.FindMobile((Serial)m.Owner), "key/splendid"), "Dungeon/loot/knowledge/injury rerolled on reload");
        Require(TreasureHunts.Profiles.Find((Serial)m.Expiring) == null, "Offline expiry retained session");
        var ground = World.FindItem((Serial)m.Ground);
        Require(ground is Gold { Amount: 777, Parent: ExpeditionRecovery }, $"Offline expiry lost ground gold: item={ground?.GetType().Name}, parent={ground?.Parent?.GetType().Name}, deleted={ground?.Deleted}");
        Require(World.FindMobile((Serial)m.RecoveryOwner) is { } owner && (owner.Map == Map.Internal ? owner.LogoutMap : owner.Map)?.Name == m.ReturnMap
            && Math.Abs((owner.Map == Map.Internal ? owner.LogoutLocation.X : owner.X) - m.ReturnX) <= 8
            && Math.Abs((owner.Map == Map.Internal ? owner.LogoutLocation.Y : owner.Y) - m.ReturnY) <= 8,
            $"Offline expiry did not return owner: {World.FindMobile((Serial)m.RecoveryOwner)?.Location}/{World.FindMobile((Serial)m.RecoveryOwner)?.LogoutLocation}, expected={m.ReturnX},{m.ReturnY}/{m.ReturnMap}");
    }
}
