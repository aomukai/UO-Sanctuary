// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Engines.Craft;
using Server.Items;

namespace Sanctuary;

public sealed record EquipmentIngredient(string Type, int Amount);
public sealed record EquipmentRecipe(string Id, string Type, string Name, SkillName Skill, GearTier Tier,
    string BaseCode, double RequiredSkill, double MaxSkill, EquipmentIngredient[] Ingredients);

public sealed record EquipmentCraftScope(string Type, GearMaterial Material, string Source, string ExcludedReason);

public static class EquipmentCrafting
{
    public static EquipmentRecipe[] Recipes { get; private set; } = [];
    public static EquipmentCraftScope[] Scope { get; private set; } = [];
    private static readonly Dictionary<Type, EquipmentCraftScope> _scope = new();
    private static readonly HashSet<Type> _outputs = [];
    private sealed record Selection(EquipmentRecipe Row, Type Material, CraftResource Resource);
    private static readonly Dictionary<(string Id, Type Material), CraftItem> _cache = new();
    private static readonly Dictionary<CraftItem, Selection> _selections = new();
    public static Type Resolve(string name) => typeof(IronIngot).Assembly.GetType("Server.Items." + name)
        ?? typeof(EquipmentCrafting).Assembly.GetType("Sanctuary." + name)
        ?? throw new InvalidDataException("Unknown recipe item: " + name);
    public static void Load()
    {
        _scope.Clear(); _outputs.Clear(); _cache.Clear(); _selections.Clear();
        Scope = BalanceCatalog.Read<EquipmentCraftScope>("equipment-crafting-scope");
        foreach (var scope in Scope)
        {
            var type = Resolve(scope.Type);
            if (!EquipmentBalance.TryGet(type, out var definition) || !Ordinary(type, definition)
                || string.IsNullOrWhiteSpace(scope.Source) || !Enum.IsDefined(scope.Material)
                || (scope.ExcludedReason != null) != (scope.Material == GearMaterial.Unknown))
            { throw new InvalidDataException("Invalid equipment crafting scope: " + scope.Type); }
            _scope.Add(type, scope);
            if (scope.ExcludedReason == null) { _outputs.Add(type); }
        }
        foreach (var definition in EquipmentBalance.All)
        {
            if (!definition.NormalDrop || !ManagedSlot(definition.Slot)) { continue; }
            var type = Resolve(definition.Type);
            if (!_scope.ContainsKey(type))
            { throw new InvalidDataException("Ordinary equipment has no recipe or explicit exclusion: " + definition.Type); }
        }
        foreach (var belt in PotionBeltCatalog.Belts)
        {
            if (!_scope.ContainsKey(Resolve(belt.Type))) { throw new InvalidDataException("Potion tabard has no recipe scope: " + belt.Type); }
        }
        Recipes = BalanceCatalog.Read<EquipmentRecipe>("equipment-recipes");
        var ids = new HashSet<string>();
        var identities = new HashSet<(Type, GearTier)>();
        foreach (var row in Recipes)
        {
            var output = Resolve(row.Type);
            var basis = EquipmentBalance.Base(row.BaseCode);
            EquipmentBalance.TryGet(output, out var gear);
            if (!_outputs.Contains(output) || !ids.Add(row.Id) || !identities.Add((output, row.Tier))
                || !Enum.IsDefined(row.Tier) || basis.Tier != row.Tier.ToString()
                || !EquipmentBalance.IsType(basis.Type, gear.AffixType)
                || row.Tier == GearTier.Normal && row.BaseCode != gear.BaseCode
                || row.Ingredients.Length is < 1 or > 4 || row.Skill != Maker(_scope[output].Material)
                || row.Ingredients[0].Type != Primary(_scope[output].Material)
                || row.RequiredSkill < 0 || row.RequiredSkill > 120 || row.MaxSkill <= row.RequiredSkill)
            { throw new InvalidDataException("Invalid equipment recipe: " + row.Id); }
            var ingredients = new HashSet<Type>();
            foreach (var ingredient in row.Ingredients)
            {
                var type = Resolve(ingredient.Type);
                if (!typeof(Item).IsAssignableFrom(type) || !ingredients.Add(type) || ingredient.Amount is < 1 or > 60000)
                { throw new InvalidDataException("Invalid equipment ingredient: " + row.Id); }
            }
        }
        foreach (var type in _outputs)
        {
            foreach (var tier in Enum.GetValues<GearTier>())
            {
                if (!identities.Contains((type, tier))) { throw new InvalidDataException("Missing equipment tier: " + type.Name); }
            }
        }
    }
    private static bool Ordinary(Type type, GearDefinition definition) => ManagedSlot(definition.Slot)
        && (definition.NormalDrop || typeof(PotionTabard).IsAssignableFrom(type));
    private static bool ManagedSlot(GearSlot slot) => slot is GearSlot.Weapon or GearSlot.Shield or GearSlot.Head
        or GearSlot.Body or GearSlot.Hands or GearSlot.Feet or GearSlot.Belt;
    private static SkillName Maker(GearMaterial material) => material switch
    { GearMaterial.Metal => SkillName.Blacksmith, GearMaterial.Wood => SkillName.Fletching, _ => SkillName.Tailoring };
    private static string Primary(GearMaterial material) => material switch
    { GearMaterial.Metal => "IronIngot", GearMaterial.Wood => "Board", GearMaterial.Leather => "Leather",
        GearMaterial.Bone => "Bone", _ => "Cloth" };
    public static GearMaterial PhysicalMaterial(Type type) => _scope.TryGetValue(type, out var scope) ? scope.Material : GearMaterial.Unknown;
    public static bool Craftable(Type type) => _outputs.Contains(type);
    public static void Configure()
    {
        TradeSkillHooks.MinimumChance = (from, system, recipe) => ClassProfessions.Affected(from) && recipe.ItemType == typeof(SocketGem) ? 1.0
            : ClassProfessions.Affected(from) && _selections.TryGetValue(recipe, out var selection) && selection.Row.RequiredSkill == 120
            ? Math.Max(.01, system.GetChanceAtMin(recipe)) : null;
        TradeSkillHooks.RecipeSelected = (from, system, recipe, tool) =>
        {
            if (ClassProfessions.Affected(from))
            {
                foreach (var resource in recipe.Resources)
                {
                    if (SocketGems.Legacy(resource.ItemType))
                    { from.SendMessage("Legacy gems are decorative and cannot be consumed by crafting."); return false; }
                }
            }
            if (!ClassProfessions.Affected(from) || !IsOutput(recipe.ItemType) && !JewelleryCrafting.IsOutput(recipe.ItemType)) { return true; }
            if (_selections.TryGetValue(recipe, out var selection) && selection.Row.Skill == system.MainSkill) { return true; }
            if (JewelleryCrafting.IsOutput(recipe.ItemType)) { from.SendMessage("Use Craft white jewellery on tinkers tools."); }
            else if (Craftable(recipe.ItemType)) { from.SendMessage("Use Craft white equipment on the appropriate profession tool for this item."); }
            else { from.SendMessage("This item has no ordinary equipment recipe in the current recipe book."); }
            return false;
        };
    }
    public static bool IsOutput(Type type) => EquipmentBalance.TryGet(type, out var gear) && ManagedSlot(gear.Slot);
    public static bool Supports(BaseTool tool) => tool is SmithHammer or FletcherTools or SewingKit;
    public static int EquipFloor(GearTier tier) => tier switch { GearTier.Exceptional => 60, GearTier.Elite => 80, _ => 0 };
    public static int ItemLevel(GearTier tier) => tier switch { GearTier.Exceptional => 70, GearTier.Elite => 99, _ => 35 };
    public static bool Identity(Type type, GearTier tier, string code, string normal)
    {
        if (tier == GearTier.Normal) { return code == normal; }
        foreach (var row in Recipes) { if (row.Type == type.Name && row.Tier == tier && row.BaseCode == code) { return true; } }
        return false;
    }
    public static string Cost(EquipmentRecipe row, int start = 0)
    {
        int count = Math.Min(2, row.Ingredients.Length - start);
        if (count <= 0) { return ""; }
        var first = row.Ingredients[start];
        if (count == 1) { return $"{first.Amount} {first.Type}"; }
        var second = row.Ingredients[start + 1];
        return $"{first.Amount} {first.Type} + {second.Amount} {second.Type}";
    }
    public static CraftItem Prepare(EquipmentRecipe row, Item material)
    {
        if (Array.IndexOf(Recipes, row) < 0 && !JewelleryCrafting.Contains(row) || material == null) { return null; }
        Type required = Resolve(row.Ingredients[0].Type), selected = material.GetType();
        if (JewelleryCrafting.Contains(row) && selected != typeof(IronIngot)) { return null; }
        bool allowed = required == typeof(IronIngot) ? material is BaseIngot
            : required == typeof(Board) ? material is Board
            : required == typeof(Leather) ? material is BaseLeather : selected == required;
        var resource = Resource(material);
        if (!allowed || resource != CraftResources.GetFromType(selected)) { return null; }
        if (_cache.TryGetValue((row.Id, selected), out var found)) { return found; }
        var recipe = new CraftItem(Resolve(row.Type), "White equipment", row.Name);
        recipe.AddSkill(row.Skill, row.RequiredSkill, row.MaxSkill);
        for (int i = 0; i < row.Ingredients.Length; i++)
        {
            var ingredient = row.Ingredients[i];
            recipe.AddRes(i == 0 ? selected : Resolve(ingredient.Type), ingredient.Type, ingredient.Amount, "Missing materials");
        }
        _cache.Add((row.Id, selected), recipe); _selections.Add(recipe, new(row, selected, resource));
        return recipe;
    }
    internal static CraftResource Resource(Item item) => item switch
    {
        BaseIngot ingot => ingot.Resource, Board board => board.Resource, BaseLeather leather => leather.Resource,
        _ => CraftResource.None
    };
    private static double MaterialSkill(CraftSystem system, CraftResource resource)
    {
        double required = 0;
        if (CraftResources.GetInfo(resource) is { } info)
        {
            foreach (var type in info.ResourceTypes)
            { if (system.CraftSubRes.SearchFor(type) is { } sub) { required = Math.Max(required, sub.RequiredSkill); } }
        }
        return required;
    }
    internal sealed record Input(Item Item, object Parent, int Amount, int Hue, ItemOrigin Origin, double Weight,
        CraftResource Resource, int Consume)
    {
        public bool Valid(Mobile from) => MaterialProcessing.Accessible(from, Item) && ReferenceEquals(Item.Parent, Parent)
            && Item.Amount == Amount && Item.Hue == Hue && Item.Origin == Origin && Item.Weight == Weight
            && EquipmentCrafting.Resource(Item) == Resource;
        public int FreedWeight => Item.PileWeight - (int)Math.Ceiling(Weight * (Amount - Consume));
    }
    internal sealed record Group(Type Type, int Amount, List<Input> Inputs);
    internal sealed record Work(EquipmentRecipe Row, CraftResource Resource, List<Group> Groups, object ToolParent, int Uses);
    internal static Work Begin(CraftTransactions.Request ticket)
    {
        if (!MaterialProcessing.ToolValid(ticket.Owner, ticket.Tool) || !_selections.TryGetValue(ticket.Recipe, out var selection) || selection.Row.Skill != ticket.System.MainSkill
            || ticket.Hue < 0 || ticket.Material != null) { return null; }
        var groups = new List<Group>();
        for (int i = 0; i < selection.Row.Ingredients.Length; i++)
        {
            var ingredient = selection.Row.Ingredients[i];
            Type type = i == 0 ? selection.Material : Resolve(ingredient.Type);
            int needed = ingredient.Amount;
            var inputs = new List<Input>();
            foreach (var item in ticket.Owner.Backpack.FindItemsByType<Item>(true))
            {
                if (item.GetType() != type || !MaterialProcessing.Accessible(ticket.Owner, item)
                    || i == 0 && (item.Hue != ticket.Hue || Resource(item) != selection.Resource)
                    || i > 0 && Resource(item) != CraftResources.GetFromType(type)) { continue; }
                int take = Math.Min(needed, item.Amount);
                inputs.Add(new(item, item.Parent, item.Amount, item.Hue, item.Origin, item.Weight, Resource(item), take));
                needed -= take;
                if (needed == 0) { break; }
            }
            if (needed != 0) { return null; }
            groups.Add(new(type, ingredient.Amount, inputs));
        }
        return new(selection.Row, selection.Resource, groups, ticket.Tool.Parent, ticket.Tool.UsesRemaining);
    }
    internal static bool Complete(CraftTransactions.Request ticket, Random random)
    {
        if (ticket.Claimed) { return false; }
        ticket.Claimed = true;
        var work = ticket.Equipment;
        var from = ticket.Owner;
        if (work == null || !CraftTransactions.Valid(ticket) || !(Supports(ticket.Tool) || JewelleryCrafting.Contains(work.Row) && ticket.Tool is TinkerTools) || !MaterialProcessing.ToolValid(from, ticket.Tool)
            || !ReferenceEquals(work.ToolParent, ticket.Tool.Parent) || work.Uses != ticket.Tool.UsesRemaining
            || from.Skills[work.Row.Skill].Base < Math.Max(work.Row.RequiredSkill, MaterialSkill(ticket.System, work.Resource))) { return false; }
        int freed = 0, freedWeight = 0;
        foreach (var group in work.Groups)
        {
            foreach (var input in group.Inputs)
            {
                if (!input.Valid(from)) { return false; }
                if (input.Consume == input.Amount) { freed++; }
                freedWeight += input.FreedWeight;
            }
        }
        double chance = ticket.Recipe.GetSuccessChance(from, null, ticket.System, false, out bool skills);
        TextDefinition message = null;
        if (!skills || chance <= 0 || !ticket.Recipe.ConsumeAttributes(from, ref message, false)) { return false; }
        var output = ItemOrigins.Create(ItemOrigin.Crafted, () => Resolve(work.Row.Type).CreateInstance<Item>());
        int weight = output.PileWeight - freedWeight, slots = 1 - freed;
        if (slots > 0 && from.Backpack.MaxItems != 0 && from.Backpack.TotalItems + slots > from.Backpack.MaxItems
            || weight > 0 && (from.TotalWeight + weight > from.MaxWeight
                || from.Backpack.MaxWeight != 0 && from.Backpack.TotalWeight + weight > from.Backpack.MaxWeight))
        { output.Delete(); from.SendMessage("Make room for the equipment. Nothing was consumed."); return false; }
        random ??= Random.Shared;
        bool success = random.NextDouble() < chance;
        foreach (var group in work.Groups)
        {
            int consume = !success && !ticket.System.ConsumeOnFailure(from, group.Type, ticket.Recipe) ? 0
                : !success && !Core.UOTD ? group.Amount - group.Amount / 2 : group.Amount;
            foreach (var input in group.Inputs)
            {
                int take = Math.Min(consume, input.Consume); input.Item.Consume(take); consume -= take;
                if (consume == 0) { break; }
            }
        }
        if (success) { ticket.Recipe.ConsumeAttributes(from, ref message, true); }
        CraftTransactions.SpendUse(from, ticket.Tool, random);
        bool broken = ticket.Tool.UsesRemaining < 1 && ticket.Tool.BreakOnDepletion;
        if (broken) { ticket.Tool.Delete(); }
        var quality = CraftQuality.Average;
        if (success)
        {
            quality = CraftTransactions.Quality(from.Skills[work.Row.Skill].Base, work.Row.RequiredSkill, random.Next(100));
            if (JewelleryCrafting.Contains(work.Row)) { JewelleryCrafting.Finish(output, work.Row, quality, random); }
            else { Finish(output, from, work.Row, work.Resource, ticket.Hue, quality, random); }
            from.Backpack.DropItem(output);
            from.SendMessage($"You crafted {CraftTransactions.QualityName(quality)} {work.Row.Tier} {work.Row.Name}.");
        }
        else { output.Delete(); }
        ticket.Recipe.GetSuccessChance(from, null, ticket.System, true, out _);
        int ending = ticket.System.PlayEndingEffect(from, !success, true, broken, 1, false, ticket.Recipe);
        if (ending > 0) { from.SendLocalizedMessage(ending); }
        return true;
    }
    internal static void Finish(Item item, Mobile maker, EquipmentRecipe row, CraftResource resource, int hue,
        CraftQuality quality, Random random)
    {
        var original = GearDetails.Read(item);
        switch (item)
        {
            case BaseWeapon weapon: weapon.Quality = WeaponQuality.Regular; weapon.Resource = resource; break;
            case BaseArmor armor: armor.Quality = ArmorQuality.Regular;
                if (resource != CraftResource.None) { armor.Resource = resource; } break;
            case BaseClothing clothing: clothing.Quality = ClothingQuality.Regular;
                if (resource != CraftResource.None) { clothing.Resource = resource; } break;
        }
        item.Hue = hue; item.Name = $"{row.Tier} {row.Name}"; item.PlayerConstructed = true;
        var basis = EquipmentBalance.Base(row.BaseCode);
        int level = ItemLevel(row.Tier), durability = maker.Skills[row.Skill].Base >= 100 ? random.Next(10, 21) : 0;
        var roll = new GearRoll(GearRarity.Normal, level, AffixBalance.Level(level, basis.QualityLevel, basis.MagicLevel),
            Math.Max(EquipFloor(row.Tier), basis.RequiredLevel), []);
        var state = new GearState(row.BaseCode, random.Next(basis.MinDefense, basis.MaxDefense + 1), false, roll)
        {
            Tier = row.Tier, Quality = quality, CraftedEquipment = true, DurabilityPercent = durability,
            NativeSockets = maker.Skills[row.Skill].Base >= 100 && GearDetails.Socketable(item) ? (int)quality : 0,
            MaterialRolls = RollMaterial(item, resource, random)
        };
        ClassGear.Profiles.Store(item, state);
        int maximum = (int)Math.Ceiling((original.MaximumDurability ?? 0) * (100 + durability) / 100.0);
        string name = durability > 0 ? maker.RawName : null;
        switch (item)
        {
            case BaseWeapon weapon: weapon.MaxHitPoints = maximum; weapon.HitPoints = maximum; weapon.Crafter = name; break;
            case BaseArmor armor: armor.MaxHitPoints = maximum; armor.HitPoints = maximum; armor.Crafter = name; break;
            case BaseClothing clothing: clothing.MaxHitPoints = maximum; clothing.HitPoints = maximum; clothing.Crafter = name; break;
        }
    }
    private static readonly string[] _elements = ["fire", "cold", "lightning", "poison"];
    private static string[] Elements(CraftResource resource, int choice) => resource switch
    {
        CraftResource.DullCopper or CraftResource.Frostwood => ["cold"],
        CraftResource.ShadowIron or CraftResource.Bloodwood => ["physical"],
        CraftResource.Copper or CraftResource.Heartwood => [_elements[choice]],
        CraftResource.Bronze or CraftResource.YewWood => ["poison"],
        CraftResource.Agapite or CraftResource.AshWood => ["lightning"],
        CraftResource.Valorite or CraftResource.OakWood => ["fire"],
        CraftResource.SpinedLeather => ["cold", "lightning"],
        CraftResource.HornedLeather => ["fire", "physical"],
        CraftResource.BarbedLeather => ["poison", _elements[choice % 3]], _ => []
    };
    internal static MaterialRoll[] RollMaterial(Item item, CraftResource resource, Random random)
    {
        bool weapon = item is BaseWeapon;
        if (resource == CraftResource.Gold) { return [new(weapon ? "gold-find" : "magic-find", random.Next(1, 3))]; }
        if (resource == CraftResource.Verite) { return [new(weapon ? "mana-per-five" : "life-per-five", 1)]; }
        int choice = resource is CraftResource.Copper or CraftResource.Heartwood ? random.Next(4)
            : resource == CraftResource.BarbedLeather ? random.Next(3) : 0;
        var elements = Elements(resource, choice);
        var rolls = new MaterialRoll[elements.Length];
        for (int i = 0; i < elements.Length; i++)
        { rolls[i] = new((weapon ? "damage-" : "resistance-") + elements[i], weapon ? random.Next(5, 11) : random.Next(1, 6)); }
        return rolls;
    }
    internal static bool ValidMaterialRolls(Item item, GearState state)
    {
        if (state.MaterialRolls == null || state.MaterialRolls.Length > 2
            || state.DurabilityPercent != 0 && state.DurabilityPercent is < 10 or > 20) { return false; }
        if (!state.CraftedEquipment)
        { return state.DurabilityPercent == 0 && state.MaterialRolls.Length == 0
            && (state.Tier == GearTier.Normal || item.Origin is ItemOrigin.Vendor or ItemOrigin.Drop); }
        if (JewelleryCrafting.IsOutput(item.GetType())) { return JewelleryCrafting.Valid(item, state); }
        if (!Craftable(item.GetType())) { return false; }
        var native = GearDetails.Read(item);
        var resource = native.Resource;
        var family = CraftResources.GetType(resource);
        if (native.Material == GearMaterial.Metal && family != CraftResourceType.Metal
            || native.Material == GearMaterial.Wood && family != CraftResourceType.Wood
            || native.Material == GearMaterial.Leather && family != CraftResourceType.Leather) { return false; }
        bool weapon = item is BaseWeapon;
        if (resource is CraftResource.Gold or CraftResource.Verite)
        {
            string code = resource == CraftResource.Gold ? weapon ? "gold-find" : "magic-find" : weapon ? "mana-per-five" : "life-per-five";
            return state.MaterialRolls.Length == 1 && state.MaterialRolls[0].Code == code
                && state.MaterialRolls[0].Value >= 1 && state.MaterialRolls[0].Value <= (resource == CraftResource.Gold ? 2 : 1);
        }
        for (int choice = 0; choice < 4; choice++)
        {
            var elements = Elements(resource, choice);
            if (state.MaterialRolls.Length != elements.Length) { continue; }
            bool valid = true;
            for (int i = 0; i < elements.Length; i++)
            {
                var roll = state.MaterialRolls[i];
                if (roll.Code != (weapon ? "damage-" : "resistance-") + elements[i]
                    || roll.Value < (weapon ? 5 : 1) || roll.Value > (weapon ? 10 : 5)) { valid = false; break; }
            }
            if (valid) { return true; }
        }
        return false;
    }
}
