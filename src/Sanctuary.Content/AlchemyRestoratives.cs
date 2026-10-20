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

public sealed record AlchemyReagent(string Type, string Name, int Amount);
public sealed record AlchemyRecipe(RestorationKind Kind, int Tier, double Skill, int HealingTier, int Healing,
    int ManaTier, int Mana, int Bottles, AlchemyReagent[] Reagents = null);
internal sealed record AlchemyInput(Item Item, object Parent, int Amount, int Take, int Group, int Art, int Hue,
    string Name, double Weight, ItemOrigin Origin, RestorationKind? Kind, int Tier)
{
    internal AlchemyInput(Item item, int take, int group) : this(item, item.Parent, item.Amount, take, group,
        item.ItemID, item.Hue, item.Name, item.Weight, item.Origin, (item as RestorationPotion)?.Kind,
        (item as RestorationPotion)?.Tier ?? 0) { }
    internal bool Valid(PlayerMobile owner) => RuneCrafting.Accessible(owner, Item) && Item.Parent == Parent
        && Item.Amount == Amount && Item.ItemID == Art && Item.Hue == Hue && Item.Name == Name && Item.Weight == Weight
        && Item.Origin == Origin && (Item as RestorationPotion)?.Kind == Kind && ((Item as RestorationPotion)?.Tier ?? 0) == Tier;
}
public sealed class AlchemyRequest
{
    public PlayerMobile Owner { get; internal init; }
    public MortarPestle Tool { get; internal init; }
    public AlchemyRecipe Row { get; internal init; }
    public CraftItem Recipe { get; internal init; }
    public double Chance { get; internal init; }
    internal List<AlchemyInput> Inputs;
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

public static class AlchemyRestoratives
{
    public static AlchemyRecipe[] Recipes { get; private set; } = [];
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static readonly Dictionary<CraftItem, AlchemyRequest> _starting = new();
    private static long _session;
    public static void Load()
    {
        Recipes = BalanceCatalog.Read<AlchemyRecipe>("alchemy-restoratives");
        if (Recipes.Length != 9) { throw new InvalidDataException("Nine Alchemy potions are required."); }
        var outputs = new HashSet<(RestorationKind, int)>();
        foreach (var row in Recipes)
        {
            if (!outputs.Add((row.Kind, row.Tier)) || (row.Kind != RestorationKind.CombatBuff && PotionBeltCatalog.Potion(row.Kind, row.Tier).DurationMs != 0)
                || row.Skill is < 0 or > 120 || row.Bottles != 1 || row.Healing is < 0 or > 100 || row.Mana is < 0 or > 100
                || row.Healing + row.Mana == 0 && row.Kind != RestorationKind.CombatBuff || row.Healing > 0 && row.HealingTier is < 1 or > 5
                || row.Mana > 0 && row.ManaTier is < 1 or > 5)
            { throw new InvalidDataException("Invalid restorative mixture."); }
            var seen = new HashSet<Type>();
            foreach (var reagent in row.Reagents ?? [])
            {
                var type = EquipmentCrafting.Resolve(reagent.Type);
                if (!typeof(BaseReagent).IsAssignableFrom(type) || !seen.Add(type) || string.IsNullOrWhiteSpace(reagent.Name)
                    || reagent.Name.Length > 30 || reagent.Amount is < 1 or > 100)
                { throw new InvalidDataException("Invalid Alchemy reagent."); }
            }
            if (row.Kind == RestorationKind.CombatBuff && (seen.Count < 2 || row.Healing + row.Mana != 0))
            { throw new InvalidDataException("Combat potions require their reagent recipe."); }
        }
    }
    public static void Configure() => EventSink.Disconnected += Invalidate;
    internal static void Invalidate(Mobile owner) => _sessions.Remove(owner.Serial);
    internal static void End(AlchemyRequest q)
    { if (_sessions.GetValueOrDefault(q.Owner.Serial) == q.Session) { _sessions.Remove(q.Owner.Serial); } }
    // These terminal leaves already carry their original treasure-table weights.
    // Rejuvenation and every unsupported outcome retain their empty probability mass.
    internal static RestorationPotion Drop(string leaf) => leaf is { Length: 3 } && leaf[1] == 'p'
        && leaf[2] is >= '1' and <= '5' && leaf[0] is 'h' or 'm'
        ? ItemOrigins.Create(ItemOrigin.Drop, () => new RestorationPotion(leaf[0] == 'h' ? RestorationKind.Healing : RestorationKind.Mana, leaf[2] - '0')) : null;
    private static int Quantity(AlchemyRecipe row, int group) => group == 0 ? row.Healing : group == 1 ? row.Mana
        : group == 2 ? row.Bottles : row.Reagents[group - 3].Amount;
    private static bool Ready(PlayerMobile owner, MortarPestle tool) => MaterialProcessing.ToolValid(owner, tool)
        && ClassProfessions.Active(owner, SkillName.Alchemy) && CraftFacilities.Allows(owner, SkillName.Alchemy);
    public static AlchemyRequest Quote(PlayerMobile owner, MortarPestle tool, AlchemyRecipe row)
    {
        if (Array.IndexOf(Recipes, row) < 0 || !Ready(owner, tool) || !owner.CanBeginAction<CraftSystem>() || owner.Skills.Alchemy.Base < row.Skill) { return null; }
        var inputs = new List<AlchemyInput>();
        for (int group = 0; group < 3 + (row.Reagents?.Length ?? 0); group++)
        {
            int remaining = Quantity(row, group);
            foreach (var item in owner.Backpack.FindItemsByType<Item>(true))
            {
                if (!RuneCrafting.Accessible(owner, item) || (group >= 3 ? item.GetType() != EquipmentCrafting.Resolve(row.Reagents[group - 3].Type) : group == 2 ? item is not Bottle
                    : item is not RestorationPotion p || p.Kind != (group == 0 ? RestorationKind.Healing : RestorationKind.Mana)
                        || p.Tier != (group == 0 ? row.HealingTier : row.ManaTier))) { continue; }
                if (remaining == 0) { break; }
                int take = Math.Min(remaining, item.Amount); inputs.Add(new(item, take, group)); remaining -= take;
            }
            if (remaining > 0) { return null; }
        }
        var recipe = new CraftItem(typeof(RestorationPotion), "Alchemy potions", PotionBeltCatalog.Potion(row.Kind, row.Tier).Name);
        // The skill-120 mastery recipe is guaranteed at the cap; the explicit quote gate remains 120.
        recipe.AddSkill(SkillName.Alchemy, row.Skill == 120 ? 119.9 : row.Skill, row.Skill == 120 ? 120 : row.Skill + 50);
        double chance = recipe.GetSuccessChance(owner, null, DefAlchemy.CraftSystem, false, out bool skills);
        if (!skills || chance <= 0) { return null; }
        return new() { Owner = owner, Tool = tool, Row = row, Recipe = recipe, Inputs = inputs, Chance = Math.Clamp(chance, 0, 1),
            Skill = owner.Skills.Alchemy.BaseFixedPoint, ToolParent = tool.Parent, Uses = tool.UsesRemaining,
            ToolOrigin = tool.Origin, ToolState = CraftTransactions.Profiles.Find(tool), Connection = owner.NetState,
            Profession = ClassProfessions.Profiles.Find(owner), Session = _sessions[owner.Serial] = ++_session };
    }
    private static bool Valid(AlchemyRequest q)
    {
        if (q is not { Claimed: false } || _sessions.GetValueOrDefault(q.Owner.Serial) != q.Session
            || !Ready(q.Owner, q.Tool) || q.Owner.Skills.Alchemy.BaseFixedPoint != q.Skill
            || !ReferenceEquals(q.Owner.NetState, q.Connection) || !ReferenceEquals(ClassProfessions.Profiles.Find(q.Owner), q.Profession)
            || q.Tool.Parent != q.ToolParent || q.Tool.UsesRemaining != q.Uses || q.Tool.Origin != q.ToolOrigin
            || CraftTransactions.Profiles.Find(q.Tool) != q.ToolState) { return false; }
        foreach (var input in q.Inputs) { if (!input.Valid(q.Owner)) { return false; } }
        return true;
    }
    public static bool Start(AlchemyRequest q)
    {
        if (q == null || q.Started || !Valid(q) || !q.Owner.CanBeginAction<CraftSystem>()) { return false; }
        q.Started = true; _starting.Add(q.Recipe, q);
        try { q.Recipe.Craft(q.Owner, DefAlchemy.CraftSystem, null, q.Tool, 0); }
        finally { _starting.Remove(q.Recipe); }
        return true;
    }
    internal static AlchemyRequest Attach(CraftTransactions.Request ticket) => _starting.GetValueOrDefault(ticket.Recipe) is { } q
        && q.Owner == ticket.Owner && q.Tool == ticket.Tool && q.Started ? q : null;
    internal static bool Complete(CraftTransactions.Request ticket, Random random)
    {
        if (ticket.Claimed) { return false; } ticket.Claimed = true; var q = ticket.Alchemy;
        if (q == null) { ticket.Owner.SendMessage("Choose an Alchemy potion on your mortar and pestle."); return false; }
        bool valid = Valid(q) && CraftTransactions.Valid(ticket);
        q.Claimed = true; End(q);
        if (!valid) { q.Owner.SendMessage("The ingredients or crafting conditions changed. Nothing was consumed."); return false; }
        double chance = q.Recipe.GetSuccessChance(q.Owner, null, DefAlchemy.CraftSystem, false, out bool skills);
        if (!skills || Math.Clamp(chance, 0, 1) != q.Chance) { return false; }
        var output = ItemOrigins.Create(ItemOrigin.Crafted, () => new RestorationPotion(q.Row.Kind, q.Row.Tier) { PlayerConstructed = true });
        var consumes = new List<(Item Item, int Amount)>();
        foreach (var input in q.Inputs) { consumes.Add((input.Item, input.Take)); }
        var delivery = MarketDelivery.Create(q.Owner, consumes, [output]);
        if (delivery == null) { output.Delete(); q.Owner.SendMessage("Make room for the potion. Nothing was consumed."); return false; }
        random ??= Random.Shared;
        bool success = random.NextDouble() < q.Chance;
        if (success) { delivery.Commit(); }
        else
        {
            for (int group = 0; group < 3 + (q.Row.Reagents?.Length ?? 0); group++)
            {
                int remaining = (Quantity(q.Row, group) + 1) / 2;
                foreach (var input in q.Inputs)
                { if (input.Group == group && remaining > 0) { int take = Math.Min(remaining, input.Take); input.Item.Consume(take); remaining -= take; } }
            }
            output.Delete();
        }
        CraftTransactions.SpendUse(q.Owner, q.Tool, random);
        if (q.Tool.UsesRemaining < 1 && q.Tool.BreakOnDepletion) { q.Tool.Delete(); q.Owner.SendMessage("Your mortar and pestle wore out."); }
        q.Recipe.GetSuccessChance(q.Owner, null, DefAlchemy.CraftSystem, true, out _);
        q.Owner.PlaySound(success ? 0x240 : 0x242);
        q.Owner.SendMessage(success ? "You crafted one potion." : "The mixture failed: half of each ingredient, rounded up, and one tool use were spent. Crafted-tool saving applies.");
        return true;
    }
}
