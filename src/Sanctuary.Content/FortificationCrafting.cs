// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class FortificationCraftRequest
{
    internal PlayerMobile Owner;
    internal MortarPestle Tool;
    internal CraftItem Recipe;
    internal List<AlchemyInput> Inputs;
    internal object ToolParent;
    internal int Uses, Skill;
    internal ItemOrigin Origin;
    internal CraftToolState ToolState;
    internal NetState Connection;
    internal ProfessionState Profession;
    internal double Chance;
    internal bool Started, Claimed;
}
public static class FortificationCrafting
{
    private static readonly Dictionary<CraftItem, FortificationCraftRequest> _starting = new();
    internal static bool Ready(PlayerMobile owner, MortarPestle tool) => MaterialProcessing.ToolValid(owner, tool)
        && ClassProfessions.Active(owner, SkillName.Alchemy) && CraftFacilities.Allows(owner, SkillName.Alchemy);
    internal static FortificationCraftRequest Quote(PlayerMobile owner, MortarPestle tool)
    {
        var rules = Fortification.Settings;
        if (!Ready(owner, tool) || !owner.CanBeginAction<CraftSystem>() || owner.Skills.Alchemy.Base < rules.RequiredSkill) { return null; }
        var inputs = new List<AlchemyInput>(); int remaining = rules.ChitinCost;
        foreach (var item in owner.Backpack.FindItemsByType<Chitin>(true))
        {
            if (!RuneCrafting.Accessible(owner, item)) { continue; }
            int take = Math.Min(remaining, item.Amount); inputs.Add(new(item, take, 0)); remaining -= take;
            if (remaining == 0) { break; }
        }
        if (remaining != 0) { return null; }
        var recipe = new CraftItem(typeof(FortifyingPowder), "Alchemy materials", "powder of fortifying");
        recipe.AddSkill(SkillName.Alchemy, rules.RequiredSkill, rules.RequiredSkill + 50);
        double chance = recipe.GetSuccessChance(owner, null, DefAlchemy.CraftSystem, false, out bool skills);
        if (!skills || chance <= 0) { return null; }
        return new() { Owner = owner, Tool = tool, Recipe = recipe, Inputs = inputs, ToolParent = tool.Parent,
            Uses = tool.UsesRemaining, Origin = tool.Origin, ToolState = CraftTransactions.Profiles.Find(tool),
            Skill = owner.Skills.Alchemy.BaseFixedPoint, Connection = owner.NetState,
            Profession = ClassProfessions.Profiles.Find(owner), Chance = Math.Clamp(chance, 0, 1) };
    }
    private static bool Valid(FortificationCraftRequest q)
    {
        if (q is not { Claimed: false } || !Ready(q.Owner, q.Tool) || q.Skill != q.Owner.Skills.Alchemy.BaseFixedPoint
            || q.Connection != q.Owner.NetState || !ReferenceEquals(q.Profession, ClassProfessions.Profiles.Find(q.Owner))
            || q.Tool.Parent != q.ToolParent || q.Tool.UsesRemaining != q.Uses || q.Tool.Origin != q.Origin
            || q.ToolState != CraftTransactions.Profiles.Find(q.Tool)) { return false; }
        foreach (var input in q.Inputs) { if (!input.Valid(q.Owner)) { return false; } }
        return true;
    }
    internal static bool Start(FortificationCraftRequest q)
    {
        if (q == null || q.Started || !Valid(q) || !q.Owner.CanBeginAction<CraftSystem>()) { return false; }
        q.Started = true; _starting.Add(q.Recipe, q);
        try { q.Recipe.Craft(q.Owner, DefAlchemy.CraftSystem, null, q.Tool, 0); }
        finally { _starting.Remove(q.Recipe); }
        return true;
    }
    internal static FortificationCraftRequest Attach(CraftTransactions.Request ticket) => _starting.GetValueOrDefault(ticket.Recipe) is { } q
        && q.Owner == ticket.Owner && q.Tool == ticket.Tool && q.Started ? q : null;
    internal static bool Complete(CraftTransactions.Request ticket, Random random)
    {
        if (ticket.Claimed) { return false; } ticket.Claimed = true;
        var q = ticket.Fortification;
        if (q == null || !Valid(q) || !CraftTransactions.Valid(ticket)) { if (q != null) { q.Claimed = true; } return false; }
        q.Claimed = true;
        double chance = q.Recipe.GetSuccessChance(q.Owner, null, DefAlchemy.CraftSystem, false, out bool skills);
        if (!skills || Math.Clamp(chance, 0, 1) != q.Chance) { return false; }
        var output = ItemOrigins.Create(ItemOrigin.Crafted, () => new FortifyingPowder { PlayerConstructed = true });
        var consumed = new List<(Item Item, int Amount)>();
        foreach (var input in q.Inputs) { consumed.Add((input.Item, input.Take)); }
        var delivery = MarketDelivery.Create(q.Owner, consumed, [output]);
        if (delivery == null) { output.Delete(); return false; }
        random ??= Random.Shared;
        bool success = random.NextDouble() < q.Chance;
        if (success) { delivery.Commit(); }
        else
        {
            int remaining = (Fortification.Settings.ChitinCost + 1) / 2;
            foreach (var input in q.Inputs)
            { int take = Math.Min(remaining, input.Take); if (take > 0) { input.Item.Consume(take); remaining -= take; } }
            output.Delete();
        }
        CraftTransactions.SpendUse(q.Owner, q.Tool, random);
        if (q.Tool.UsesRemaining < 1 && q.Tool.BreakOnDepletion) { q.Tool.Delete(); }
        q.Recipe.GetSuccessChance(q.Owner, null, DefAlchemy.CraftSystem, true, out _);
        q.Owner.PlaySound(success ? 0x240 : 0x242);
        q.Owner.SendMessage(success ? "You crafted one powder of fortifying." : "The mixture failed: half the chitin and one mortar use were spent. Crafted-tool saving applies.");
        return true;
    }
}
