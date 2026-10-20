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
using Server.Misc;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ProfessionChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(ProfessionChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "profession-checks.json");
    private sealed record Saved(uint Owner, SkillName Craft, bool Confirmed, int[] Bases, int[] Caps, byte[] Locks, int TotalCap, uint Tool, int Uses);
    private sealed class Roll(int value) : Random { public override int Next(int maxValue) => Math.Min(value, maxValue - 1); }
    private static readonly Func<BaseTool>[] _tools = [() => new MortarPestle(50), () => new SmithHammer(50),
        () => new FletcherTools(50), () => new Saw(50), () => new MapmakersPen(50), () => new Skillet(50),
        () => new ScribesPen(50), () => new SewingKit(50), () => new TinkerTools(50), () => new ImbuingLadle()];
    private static readonly Type[] _inputs = [typeof(Ginseng), typeof(IronIngot), typeof(Log), typeof(Board),
        typeof(BlankScroll), typeof(RawFishSteak), typeof(BlankScroll), typeof(Cloth), typeof(IronIngot), typeof(MagicEssence)];
    public static void Configure()
    {
        Register("profession-check", Setup);
        Register("profession-reload-check", Reload);
    }
    private static void Register(string command, Action check) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable profession selection/training check", _ => Core.LoopContext.Post(() =>
        {
            try { check(); _logger.Information("{Command}: passed", command); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }
    private static PlayerMobile Player(string name)
    {
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = name, RawStr = 200 };
        new Account(name.ToLowerInvariant(), Guid.NewGuid().ToString("N"))[0] = player;
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        player.RawStr = 200;
        player.AddItem(new Backpack());
        player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5550, 1150), Map.Trammel);
        return player;
    }
    private static void Ask(PlayerMobile player, BaseTool tool, bool confirm)
    {
        ClassProfessions.Request(player, tool.CraftSystem.MainSkill, tool, true);
        var pending = ClassProfessions.Pending(player);
        Require(pending != null, "Tool did not request first-profession confirmation");
        Require(ClassProfessions.Respond(player, pending.Token, confirm) == confirm, "Confirmation result differed");
    }
    private static void Setup()
    {
        Require(ClassProfessions.Enabled && ClassProfessions.GoldPerTenth == 1, "Explicit test-world settings missing");
        Require(TradeSkills.Crafting.Length == 10 && TradeSkills.IsCraft((int)SkillName.Cartography), "Ten-craft catalogue wrong");
        var floor = CombatPractice.FindFloor(Map.Trammel, 5550, 1150);
        new Static(4015).MoveToWorld(floor, Map.Trammel);
        new Static(4017).MoveToWorld(floor, Map.Trammel);
        var saved = new List<Saved>();
        for (int i = 0; i < _tools.Length; i++)
        {
            var player = Player("Profession" + i);
            player.Skills.Focus.BaseFixedPoint = 51;
            player.Skills.Focus.SetLockNoRelay(SkillLock.Locked);
            player.Skills.Mining.Cap = 105;
            var initial = ClassProfessions.Profiles.Find(player);
            Require(initial == new ProfessionState(SkillName.Alchemy, false), "Existing/new default already locked");
            var tool = _tools[i]();
            player.Backpack.DropItem(tool);
            var skill = tool.CraftSystem.MainSkill;
            player.Skills[skill].BaseFixedPoint = 427;
            player.Skills[skill].CapFixedPoint = 1100;
            var trainer = new ProfessionInstructor();
            trainer.MoveToWorld(player.Location, player.Map);
            trainer.AIObject.AITimer.Stop();
            Ask(player, tool, false);
            Require(ClassProfessions.Profiles.Find(player) == initial && tool.UsesRemaining > 0, "Cancel changed profession or used tool");
            ClassProfessions.Request(player, tool.CraftSystem.MainSkill, tool, true);
            var stale = ClassProfessions.Pending(player);
            ClassProfessions.Request(player, tool.CraftSystem.MainSkill, tool, true);
            var latest = ClassProfessions.Pending(player);
            Require(!ClassProfessions.Respond(player, stale.Token, true), "Stale prompt locked profession");
            tool.Internalize();
            Require(!ClassProfessions.Respond(player, latest.Token, true), "Missing tool locked profession");
            player.Backpack.DropItem(tool);
            if (i == 0)
            {
                Require(!trainer.Teach(skill, player, 0, false), "Trainer bypassed first-choice confirmation");
                var pending = ClassProfessions.Pending(player);
                Require(pending != null && pending.Trainer == trainer && ClassProfessions.Respond(player, pending.Token, true),
                    "Trainer confirmation did not lock the requested craft");
            }
            else { Ask(player, tool, true); }
            Require(ClassProfessions.Profiles.Find(player) == new ProfessionState(skill, true), "Wrong selected craft");
            Require(player.Skills.Focus.BaseFixedPoint == 51 && player.Skills.Mining.Cap == 105, "Selection changed existing skills/caps");
            Require(player.Skills[skill].BaseFixedPoint == 427 && player.Skills[skill].CapFixedPoint == 1100,
                "Selection reset existing craft progress or cap");
            var other = skill == SkillName.Tailoring ? new MortarPestle() as BaseTool : new SewingKit();
            player.Backpack.DropItem(other);
            Require(!ClassProfessions.Request(player, other.CraftSystem.MainSkill, other, true)
                && ClassProfessions.Profiles.Find(player).Craft == skill, "Another tool changed the confirmed craft");
            other.Delete();
            Training(player, trainer, skill);
            IndependentGains(player, skill);
            Practice(player, tool, i);
            var retained = _tools[i]();
            retained.UsesRemaining = 17;
            player.Backpack.DropItem(retained);
            saved.Add(Snapshot(player, retained));
            trainer.Delete();
        }
        var gatherer = Player("ProfessionGatherer");
        var gatheringTrainer = new ProfessionInstructor();
        gatheringTrainer.MoveToWorld(gatherer.Location, gatherer.Map);
        gatheringTrainer.AIObject.AITimer.Stop();
        foreach (byte index in TradeSkills.Gathering)
        {
            var skill = (SkillName)index;
            Training(gatherer, gatheringTrainer, skill);
            Require(!ClassProfessions.Profiles.Find(gatherer).Confirmed, "Gathering training selected a craft");
            Require(ClassSkills.CanGain(gatherer, skill), "Gathering was disabled");
        }
        Require(!gatheringTrainer.Teach(SkillName.Magery, gatherer, 0, false), "Unrelated skill became trainable");
        saved.Add(Snapshot(gatherer, null));
        gatheringTrainer.Delete();
        BypassesAndExceptions();
        Serialization();
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
        _logger.Information("Profession coverage: ten first choices, thirteen trainer skills, forty practice bands, independent gains, native bypasses and persisted selections");
    }
    private static void Training(PlayerMobile player, ProfessionInstructor trainer, SkillName skillName)
    {
        var skill = player.Skills[skillName];
        skill.Base = 0;
        Require(trainer.Teach(skillName, player, 0, false), "Trainer did not offer active/gathering skill");
        var partial = new Gold(137);
        Require(trainer.OnGoldGiven(player, partial) && partial.Deleted && skill.BaseFixedPoint == 137, "Partial payment not exact");
        var stale = new Gold(10);
        Require(!trainer.OnGoldGiven(player, stale) && !stale.Deleted && skill.BaseFixedPoint == 137, "Stale payment was consumed");
        stale.Delete();
        Require(trainer.Teach(skillName, player, 0, false), "Partial training could not resume");
        int beforeGold = player.Backpack.GetAmount(typeof(Gold));
        var remainder = ItemOrigins.Create(ItemOrigin.Vendor, () => new Gold(1000));
        Require(trainer.OnGoldGiven(player, remainder) && skill.BaseFixedPoint == 300
            && player.Backpack.GetAmount(typeof(Gold)) == beforeGold + 837
            && player.Backpack.Items.OfType<Gold>().All(g => g.Origin == ItemOrigin.Vendor),
            $"Training cap, exact cost or returned excess gold was wrong: {skillName}");
        Require(!trainer.Teach(skillName, player, 0, false), "Trainer taught above30");
    }
    private static void IndependentGains(PlayerMobile player, SkillName selected)
    {
        foreach (byte index in TradeSkills.Crafting)
        {
            player.Skills[(SkillName)index].Base = 100;
            player.Skills[(SkillName)index].SetLockNoRelay(SkillLock.Down);
        }
        var active = player.Skills[selected];
        active.Base = 30; active.SetLockNoRelay(SkillLock.Up);
        int[] before = Enumerable.Range(0, player.Skills.Length).Select(id => player.Skills[id]).Select(s => s.BaseFixedPoint).ToArray();
        Require(player.Skills.Total > player.Skills.Cap, "Fixture is not over aggregate cap");
        SkillCheck.Gain(player, active);
        Require(active.BaseFixedPoint == 301, "Total cap blocked independent gain");
        for (int i = 0; i < before.Length; i++)
        {
            if (i != (int)selected) { Require(player.Skills[i].BaseFixedPoint == before[i], "Independent gain erased another skill"); }
        }
        var inactive = player.Skills[selected == SkillName.Tailoring ? SkillName.Alchemy : SkillName.Tailoring];
        inactive.SetLockNoRelay(SkillLock.Up);
        inactive.Base = 30;
        SkillCheck.Gain(player, inactive);
        Require(inactive.BaseFixedPoint == 300, "Inactive crafting gained through SkillCheck.Gain");
        foreach (byte index in TradeSkills.Crafting) { player.Skills[(SkillName)index].SetLockNoRelay(SkillLock.Up); }
    }
    private static void Practice(PlayerMobile player, BaseTool tool, int index)
    {
        var name = tool.CraftSystem.MainSkill;
        var skill = player.Skills[name];
        int originalCap = skill.CapFixedPoint;
        var input = _inputs[index].CreateInstance<Item>();
        input.Amount = 100;
        player.Backpack.DropItem(input);
        tool.UsesRemaining = 20;
        for (int band = 0; band < 4; band++)
        {
            skill.BaseFixedPoint = 300 + band * 100;
            Require(ProfessionPractice.Chance(skill.BaseFixedPoint) == 100, "Band does not start at100%");
            player.NextSkillTime = Core.TickCount;
            int before = input.Amount, uses = tool.UsesRemaining;
            Require(ProfessionPractice.Attempt(player, tool, band, out bool gained, new Roll(99)) && gained
                && skill.BaseFixedPoint == 301 + band * 100 && input.Amount < before && tool.UsesRemaining == uses - 1,
                $"Practice start, exact0.1 gain or consumption failed: {name}, band {band}");
            Require(!ProfessionPractice.Attempt(player, tool, band, out _), "Practice cooldown was bypassed");
            skill.BaseFixedPoint = 399 + band * 100;
            Require(ProfessionPractice.Chance(skill.BaseFixedPoint) == 1, "Band does not end at1%");
            player.NextSkillTime = Core.TickCount;
            before = input.Amount;
            Require(ProfessionPractice.Attempt(player, tool, band, out gained, new Roll(99)) && !gained && input.Amount < before,
                "No-gain practice did not consume inputs");
            player.NextSkillTime = Core.TickCount;
            Require(ProfessionPractice.Attempt(player, tool, band, out gained, new Roll(0)) && gained
                && skill.BaseFixedPoint == 400 + band * 100, "Practice could not finish the band");
            player.NextSkillTime = Core.TickCount;
            before = input.Amount; uses = tool.UsesRemaining;
            Require(!ProfessionPractice.Attempt(player, tool, band, out _) && input.Amount == before && tool.UsesRemaining == uses,
                "Stale practice band consumed inputs");
        }
        Require(skill.BaseFixedPoint == 700 && ProfessionPractice.Chance(700) == 0, "Practice exceeded70");
        skill.BaseFixedPoint = 300; skill.CapFixedPoint = 300;
        Require(!ProfessionPractice.Attempt(player, tool, 0, out _), "Practice ignored per-skill cap");
        skill.CapFixedPoint = originalCap;
        skill.SetLockNoRelay(SkillLock.Locked);
        Require(!ProfessionPractice.Attempt(player, tool, 0, out _), "Practice ignored skill lock");
        skill.SetLockNoRelay(SkillLock.Up);
        input.Delete();
        Require(!ProfessionPractice.Attempt(player, tool, 0, out _), "Missing inputs permitted practice");
        tool.Delete();
    }
    private static void BypassesAndExceptions()
    {
        var player = Player("ProfessionBypasses");
        var mortar = new MortarPestle(); player.Backpack.DropItem(mortar); Ask(player, mortar, true);
        var sewing = new SewingKit(50); player.Backpack.DropItem(sewing);
        player.Skills.Tailoring.Base = 100;
        var cloth = new Cloth(100); player.Backpack.DropItem(cloth);
        var system = DefTailoring.CraftSystem;
        var recipe = system.CraftItems.Single(r => r.ItemType == typeof(Shirt));
        recipe.Craft(player, system, null, sewing);
        recipe.Craft(player, system, null, sewing, 0);
        recipe.CompleteCraft(1, false, player, system, null, sewing, null);
        recipe.CompleteCraft(1, false, player, system, null, sewing, null, 0);
        Require(cloth.Amount == 100 && sewing.UsesRemaining == 50 && !player.Backpack.Items.Any(i => i is Shirt)
            && player.CanBeginAction<CraftSystem>(), "A native craft path bypassed the active profession");
        recipe.GetSuccessChance(player, null, system, true, out bool allowed);
        Require(!allowed && player.Skills.Tailoring.Base == 100, "Native gain path bypassed selection");
        Repair.Do(player, system, sewing);
        Require(player.Target == null, "Inactive repair opened a target");
        TextDefinition message = null;
        Require(Enhance.Invoke(player, system, sewing, new LeatherCap(), CraftResource.SpinedLeather, typeof(SpinedLeather), ref message)
            == EnhanceResult.None, "Inactive enhancement was permitted");
        var white = new Longsword(); player.Backpack.DropItem(white);
        Require(EquipmentEconomy.Quote(player, white, EssenceAction.Imbue) == null, "Prototype Imbuing bypassed activation");
        var salvageHammer = new SmithHammer(); player.Backpack.DropItem(salvageHammer);
        Require(EquipmentEconomy.Quote(player, white, EssenceAction.Salvage) != null, "Salvage was craft-locked");
        salvageHammer.Delete();
        var logs = new Log(5); player.Backpack.DropItem(logs);
        Require(logs.Axe(player, null) && player.Backpack.GetAmount(typeof(Board)) == 5,
            "Basic board processing required selected Carpentry");
        var boardRecipe = new CraftItem(typeof(Board), "processing", "boards");
        Require(TradeSkillHooks.CheckRecipe(player, DefCarpentry.CraftSystem, boardRecipe, null), "Processing recipe was blocked");
        var instructor = new ProfessionInstructor(); instructor.MoveToWorld(player.Location, player.Map);
        Require(!instructor.Teach(SkillName.Tailoring, player, 0, false), "Inactive trainer bypassed selection");
        var gift = new Gold(300);
        Require(!instructor.OnGoldGiven(player, gift) && gift.Amount == 300 && !gift.Deleted, "Unquoted trainer payment became a gift");
        gift.Delete(); instructor.Delete(); player.Delete();
        var staff = new PlayerMobile { AccessLevel = AccessLevel.Owner };
        staff.SkillsCap = 7000;
        Require(!ClassProfessions.Affected(staff), "Legacy staff became profession-restricted");
        staff.Delete();
    }
    private static byte[] Serialize()
    {
        var writer = new BufferWriter(new byte[65536], true);
        ClassProfessions.Profiles.Serialize(writer);
        return writer.Buffer[..(int)writer.Position];
    }
    private static void Serialization()
    {
        byte[] before = Serialize();
        try
        {
            ClassProfessions.Profiles.Deserialize(new BufferReader(new byte[8]));
            var legacy = new PlayerMobile();
            legacy.Skills.Focus.BaseFixedPoint = 51;
            Require(ClassProfessions.Profiles.Find(legacy) == new ProfessionState(SkillName.Alchemy, false)
                && legacy.Skills.Focus.BaseFixedPoint == 51, "Empty legacy selection changed skills or default");
            legacy.Delete();
        }
        finally { ClassProfessions.Profiles.Deserialize(new BufferReader(before)); }
    }
    private static Saved Snapshot(PlayerMobile player, BaseTool tool)
    {
        var state = ClassProfessions.Profiles.Find(player);
        return new(player.Serial.Value, state.Craft, state.Confirmed, Enumerable.Range(0, player.Skills.Length).Select(id => player.Skills[id]).Select(s => s.BaseFixedPoint).ToArray(),
            Enumerable.Range(0, player.Skills.Length).Select(id => player.Skills[id]).Select(s => s.CapFixedPoint).ToArray(),
            Enumerable.Range(0, player.Skills.Length).Select(id => (byte)player.Skills[id].Lock).ToArray(),
            player.Skills.Cap, tool?.Serial.Value ?? 0, tool?.UsesRemaining ?? 0);
    }
    private static void Reload()
    {
        ProfessionUiChecks.Reload();
        foreach (var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var player = World.FindMobile((Serial)saved.Owner) as PlayerMobile;
            Require(player != null && ClassProfessions.Profiles.Find(player) == new ProfessionState(saved.Craft, saved.Confirmed)
                && Enumerable.Range(0, player.Skills.Length).Select(id => player.Skills[id]).Select(s => s.BaseFixedPoint).SequenceEqual(saved.Bases)
                && Enumerable.Range(0, player.Skills.Length).Select(id => player.Skills[id]).Select(s => s.CapFixedPoint).SequenceEqual(saved.Caps) && player.Skills.Cap == saved.TotalCap,
                "Saved selection, independent skill values or caps changed on reload");
            Require(Enumerable.Range(0, player.Skills.Length).Select(id => (byte)player.Skills[id].Lock).SequenceEqual(saved.Locks),
                "Saved skill locks changed on reload");
            if (saved.Tool != 0)
            {
                Require(World.FindItem((Serial)saved.Tool) is BaseTool tool && tool.UsesRemaining == saved.Uses
                    && tool.IsChildOf(player.Backpack), "Saved native/custom tool changed on reload");
            }
        }
    }
}
