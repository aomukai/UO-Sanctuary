using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ImbuingChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(ImbuingChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "imbuing-checks.json");
    private sealed record Saved(uint Item, string State, NativeGearDetails Native, string Name, ItemOrigin Origin);
    private sealed class Roll(int first = 99, double success = 0) : Random
    {
        private bool _first = true;
        public override int Next(int max) { int value = _first ? first : 0; _first = false; return Math.Min(value, max - 1); }
        public override int Next(int min, int max) => min;
        public override double NextDouble() => success;
    }
    public static void Configure()
    { Register("imbuing-check", Check); Register("imbuing-reload-check", Reload); }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable tiered Imbuing and jewellery checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", command); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    internal static void Require(bool value, string message) { if (!value) { throw new InvalidOperationException(message); } }
    internal static PlayerMobile Player(SkillName craft)
    {
        var p = new PlayerMobile { Body = 0x190, Player = true, Name = "TieredImbuing" };
        ClassCreation.Profiles.Assign(p, DiabloClass.Necromancer); ClassCreation.Profiles.AdvanceLevel(p, 99);
        p.AddItem(new Backpack()); p.RawStr = p.RawDex = 1000;
        FixtureSpace.Place(p,new Point3D(5550, 1151, 0), Map.Trammel);
        CraftFacilities.Register(new(craft,"Trammel",p.X+1,p.Y,p.Z,0xB4A));
        ClassProfessions.Profiles.Confirm(p, craft);
        p.Skills[craft].Cap = 120; p.Skills[craft].Base = 120;
        return p;
    }
    private static T Put<T>(Mobile p, T item) where T : Item { p.Backpack.DropItem(item); return item; }
    internal static EssenceQuote Quote(PlayerMobile p, Item item) => EquipmentEconomy.Quote(p, item, EssenceAction.Imbue);
    private static string State(Item item) => JsonSerializer.Serialize(ClassGear.State(item));
    private static Saved Snapshot(Item item) => new(item.Serial.Value, State(item), GearDetails.Read(item), item.Name, item.Origin);
    private static void Check()
    {
        var saved = new List<Saved>(); var p = Player(SkillName.Imbuing);
        var ladle = Put(p, new ImbuingLadle { UsesRemaining = 100 }); Put(p, new Gold(50000)); Put(p, new MagicEssence(1000));
        foreach (var tier in TieredImbuing.Equipment)
        {
            var row = EquipmentCraftingChecks.Row("Longsword", tier.Tier);
            var item = Put(p, ItemOrigins.Create(ItemOrigin.Crafted, () => new Longsword()));
            EquipmentCrafting.Finish(item, p, row, CraftResource.Verite, 0x89F, CraftQuality.HighQuality, new Random(42));
            item.HitPoints--;
            var before = ClassGear.State(item); var native = GearDetails.Read(item); string name = item.Name;
            p.Skills.Imbuing.Base = tier.Skill - .1;
            Require(Quote(p, item) == null, "Below-tier Imbuing permitted");
            p.Skills.Imbuing.Base = tier.Skill;
            int gold = p.Backpack.GetAmount(typeof(Gold)), essence = p.Backpack.GetAmount(typeof(MagicEssence)), uses = ladle.UsesRemaining;
            var quote = Quote(p, item);
            Require(quote != null && quote.Gold == tier.Gold && quote.Essence == tier.Essence, "Wrong equipment band cost");
            Require(EquipmentEconomy.Commit(p, quote, out _, new Roll()) && !EquipmentEconomy.Commit(p, quote, out _), "Imbuing failed/replayed");
            var after = ClassGear.State(item);
            Require(after.Roll.Rarity == GearRarity.Magic && after.Roll.Affixes.All(a => ClassGear.Implemented(a.Definition))
                && after == (before with { Roll = after.Roll }) && after.Roll.ItemLevel == before.Roll.ItemLevel
                && after.Roll.RequiredLevel >= before.Roll.RequiredLevel && item.Name == name && GearDetails.Read(item) == native,
                "Imbuing changed base/quality/sockets/wear/material/maker/ilvl or enabled unsupported effects");
            Require(p.Backpack.GetAmount(typeof(Gold)) == gold - tier.Gold && p.Backpack.GetAmount(typeof(MagicEssence)) == essence - tier.Essence
                && ladle.UsesRemaining == uses - 1 && Quote(p, item) == null, "Wrong payment/charge or reroll permitted");
            saved.Add(Snapshot(item));
        }
        foreach (var (skill, roll, expected) in new[] { (119.9, 24, GearRarity.Rare), (119.9, 25, GearRarity.Magic),
            (120.0, 25, GearRarity.Rare), (120.0, 29, GearRarity.Rare), (120.0, 30, GearRarity.Magic) })
        {
            p.Skills.Imbuing.Base = skill; var item = Put(p, new Dagger()); ClassGear.Profiles.Convert(item, 35);
            Require(EquipmentEconomy.Commit(p, Quote(p, item), out _, new Roll(roll)) && ClassGear.State(item).Roll.Rarity == expected,
                "Imbuing rarity threshold failed"); saved.Add(Snapshot(item));
        }
        Rejects(p, ladle);
        var crafter = Player(SkillName.Tinkering); var tool = Put(crafter, new TinkerTools(100));
        Require(JewelleryCrafting.Recipes.Length == 10, "Missing jewellery recipes");
        foreach (var row in JewelleryCrafting.Recipes)
        {
            var band = JewelleryCrafting.Band(row); var input = Put(crafter, new IronIngot(band.Ingots + 5));
            Require(EquipmentCrafting.Prepare(row, new DullCopperIngot()) == null, "Coloured jewellery recipe invented");
            crafter.Skills.Tinkering.Base = band.Skill - .1;
            var ticket = EquipmentCraftingChecks.Ticket(crafter, tool, row, input);
            int uses = tool.UsesRemaining;
            Require(!CraftTransactions.Complete(ticket, new Roll()) && input.Amount == band.Ingots + 5 && tool.UsesRemaining == uses,
                "Under-skilled jewellery consumed inputs");
            crafter.Skills.Tinkering.Base = 120;
            ticket = EquipmentCraftingChecks.Ticket(crafter, tool, row, input);
            Require(CraftTransactions.Complete(ticket, new Roll()) && !CraftTransactions.Complete(ticket, new Roll()), "Jewellery failed/replayed");
            var item = crafter.Backpack.Items.Single(i => i.Name == row.Name); var state = ClassGear.State(item);
            Require(state is { CraftedEquipment: true, NativeSockets: 0, TinkerSockets: 0, DurabilityPercent: 0 }
                && state.Roll.Rarity == GearRarity.Normal && state.Roll.ItemLevel == band.ItemLevel && state.Roll.RequiredLevel == band.EquipLevel
                && state.MaterialRolls.Length == 0 && item.Origin == ItemOrigin.Crafted && input.Amount == 5 && tool.UsesRemaining == uses - 1,
                "Jewellery identity/recipe cost/perks wrong");
            p.Backpack.DropItem(item); p.Skills.Imbuing.Base = band.Skill - .1;
            Require(Quote(p, item) == null, "Under-skilled jewellery Imbuing permitted"); p.Skills.Imbuing.Base = band.Skill;
            var quote = Quote(p, item);
            Require(quote is { } && quote.Gold == band.Gold && quote.Essence == band.Essence
                && EquipmentEconomy.Commit(p, quote, out _, new Roll(0)) && ClassGear.State(item).Roll.Rarity == GearRarity.Rare
                && ClassGear.State(item).Roll.RequiredLevel >= band.EquipLevel && ClassGear.State(item).Roll.ItemLevel == band.ItemLevel,
                "Jewellery Imbuing ladder failed");
            // Retuning future grades must not invalidate a previously crafted roll.
            int bandIndex = Array.IndexOf(TieredImbuing.Jewellery, band);
            TieredImbuing.Jewellery[bandIndex] = band with { ItemLevel = Math.Max(1, band.ItemLevel - 1), EquipLevel = Math.Min(99, band.EquipLevel + 1) };
            try { GearProfiles.Validate(item, ClassGear.State(item)); }
            finally { TieredImbuing.Jewellery[bandIndex] = band; }
            saved.Add(Snapshot(item)); input.Delete();
        }
        // A native ring recipe must not provide a cheaper route around the grade book.
        var nativeRecipe = DefTinkering.CraftSystem.CraftItems.SearchFor(typeof(GoldRing));
        Require(nativeRecipe != null && !TradeSkillHooks.RecipeSelected(crafter, DefTinkering.CraftSystem, nativeRecipe, tool), "Native jewellery bypass open");
        var blank = Put(crafter, new IronIngot(20)); var failure = EquipmentCraftingChecks.Ticket(crafter, tool, JewelleryCrafting.Recipes[^1], blank);
        int count = crafter.Backpack.Items.Count, charges = tool.UsesRemaining;
        Require(CraftTransactions.Complete(failure, new Roll(success: 1)) && crafter.Backpack.Items.Count == count
            && blank.Amount < 20 && tool.UsesRemaining == charges - 1, "Jewellery failure did not use native failure costs");
        EffectConsumer(p);
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
    }
    private static void Rejects(PlayerMobile p, ImbuingLadle tool)
    {
        p.Skills.Imbuing.Base = 120; var item = Put(p, new Longsword()); var state = ClassGear.Profiles.Convert(item, 35);
        ClassGear.Profiles.Store(item, state with { Quality = CraftQuality.HighQuality, NativeSockets = 1, TinkerSockets = 1 });
        Require(Quote(p, item) == null, "Mixed native/Tinker sockets allowed"); ClassGear.Profiles.Store(item, state);
        int gold = p.Backpack.GetAmount(typeof(Gold)), essence = p.Backpack.GetAmount(typeof(MagicEssence)), uses = tool.UsesRemaining;
        var old = Quote(p, item); Quote(p, item); Require(!EquipmentEconomy.Commit(p, old, out _), "Replaced quote accepted");
        old = Quote(p, item); item.HitPoints--; Require(!EquipmentEconomy.Commit(p, old, out _), "Wear change accepted");
        old = Quote(p, item); p.Skills.Imbuing.Base = 119.9; Require(!EquipmentEconomy.Commit(p, old, out _), "Skill change accepted"); p.Skills.Imbuing.Base = 120;
        old = Quote(p, item); tool.UsesRemaining--; Require(!EquipmentEconomy.Commit(p, old, out _), "Used ladle accepted"); tool.UsesRemaining++;
        old = Quote(p, item); tool.Internalize(); Require(!EquipmentEconomy.Commit(p, old, out _), "Missing ladle accepted"); p.Backpack.DropItem(tool);
        old = Quote(p, item); ClassProfessions.Profiles.Select(p, SkillName.Tinkering); ClassProfessions.Profiles.Select(p, SkillName.Imbuing);
        Require(!EquipmentEconomy.Commit(p, old, out _), "Switch-away/back accepted");
        old = Quote(p, item); var location = p.Location; p.MoveToWorld(new Point3D(5558, 1180, 0), p.Map);
        Require(!EquipmentEconomy.Commit(p, old, out _), "Remote cauldron accepted"); p.MoveToWorld(location, p.Map);
        old = Quote(p, item); EventSink.InvokeDisconnected(p); EventSink.InvokeConnected(p);
        Require(!EquipmentEconomy.Commit(p, old, out _), "Reconnect accepted old quote");
        Require(ClassGear.State(item).Roll.Rarity == GearRarity.Normal && p.Backpack.GetAmount(typeof(Gold)) == gold
            && p.Backpack.GetAmount(typeof(MagicEssence)) == essence && tool.UsesRemaining == uses, "Rejection charged resources");
    }
    private static void EffectConsumer(PlayerMobile p)
    {
        int before = p.Str, expected = 0;
        foreach (var item in new Item[] { new GoldRing(), new SilverNecklace() })
        {
            var affix = AffixBalance.All.First(a => ClassGear.Implemented(a) && a.Modifiers.Length == 1 && a.Modifiers[0].Code == "str"
                && AffixBalance.Eligible(a, ClassGear.Base(item).Type, 99, GearRarity.Magic));
            expected += affix.Modifiers[0].Max;
            Put(p, item); var state = ClassGear.Profiles.Convert(item, 99); var mod = affix.Modifiers[0];
            var roll = state.Roll with { Rarity = GearRarity.Magic, RequiredLevel = Math.Max(state.Roll.RequiredLevel, affix.RequiredLevel),
                Affixes = [new(affix, [new(mod.Code, mod.Parameter, mod.Min, mod.Max, mod.Mode, mod.Max)])] };
            ClassGear.Profiles.Store(item, state with { Roll = roll }); Require(p.EquipItem(item), "Jewellery cannot equip");
            Require(ClassGear.Active(p, item), "Jewellery slot inactive");
        }
        ClassGear.Refresh(p);
        Require(p.Str == before + expected, "Ring/amulet numerical effect missing");
    }
    private static void Reload()
    {
        foreach (var row in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var item = World.FindItem((Serial)row.Item);
            Require(item != null && State(item) == row.State && GearDetails.Read(item) == row.Native && item.Name == row.Name && item.Origin == row.Origin,
                "Tiered Imbuing/jewellery changed after save/reload");
        }
    }
}
