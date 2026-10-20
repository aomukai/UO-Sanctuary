using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary.Integration;

public static class CombatPotionChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CombatPotionChecks));
    private sealed record Saved(uint Item, int Tier, uint Parent, uint Owner, string Id, int Magnitude, DateTime Expiry);
    private static string Manifest => Path.Join(Core.BaseDirectory, "combat-potions.json");
    public static void Configure() { Register("combat-potion-check", Check); Register("combat-potion-reload-check", Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Combat potion recipes, consumers and persistence", _ => Core.LoopContext.Post(() =>
    { try { action(); _logger.Information("{Command}: passed", name); } catch (Exception e) { _logger.Error(e, "{Command}: failed", name); } }));
    private static void Require(bool v, string why) => RuneChecks.Require(v, why);
    private static void Check()
    {
        Recipes(); Drinking(); Damage(); Leech();
        _logger.Information("Combat potion coverage: five exact recipes/mastery/failure losses, belt/single-slot/90s/death, real spell/weapon damage, MDR/protection/poison offline boundaries, three-difficulty leech and saved identities passed");
    }
    private static void Recipes()
    {
        var saved = new List<Saved>();
        foreach (var r in AlchemyRestoratives.Recipes.Where(r => r.Kind == RestorationKind.CombatBuff))
        {
            var p = AlchemyChecks.Player(); var tool = new MortarPestle(100); p.Backpack.DropItem(tool); AlchemyChecks.Inputs(p, r, 3);
            p.Skills.Alchemy.Base = r.Skill - .1; Require(AlchemyRestoratives.Quote(p, tool, r) == null, "Recipe ignored trained requirement");
            p.Skills.Alchemy.Base = 120; var q = AlchemyRestoratives.Quote(p, tool, r);
            double expected = r.Skill == 120 ? 1 : Math.Min(1, (120 - r.Skill) / 50);
            Require(q != null && Math.Abs(q.Chance - expected) < 1e-9, "Wrong recipe chance/mastery");
            Require(CraftTransactions.Complete(AlchemyChecks.Ticket(q), new AlchemyChecks.Roll(r.Skill == 120 ? .999999 : 0)), "Success rejected");
            var output = p.Backpack.Items.OfType<RestorationPotion>().Single();
            Require(output.Kind == r.Kind && output.Tier == r.Tier && output.Amount == 1 && output.Origin == ItemOrigin.Crafted
                && output.PlayerConstructed && tool.UsesRemaining == 99 && p.Backpack.GetAmount(typeof(Bottle)) == 2, "Wrong output/tool/bottle");
            foreach (var input in r.Reagents) { Require(p.Backpack.GetAmount(EquipmentCrafting.Resolve(input.Type)) == 2 * input.Amount, "Wrong reagent success cost"); }
            if (r.Skill != 120)
            {
                p.Skills.Alchemy.Base = r.Skill + 10; q = AlchemyRestoratives.Quote(p, tool, r);
                Require(q != null && CraftTransactions.Complete(AlchemyChecks.Ticket(q), new AlchemyChecks.Roll()), "Failure rejected");
                Require(tool.UsesRemaining == 98 && p.Backpack.GetAmount(typeof(Bottle)) == 1
                    && p.Backpack.Items.OfType<RestorationPotion>().Sum(i => i.Amount) == 1, "Failure gave output or wrong tool/bottle loss");
                foreach (var input in r.Reagents) { Require(p.Backpack.GetAmount(EquipmentCrafting.Resolve(input.Type)) == 2 * input.Amount - (input.Amount + 1) / 2, "Failure not half each reagent rounded up"); }
            }
            Require(CombatPotions.Apply(p, r.Tier), "Saved buff rejected");
            var buff = TimedEffects.Buff(p, TimedEffectCategory.Potion);
            saved.Add(new(output.Serial.Value, output.Tier, p.Backpack.Serial.Value, p.Serial.Value, buff.Id, buff.Magnitude, buff.ExpiresUtc));
        }
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
    }
    private static void Drinking()
    {
        var p = AlchemyChecks.Player(); p.Hits = p.HitsMax; p.Mana = p.ManaMax;
        TimedEffects.ApplyBuff(p, TimedEffectCategory.Food, "meal", 4);
        var food = TimedEffects.Buff(p, TimedEffectCategory.Food);
        for (int tier = 1; tier <= 5; tier++)
        {
            var potion = AlchemyChecks.Potion(p, RestorationKind.CombatBuff, tier);
            Require(PotionBelts.Load(p, 0, potion) && PotionBelts.Use(p, 0) && potion.Deleted, "Full-pool combat potion belt rejected");
            Require(TimedEffects.Buff(p, TimedEffectCategory.Food) == food && TimedEffects.Snapshot().Single(s => s.Owner == p.Serial.Value).Buffs.Length == 2,
                "Combat buff stacked with another potion or displaced food");
        }
        TimedEffects.Clear(p); DateTime now = TimedEffects.Now;
        Require(CombatPotions.Apply(p, 1, now) && CombatPotions.Apply(p, 1, now.AddSeconds(18))
            && TimedEffects.Buff(p, TimedEffectCategory.Potion, now).ExpiresUtc == now.AddSeconds(60), "Repeated buff not +30s");
        Require(CombatPotions.Apply(p, 1, now.AddSeconds(18)) && CombatPotions.Apply(p, 1, now.AddSeconds(18))
            && !CombatPotions.Apply(p, 1, now.AddSeconds(18))
            && TimedEffects.Buff(p, TimedEffectCategory.Potion, now).ExpiresUtc == now.AddSeconds(108), "90-second remaining cap wrong");
        Require(CombatPotions.Apply(p, 2, now.AddSeconds(18)) && TimedEffects.Buff(p, TimedEffectCategory.Potion, now).ExpiresUtc == now.AddSeconds(48), "Replacement carried old duration");
        TimedEffects.Clear(p); CombatPotions.Apply(p, 1);
        Require(CombatPotions.RunBonus(p) == ClassGear.RunBonus(p) + 30 && ClassCombatHooks.RunningDelay(p, 200) == 154
            && ClassStats.Snapshot(p)[(int)StatField.RunBonus] == 30, "Second Wind server delay/status wrong");
        ClassProfessions.Profiles.Select(p, SkillName.Tinkering); Require(CombatPotions.RunBonus(p) == 30, "Craft change removed shared buff");
        TimedEffects.Settle(p, now.AddMinutes(3)); Require(CombatPotions.RunBonus(p) == 0, "Offline expiry retained bonus");
        TimedEffects.Clear(p); CombatPotions.Apply(p, 5); p.Kill(); Require(CombatPotions.Bonus(p, "protection") == 0 && TimedEffects.Buff(p, TimedEffectCategory.Potion) == null, "Death retained potion");
    }
    private static void Damage()
    {
        var p = AlchemyChecks.Player(); var plain = ClassStats.PhysicalDamageRange(p);
        CombatPotions.Apply(p, 2); var buffed = ClassStats.PhysicalDamageRange(p);
        Require(buffed.Min == plain.Min + 10 && buffed.Max == plain.Max + 10, "Deep Wounds physical range wrong");
        TimedEffects.Clear(p); CombatPotions.Apply(p, 3);
        var dummy = new TimedEffectDummy(); dummy.MoveToWorld(p.Location, p.Map); dummy.Hits = dummy.HitsMax;
        int before = dummy.Hits; SpellHelper.Damage(TimeSpan.Zero, dummy, p, 100, 0, 100, 0, 0, 0);
        Require(before - dummy.Hits == 140 && GearEffects.ChillPercent(dummy) == 0
            && !TimedEffects.Snapshot().Any(s => s.Owner == dummy.Serial.Value && s.PoisonMs > 0), "Typed spell bonus/status wrong");
        before = dummy.Hits; AOS.Damage(dummy, p, 100, false, 0, 100, 0, 0, 0, weaponDamage: true);
        Require(before - dummy.Hits == 100, "Weapon acquired spell bonus");
        Require(CombatPotions.TeethDamage(p, dummy, 100, 0) == 140, "Teeth missing four components");
        before = dummy.Hits; SpellHelper.Damage(TimeSpan.Zero, dummy, p, 100);
        Require(before - dummy.Hits == 140, "Direct spell bonus wrong");
        TimedEffects.Clear(p); before = dummy.Hits; SpellHelper.Damage(TimeSpan.Zero, dummy, p, 100, 0, 100, 0, 0, 0);
        Require(before - dummy.Hits == 100, "Expired spell bonus survived");
        var target = AlchemyChecks.Player(); var helm = RuneSocketChecks.Host(target, typeof(PlateHelm));
        GemActivationChecks.Install(target, GemActivationChecks.Gem(target, "StarSapphire"), helm); target.AddItem(helm); ClassGear.Refresh(target);
        CombatPotions.Apply(target, 5); CombatPotions.Apply(p, 3);
        Require(CombatPotions.SpellDamage(target, p, 100, 0, 100, 0, 0, 0) == 125, "MDR not once per combined element");
        var friendly = ClassCombatHooks.Friendly;
        try
        {
            // Isolate mitigation from the separate friendly-fire policy in this disposable fixture.
            ClassCombatHooks.Friendly = (_, _) => false;
            target.Hits = target.HitsMax; before = target.Hits;
            SpellHelper.Damage(TimeSpan.Zero, target, p, 100, 0, 100, 0, 0, 0);
            Require(before - target.Hits == 115, "Protection not once after combined spell/MDR");
        }
        finally { ClassCombatHooks.Friendly = friendly; }
        var ogre = new Ogre(); ogre.MoveToWorld(target.Location, target.Map);
        target.Hits = target.HitsMax; before = target.Hits; AOS.Damage(target, ogre, 100, 0, 100, 0, 0, 0);
        Require(before - target.Hits == 85, "Protection/MDR ordering wrong");
        before = target.Hits; target.Damage(5, ogre); Require(target.Hits == before, "Protection minimum not zero");
        TimedEffects.Clear(target); DateTime start = TimedEffects.Now.AddSeconds(-32);
        CombatPotions.Apply(target, 5, start); TimedEffects.Poison(ogre, target, 1600, 32000, start);
        target.Hits = target.HitsMax; before = target.Hits;
        TimedEffects.Settle(target, start.AddSeconds(32));
        // 5 damage per 100ms: ticks before30s are blocked; the expiry tick and following20 ticks land.
        Require(before - target.Hits == 105, $"Offline poison ignored historical Protection expiry: {before} -> {target.Hits}; active={CombatPotions.Bonus(target, "protection")}");
        Require(TimedEffects.Buff(target, TimedEffectCategory.Potion) == null, "Protection survived offline expiry");
        dummy.Delete(); ogre.Delete(); helm.Delete(); TimedEffects.Clear(target); TimedEffects.Clear(p);
    }
    private static void Leech()
    {
        var p = AlchemyChecks.Player(); p.RawInt = 1000; CombatPotions.Apply(p, 4);
        foreach (var difficulty in Enum.GetValues<MonsterDifficulty>())
        {
            var zombie = new Zombie(); MonsterVariants.Assign(zombie, difficulty);
            p.Hits = p.HitsMax - 100; p.Mana = p.ManaMax - 100; int hp = p.Hits, mana = p.Mana;
            int divisor = difficulty == MonsterDifficulty.Normal ? 1 : difficulty == MonsterDifficulty.Nightmare ? 2 : 3;
            int gain = 300 * 7 * GemCombat.Drain(zombie) / (10000 * divisor);
            GemCombat.Leech(p, zombie, 300); Require(p.Hits == hp + gain && p.Mana == mana + gain, "Stealing drain/difficulty wrong"); zombie.Delete();
        }
        var skeleton = new Skeleton(); int oldHp = p.Hits, oldMana = p.Mana; GemCombat.Leech(p, skeleton, 1000);
        Require(p.Hits == oldHp && p.Mana == oldMana, "Stealing bypassed zero drain"); skeleton.Delete(); TimedEffects.Clear(p);
    }
    private static void Reload()
    {
        foreach (var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var item = World.FindItem((Serial)s.Item) as RestorationPotion;
            Require(item != null && item.Kind == RestorationKind.CombatBuff && item.Tier == s.Tier && item.Amount == 1
                && item.Origin == ItemOrigin.Crafted && item.PlayerConstructed && (item.Parent as Item)?.Serial.Value == s.Parent,
                "Crafted combat potion changed on reload");
            var state = TimedEffects.Snapshot().FirstOrDefault(r => r.Owner == s.Owner);
            if (s.Expiry > TimedEffects.Now)
            { Require(state?.Buffs.Any(b => b.Id == s.Id && b.Magnitude == s.Magnitude && b.ExpiresUtc == s.Expiry) == true, "Saved buff changed expiry/magnitude"); }
            else { Require(TimedEffects.Buff(World.FindMobile((Serial)s.Owner), TimedEffectCategory.Potion) == null, "Reload restarted expired buff"); }
        }
    }
}
