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

public sealed record InscriptionRecipe(string Id, string Type, string Name, string Category, double Skill, int Charges, CarpentryIngredient[] Ingredients);
public sealed class InscriptionRequest
{
    public PlayerMobile Owner { get; internal init; }
    public BaseTool Tool { get; internal init; }
    public InscriptionRecipe Row { get; internal init; }
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
public static class InscriptionCrafting
{
    public static InscriptionRecipe[] Recipes { get; private set; } = [];
    public static readonly string[] Categories = ["Identify", "Town Portal"];
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static readonly Dictionary<CraftItem, InscriptionRequest> _starting = new();
    private static readonly Dictionary<Serial, InscriptionRequest> _quotes = new();
    private static long _session;
    public static void Load()
    {
        Recipes = BalanceCatalog.Read<InscriptionRecipe>("inscription-utilities");
        var ids = new HashSet<string>();
        foreach (var r in Recipes)
        {
            if (!ids.Add(r.Id) || !IsOutput(EquipmentCrafting.Resolve(r.Type)) || r.Skill is < 0 or > 100
                || r.Charges is < 1 or > 10 || Array.IndexOf(Categories,r.Category) < 0 || r.Ingredients.Length is < 2 or > 3)
            { throw new InvalidDataException("Invalid Inscription utility recipe."); }
            foreach (var i in r.Ingredients)
            { if (i.Amount is < 1 or > 10 || EquipmentCrafting.Resolve(i.Type) is not { } type || !typeof(Item).IsAssignableFrom(type)) { throw new InvalidDataException("Invalid Inscription input."); } }
        }
        if (Recipes.Length != 14) { throw new InvalidDataException("Incomplete scroll/tome ladder."); }
    }
    public static void Configure() => EventSink.Disconnected += Invalidate;
    public static bool IsOutput(Type type) => type == typeof(IdentifyScroll) || type == typeof(TownPortalScroll) || type == typeof(IdentifyTome) || type == typeof(TownPortalTome);
    public static bool Supports(BaseTool tool) => tool is ScribesPen;
    internal static void Invalidate(Mobile owner) { _sessions.Remove(owner.Serial); _quotes.Remove(owner.Serial); }
    internal static void End(InscriptionRequest q)
    { if (_sessions.GetValueOrDefault(q.Owner.Serial) == q.Session) { _sessions.Remove(q.Owner.Serial); _quotes.Remove(q.Owner.Serial); } }
    private static bool Ready(PlayerMobile owner, BaseTool tool) => MaterialProcessing.ToolValid(owner, tool)
        && Supports(tool) && ClassProfessions.Active(owner, SkillName.Inscribe) && CraftFacilities.Allows(owner, SkillName.Inscribe);
    public static InscriptionRequest Quote(PlayerMobile owner, BaseTool tool, InscriptionRecipe row)
    {
        if (Array.IndexOf(Recipes, row) < 0 || !Ready(owner, tool) || !owner.CanBeginAction<CraftSystem>() || owner.Skills.Inscribe.Base < row.Skill) { return null; }
        var inputs = new List<CarpentryInput>();
        for (int group = 0; group < row.Ingredients.Length; group++)
        {
            var r = row.Ingredients[group]; int remaining = r.Amount; var type = EquipmentCrafting.Resolve(r.Type);
            foreach (var item in owner.Backpack.FindItemsByType<Item>(true))
            {
                if (remaining == 0) { break; }
                if (item.GetType() != type || !CarpentryCrafting.Component(owner, item)) { continue; }
                int take = Math.Min(remaining, item.Amount); inputs.Add(new(item, take, group)); remaining -= take;
            }
            if (remaining > 0) { return null; }
        }
        var recipe = new CraftItem(EquipmentCrafting.Resolve(row.Type), "Inscription", row.Name);
        recipe.AddSkill(SkillName.Inscribe, row.Skill, row.Skill + 50);
        double chance = recipe.GetSuccessChance(owner, null, DefInscription.CraftSystem, false, out bool skills);
        if (!skills || chance <= 0) { return null; }
        return _quotes[owner.Serial] = new() { Owner = owner, Tool = tool, Row = row, Recipe = recipe, Inputs = inputs, Chance = Math.Clamp(chance, 0, 1),
            Skill = owner.Skills.Inscribe.BaseFixedPoint, ToolParent = tool.Parent, Uses = tool.UsesRemaining, ToolOrigin = tool.Origin,
            ToolState = CraftTransactions.Profiles.Find(tool), Connection = owner.NetState,
            Profession = ClassProfessions.Profiles.Find(owner), Session = _sessions[owner.Serial] = ++_session };
    }
    private static bool Valid(InscriptionRequest q)
    {
        if (q is not { Claimed: false } || _sessions.GetValueOrDefault(q.Owner.Serial) != q.Session || !Ready(q.Owner, q.Tool)
            || q.Owner.Skills.Inscribe.BaseFixedPoint != q.Skill || !ReferenceEquals(q.Owner.NetState, q.Connection)
            || !ReferenceEquals(ClassProfessions.Profiles.Find(q.Owner), q.Profession) || q.Tool.Parent != q.ToolParent
            || q.Tool.UsesRemaining != q.Uses || q.Tool.Origin != q.ToolOrigin || CraftTransactions.Profiles.Find(q.Tool) != q.ToolState) { return false; }
        foreach (var input in q.Inputs) { if (!input.Valid(q.Owner)) { return false; } }
        return true;
    }
    public static bool Start(InscriptionRequest q)
    {
        if (q == null || q.Started || !Valid(q) || !q.Owner.CanBeginAction<CraftSystem>()) { return false; }
        q.Started = true; _starting.Add(q.Recipe, q);
        try { q.Recipe.Craft(q.Owner, DefInscription.CraftSystem, null, q.Tool, 0); }
        finally { _starting.Remove(q.Recipe); }
        return true;
    }
    internal static InscriptionRequest Attach(CraftTransactions.Request t) => _starting.GetValueOrDefault(t.Recipe) is { } q
        && q.Owner == t.Owner && q.Tool == t.Tool && q.Started ? q : null;
    internal static bool Bound(Mobile owner, CraftItem recipe) => _quotes.GetValueOrDefault(owner.Serial)?.Recipe == recipe;
    internal static bool Complete(CraftTransactions.Request ticket, Random random)
    {
        if (ticket.Claimed) { return false; } ticket.Claimed = true; var q = ticket.Scribing;
        if (q == null) { ticket.Owner.SendMessage("Choose a scroll or tome in the Inscription book."); return false; }
        bool valid = Valid(q) && CraftTransactions.Valid(ticket); q.Claimed = true;
        if (!valid) { End(q); q.Owner.SendMessage("The materials or crafting conditions changed. Nothing was consumed."); return false; }
        double chance = q.Recipe.GetSuccessChance(q.Owner, null, DefInscription.CraftSystem, false, out bool skills);
        if (!skills || Math.Clamp(chance, 0, 1) != q.Chance) { End(q); return false; }
        var output = ItemOrigins.Create(ItemOrigin.Crafted, () => q.Recipe.ItemType.CreateInstance<Item>());
        if (output == null) { End(q); return false; }
        output.PlayerConstructed = true;
        var bonus = q.Row.Skill == 100 && q.Row.Charges == 10 ? ItemOrigins.Create(ItemOrigin.Crafted, () => new TeleportScroll { PlayerConstructed = true }) : null;
        var consumes = new List<(Item Item, int Amount)>();
        foreach (var input in q.Inputs) { consumes.Add((input.Item, input.Take)); }
        var delivery = MarketDelivery.Create(q.Owner, consumes, bonus == null ? [output] : [output, bonus]);
        if (delivery == null) { End(q); output.Delete(); bonus?.Delete(); q.Owner.SendMessage("Make room for the tome and any bonus scroll. Nothing was consumed."); return false; }
        random ??= Random.Shared; bool success = random.NextDouble() < q.Chance;
        if (success)
        {
            if (output is UtilityTome tome) { tome.Finish(q.Row.Charges, CraftTransactions.Quality(q.Skill / 10.0, q.Row.Skill, random.Next(100))); }
            if (bonus != null && random.Next(100) != 0) { bonus.Delete(); bonus = null; delivery = MarketDelivery.Create(q.Owner, consumes, [output]); }
            delivery.Commit();
            if (bonus != null) { q.Owner.SendMessage("Your full-tome craft also produced a Teleport scroll!"); }
        }
        else
        {
            for (int group = 0; group < q.Row.Ingredients.Length; group++)
            {
                int remaining = (q.Row.Ingredients[group].Amount + 1) / 2;
                foreach (var input in q.Inputs)
                { if (input.Group == group && remaining > 0) { int take = Math.Min(remaining, input.Take); input.Item.Consume(take); remaining -= take; } }
            }
            output.Delete(); bonus?.Delete();
        }
        CraftTransactions.SpendUse(q.Owner, q.Tool, random);
        if (q.Tool.UsesRemaining < 1 && q.Tool.BreakOnDepletion) { q.Tool.Delete(); q.Owner.SendMessage("Your Inscription pen wore out."); }
        q.Recipe.GetSuccessChance(q.Owner, null, DefInscription.CraftSystem, true, out _); End(q);
        q.Owner.PlaySound(success ? 0x23D : 0x242);
        q.Owner.SendMessage(success ? "You inscribed one scroll or tome." : "The attempt failed: half of each material, rounded up, and one tool use were spent. Crafted-tool saving applies.");
        return true;
    }
    public static string Cost(InscriptionRecipe r)
    {
        var parts = new string[r.Ingredients.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            var input = r.Ingredients[i];
            string name = input.Type switch
            { "BlankScroll" => "blank scrolls", "BlackPearl" => "black pearl", "Bloodmoss" => "bloodmoss", "Leather" => "leather", _ => input.Type };
            parts[i] = $"{input.Amount} {name}";
        }
        return string.Join(", ", parts);
    }
}
