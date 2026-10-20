// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

internal sealed record GemInput(SocketGem Item, int Take, object Parent, EquipmentSalvage.Snapshot Snapshot, string Effects);
public sealed class GemUpgradeRequest
{
    public PlayerMobile Owner { get; internal init; }
    public TinkerTools Tool { get; internal init; }
    public string Family { get; internal init; }
    public GemQuality Quality { get; internal init; }
    public double RequiredSkill { get; internal init; }
    internal CraftItem Recipe;
    internal NetState Connection;
    internal ProfessionState Profession;
    internal List<GemInput> Inputs;
    internal object ToolParent;
    internal int Uses, Skill;
    internal CraftToolState ToolState;
    internal ItemOrigin ToolOrigin;
    internal long Session;
    public bool Claimed { get; internal set; }
    public bool Started { get; internal set; }
}
public static class GemUpgrading
{
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static readonly Dictionary<CraftItem, GemUpgradeRequest> _starting = new();
    private static long _next;
    public static void Configure()
    {
        EventSink.Disconnected += from => _sessions.Remove(from.Serial);
        EventSink.WorldSave += () =>
        {
            var removed = new List<Serial>();
            foreach (var serial in _sessions.Keys) { if (World.FindMobile(serial) is not { Deleted: false }) { removed.Add(serial); } }
            foreach (var serial in removed) { _sessions.Remove(serial); }
        };
    }
    internal static long Begin(PlayerMobile owner) => _sessions[owner.Serial] = ++_next;
    internal static bool Current(PlayerMobile owner, long session) => session != 0 && _sessions.GetValueOrDefault(owner.Serial) == session;
    internal static void End(PlayerMobile owner, long session) { if (Current(owner, session)) { _sessions.Remove(owner.Serial); } }
    private static bool Accessible(PlayerMobile owner, SocketGem item) => item is { Deleted: false, Movable: true, Amount: > 0,
        Insured: false, Nontransferable: false, QuestItem: false } && item.Origin != ItemOrigin.Vendor
        && item.IsChildOf(owner.Backpack) && item.IsStandardLoot() && item.BlessedFor == null && !item.IsLockedDown && !item.IsSecure
        && item.IsAccessibleTo(owner) && item.VerifyMove(owner);
    public static GemUpgradeRequest Quote(PlayerMobile owner, TinkerTools tool, SocketGem selected)
    {
        if (!TinkeringSockets.ToolValid(owner, tool) || !owner.CanBeginAction<CraftSystem>() || !Accessible(owner, selected)
            || selected.Quality == GemQuality.Perfect || !ClassProfessions.Active(owner, SkillName.Tinkering)
            || !CraftFacilities.Allows(owner, SkillName.Tinkering)) { return null; }
        double required = SocketGems.Settings.UpgradeSkills[(int)selected.Quality];
        if (owner.Skills.Tinkering.Base < required) { return null; }
        int remaining = SocketGems.Settings.UpgradeRatio; var inputs = new List<GemInput>();
        foreach (var item in owner.Backpack.FindItemsByType<SocketGem>(true))
        {
            if (!Accessible(owner, item) || item.Family != selected.Family || item.Quality != selected.Quality) { continue; }
            int take = Math.Min(item.Amount, remaining); inputs.Add(new(item, take, item.Parent, new(item), item.EffectIdentity)); remaining -= take;
            if (remaining == 0) { break; }
        }
        if (remaining != 0) { return null; }
        var recipe = new CraftItem(typeof(SocketGem), "Gem upgrades", $"{selected.Quality + 1} {SocketGems.Family(selected.Family).Name}");
        recipe.AddSkill(SkillName.Tinkering, required, required + 50);
        return new() { Owner = owner, Tool = tool, Family = selected.Family, Quality = selected.Quality, RequiredSkill = required,
            Recipe = recipe, Connection = owner.NetState, Profession = ClassProfessions.Profiles.Find(owner), Inputs = inputs,
            ToolParent = tool.Parent, Uses = tool.UsesRemaining, ToolState = CraftTransactions.Profiles.Find(tool), ToolOrigin = tool.Origin,
            Skill = owner.Skills.Tinkering.BaseFixedPoint, Session = Begin(owner) };
    }
    private static bool Valid(GemUpgradeRequest q)
    {
        if (q == null || q.Claimed || !Current(q.Owner, q.Session) || !TinkeringSockets.ToolValid(q.Owner, q.Tool)
            || !ReferenceEquals(q.Owner.NetState, q.Connection) || !ReferenceEquals(ClassProfessions.Profiles.Find(q.Owner), q.Profession)
            || !ClassProfessions.Active(q.Owner, SkillName.Tinkering) || q.Owner.Skills.Tinkering.BaseFixedPoint != q.Skill
            || q.Tool.UsesRemaining != q.Uses || !ReferenceEquals(q.Tool.Parent, q.ToolParent) || q.Tool.Origin != q.ToolOrigin
            || q.ToolState != CraftTransactions.Profiles.Find(q.Tool) || !CraftFacilities.Allows(q.Owner, SkillName.Tinkering)) { return false; }
        foreach (var input in q.Inputs)
        {
            if (!Accessible(q.Owner, input.Item) || !ReferenceEquals(input.Parent, input.Item.Parent) || !input.Snapshot.Valid
                || input.Item.Family != q.Family || input.Item.Quality != q.Quality || input.Item.EffectIdentity != input.Effects) { return false; }
        }
        return true;
    }
    public static bool Start(GemUpgradeRequest q)
    {
        if (q == null || q.Started || !Valid(q) || !q.Owner.CanBeginAction<CraftSystem>()) { return false; }
        q.Started = true; _starting.Add(q.Recipe, q);
        try { q.Recipe.Craft(q.Owner, DefTinkering.CraftSystem, null, q.Tool, 0); }
        finally { _starting.Remove(q.Recipe); }
        return true;
    }
    internal static GemUpgradeRequest Attach(CraftTransactions.Request ticket) => _starting.GetValueOrDefault(ticket.Recipe) is { } q
        && ReferenceEquals(ticket.Owner, q.Owner) && ReferenceEquals(ticket.Tool, q.Tool) && q.Started ? q : null;
    internal static bool Complete(CraftTransactions.Request ticket, Random random)
    {
        if (ticket.Claimed) { return false; } ticket.Claimed = true; var q = ticket.GemUpgrade;
        if (!Valid(q) || !CraftTransactions.Valid(ticket)) { return false; } q.Claimed = true; End(q.Owner, q.Session);
        var output = ItemOrigins.Create(ItemOrigin.Crafted, () => new SocketGem(q.Family, q.Quality + 1));
        var consumes = new List<(Item Item, int Amount)>();
        foreach (var input in q.Inputs) { consumes.Add((input.Item, input.Take)); }
        var delivery = MarketDelivery.Create(q.Owner, consumes, [output]);
        if (delivery == null)
        { output.Delete(); q.Owner.SendMessage("Make room for the upgraded gem. Nothing was consumed."); return false; }
        delivery.Commit();
        CraftTransactions.SpendUse(q.Owner, q.Tool, random);
        bool broken = q.Tool.UsesRemaining < 1 && q.Tool.BreakOnDepletion; if (broken) { q.Tool.Delete(); }
        q.Recipe.GetSuccessChance(q.Owner, null, DefTinkering.CraftSystem, true, out _);
        q.Owner.SendMessage($"You created {output.Name}.");
        int ending = DefTinkering.CraftSystem.PlayEndingEffect(q.Owner, false, true, broken, 1, false, q.Recipe);
        if (ending > 0) { q.Owner.SendLocalizedMessage(ending); } return true;
    }
}
