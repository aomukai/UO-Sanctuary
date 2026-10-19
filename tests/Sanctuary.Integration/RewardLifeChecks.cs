using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class RewardLifeChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(RewardLifeChecks));
    private static readonly Point3D _point = new(5530, 1150, 0);
    private static string Pathname => Path.Join(Core.BaseDirectory, "reward-life-check.json");
    private sealed record Saved(uint Player, uint Creature, uint Corpse, uint Sword, int Gold, int GoldFind, long Experience);
    private sealed class Choices(params int[] values) : Random
    {
        private int _index;
        public override int Next(int maximum)
        {
            if (_index >= values.Length) { return 0; }
            Require(values[_index] < maximum, "Unexpected gold random call");
            return values[_index++];
        }
    }
    public static void Configure()
    {
        Register("reward-life-check", Check);
        Register("reward-life-reload-check", Reload);
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable shared reward-life and manual gold checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", command); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    private static void Require(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
    private static long Xp(PlayerMobile player)
    {
        ClassCreation.Profiles.TryGet(player.Serial, out var profile);
        return profile.Experience;
    }
    private static int GoldIn(Corpse corpse) => corpse.Items.OfType<Gold>().Sum(g => g.Amount);
    private static Corpse Body(BaseCreature creature, Mobile killer)
    {
        creature.MoveToWorld(_point, Map.Trammel);
        creature.AIObject?.AITimer.Stop(); creature.Frozen = true;
        creature.LastKiller = killer;
        var corpse = new Corpse(creature, new List<Item>());
        corpse.MoveToWorld(_point, Map.Trammel);
        if (killer is PlayerMobile player) { corpse.Aggressors.Add(player); }
        return corpse;
    }
    private static void Check()
    {
        Require(CreatureGold.Policies.Count == CreatureProfiles.All.Count
            && CreatureGold.Policies.Count(p => p.Eligible) == 324, "Gold roster coverage drifted");
        // Skeleton: NoDrop 100, gold 21, equipment 16, junk 21, good 2.
        for (int choice = 0; choice < 160; choice++)
        {
            var random = new Choices(choice, 0);
            int amount = CreatureGold.Roll(typeof(Skeleton), 2, 0, random);
            Require((amount > 0) == (choice >= 100 && choice < 121), "D2 gold branch boundaries changed");
        }
        Require(CreatureGold.Roll(typeof(Skeleton), 2, 0, new Choices(100, 9)) == 11
            && CreatureGold.Roll(typeof(Skeleton), 2, 50, new Choices(100, 9)) == 16
            && CreatureGold.Roll(typeof(Skeleton), 2, 500, new Choices(0)) == 0,
            "Pile maximum, Gold Find rounding or chance changed");
        Require(CreatureGold.Roll(typeof(Cow), 2, 0, new Choices()) == 0, "Excluded creature rolled gold");
        foreach (var definition in CreatureProfiles.All)
        {
            var type = typeof(BaseCreature).Assembly.GetType(definition.Type) ?? typeof(GraveyardUndead).Assembly.GetType(definition.Type);
            var policy = CreatureGold.Policy(type);
            for (int seed = 0; seed < 100; seed++)
            {
                int amount = CreatureGold.Roll(type, 43, 0, new Random(seed));
                Require(amount is >= 0 and <= 257 && (policy.Eligible || amount == 0), "Roster gold bound failed");
            }
        }
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = "RewardLife" };
        player.AddItem(new Backpack());
        player.MoveToWorld(_point, Map.Trammel);
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        new Account("reward-life-fixture", Guid.NewGuid().ToString("N"))[0] = player;
        ClassCreation.Profiles.AdvanceLevel(player, 99);
        player.RawStr = 200; player.RawDex = 200;
        var sword = new Longsword();
        var recipe = EquipmentCrafting.Recipes.First(r => r.Type == nameof(Longsword) && r.Tier == GearTier.Normal);
        EquipmentCrafting.Finish(sword, player, recipe, CraftResource.Gold, 0, CraftQuality.Average, new Random(4));
        player.Backpack.DropItem(sword);
        var boots = new Boots(); player.Backpack.DropItem(boots);
        for (int seed = 0; seed < 10000 && ClassGear.Bonus(boots, "gold%") == 0; seed++)
        { ClassGear.Profiles.Convert(boots, 99, GearRarity.Magic, new Random(seed)); }
        int bonus = ClassGear.Bonus(boots, "gold%");
        int material = ClassGear.Material(sword, "gold-find");
        Require(bonus > 0 && material is >= 1 and <= 2, "Gold Find affix/material not generated");
        Require(CreatureGold.GoldFind(player) == 0 && player.EquipItem(sword) && player.EquipItem(boots)
            && CreatureGold.GoldFind(player) == bonus + material, "Gold Find equipment aggregation failed");
        sword.HitPoints = 0;
        Require(CreatureGold.GoldFind(player) == bonus, "Broken material Gold Find remained active");
        boots.MaxHitPoints = 100; boots.HitPoints = 0;
        Require(CreatureGold.GoldFind(player) == 0, "Broken affix Gold Find remained active");
        boots.HitPoints = 100; sword.HitPoints = sword.MaxHitPoints;
        int gf = CreatureGold.GoldFind(player);
        var victim = new Skeleton();
        var body = Body(victim, player);
        body.DropItem(new Gold(999)); body.DropItem(new Dagger());
        long before = Xp(player);
        Require(!RewardLife.Process(victim, body, new Choices(100, 9)), "Level-99 death increased XP");
        int expected = 11 * (100 + gf) / 100;
        Require(body.SanctuaryNoRewards && GoldIn(body) == expected && body.Items.Count == 1
            && player.Backpack.GetAmount(typeof(Gold)) == 0, "Gold was auto-collected, native loot survived or level99 lost rewards");
        ClassLootHooks.CreatureDied(victim, body);
        Require(GoldIn(body) == expected && Xp(player) == before, "Repeated callback erased or duplicated rewards");
        var filler = new Dagger(); player.Backpack.DropItem(filler);
        int limit = player.Backpack.MaxItems;
        player.Backpack.MaxItems = player.Backpack.TotalItems;
        var gold = body.Items.OfType<Gold>().Single();
        Require(!ClassLoot.Take(player, gold) && gold.Parent == body, "Full backpack lost corpse gold");
        player.Backpack.MaxItems = limit; filler.Delete();
        Require(ClassLoot.Take(player, gold) && player.Backpack.GetAmount(typeof(Gold)) == expected, "Manual gold looting failed");
        // Keep one paid corpse and its live synthetic owner across multiple saves.
        var savedBody = Body(victim, player);
        RewardLife.Process(victim, savedBody, new Choices(100, 9));
        var variants = (Dictionary<(string, MonsterDifficulty), CreatureVariant>)typeof(CreatureProfiles)
            .GetField("_variants", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var key = (typeof(Skeleton).FullName, MonsterDifficulty.Normal);
        var original = variants[key];
        try
        {
            variants[key] = original with { Rewards = original.Rewards with { Experience = 0 } };
            CheckClaim(new Skeleton(), player, true, gf);
        }
        finally { variants[key] = original; }
        var pet = new Skeleton { Controlled = true, ControlMaster = player };
        player.MoveToWorld(new Point3D(_point.X + 30, _point.Y, 0), Map.Trammel);
        CheckClaim(new Skeleton(), pet, true, gf);
        Require(Xp(player) == before, "Remote pet kill awarded XP");
        player.MoveToWorld(_point, Map.Trammel);
        player.Kill();
        CheckClaim(new Skeleton(), pet, true, CreatureGold.GoldFind(player));
        Require(Xp(player) == before, "Dead owner awarded XP");
        int deadPetGold = 0;
        for (int i = 0; i < 128 && deadPetGold == 0; i++)
        {
            var enemy = new Skeleton();
            enemy.MoveToWorld(_point, Map.Trammel); enemy.AIObject.AITimer.Stop(); enemy.Frozen = true;
            enemy.Damage(100000, pet);
            Require(enemy.Corpse is Corpse { SanctuaryNoRewards: true }, "Dead-owner pet death bypassed claim");
            var corpse = (Corpse)enemy.Corpse; deadPetGold += GoldIn(corpse);
            corpse.Delete(); enemy.Delete();
        }
        Require(deadPetGold > 0, "Actual pet kill lost dead-owner gold");
        player.Resurrect();
        Require(ClassDeath.Recover(player, (Corpse)player.Corpse) && sword.Parent == player, "Dead-owner fixture equipment recovery failed");
        CheckClaim(new Skeleton { Controlled = true, ControlMaster = player }, player, false, gf);
        CheckClaim(new Skeleton { Summoned = true, SummonMaster = player }, player, false, gf);
        CheckClaim(new Skeleton { NoKillAwards = true }, player, false, gf);
        var raised = new GraveyardSkeleton();
        typeof(GraveyardUndead).GetMethod("MarkRaised", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(raised, [true]);
        CheckClaim(raised, player, false, gf);
        CheckClaim(new Skeleton(), null, false, gf);
        var legacy = new PlayerMobile { Body = 0x190, Player = true };
        CheckClaim(new Skeleton(), legacy, false, gf); legacy.Delete(); pet.Delete();
        // Poison delivers the credited final damage after its owner leaves.
        for (int i = 0, paid = 0; i < 128; i++)
        {
            var enemy = new Skeleton();
            enemy.MoveToWorld(_point, Map.Trammel); enemy.AIObject.AITimer.Stop(); enemy.Frozen = true;
            enemy.SetResistance(ResistanceType.Poison, 0); enemy.Hits = 1;
            long now = Core.TickCount;
            GearEffects.ApplyPoison(player, enemy, 100, now);
            player.MoveToWorld(new Point3D(_point.X + 30, _point.Y, 0), Map.Trammel);
            GearEffects.Tick(now + 3000);
            Require(enemy.Corpse is Corpse { SanctuaryNoRewards: true }, "Poison kill bypassed reward-life claim");
            var corpse = (Corpse)enemy.Corpse;
            paid += GoldIn(corpse);
            corpse.Delete(); enemy.Delete();
            if (paid > 0) { break; }
            Require(i < 127, "Delayed poison kills never produced gold");
        }
        player.MoveToWorld(_point, Map.Trammel);
        // Shadow fiends normally delete their corpses; a successful gold roll must survive.
        int shadowGold = 0;
        for (int i = 0; i < 128 && shadowGold == 0; i++)
        {
            var shadow = new ShadowFiend();
            shadow.MoveToWorld(_point, Map.Trammel); shadow.AIObject.AITimer.Stop(); shadow.Frozen = true;
            shadow.Damage(100000, player);
            if (shadow.Corpse is Corpse { Deleted: false } corpse)
            {
                shadowGold += GoldIn(corpse); corpse.Delete();
            }
            shadow.Delete();
        }
        Require(shadowGold > 0, "Corpse-deleting eligible creature lost its gold");
        File.WriteAllText(Pathname, JsonSerializer.Serialize(new Saved(player.Serial.Value, victim.Serial.Value,
            savedBody.Serial.Value, sword.Serial.Value, expected, gf, Xp(player))));
        body.Delete();
    }
    private static void CheckClaim(BaseCreature victim, Mobile killer, bool pays, int gf)
    {
        var corpse = Body(victim, killer);
        RewardLife.Process(victim, corpse, new Choices(100, 9));
        Require(corpse.SanctuaryNoRewards && (GoldIn(corpse) > 0) == pays, "Shared claim eligibility failed");
        if (pays) { Require(GoldIn(corpse) == 11 * (100 + gf) / 100, "Death-time owner Gold Find missing"); }
        ClassLootHooks.CreatureDied(victim, corpse);
        Require((GoldIn(corpse) > 0) == pays, "Duplicate callback changed payout");
        corpse.Delete(); victim.Delete();
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Pathname));
        var player = (PlayerMobile)World.FindMobile((Serial)saved.Player);
        var victim = (BaseCreature)World.FindMobile((Serial)saved.Creature);
        var corpse = (Corpse)World.FindItem((Serial)saved.Corpse);
        var sword = World.FindItem((Serial)saved.Sword);
        Require(corpse is { SanctuaryNoRewards: true } && GoldIn(corpse) == saved.Gold && Xp(player) == saved.Experience,
            "Corpse gold, life claim or XP did not survive reload");
        Require(CreatureGold.GoldFind(player) == saved.GoldFind && ClassGear.State(sword).MaterialRolls.Length > 0,
            "Gold Find gear changed across reload");
        ClassLootHooks.CreatureDied(victim, corpse);
        Require(GoldIn(corpse) == saved.Gold && Xp(player) == saved.Experience, "Reload callback erased or duplicated rewards");
    }
}
