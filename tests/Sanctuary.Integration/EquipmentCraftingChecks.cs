using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class EquipmentCraftingChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(EquipmentCraftingChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "equipment-crafting-checks.json");
    private sealed record Saved(uint Item, string State, NativeGearDetails Native, string Name, string Type, int Hue, ItemOrigin Origin);
    private sealed class Roll(int quality = 99, double success = 0) : Random
    {
        public override double NextDouble() => success;
        public override int Next(int max) => Math.Min(quality, max - 1);
        public override int Next(int min, int max) => min;
    }
    public static void Configure()
    {
        Register("equipment-recipe-audit", Audit); Register("equipment-crafting-check", Setup); Register("equipment-crafting-reload-check", Reload);
    }
    private static void Audit()
    {
        var rows = new List<object>();
        foreach (var definition in EquipmentBalance.All)
        {
            if (!definition.NormalDrop || definition.Slot is GearSlot.Cosmetic or GearSlot.Ring or GearSlot.Amulet) { continue; }
            var item = EquipmentCrafting.Resolve(definition.Type).CreateInstance<Item>();
            try
            {
                var recipes = new List<object>();
                foreach (var system in new CraftSystem[] { DefBlacksmithy.CraftSystem, DefTailoring.CraftSystem,
                    DefBowFletching.CraftSystem, DefCarpentry.CraftSystem, DefTinkering.CraftSystem })
                {
                    foreach (CraftItem recipe in system.CraftItems)
                    {
                        if (recipe.ItemType != item.GetType()) { continue; }
                        var ingredients = new List<object>();
                        foreach (CraftRes resource in recipe.Resources)
                        { ingredients.Add(new { Type = resource.ItemType.Name, resource.Amount }); }
                        var skill = recipe.Skills[0];
                        recipes.Add(new { Skill = system.MainSkill.ToString(), skill.MinSkill, skill.MaxSkill, Ingredients = ingredients });
                    }
                }
                rows.Add(new { definition.Type, definition.Category, definition.BaseCode, Slot = definition.Slot.ToString(),
                    Native = GearDetails.Read(item), Material = EquipmentEconomy.Material(item).ToString(),
                    ArmorMaterial = (item as BaseArmor)?.MaterialType.ToString(), item.LabelNumber, Recipes = recipes });
            }
            finally { item.Delete(); }
        }
        var path = Environment.GetEnvironmentVariable("SANCTUARY_RECIPE_AUDIT")
            ?? throw new InvalidOperationException("Explicit equipment authoring output required");
        File.WriteAllText(path, JsonSerializer.Serialize(rows));
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable white equipment recipe checks", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _logger.Information("{Command}: passed", command); }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
        }));
    private static void Require(bool value, string message) => CraftingChecks.Require(value, message);
    internal static BaseTool Tool(SkillName skill) => skill switch
    { SkillName.Blacksmith => new SmithHammer(50), SkillName.Fletching => new FletcherTools(50), _ => new SewingKit(50) };
    internal static EquipmentRecipe Row(string type, GearTier tier = GearTier.Normal) => EquipmentCrafting.Recipes.Single(r => r.Type == type && r.Tier == tier);
    internal static T Put<T>(Mobile owner, T item) where T : Item { owner.Backpack.DropItem(item); return item; }
    private static PlayerMobile Player(string name, EquipmentRecipe row)
    {
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = name, RawStr = 200 };
        player.AddItem(new Backpack()); var basis = EquipmentBalance.Base(row.BaseCode);
        var craftClass = EquipmentBalance.Types[basis.Type].CharacterClass == "ass" ? DiabloClass.Assassin : DiabloClass.Necromancer;
        ClassCreation.Profiles.Assign(player, craftClass);
        ClassProfessions.Profiles.Confirm(player, row.Skill);
        player.Skills[row.Skill].Cap = 120; player.Skills[row.Skill].Base = 120;
        FixtureSpace.Place(player, new Point3D(5750, 1151, 0), Map.Trammel);
        CraftFacilities.Register(new(row.Skill,player.Map.Name,player.X+1,player.Y,player.Z,0xFAF));
        if(row.Skill==SkillName.Blacksmith)
        { CraftFacilities.Register(new(row.Skill,player.Map.Name,player.X+1,player.Y+1,player.Z,0xFB1)); }
        player.RawStr = 1000; player.RawDex = 1000;
        ClassCreation.Profiles.AdvanceLevel(player, 99); player.RawStr = 1000; player.RawDex = 1000;
        TreasureChecks.LearnAll(player);
        return player;
    }
    internal static CraftTransactions.Request Ticket(Mobile player, BaseTool tool, EquipmentRecipe row, Item primary)
    {
        var recipe = EquipmentCrafting.Prepare(row, primary);
        Require(recipe != null, "Approved ingredient failed recipe preparation");
        return (CraftTransactions.Request)CraftTransactions.Begin(player, tool.CraftSystem, recipe, null, tool, primary.Hue);
    }
    private static void Setup()
    {
        Require(ClassProfessions.Enabled && EquipmentCrafting.Recipes.Length == 603, "Equipment batch/feature missing");
        var forge = new Forge(); forge.MoveToWorld(new Point3D(5551, 1150, 0), Map.Trammel);
        var anvil = new Item(0xFAF) { Movable = false }; anvil.MoveToWorld(new Point3D(5549, 1150, 0), Map.Trammel);
        var saved = new List<Saved>(); int index = 0;
        foreach (var row in EquipmentCrafting.Recipes)
        {
            foreach (int quality in new[] { 99, 1, 0 })
            {
                var player = Player("WhiteCraft" + index++, row); var tool = Put(player, Tool(row.Skill));
                var inputs = new List<Item>();
                foreach (var ingredient in row.Ingredients)
                { var item = EquipmentCrafting.Resolve(ingredient.Type).CreateInstance<Item>(); item.Amount = ingredient.Amount + 5; inputs.Add(Put(player, item)); }
                var ticket = Ticket(player, tool, row, inputs[0]);
                Require(CraftTransactions.Complete(ticket, new Roll(quality)), "Recipe rejected: " + row.Id
                    + " work=" + (ticket.Equipment != null) + " valid=" + CraftTransactions.Valid(ticket)
                    + " skill=" + player.Skills[row.Skill].Base + " resource=" + EquipmentCrafting.Resource(inputs[0])
                    + " canonical=" + CraftResources.GetFromType(inputs[0].GetType()) + " hue=" + inputs[0].Hue);
                var output = player.Backpack.Items.Single(i => i.GetType().Name == row.Type);
                var state = ClassGear.State(output); var basis = EquipmentBalance.Base(row.BaseCode);
                var expected = quality == 0 ? CraftQuality.Superior : quality == 1 ? CraftQuality.HighQuality : CraftQuality.Average;
                Require(state is { CraftedEquipment: true } && state.Tier == row.Tier && state.Quality == expected
                    && state.BaseCode == row.BaseCode && state.Roll.ItemLevel == EquipmentCrafting.ItemLevel(row.Tier)
                    && state.Roll.Rarity == GearRarity.Normal && state.Roll.Affixes.Length == 0
                    && state.NativeSockets == (GearDetails.Socketable(output) ? (int)expected : 0) && state.TinkerSockets == 0 && state.DurabilityPercent == 10
                    && output.Origin == ItemOrigin.Crafted && output.Name.StartsWith(row.Tier.ToString()) && tool.UsesRemaining == 49,
                    "Tier/quality/origin/sockets/charges wrong: " + row.Id);
                Require(inputs.All(i => i.Amount == 5) && GearDetails.Read(output) is { Creator: { Length: > 0 } } native
                    && native.Durability == native.MaximumDurability, "Resources or fresh durability/maker wrong");
                Require(ClassGear.Stat(output, ClassGearStat.StrengthRequirement) == (ClassGear.CosmeticClothing(output) ? 0 : basis.Strength)
                    && ClassGear.RequiredLevel(output) == (ClassGear.CosmeticClothing(output) ? 0 : Math.Max(basis.RequiredLevel, EquipmentCrafting.EquipFloor(row.Tier)))
                    && ClassGear.CanEquip(output, player) == true && GearDetails.Read(output).Material == EquipmentCrafting.PhysicalMaterial(output.GetType()), "Effective tier requirement/equip projection wrong");
                if (row.Type == "Longsword") { Require(ClassGear.Damage(output) == (basis.MinDamage, basis.MaxDamage), "Tier weapon damage wrong"); }
                Require(!CraftTransactions.Complete(ticket, new Roll()) && tool.UsesRemaining == 49, "Equipment response replayed");
                string tooltip = Encoding.Unicode.GetString(output.PropertyList.Buffer.AsSpan(1));
                Require((ClassGear.CosmeticClothing(output) ? tooltip.Contains("Cosmetic") : tooltip.Contains("Base tier: " + row.Tier) && tooltip.Contains("quality: " + CraftTransactions.QualityName(expected)))
                    && (!GearDetails.Socketable(output) || expected == CraftQuality.Average || tooltip.Contains("Empty sockets: " + (int)expected)), "Compiled tooltip hides tier/quality/empty sockets");
                saved.Add(Snapshot(output));
                if (row.Type == "BoneChest")
                {
                    Require(player.EquipItem(output) && EquipmentBalance.RunPenalty(player) == 10, "Crafted bone is not Heavy");
                    player.Backpack.DropItem(output);
                }
                // Keep saved items/owners, but vacate the workshop for the next independent recipe.
                player.MoveToWorld(new Point3D(5750,1200,0),Map.Trammel);
            }
        }
        Colours(saved); ExpandedMaterials(saved); Boundaries(saved); Transfers(saved); ExactSelectionAndSaving(); Rejects(); BookCoverage();
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
        _logger.Information("Equipment: 603 recipes x three qualities, tier stats/equip floors, coloured materials, wear/socket boundaries, exact mixed inputs, bypasses and rejection/replay passed");
    }
    private static void BookCoverage()
    {
        Require(EquipmentCrafting.Scope.Length == 211 && EquipmentCrafting.Scope.Count(r => r.ExcludedReason != null) == 10,
            "Ordinary roster/exotic exclusions changed");
        var player = Player("BookGuards", Row("Longsword")); var tool = Put(player, new SmithHammer(50));
        foreach (var scope in EquipmentCrafting.Scope)
        {
            var type = EquipmentCrafting.Resolve(scope.Type);
            Require(EquipmentCrafting.Recipes.Count(r => r.Type == scope.Type) == (scope.ExcludedReason == null ? 3 : 0),
                "Recipe/exclusion coverage wrong: " + scope.Type);
            var native = new CraftItem(type, "native", scope.Type);
            Require(!TradeSkillHooks.CheckRecipe(player, tool.CraftSystem, native, tool), "Native book bypass: " + scope.Type);
        }
        Require(!TradeSkillHooks.CheckRecipe(player, tool.CraftSystem, new CraftItem(typeof(AbyssReaver), "reward", "reward"), tool),
            "Named equipment bypassed ordinary white recipes");
        var capRow = Row("ChaosShield", GearTier.Elite); var capInput = Put(player, new IronIngot(100));
        player.Skills.Blacksmith.Base = 119.9;
        Require(!CraftTransactions.Complete(Ticket(player, tool, capRow, capInput), new Roll()) && capInput.Amount == 100,
            "Hard-cap chance floor bypassed required120 skill");
        player.Skills.Blacksmith.Base = 120;
        var capTicket = Ticket(player, tool, capRow, capInput);
        Require(capTicket.Recipe.GetSuccessChance(player, null, tool.CraftSystem, false, out bool capSkill) == .01 && capSkill,
            "Hard-cap smith recipe is impossible at120");
        capInput.Delete();
        var row = Row("GemmedCirclet"); var inputs = new List<Item>();
        foreach (var ingredient in row.Ingredients)
        {
            var item = EquipmentCrafting.Resolve(ingredient.Type).CreateInstance<Item>(); item.Amount = ingredient.Amount + 5;
            inputs.Add(Put(player, item));
        }
        var ticket = Ticket(player, tool, row, inputs[0]); inputs[3].Amount--;
        Require(!CraftTransactions.Complete(ticket, new Roll()) && inputs[0].Amount == row.Ingredients[0].Amount + 5
            && tool.UsesRemaining == 50, "Changed fourth component consumed costs");
        var failure = Ticket(player, tool, row, inputs[0]);
        Require(CraftTransactions.Complete(failure, new Roll(0, .999)) && tool.UsesRemaining == 49
            && !player.Backpack.Items.Any(i => i is GemmedCirclet), "Four-component native failure did not commit once");
        for (int index = 0; index < inputs.Count; index++)
        {
            int before = row.Ingredients[index].Amount + 5 - (index == 3 ? 1 : 0);
            int amount = row.Ingredients[index].Amount;
            int consumed = tool.CraftSystem.ConsumeOnFailure(player, inputs[index].GetType(), failure.Recipe)
                ? Core.UOTD ? amount : amount - amount / 2 : 0;
            Require(inputs[index].Amount == before - consumed, "Four-component failure quantity wrong");
        }
    }
    private static void ExpandedMaterials(List<Saved> saved)
    {
        foreach (string type in new[] { "Boots", "ThighBoots", "LeatherGloves", "Bow", "WoodlandChest", "Nunchaku", "BoneHarvester", "PotionPlatedBelt", "Tekagi" })
        {
            var row = Row(type, GearTier.Elite); var player = Player("Expanded" + type, row); var tool = Put(player, Tool(row.Skill));
            var inputs = new List<Item>();
            foreach (var ingredient in row.Ingredients)
            {
                Item source = ingredient == row.Ingredients[0] && ingredient.Type == "Leather" ? new BarbedLeather()
                    : ingredient == row.Ingredients[0] && ingredient.Type == "Board" ? new OakBoard()
                    : EquipmentCrafting.Resolve(ingredient.Type).CreateInstance<Item>();
                source.Amount = ingredient.Amount + 5; inputs.Add(Put(player, source));
            }
            Require(CraftTransactions.Complete(Ticket(player, tool, row, inputs[0]), new Roll()), "Expanded material rejected: " + type);
            var item = player.Backpack.Items.Single(i => i.GetType().Name == type); var state = ClassGear.State(item);
            Require(state.MaterialRolls.Length == (inputs[0] is BarbedLeather ? 2 : inputs[0] is OakBoard ? 1 : 0)
                && GearDetails.Read(item).Resource == EquipmentCrafting.Resource(inputs[0]), "Expanded coloured identity lost: " + type);
            if (item is PotionTabard)
            { Require(state.SocketCount == 0 && PotionBeltCatalog.Belt(item).Rows == 4, "Potion tabard lost row count or gained sockets"); }
            if (type == "Tekagi")
            { Require(ClassGear.Base(item).Type == "h2h2" && ClassGear.CanEquip(item, player) == true, "Claw subtype/class transition lost"); }
            if (type == "WoodlandChest") { player.Race = Race.Elf; }
            Require(player.EquipItem(item) && ClassGear.Active(player, item), "Native expanded equip rejected: " + type);
            player.Backpack.DropItem(item);
            var copy = item.GetType().CreateInstance<Item>(); item.Dupe(copy); Put(player, copy);
            Require(JsonSerializer.Serialize(ClassGear.State(copy)) == JsonSerializer.Serialize(state) && GearDetails.Read(copy) == GearDetails.Read(item) && copy.Hue == item.Hue,
                "Expanded copy changed material/tier/wear: " + type);
            saved.Add(Snapshot(item)); saved.Add(Snapshot(copy));
        }
    }
    private static Saved Snapshot(Item item) => new(item.Serial.Value, JsonSerializer.Serialize(ClassGear.State(item)), GearDetails.Read(item), item.Name, item.GetType().Name, item.Hue, item.Origin);
    private static void Colours(List<Saved> saved)
    {
        foreach (var source in new Item[] { new DullCopperIngot(20), new ShadowIronIngot(20), new CopperIngot(20),
            new BronzeIngot(20), new GoldIngot(20), new AgapiteIngot(20), new VeriteIngot(20), new ValoriteIngot(20),
            new OakBoard(20), new AshBoard(20), new YewBoard(20), new HeartwoodBoard(20), new BloodwoodBoard(20), new FrostwoodBoard(20),
            new SpinedLeather(20), new HornedLeather(20), new BarbedLeather(20) })
        {
            var row = Row(source is BaseIngot ? "Longsword" : source is Board ? "WoodenShield" : "LeatherCap");
            var player = Player("ColouredWhite" + source.Serial.Value, row); var tool = Put(player, Tool(row.Skill)); Put(player, source);
            var resource = EquipmentCrafting.Resource(source);
            Require(CraftTransactions.Complete(Ticket(player, tool, row, source), new Roll(0)), "Coloured craft failed: " + source.GetType());
            var output = player.Backpack.Items.Single(i => i.GetType().Name == row.Type);
            Require(GearDetails.Read(output).Resource == resource && ClassGear.State(output).MaterialRolls.Length > 0,
                "Material identity/roll lost");
            if (source is SpinedLeather or HornedLeather or BarbedLeather)
            { Require(ClassGear.State(output).MaterialRolls.Length == 2 && ClassGear.State(output).MaterialRolls.All(r => r.Value is >= 1 and <= 5), "Dual resistance budget wrong"); }
            Require(ClassGear.Bonus(output, "res-fire") == ClassGear.Material(output, "resistance-fire") && ClassGear.Bonus(output, "dmg%") == 0, "Material resistance or local damage modifier projected incorrectly");
            var native = GearDetails.Read(output);
            if (output is BaseArmor armor) { Require(armor.GetDurabilityBonus() == 0, "Native coloured durability stacked"); }
            if (output is BaseWeapon weapon) { Require(weapon.GetDurabilityBonus() == 0, "Native weapon durability stacked"); }
            var copy = output.GetType().CreateInstance<Item>(); output.Dupe(copy); Put(player, copy);
            Require(JsonSerializer.Serialize(ClassGear.State(copy)) == JsonSerializer.Serialize(ClassGear.State(output))
                && GearDetails.Read(copy) == native && !ReferenceEquals(ClassGear.State(copy).MaterialRolls, ClassGear.State(output).MaterialRolls),
                "Copied material or native wear changed");
            var imbued = ClassGear.Profiles.PrepareImbue(output, GearRarity.Magic, new Random(91));
            Require(imbued != null && imbued.Tier == ClassGear.State(output).Tier && imbued.MaterialRolls.SequenceEqual(ClassGear.State(output).MaterialRolls),
                "Imbuing discarded native sockets/tier/material");
            ClassGear.Profiles.ApplyImbue(output, imbued);
            Require(GearDetails.Read(output) == native, "Imbuing changed wear/maker/resource");
            saved.Add(Snapshot(output)); saved.Add(Snapshot(copy));
        }
        var ring = new RingmailChest();
        foreach (var resource in new[] { CraftResource.Gold, CraftResource.Verite })
        {
            var rolls = EquipmentCrafting.RollMaterial(ring, resource, new Roll());
            Require(rolls.Length == 1 && rolls[0].Code == (resource == CraftResource.Gold ? "magic-find" : "life-per-five"), "Armour special material identity wrong");
        }
        ring.Delete();
    }
    private static void Boundaries(List<Saved> saved)
    {
        var row = Row("LeatherCap", GearTier.Elite); var player = Player("WhiteBoundary", row);
        foreach (double skill in new[] { 99.9, 100.0 })
        {
            player.Skills.Tailoring.Base = skill;
            var item = ItemOrigins.Create(ItemOrigin.Crafted, () => new LeatherCap { MaxHitPoints = 11, HitPoints = 7 });
            EquipmentCrafting.Finish(item, player, row, CraftResource.SpinedLeather, 1234, CraftQuality.Superior, new Roll()); Put(player, item);
            var native = GearDetails.Read(item);
            Require(native.MaximumDurability == (skill < 100 ? 11 : 13) && native.Durability == native.MaximumDurability
                && ClassGear.State(item).NativeSockets == (skill < 100 ? 0 : 2)
                && (native.Creator != null) == (skill >= 100), "100-skill one-time durability/socket/maker boundary wrong");
            item.Dupe(new LeatherCap());
            saved.Add(Snapshot(item));
        }
        var elite = player.Backpack.Items.OfType<LeatherCap>().First();
        var low = ProcessingChecks.Player("WhiteLevelGate"); low.RawStr = 200; low.RawDex = 200;
        Require(ClassGear.CanEquip(elite, low) == false, "Elite level floor ignored");
        ClassCreation.Profiles.AdvanceLevel(low, 80); low.RawStr = 49; low.RawDex = 200;
        Require(ClassGear.CanEquip(elite, low) == false, "Tier attribute requirement ignored");
        low.RawStr = 50; Require(ClassGear.CanEquip(elite, low) == true, "Valid tier requirement rejected");
        var oldBone = new BoneChest(); Require(EquipmentBalance.EffectiveWeight(oldBone) == GearWeight.Medium, "Legacy bone classification changed"); oldBone.Delete();
        low.Delete();
    }
    private static void Transfers(List<Saved> saved)
    {
        var row = Row("Longsword", GearTier.Elite); var owner = Player("WhiteTransfers", row);
        var item = ItemOrigins.Create(ItemOrigin.Crafted, () => new Longsword());
        EquipmentCrafting.Finish(item, owner, row, CraftResource.Copper, 1234, CraftQuality.Superior, new Roll()); Put(owner, item);
        var native = GearDetails.Read(item); string before = JsonSerializer.Serialize(ClassGear.State(item));
        var receiver = ProcessingChecks.Player("WhiteTradeReceiver"); receiver.MoveToWorld(owner.Location, owner.Map);
        var cancelled = new SecureTrade(owner, receiver); cancelled.From.Container.DropItem(item); cancelled.Cancel();
        Require(item.IsChildOf(owner.Backpack) && before == JsonSerializer.Serialize(ClassGear.State(item)), "Cancelled tier trade changed metadata");
        var accepted = new SecureTrade(owner, receiver); accepted.From.Container.DropItem(item);
        accepted.From.Accepted = accepted.To.Accepted = true; accepted.Update();
        Require(!accepted.Valid && item.IsChildOf(receiver.Backpack) && before == JsonSerializer.Serialize(ClassGear.State(item))
            && item.Origin == ItemOrigin.Crafted && GearDetails.Read(item) == native, "Accepted tier trade changed identity/state/wear");
        var magic = ClassGear.Profiles.PrepareImbue(item, GearRarity.Magic, new Random(121));
        Require(magic is { Tier: GearTier.Elite, NativeSockets: 2 } && magic.BaseCode == "7ls" && magic.Roll.RequiredLevel >= 80,
            "Imbuing dropped tier identity/equip floor/sockets");
        ClassGear.Profiles.ApplyImbue(item, magic);
        Require(GearDetails.Read(item) == native, "Tier Imbuing reapplied durability bonus"); saved.Add(Snapshot(item));
    }
    private static void ExactSelectionAndSaving()
    {
        var row = Row("SkullCap"); var player = Player("WhiteSelection", row); var tool = Put(player, new SewingKit(50));
        var chosen = Put(player, new Cloth(10) { Hue = 1234 }); var other = Put(player, new Cloth(10) { Hue = 5678 });
        Require(CraftTransactions.Complete(Ticket(player, tool, row, chosen), new Roll()) && chosen.Amount == 8 && other.Amount == 10
            && player.Backpack.Items.OfType<SkullCap>().Single().Hue == 1234, "Exact colour selection consumed wrong cloth");
        var stale = Ticket(player, tool, row, chosen); chosen.Hue++;
        Require(!CraftTransactions.Complete(stale, new Roll()) && chosen.Amount == 8 && tool.UsesRemaining == 49, "Changed hue committed");
        chosen.Hue = 1234;
        var used = Ticket(player, tool, row, chosen); tool.UsesRemaining--;
        Require(!CraftTransactions.Complete(used, new Roll()) && chosen.Amount == 8, "Changed tool uses committed");
        var crafted = ItemOrigins.Create(ItemOrigin.Crafted, () => new SewingKit());
        CraftTransactions.FinishTool(crafted, player, CraftQuality.Average); Put(player, crafted);
        Require(CraftTransactions.Complete(Ticket(player, crafted, row, chosen), new Roll(0)) && crafted.UsesRemaining == 100
            && chosen.Amount == 6, "Crafted tool saving changed gear ingredient cost");
        player.Skills.Tailoring.Base = 0;
        Require(CraftTransactions.Complete(Ticket(player, crafted, row, chosen), new Roll(0, .999)) && crafted.UsesRemaining == 100
            && chosen.Amount == 6 - (Core.UOTD ? 2 : 1), "Failed gear craft missed tool saving/native loss");
        var colouredRow = Row("LeatherCap"); var leather = Put(player, new SpinedLeather(10)); player.Skills.Tailoring.Base = 64.9;
        Require(!CraftTransactions.Complete(Ticket(player, tool, colouredRow, leather), new Roll()) && leather.Amount == 10,
            "Native coloured material skill threshold bypassed");
        player.Skills.Tailoring.Base = 120;
        var plain = ItemOrigins.Create(ItemOrigin.Crafted, () => new LeatherCap());
        EquipmentCrafting.Finish(plain, player, colouredRow, CraftResource.RegularLeather, 0, CraftQuality.Average, new Roll()); Put(player, plain);
        int uses = tool.UsesRemaining; TextDefinition message = null;
        Require(Enhance.Invoke(player, tool.CraftSystem, tool, plain, CraftResource.SpinedLeather, typeof(SpinedLeather), ref message) == EnhanceResult.BadItem
            && plain.Resource == CraftResource.RegularLeather && leather.Amount == 10 && tool.UsesRemaining == uses,
            "Native enhancement bypassed fixed crafted material/rolls");
        int[] qualityCounts = new int[3];
        for (int roll = 0; roll < 100; roll++) { qualityCounts[(int)CraftTransactions.Quality(120, 119, roll)]++; }
        Require(qualityCounts.SequenceEqual(new[] { 89, 10, 1 }), "Quality odds changed");
    }
    private static void Rejects()
    {
        var row = Row("BoneChest"); var player = Player("WhiteRejections", row); var tool = Put(player, Tool(row.Skill));
        var bone = Put(player, new Bone(40)); var leather = Put(player, new Leather(50));
        var changed = Ticket(player, tool, row, bone); leather.Amount--;
        Require(!CraftTransactions.Complete(changed, new Roll()) && bone.Amount == 40 && leather.Amount == 49 && tool.UsesRemaining == 50, "Changed secondary ingredient consumed");
        var missing = Ticket(player, tool, row, bone); leather.Internalize();
        Require(!CraftTransactions.Complete(missing, new Roll()) && bone.Amount == 40, "Missing mixed ingredient consumed primary"); Put(player, leather);
        var moved = Ticket(player, tool, row, bone); bone.Internalize();
        Require(!CraftTransactions.Complete(moved, new Roll()), "Moved primary committed"); Put(player, bone);
        var missingTool = Ticket(player, tool, row, bone); tool.Internalize();
        Require(!CraftTransactions.Complete(missingTool, new Roll()), "Moved tool committed"); Put(player, tool);
        var point = player.Location; var outside = Ticket(player, tool, row, bone); player.MoveToWorld(new Point3D(point.X + 20, point.Y, point.Z), player.Map);
        Require(!CraftTransactions.Complete(outside, new Roll()), "Missing station committed"); player.MoveToWorld(point, player.Map);
        var fills = new List<Item>();
        while (player.Backpack.TotalItems < player.Backpack.MaxItems) { var fill = Put(player, new Bag { Weight = 0 }); fills.Add(fill); }
        Require(!CraftTransactions.Complete(Ticket(player, tool, row, bone), new Roll()) && tool.UsesRemaining == 50, "Full pack charged");
        foreach (var fill in fills) { fill.Delete(); }
        var heavy = Put(player, new Bag { Weight = 1000 });
        var cloth = Put(player, new Cloth(20));
        Require(!CraftTransactions.Complete(Ticket(player, tool, Row("SkullCap"), cloth), new Roll()) && bone.Amount == 40, "Overweight result accepted"); heavy.Delete();
        var recipe = DefTailoring.CraftSystem.CraftItems.SearchFor(typeof(BoneChest));
        recipe.CompleteCraft(2, true, player, DefTailoring.CraftSystem, null, tool, null);
        Require(!player.Backpack.Items.OfType<BoneChest>().Any() && bone.Amount == 40 && tool.UsesRemaining == 50,
            "Native completion bypassed authored recipe");
        Require(!TradeSkillHooks.CheckRecipe(player, DefTailoring.CraftSystem, recipe, tool), "Native recipe entry accepted");
        player.Skills.Tailoring.Base = 70;
        Require(CraftTransactions.Complete(Ticket(player, tool, row, bone), new Roll(99, .999)) && tool.UsesRemaining == 49
            && bone.Amount == 40 - (Core.UOTD ? 10 : 5) && leather.Amount == 49 - (Core.UOTD ? 12 : 6)
            && !player.Backpack.Items.OfType<BoneChest>().Any(), "Native-style mixed failure loss wrong");
        // Exact ingredients free their slots for the completed item, even when item-full.
        bone.Amount = 10; leather.Amount = 12;
        while (player.Backpack.TotalItems < player.Backpack.MaxItems) { Put(player, new Bag { Weight = 0 }); }
        Require(CraftTransactions.Complete(Ticket(player, tool, row, bone), new Roll()) && bone.Deleted && leather.Deleted,
            "Consumed source slots were not reused");
    }
    private static void Reload()
    {
        foreach (var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var item = World.FindItem((Serial)saved.Item);
            Require(item != null && saved.State == JsonSerializer.Serialize(ClassGear.State(item))
                && saved.Native == GearDetails.Read(item) && saved.Name == item.Name && saved.Hue == item.Hue && saved.Origin == item.Origin, "Equipment changed after restart: " + saved.Type + " serial=" + saved.Item + " found=" + (item != null)
                + " state=" + (item == null ? "missing" : JsonSerializer.Serialize(ClassGear.State(item)))
                + " expected=" + saved.State + " native=" + (item == null ? "missing" : JsonSerializer.Serialize(GearDetails.Read(item)))
                + " expectedNative=" + JsonSerializer.Serialize(saved.Native) + " name=" + item?.Name + " expectedName=" + saved.Name + " hue=" + item?.Hue + " expectedHue=" + saved.Hue);
        }
        EquipmentCraftingUiChecks.Reload();
    }
}
