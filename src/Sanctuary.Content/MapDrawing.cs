// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class SurveyRequest
{
    public PlayerMobile Owner { get; internal init; }
    public MapmakersPen Tool { get; internal init; }
    public SurveyRecipe Row { get; internal init; }
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
public static class MapDrawing
{
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static readonly Dictionary<CraftItem, SurveyRequest> _starting = new();
    private static long _session;
    public static void Configure() => EventSink.Disconnected += Invalidate;
    internal static void Invalidate(Mobile owner) => _sessions.Remove(owner.Serial);
    internal static void End(SurveyRequest q)
    { if (_sessions.GetValueOrDefault(q.Owner.Serial) == q.Session) { _sessions.Remove(q.Owner.Serial); } }
    private static bool Ready(PlayerMobile owner, MapmakersPen tool) => MaterialProcessing.ToolValid(owner, tool)
        && ClassProfessions.Active(owner, SkillName.Cartography) && CraftFacilities.Allows(owner, SkillName.Cartography);
    public static SurveyRequest Quote(PlayerMobile owner, MapmakersPen tool, SurveyRecipe row)
    {
        if (Array.IndexOf(MapSurveys.Recipes, row) < 0 || !Ready(owner, tool) || !owner.CanBeginAction<CraftSystem>() || owner.Skills.Cartography.Base < row.Skill) { return null; }
        var inputs = new List<CarpentryInput>();
        foreach (var item in owner.Backpack.FindItemsByType<BlankMap>(true))
        {
            if (item.GetType() == typeof(BlankMap) && CarpentryCrafting.Component(owner, item)) { inputs.Add(new(item, 1, 0)); break; }
        }
        if (inputs.Count == 0) { return null; }
        var recipe = new CraftItem(typeof(SurveyMap), "Consumable maps", row.Name); recipe.AddSkill(SkillName.Cartography, row.Skill, row.Maximum);
        double chance = recipe.GetSuccessChance(owner, null, DefCartography.CraftSystem, false, out bool skills);
        if (!skills || chance <= 0) { return null; }
        return new() { Owner = owner, Tool = tool, Row = row, Recipe = recipe, Inputs = inputs, Chance = Math.Clamp(chance, 0, 1),
            Skill = owner.Skills.Cartography.BaseFixedPoint, ToolParent = tool.Parent, Uses = tool.UsesRemaining, ToolOrigin = tool.Origin,
            ToolState = CraftTransactions.Profiles.Find(tool), Connection = owner.NetState,
            Profession = ClassProfessions.Profiles.Find(owner), Session = _sessions[owner.Serial] = ++_session };
    }
    private static bool Valid(SurveyRequest q)
    {
        if (q is not { Claimed: false } || _sessions.GetValueOrDefault(q.Owner.Serial) != q.Session || !Ready(q.Owner, q.Tool)
            || q.Owner.Skills.Cartography.BaseFixedPoint != q.Skill || !ReferenceEquals(q.Owner.NetState, q.Connection)
            || !ReferenceEquals(ClassProfessions.Profiles.Find(q.Owner), q.Profession) || q.Tool.Parent != q.ToolParent
            || q.Tool.UsesRemaining != q.Uses || q.Tool.Origin != q.ToolOrigin || CraftTransactions.Profiles.Find(q.Tool) != q.ToolState) { return false; }
        foreach (var input in q.Inputs) { if (!input.Valid(q.Owner)) { return false; } }
        return true;
    }
    public static bool Start(SurveyRequest q)
    {
        if (q == null || q.Started || !Valid(q) || !q.Owner.CanBeginAction<CraftSystem>()) { return false; }
        q.Started = true; _starting.Add(q.Recipe, q);
        try { q.Recipe.Craft(q.Owner, DefCartography.CraftSystem, null, q.Tool, 0); }
        finally { _starting.Remove(q.Recipe); }
        return true;
    }
    internal static SurveyRequest Attach(CraftTransactions.Request ticket) => _starting.GetValueOrDefault(ticket.Recipe) is { } q
        && q.Owner == ticket.Owner && q.Tool == ticket.Tool && q.Started ? q : null;
    internal static bool Complete(CraftTransactions.Request ticket, Random random)
    {
        if (ticket.Claimed) { return false; } ticket.Claimed = true; var q = ticket.Survey;
        if (q == null) { ticket.Owner.SendMessage("Choose a consumable map on your Cartography pen."); return false; }
        bool valid = Valid(q) && CraftTransactions.Valid(ticket); q.Claimed = true; End(q);
        if (!valid) { q.Owner.SendMessage("The map or drawing conditions changed. Nothing was consumed."); return false; }
        double chance = q.Recipe.GetSuccessChance(q.Owner, null, DefCartography.CraftSystem, false, out bool skills);
        if (!skills || Math.Clamp(chance, 0, 1) != q.Chance) { return false; }
        var output = ItemOrigins.Create(ItemOrigin.Crafted, () => new SurveyMap(q.Row.Id) { PlayerConstructed = true });
        var consumes = new List<(Item Item, int Amount)>();
        foreach (var input in q.Inputs) { consumes.Add((input.Item, input.Take)); }
        var delivery = MarketDelivery.Create(q.Owner, consumes, [output]);
        if (delivery == null) { output.Delete(); q.Owner.SendMessage("Make room for the map. Nothing was consumed."); return false; }
        random ??= Random.Shared; bool success = random.NextDouble() < q.Chance;
        if (success) { delivery.Commit(); }
        else { q.Inputs[0].Item.Consume(1); output.Delete(); }
        CraftTransactions.SpendUse(q.Owner, q.Tool, random);
        if (q.Tool.UsesRemaining < 1 && q.Tool.BreakOnDepletion) { q.Tool.Delete(); q.Owner.SendMessage("Your Cartography pen wore out."); }
        q.Recipe.GetSuccessChance(q.Owner, null, DefCartography.CraftSystem, true, out _);
        q.Owner.PlaySound(success ? 0x57 : 0x242);
        q.Owner.SendMessage(success ? "You drew one consumable map." : "The map was spoiled and a pen use spent. Crafted-tool saving applies.");
        return true;
    }
}
