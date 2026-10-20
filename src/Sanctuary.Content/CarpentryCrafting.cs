// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed record CarpentryIngredient(string Type, int Amount);
public sealed record CarpentryRecipe(string Type, string Name, string Category, double Skill, CarpentryIngredient[] Ingredients);
internal sealed record CarpentryInput(Item Item, object Parent, int Amount, int Take, int Group, int Art, int Hue,
    double Weight, ItemOrigin Origin)
{
    internal CarpentryInput(Item item, int take, int group) : this(item, item.Parent, item.Amount, take, group,
        item.ItemID, item.Hue, item.Weight, item.Origin) { }
    internal bool Valid(PlayerMobile owner) => CarpentryCrafting.Component(owner, Item) && Item.Parent == Parent
        && Item.Amount == Amount && Item.ItemID == Art && Item.Hue == Hue && Item.Weight == Weight && Item.Origin == Origin;
}
public sealed class CarpentryRequest
{
    public PlayerMobile Owner { get; internal init; }
    public BaseTool Tool { get; internal init; }
    public CarpentryRecipe Row { get; internal init; }
    public CraftItem Recipe { get; internal init; }
    public double Chance { get; internal init; }
    internal List<CarpentryInput> Inputs;
    internal object ToolParent;
    internal int Uses, Skill;
    internal ItemOrigin ToolOrigin;
    internal CraftToolState ToolState;
    internal NetState Connection;
    internal ProfessionState Profession;
    internal long Session;
    public bool Started { get; internal set; }
    public bool Claimed { get; internal set; }
}
public static class CarpentryCrafting
{
    public static CarpentryRecipe[] Recipes { get; private set; } = [];
    public static readonly string[] Categories = ["Parts", "Seating", "Tables", "Storage", "Household", "Stations"];
    private static readonly HashSet<Type> _outputs = new();
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static readonly Dictionary<CraftItem, CarpentryRequest> _starting = new();
    private static readonly Dictionary<Serial, CarpentryRequest> _quotes = new();
    private static long _session;
    public static void Load()
    {
        Recipes = BalanceCatalog.Read<CarpentryRecipe>("carpentry");
        foreach (var r in Recipes)
        {
            var type = EquipmentCrafting.Resolve(r.Type);
            if (!_outputs.Add(type) || !typeof(Item).IsAssignableFrom(type) || EquipmentCrafting.IsOutput(type)
                || typeof(BaseTool).IsAssignableFrom(type) || r.Skill is < 0 or > 115 || Array.IndexOf(Categories, r.Category) < 0
                || r.Name is not { Length: > 0 and <= 45 } || r.Ingredients is not { Length: >= 1 and <= 4 })
            { throw new InvalidDataException("Invalid Carpentry recipe."); }
            var inputs = new HashSet<Type>();
            foreach (var i in r.Ingredients)
            {
                if (!inputs.Add(EquipmentCrafting.Resolve(i.Type)) || i.Amount is < 1 or > 200)
                { throw new InvalidDataException("Invalid Carpentry material."); }
            }
        }
    }
    public static void Configure() => EventSink.Disconnected += Invalidate;
    public static bool IsOutput(Type type) => _outputs.Contains(type);
    public static bool Supports(BaseTool tool) => tool?.CraftSystem == DefCarpentry.CraftSystem;
    internal static void Invalidate(Mobile owner) { _sessions.Remove(owner.Serial); _quotes.Remove(owner.Serial); }
    internal static void End(CarpentryRequest q)
    { if (_sessions.GetValueOrDefault(q.Owner.Serial) == q.Session) { _sessions.Remove(q.Owner.Serial); _quotes.Remove(q.Owner.Serial); } }
    internal static bool Component(PlayerMobile owner, Item item)
    {
        if (!RuneCrafting.Accessible(owner, item)) { return false; }
        for (Item ancestor = item; ancestor != owner.Backpack; ancestor = ancestor.Parent as Item)
        {
            if (ancestor == null || ancestor is Container { TotalItems: > 0 } && ancestor == item
                || ancestor is LockableContainer { Locked: true } || ancestor is TrappableContainer { TrapType: not TrapType.None }) { return false; }
        }
        return true;
    }
    private static bool Ready(PlayerMobile owner, BaseTool tool) => MaterialProcessing.ToolValid(owner, tool)
        && Supports(tool) && ClassProfessions.Active(owner, SkillName.Carpentry) && CraftFacilities.Allows(owner, SkillName.Carpentry);
    public static CarpentryRequest Quote(PlayerMobile owner, BaseTool tool, CarpentryRecipe row)
    {
        if (Array.IndexOf(Recipes, row) < 0 || !Ready(owner, tool) || !owner.CanBeginAction<CraftSystem>() || owner.Skills.Carpentry.Base < row.Skill) { return null; }
        var inputs = new List<CarpentryInput>();
        for (int group = 0; group < row.Ingredients.Length; group++)
        {
            var r = row.Ingredients[group]; int remaining = r.Amount; var type = EquipmentCrafting.Resolve(r.Type);
            foreach (var item in owner.Backpack.FindItemsByType<Item>(true))
            {
                if (remaining == 0) { break; }
                if (item.GetType() != type || !Component(owner, item)) { continue; }
                int take = Math.Min(remaining, item.Amount); inputs.Add(new(item, take, group)); remaining -= take;
            }
            if (remaining > 0) { return null; }
        }
        var recipe = new CraftItem(EquipmentCrafting.Resolve(row.Type), "Carpentry", row.Name);
        recipe.AddSkill(SkillName.Carpentry, row.Skill, row.Skill + 50);
        double chance = recipe.GetSuccessChance(owner, null, DefCarpentry.CraftSystem, false, out bool skills);
        if (!skills || chance <= 0) { return null; }
        return _quotes[owner.Serial] = new() { Owner = owner, Tool = tool, Row = row, Recipe = recipe, Inputs = inputs, Chance = Math.Clamp(chance, 0, 1),
            Skill = owner.Skills.Carpentry.BaseFixedPoint, ToolParent = tool.Parent, Uses = tool.UsesRemaining, ToolOrigin = tool.Origin,
            ToolState = CraftTransactions.Profiles.Find(tool), Connection = owner.NetState,
            Profession = ClassProfessions.Profiles.Find(owner), Session = _sessions[owner.Serial] = ++_session };
    }
    private static bool Valid(CarpentryRequest q)
    {
        if (q is not { Claimed: false } || _sessions.GetValueOrDefault(q.Owner.Serial) != q.Session || !Ready(q.Owner, q.Tool)
            || q.Owner.Skills.Carpentry.BaseFixedPoint != q.Skill || !ReferenceEquals(q.Owner.NetState, q.Connection)
            || !ReferenceEquals(ClassProfessions.Profiles.Find(q.Owner), q.Profession) || q.Tool.Parent != q.ToolParent
            || q.Tool.UsesRemaining != q.Uses || q.Tool.Origin != q.ToolOrigin || CraftTransactions.Profiles.Find(q.Tool) != q.ToolState) { return false; }
        foreach (var input in q.Inputs) { if (!input.Valid(q.Owner)) { return false; } }
        return true;
    }
    public static bool Start(CarpentryRequest q)
    {
        if (q == null || q.Started || !Valid(q) || !q.Owner.CanBeginAction<CraftSystem>()) { return false; }
        q.Started = true; _starting.Add(q.Recipe, q);
        try { q.Recipe.Craft(q.Owner, DefCarpentry.CraftSystem, null, q.Tool, 0); }
        finally { _starting.Remove(q.Recipe); }
        return true;
    }
    internal static CarpentryRequest Attach(CraftTransactions.Request t) => _starting.GetValueOrDefault(t.Recipe) is { } q
        && q.Owner == t.Owner && q.Tool == t.Tool && q.Started ? q : null;
    internal static bool Bound(Mobile owner, CraftItem recipe) => _quotes.GetValueOrDefault(owner.Serial)?.Recipe == recipe;
    internal static bool Complete(CraftTransactions.Request ticket, Random random)
    {
        if (ticket.Claimed) { return false; } ticket.Claimed = true; var q = ticket.Carpentry;
        if (q == null) { ticket.Owner.SendMessage("Choose furniture or a keg in the Carpentry book."); return false; }
        bool valid = Valid(q) && CraftTransactions.Valid(ticket); q.Claimed = true;
        if (!valid) { End(q); q.Owner.SendMessage("The materials or crafting conditions changed. Nothing was consumed."); return false; }
        double chance = q.Recipe.GetSuccessChance(q.Owner, null, DefCarpentry.CraftSystem, false, out bool skills);
        if (!skills || Math.Clamp(chance, 0, 1) != q.Chance) { End(q); return false; }
        var output = ItemOrigins.Create(ItemOrigin.Crafted, () => q.Recipe.ItemType.CreateInstance<Item>());
        if (output == null) { End(q); return false; }
        output.PlayerConstructed = true;
        var consumes = new List<(Item Item, int Amount)>();
        foreach (var input in q.Inputs) { consumes.Add((input.Item, input.Take)); }
        var delivery = MarketDelivery.Create(q.Owner, consumes, [output]);
        if (delivery == null) { End(q); output.Delete(); q.Owner.SendMessage("Make room for the finished object. Nothing was consumed."); return false; }
        random ??= Random.Shared; bool success = random.NextDouble() < q.Chance;
        if (success) { delivery.Commit(); }
        else
        {
            for (int group = 0; group < q.Row.Ingredients.Length; group++)
            {
                int remaining = (q.Row.Ingredients[group].Amount + 1) / 2;
                foreach (var input in q.Inputs)
                { if (input.Group == group && remaining > 0) { int take = Math.Min(remaining, input.Take); input.Item.Consume(take); remaining -= take; } }
            }
            output.Delete();
        }
        CraftTransactions.SpendUse(q.Owner, q.Tool, random);
        if (q.Tool.UsesRemaining < 1 && q.Tool.BreakOnDepletion) { q.Tool.Delete(); q.Owner.SendMessage("Your Carpentry tool wore out."); }
        q.Recipe.GetSuccessChance(q.Owner, null, DefCarpentry.CraftSystem, true, out _); End(q);
        q.Owner.PlaySound(success ? 0x23D : 0x242);
        q.Owner.SendMessage(success ? "You crafted one object." : "The attempt failed: half of each material, rounded up, and one tool use were spent. Crafted-tool saving applies.");
        return true;
    }
    public static string Cost(CarpentryRecipe r)
    {
        var parts = new string[r.Ingredients.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            var input = r.Ingredients[i];
            string name = input.Type switch
            {
                "Board" => "boards", "IronIngot" => "iron ingots", "Cloth" => "cloth",
                "BarrelStaves" => "barrel staves", "BarrelLid" => "barrel lid", "BarrelHoops" => "barrel hoops",
                "Keg" => "empty wooden keg", "Bottle" => "empty bottles", "BarrelTap" => "barrel tap", _ => input.Type
            };
            parts[i] = $"{input.Amount} {name}";
        }
        return string.Join(", ", parts);
    }
}
