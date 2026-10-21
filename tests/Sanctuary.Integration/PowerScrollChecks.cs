using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Misc;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class PowerScrollChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(PowerScrollChecks));
    private static string Filepath => Path.Join(Core.BaseDirectory, "power-scroll-checks.json");
    private sealed record Saved(uint Player, int[] Bases, int[] Caps, int[] Locks, uint Scroll, uint Corpse, string Recipe);
    private sealed class Sequence(params int[] values) : Random
    {
        internal int Calls;
        public override int Next(int max)
        {
            Require(Calls < values.Length && values[Calls] >= 0 && values[Calls] < max, "Unexpected Power Scroll random draw");
            return values[Calls++];
        }
    }
    private sealed class ChainRoll(int tier) : Random
    {
        public override int Next(int max) => max == 4 ? tier : 0;
        public override int Next(int min, int max) => min;
        public override double NextDouble() => 0;
    }
    private static void Require(bool condition, string why) => GearDropChecks.Require(condition, why);
    private static T Put<T>(PlayerMobile p, T item) where T : Item { p.Backpack.DropItem(item); return item; }
    internal static PlayerMobile Player(bool confirmed = true)
    {
        var p = new PlayerMobile { Body = 0x190, Player = true, Name = "PowerScrollHunter", Blessed = true };
        new Account("powers-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"))[0] = p;
        p.AddItem(new Backpack()); ClassCreation.Profiles.Assign(p, DiabloClass.Necromancer); ClassCreation.Profiles.AdvanceLevel(p, 99);
        if (confirmed) { ClassProfessions.Profiles.Confirm(p, SkillName.Alchemy); }
        p.RawStr = p.RawDex = 1000;
        FixtureSpace.Place(p, new(5340, 1260, 0), Map.Trammel);
        return p;
    }
    private static EncounterCreature Boss(int baseLevel = 90, EncounterGrade grade = EncounterGrade.Unique, MonsterDifficulty difficulty = MonsterDifficulty.Hell)
    {
        var mob = new EncounterCreature(); int band = baseLevel / 10 * 10;
        mob.Assign(EncounterProfiles.Profile($"crypt-sentinel-{band}"), baseLevel, difficulty, grade, ChampionKind.Champion,
            [], EliteAura.None, null, new Random(9)); mob.Frozen = true; mob.AIObject?.AITimer.Stop(); return mob;
    }
    public static void Configure()
    { Command("power-scroll-check", Check); Command("power-scroll-reload-check", Reload); }
    private static void Command(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Boss Power Scroll drops, ordered caps and native use", _ => Core.LoopContext.Post(() =>
    { try { action(); _log.Information("{Command}: passed", name); } catch (Exception e) { _log.Error(e, "{Command}: failed", name); } }));
    private static void Drops()
    {
        Require(ProfessionPowerScrolls.Skills.Length == 13 && ProfessionPowerScrolls.Skills.ToArray().Distinct().Count() == 13
            && ProfessionPowerScrolls.Skills.ToArray().All(s => TradeSkills.Includes((int)s)), "Wrong retained scroll skill pool");
        var p = Player();
        foreach (int level in new[] { 59, 60, 69, 70, 79, 80, 89, 90, 99 })
        {
            int count = level < 60 ? 0 : Math.Min(4, (level - 60) / 10 + 1);
            Require(ProfessionPowerScrolls.TierCount(level) == count, "Boss level unlock boundary differs");
            foreach (var grade in Enum.GetValues<EncounterGrade>())
            {
                foreach (var difficulty in Enum.GetValues<MonsterDifficulty>())
                {
                    var boss = Boss(90, grade, difficulty); var corpse = GearDropChecks.Body(boss, p);
                    int tickets = grade == EncounterGrade.Unique ? 100 : grade == EncounterGrade.SuperUnique ? 500 : 0;
                    foreach (int ticket in new[] { 0, 99, 100, 499, 500, 9999 })
                    {
                        var random = new Sequence(ticket, 0, 0);
                        ProfessionPowerScrolls.Award(new(boss, corpse, p, level, 0, 1000000, difficulty), random);
                        bool success = count > 0 && ticket < tickets;
                        var scrolls = corpse.Items.OfType<PowerScroll>().ToArray();
                        Require(scrolls.Length == (success ? 1 : 0) && random.Calls == (count == 0 || tickets == 0 ? 0 : success ? 3 : 1), "Scroll rate boundary, excluded grade or extra roll differs");
                        if (success)
                        { Require(scrolls[0].Skill == SkillName.Alchemy && scrolls[0].Value == 105 && scrolls[0].Origin == ItemOrigin.Drop && scrolls[0].LootType == LootType.Regular, "Dropped scroll identity/origin/tradability differs"); scrolls[0].Delete(); }
                    }
                    corpse.Delete(); boss.Delete();
                }
            }
            if (count == 0) { continue; }
            var unique = Boss(); var body = GearDropChecks.Body(unique, p);
            for (int tier = 0; tier < count; tier++)
            {
                for (int skill = 0; skill < 13; skill++)
                {
                    ProfessionPowerScrolls.Award(new(unique, body, p, level, 0, 0), new Sequence(0, tier, skill));
                    var scroll = body.Items.OfType<PowerScroll>().Single();
                    Require(scroll.Value == 105 + 5 * tier && scroll.Skill == ProfessionPowerScrolls.Skills[skill], "Uniform unlocked-tier or retained-skill selection differs"); scroll.Delete();
                }
            }
            body.Delete(); unique.Delete();
        }
        var ordinary = new Skeleton(); var ordinaryBody = GearDropChecks.Body(ordinary, p);
        ProfessionPowerScrolls.Award(new(ordinary, ordinaryBody, p, 99, 0, 0), new Sequence());
        Require(!ordinaryBody.Items.OfType<PowerScroll>().Any(), "Native analogue gained boss scroll drops"); ordinaryBody.Delete(); ordinary.Delete();
        for (int excluded = 0; excluded < 4; excluded++)
        {
            var boss = Boss(96); boss.Controlled = excluded == 0; boss.Summoned = excluded == 1; boss.NoKillAwards = excluded == 2;
            var body = GearDropChecks.Body(boss, p); body.SanctuaryNoRewards = excluded == 3;
            RewardLife.Process(boss, body, new Sequence());
            Require(!body.Items.OfType<PowerScroll>().Any(), "Excluded/paid boss life awarded a scroll"); body.Delete(); boss.Delete();
        }
        p.Delete();
    }
    private static void UsesAndGuards()
    {
        var p = Player(false); var other = Player();
        foreach (var name in ProfessionPowerScrolls.Skills)
        {
            var skill = p.Skills[name]; skill.BaseFixedPoint = 237; skill.Cap = 100; skill.SetLockNoRelay((SkillLock)((int)name % 3));
            foreach (int cap in new[] { 105, 110, 115, 120 })
            {
                var scroll = Put(p, new PowerScroll(name, cap)); var before = skill.BaseFixedPoint; var skillLock = skill.Lock;
                var q = ProfessionPowerScrolls.Quote(p, scroll);
                Require(q != null && ProfessionPowerScrolls.Commit(q) && scroll.Deleted && skill.Cap == cap && skill.BaseFixedPoint == before
                    && skill.Lock == skillLock && !ProfessionPowerScrolls.Commit(q), "Ordered cap use granted skill, changed lock, failed or replayed");
                var duplicate = Put(p, new PowerScroll(name, cap)); Require(ProfessionPowerScrolls.Quote(p, duplicate) == null && !duplicate.Deleted, "Duplicate consumed"); duplicate.Delete();
            }
            Require(other.Skills[name].Cap == 100, "Another character inherited a cap");
        }
        Require(!ClassProfessions.Profiles.Find(p).Confirmed, "Inactive scroll use selected a first craft");
        var name2 = SkillName.Alchemy; p.Skills[name2].Cap = 100;
        foreach (double value in new[] { 100, 107.5, 110, 115, 120, 125 })
        { var scroll = Put(p, new PowerScroll(name2, value)); scroll.Use(p); Require(!scroll.Deleted && p.Skills[name2].Cap == 100 && ProfessionPowerScrolls.Quote(p, scroll) == null, "Invalid/out-of-order native scroll bypassed ladder"); scroll.Delete(); }
        var removed = Put(p, new PowerScroll(SkillName.Swords, 105)); removed.Use(p);
        Require(!removed.Deleted && p.Skills.Swords.Cap == 100 && ProfessionPowerScrolls.Quote(p, removed) == null, "Removed skill cap bypassed native routing"); removed.Delete();
        var valid = Put(p, new PowerScroll(name2, 105)); valid.Use(p);
        Require(!valid.Deleted && p.Skills[name2].Cap == 100, "Direct native Use bypassed confirmation");
        var ticket = ProfessionPowerScrolls.Quote(p, valid); other.Backpack.DropItem(valid);
        Require(!ProfessionPowerScrolls.Commit(ticket) && !valid.Deleted && p.Skills[name2].Cap == 100, "Traded pending scroll applied to previous owner");
        Require(ProfessionPowerScrolls.Commit(ProfessionPowerScrolls.Quote(other, valid)) && other.Skills[name2].Cap == 105 && valid.Deleted, "Traded scroll could not be used by recipient");
        void Stale(Action<PowerScroll> change, Action reset)
        {
            var scroll = Put(p, new PowerScroll(name2, 105)); var q = ProfessionPowerScrolls.Quote(p, scroll); Require(q != null, "Missing stale fixture quote");
            change(scroll); Require(!ProfessionPowerScrolls.Commit(q) && !scroll.Deleted, "Stale scroll confirmation committed"); reset(); scroll.Delete();
        }
        Stale(s => s.Value = 110, () => { }); Stale(s => s.Skill = SkillName.Cooking, () => { });
        Stale(s => s.Amount++, () => { });
        Stale(s => p.Skills[name2].Cap = 110, () => p.Skills[name2].Cap = 100);
        Stale(s => p.Skills[name2].BaseFixedPoint++, () => p.Skills[name2].BaseFixedPoint--);
        var original = p.Skills[name2].Lock;
        Stale(s => p.Skills[name2].SetLockNoRelay(original == SkillLock.Up ? SkillLock.Down : SkillLock.Up), () => p.Skills[name2].SetLockNoRelay(original));
        Stale(s => p.Frozen = true, () => p.Frozen = false); Stale(s => p.Paralyzed = true, () => p.Paralyzed = false);
        var first = Put(p, new PowerScroll(name2, 105)); var second = Put(p, new PowerScroll(name2, 105));
        var firstQuote = ProfessionPowerScrolls.Quote(p, first); var secondQuote = ProfessionPowerScrolls.Quote(p, second);
        Require(ProfessionPowerScrolls.Commit(firstQuote) && !ProfessionPowerScrolls.Commit(secondQuote) && !second.Deleted
            && p.Skills[name2].Cap == 105, "Concurrent confirmations consumed a duplicate or granted another step"); second.Delete(); p.Skills[name2].Cap = 100;
        var banked = new PowerScroll(name2, 105); p.BankBox.DropItem(banked); Require(ProfessionPowerScrolls.Quote(p, banked) == null, "Remote/banked use allowed"); banked.Delete();
        p.Skills.Mining.Cap = 123; var lower = Put(p, new PowerScroll(SkillName.Mining, 120));
        Require(ProfessionPowerScrolls.Quote(p, lower) == null && p.Skills.Mining.Cap == 123, "Legacy staff cap was reset"); lower.Delete();
        p.AccessLevel = AccessLevel.GameMaster; var native = Put(p, new PowerScroll(name2, 120)); native.Use(p);
        Require(native.Deleted && p.Skills[name2].Cap == 120, "Non-class/staff native scroll behavior changed"); p.Delete(); other.Delete();
    }
    private static void Check()
    {
        Drops(); UsesAndGuards();
        var p = Player(); foreach (var name in ProfessionPowerScrolls.Skills) { p.Skills[name].Base = 100; }
        var skill = p.Skills.Alchemy; skill.SetLockNoRelay(SkillLock.Up); SkillCheck.Gain(p, skill);
        Require(skill.Base == 100, "Native gain exceeded old cap before unlocking");
        for (int step = 0; step < 4; step++)
        {
            var boss = Boss(96); boss.LastKiller = p; boss.MoveToWorld(new(p.X + 1, p.Y, p.Z), p.Map);
            var previous = ClassLootHooks.CreatureDied;
            try { ClassLootHooks.CreatureDied = (creature, container) => RewardLife.Process(creature, container, new ChainRoll(step)); boss.Kill(); }
            finally { ClassLootHooks.CreatureDied = previous; }
            var corpse = boss.Corpse as Corpse; Require(corpse != null && corpse.SanctuaryNoRewards, "Native boss death did not use shared reward claim");
            var scroll = corpse.Items.OfType<PowerScroll>().Single(); Require(scroll.Skill == SkillName.Alchemy && scroll.Value == 105 + 5 * step, "Native boss reward did not enter cap ladder");
            int before = skill.BaseFixedPoint; Put(p, scroll);
            Require(ProfessionPowerScrolls.Commit(ProfessionPowerScrolls.Quote(p, scroll)) && skill.BaseFixedPoint == before, "Looted scroll granted trained points");
            for (int gain = 0; gain < 50; gain++) { SkillCheck.Gain(p, skill); }
            Require(skill.BaseFixedPoint == skill.CapFixedPoint && skill.Cap == 105 + 5 * step, "Native gains did not reach unlocked cap independently of aggregate cap");
            int count = corpse.Items.Count; Require(!RewardLife.Process(boss, corpse, new ChainRoll(step)) && corpse.Items.Count == count, "Boss scroll reward replayed"); corpse.Delete(); boss.Delete();
        }
        Require(p.Skills.Total > p.Skills.Cap && p.Skills.Cooking.Base == 100 && p.Skills.Cooking.Cap == 100, "Aggregate or other craft changed through unlocks/gains");
        string recipe = RareRecipes.Alchemy(AlchemyRestoratives.Recipes.Single(r => r.Skill == 120)); var formula = Put(p, new RareRecipeScroll(recipe)); formula.OnDoubleClick(p);
        var protection = AlchemyRestoratives.Recipes.Single(r => r.Skill == 120); var tool = Put(p, new MortarPestle(10)); AlchemyChecks.Inputs(p, protection);
        CraftFacilities.Register(new(SkillName.Alchemy, p.Map.Name, p.X + 1, p.Y, p.Z, 0xB4A));
        var craft = AlchemyRestoratives.Quote(p, tool, protection);
        Require(craft != null && CraftTransactions.Complete(AlchemyChecks.Ticket(craft), new AlchemyChecks.Roll(0)), "Boss scroll → normal training → learned skill-120 recipe failed");
        ClassProfessions.Profiles.Select(p, SkillName.Cooking);
        Require(p.Skills.Alchemy.Cap == 120 && p.Skills.Alchemy.Base == 120, "Inactive craft lost unlocked cap/training");
        var savedBoss = Boss(); var savedCorpse = GearDropChecks.Body(savedBoss, p); RewardLife.Process(savedBoss, savedCorpse, new ChainRoll(0));
        var savedScroll = savedCorpse.Items.OfType<PowerScroll>().Single();
        File.WriteAllText(Filepath, JsonSerializer.Serialize(new Saved(p.Serial.Value,
            Enumerable.Range(0, p.Skills.Length).Select(i => p.Skills[i].BaseFixedPoint).ToArray(),
            Enumerable.Range(0, p.Skills.Length).Select(i => p.Skills[i].CapFixedPoint).ToArray(),
            Enumerable.Range(0, p.Skills.Length).Select(i => (int)p.Skills[i].Lock).ToArray(), savedScroll.Serial.Value, savedCorpse.Serial.Value, recipe)));
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Filepath)); var p = World.FindMobile((Serial)saved.Player) as PlayerMobile;
        Require(p != null && Enumerable.Range(0, p.Skills.Length).All(i => p.Skills[i].BaseFixedPoint == saved.Bases[i]
            && p.Skills[i].CapFixedPoint == saved.Caps[i] && (int)p.Skills[i].Lock == saved.Locks[i])
            && ClassProfessions.Profiles.Find(p).Craft == SkillName.Cooking && RareRecipes.Profiles.Knows(p, saved.Recipe), "Caps/trained skill/locks/inactive craft/recipe knowledge changed on reload");
        var scroll = World.FindItem((Serial)saved.Scroll) as PowerScroll; var corpse = World.FindItem((Serial)saved.Corpse) as Corpse;
        Require(scroll is { Skill: SkillName.Alchemy, Value: 105, LootType: LootType.Regular } && scroll.Parent == corpse
            && corpse is { SanctuaryNoRewards: true } && scroll.Origin == ItemOrigin.Drop, "Saved dropped/tradable scroll or claimed boss changed");
    }
}
