// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

internal sealed record MealInput(Item Item, object Parent, int Amount, int Quantity, int Take, int Group,
    int Art, int Hue, string Name, double Weight, ItemOrigin Origin, Poison Poison)
{
    internal static int Available(Item item) => item is SackFlour flour ? flour.Quantity : item.Amount;
    internal MealInput(Item item, int take, int group) : this(item, item.Parent, item.Amount, Available(item), take, group,
        item.ItemID, item.Hue, item.Name, item.Weight, item.Origin, (item as Food)?.Poison) { }
    internal bool Valid(PlayerMobile owner) => RuneCrafting.Accessible(owner, Item) && Item.Parent == Parent
        && Item.Amount == Amount && Available(Item) == Quantity && Item.ItemID == Art && Item.Hue == Hue
        && Item.Name == Name && Item.Weight == Weight && Item.Origin == Origin && (Item as Food)?.Poison == Poison;
    internal void Consume(int amount)
    { if (Item is SackFlour flour) { flour.Quantity -= amount; } else { Item.Consume(amount); } }
}
public sealed class MealRequest
{
    public PlayerMobile Owner { get; internal init; }
    public Skillet Tool { get; internal init; }
    public MealRecipe Row { get; internal init; }
    public CraftItem Recipe { get; internal init; }
    public double Chance { get; internal init; }
    internal List<MealInput> Inputs;
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
public static class MealCooking
{
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static readonly Dictionary<CraftItem, MealRequest> _starting = new();
    private static long _session;
    public static void Configure() => EventSink.Disconnected += Invalidate;
    internal static void Invalidate(Mobile owner) => _sessions.Remove(owner.Serial);
    internal static void End(MealRequest q)
    { if (_sessions.GetValueOrDefault(q.Owner.Serial) == q.Session) { _sessions.Remove(q.Owner.Serial); } }
    private static bool Ready(PlayerMobile owner, Skillet tool) => MaterialProcessing.ToolValid(owner, tool)
        && ClassProfessions.Active(owner, SkillName.Cooking) && CraftFacilities.Allows(owner, SkillName.Cooking);
    internal static int Quantity(MealRecipe r, int group) => group == r.Ingredients.Length ? r.Chips : r.Ingredients[group].Amount;
    public static MealRequest Quote(PlayerMobile owner, Skillet tool, MealRecipe row)
    {
        if (Array.IndexOf(Meals.Recipes, row) < 0 || !RareRecipes.Allowed(owner, RareRecipes.Meal(row)) || !Ready(owner, tool) || !owner.CanBeginAction<CraftSystem>() || owner.Skills.Cooking.Base < row.Skill) { return null; }
        var inputs = new List<MealInput>();
        for (int group = 0; group <= row.Ingredients.Length; group++)
        {
            int remaining = Quantity(row, group);
            var type = group == row.Ingredients.Length ? typeof(WoodenChips) : EquipmentCrafting.Resolve(row.Ingredients[group].Type);
            foreach (var item in owner.Backpack.FindItemsByType<Item>(true))
            {
                if (!RuneCrafting.Accessible(owner, item) || item.GetType() != type || item is Food { Poison: not null }) { continue; }
                if (remaining == 0) { break; }
                int take = Math.Min(remaining, MealInput.Available(item)); if (take == 0) { continue; }
                inputs.Add(new(item, take, group)); remaining -= take;
            }
            if (remaining > 0) { return null; }
        }
        var recipe = new CraftItem(typeof(PreparedMeal), "Prepared meals", row.Name); recipe.AddSkill(SkillName.Cooking, row.Skill, row.Skill + 50);
        double chance = recipe.GetSuccessChance(owner, null, DefCooking.CraftSystem, false, out bool skills);
        if (!skills || chance <= 0) { return null; }
        return new() { Owner = owner, Tool = tool, Row = row, Recipe = recipe, Inputs = inputs, Chance = Math.Clamp(chance, 0, 1),
            Skill = owner.Skills.Cooking.BaseFixedPoint, ToolParent = tool.Parent, Uses = tool.UsesRemaining, ToolOrigin = tool.Origin,
            ToolState = CraftTransactions.Profiles.Find(tool), Connection = owner.NetState,
            Profession = ClassProfessions.Profiles.Find(owner), Session = _sessions[owner.Serial] = ++_session };
    }
    private static bool Valid(MealRequest q)
    {
        if (q is not { Claimed: false } || !RareRecipes.Allowed(q.Owner, RareRecipes.Meal(q.Row)) || _sessions.GetValueOrDefault(q.Owner.Serial) != q.Session || !Ready(q.Owner, q.Tool)
            || q.Owner.Skills.Cooking.BaseFixedPoint != q.Skill || !ReferenceEquals(q.Owner.NetState, q.Connection)
            || !ReferenceEquals(ClassProfessions.Profiles.Find(q.Owner), q.Profession) || q.Tool.Parent != q.ToolParent
            || q.Tool.UsesRemaining != q.Uses || q.Tool.Origin != q.ToolOrigin || CraftTransactions.Profiles.Find(q.Tool) != q.ToolState) { return false; }
        foreach (var input in q.Inputs) { if (!input.Valid(q.Owner)) { return false; } }
        return true;
    }
    public static bool Start(MealRequest q)
    {
        if (q == null || q.Started || !Valid(q) || !q.Owner.CanBeginAction<CraftSystem>()) { return false; }
        q.Started = true; _starting.Add(q.Recipe, q);
        try { q.Recipe.Craft(q.Owner, DefCooking.CraftSystem, null, q.Tool, 0); }
        finally { _starting.Remove(q.Recipe); }
        return true;
    }
    internal static MealRequest Attach(CraftTransactions.Request ticket) => _starting.GetValueOrDefault(ticket.Recipe) is { } q
        && q.Owner == ticket.Owner && q.Tool == ticket.Tool && q.Started ? q : null;
    internal static bool Complete(CraftTransactions.Request ticket, Random random)
    {
        if (ticket.Claimed) { return false; } ticket.Claimed = true; var q = ticket.Meal;
        if (q == null) { ticket.Owner.SendMessage("Choose a meal on your skillet."); return false; }
        bool valid = Valid(q) && CraftTransactions.Valid(ticket); q.Claimed = true; End(q);
        if (!valid) { q.Owner.SendMessage("The ingredients or cooking conditions changed. Nothing was consumed."); return false; }
        double chance = q.Recipe.GetSuccessChance(q.Owner, null, DefCooking.CraftSystem, false, out bool skills);
        if (!skills || Math.Clamp(chance, 0, 1) != q.Chance) { return false; }
        var output = ItemOrigins.Create(ItemOrigin.Crafted, () => new PreparedMeal(q.Row.Id) { PlayerConstructed = true });
        var consumes = new List<(Item Item, int Amount)>();
        foreach (var input in q.Inputs)
        {
            if (input.Item is not SackFlour) { consumes.Add((input.Item, input.Take)); }
            else if (input.Take == input.Quantity) { consumes.Add((input.Item, input.Amount)); }
        }
        var delivery = MarketDelivery.Create(q.Owner, consumes, [output]);
        if (delivery == null) { output.Delete(); q.Owner.SendMessage("Make room for the meal. Nothing was consumed."); return false; }
        random ??= Random.Shared; bool success = random.NextDouble() < q.Chance;
        if (success)
        {
            delivery.Commit();
            foreach (var input in q.Inputs) { if (input.Item is SackFlour && input.Take < input.Quantity) { input.Consume(input.Take); } }
        }
        else
        {
            for (int group = 0; group <= q.Row.Ingredients.Length; group++)
            {
                int remaining = (Quantity(q.Row, group) + 1) / 2;
                foreach (var input in q.Inputs)
                { if (input.Group == group && remaining > 0) { int take = Math.Min(remaining, input.Take); input.Consume(take); remaining -= take; } }
            }
            output.Delete();
        }
        CraftTransactions.SpendUse(q.Owner, q.Tool, random);
        if (q.Tool.UsesRemaining < 1 && q.Tool.BreakOnDepletion) { q.Tool.Delete(); q.Owner.SendMessage("Your skillet wore out."); }
        q.Recipe.GetSuccessChance(q.Owner, null, DefCooking.CraftSystem, true, out _);
        q.Owner.PlaySound(success ? 0x57 : 0x242);
        q.Owner.SendMessage(success ? "You cooked one meal." : "The recipe failed: half of each ingredient and the fuel, rounded up, and one tool use were spent. Crafted-tool saving applies.");
        return true;
    }
}
