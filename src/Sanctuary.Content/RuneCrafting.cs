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

public sealed record RuneRecipe(int Tier, double RequiredSkill, double MaximumSkill, int Bark);
internal sealed record RuneInput(Item Item, object Parent, int Amount, int Take, int Tier, int Hue, int Art,
    string Name, ItemOrigin Origin, double Weight)
{
    public RuneInput(Item item, int take, int tier) : this(item, item.Parent, item.Amount, take, tier, item.Hue,
        item.ItemID, item.Name, item.Origin, item.Weight) { }
    public bool Valid(PlayerMobile owner) => RuneCrafting.Accessible(owner, Item) && ReferenceEquals(Parent, Item.Parent)
        && Item.Amount == Amount && Item.Hue == Hue && Item.ItemID == Art && Item.Name == Name && Item.Origin == Origin
        && Item.Weight == Weight && (Item is not Runebark bark || bark.Tier == Tier);
    public int FreedWeight => Item.PileWeight - (int)Math.Ceiling(Weight * (Amount - Take));
}
public sealed class RuneRequest
{
    public PlayerMobile Owner { get; internal init; }
    public ScribesPen Tool { get; internal init; }
    public RuneMould Mould { get; internal init; }
    public RunePattern Pattern { get; internal init; }
    public RuneRecipe Row { get; internal init; }
    public CraftItem Recipe { get; internal init; }
    internal NetState Connection;
    internal ProfessionState Profession;
    internal RuneInput MouldInput;
    internal List<RuneInput> Bark;
    internal object ToolParent;
    internal int Uses, Skill;
    internal ItemOrigin ToolOrigin;
    internal CraftToolState ToolState;
    internal long Session;
    public bool Started { get; internal set; }
    public bool Claimed { get; internal set; }
    public double Chance { get; internal init; }
}

public static class RuneCrafting
{
    public static RuneRecipe[] Recipes { get; private set; } = [];
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static readonly Dictionary<CraftItem, RuneRequest> _starting = new();
    private static long _nextSession;
    public static void Load()
    {
        Recipes = BalanceCatalog.Read<RuneRecipe>("rune-recipes");
        if (Recipes.Length != 8) { throw new InvalidDataException("Eight rune recipes are required."); }
        for (int i = 0; i < Recipes.Length; i++)
        {
            var row = Recipes[i];
            if (row.Tier != i + 1 || row.RequiredSkill != 30 + i * 10 || row.MaximumSkill != row.RequiredSkill + 50 || row.Bark != i + 1)
            { throw new InvalidDataException("Invalid rune progression."); }
        }
    }
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
    internal static long Begin(PlayerMobile player) => _sessions[player.Serial] = ++_nextSession;
    internal static bool Current(PlayerMobile player, long session) => session != 0 && _sessions.GetValueOrDefault(player.Serial) == session;
    internal static void End(PlayerMobile player, long session) { if (Current(player, session)) { _sessions.Remove(player.Serial); } }
    internal static bool Accessible(PlayerMobile owner, Item item) => item is { Deleted: false, Movable: true, Amount: > 0,
        IsLockedDown: false, IsSecure: false } && item.IsChildOf(owner.Backpack) && item.VerifyMove(owner) && item.IsAccessibleTo(owner);
    public static bool ToolValid(PlayerMobile owner, ScribesPen tool) => ClassProfessions.Affected(owner) && ClassProfessions.Ready(owner)
        && tool is { Deleted: false, Movable: true, UsesRemaining: > 0 } && (tool.Parent == owner || tool.IsChildOf(owner.Backpack))
        && tool.VerifyMove(owner) && tool.IsAccessibleTo(owner);
    public static CraftQuality Quality(double skill, double required, int roll) =>
        skill < required + 10 ? CraftQuality.Average
        : skill < required + 20 ? roll < 10 ? CraftQuality.HighQuality : CraftQuality.Average
        : roll < 1 ? CraftQuality.Superior : roll < 11 ? CraftQuality.HighQuality : CraftQuality.Average;
    public static RuneRequest Quote(PlayerMobile owner, ScribesPen tool, RuneMould mould)
    {
        if (!ToolValid(owner, tool) || !ClassProfessions.Active(owner, SkillName.Inscribe)
            || !owner.CanBeginAction<CraftSystem>() || !Accessible(owner, mould) || mould.Amount != 1
            || !CraftFacilities.Allows(owner, SkillName.Inscribe)) { return null; }
        var row = Recipes[mould.Tier - 1];
        if (owner.Skills.Inscribe.Base < row.RequiredSkill) { return null; }
        var pattern = RunePattern.From(mould);
        var recipe = new CraftItem(typeof(InscribedRune), "Runes", pattern.Name);
        recipe.AddSkill(SkillName.Inscribe, row.RequiredSkill, row.MaximumSkill);
        double chance = recipe.GetSuccessChance(owner, null, DefInscription.CraftSystem, false, out bool skills);
        if (!skills || chance <= 0) { return null; }
        var inputs = new List<RuneInput>(); int remaining = row.Bark;
        for (int tier = mould.Tier; tier <= 8 && remaining > 0; tier++)
        {
            foreach (var bark in owner.Backpack.FindItemsByType<Runebark>(true))
            {
                if (bark.Tier != tier || !Accessible(owner, bark)) { continue; }
                int take = Math.Min(remaining, bark.Amount); inputs.Add(new(bark, take, tier)); remaining -= take;
                if (remaining == 0) { break; }
            }
        }
        if (remaining > 0) { return null; }
        return new RuneRequest { Owner = owner, Tool = tool, Mould = mould, Pattern = pattern, Row = row, Recipe = recipe,
            Connection = owner.NetState, Profession = ClassProfessions.Profiles.Find(owner), MouldInput = new(mould, 1, mould.Tier),
            Bark = inputs, ToolParent = tool.Parent, Uses = tool.UsesRemaining, ToolOrigin = tool.Origin,
            ToolState = CraftTransactions.Profiles.Find(tool), Skill = owner.Skills.Inscribe.BaseFixedPoint,
            Chance = Math.Clamp(chance, 0, 1), Session = Begin(owner) };
    }
    private static bool Valid(RuneRequest q) => q is { Claimed: false } && Current(q.Owner, q.Session) && ToolValid(q.Owner, q.Tool)
        && ReferenceEquals(q.Connection, q.Owner.NetState) && ReferenceEquals(q.Profession, ClassProfessions.Profiles.Find(q.Owner))
        && ClassProfessions.Active(q.Owner, SkillName.Inscribe) && q.Owner.Skills.Inscribe.BaseFixedPoint == q.Skill
        && ReferenceEquals(q.ToolParent, q.Tool.Parent) && q.Tool.UsesRemaining == q.Uses && q.Tool.Origin == q.ToolOrigin
        && q.ToolState == CraftTransactions.Profiles.Find(q.Tool) && q.MouldInput.Valid(q.Owner) && RunePattern.From(q.Mould) == q.Pattern
        && CraftFacilities.Allows(q.Owner, SkillName.Inscribe) && BarkValid(q);
    private static bool BarkValid(RuneRequest q)
    {
        foreach (var input in q.Bark) { if (!input.Valid(q.Owner)) { return false; } }
        return true;
    }
    public static bool Start(RuneRequest q)
    {
        if (q == null || q.Started || !Valid(q) || !q.Owner.CanBeginAction<CraftSystem>()) { return false; }
        q.Started = true; _starting.Add(q.Recipe, q);
        try { q.Recipe.Craft(q.Owner, DefInscription.CraftSystem, null, q.Tool, 0); }
        finally { _starting.Remove(q.Recipe); }
        return true;
    }
    internal static RuneRequest Attach(CraftTransactions.Request ticket) => _starting.GetValueOrDefault(ticket.Recipe) is { } q
        && ReferenceEquals(q.Owner, ticket.Owner) && ReferenceEquals(q.Tool, ticket.Tool) && q.Started ? q : null;
    internal static bool Complete(CraftTransactions.Request ticket, Random random)
    {
        if (ticket.Claimed) { return false; } ticket.Claimed = true;
        var q = ticket.Rune;
        if (q == null)
        { ticket.Owner.SendMessage("Use Craft rune from mould on your scribe's pen to choose a fresh pattern."); return false; }
        if (!Valid(q) || !CraftTransactions.Valid(ticket))
        { q.Claimed = true; End(q.Owner, q.Session); q.Owner.SendMessage("The rune inputs or crafting conditions changed. Nothing was consumed."); return false; }
        q.Claimed = true; End(q.Owner, q.Session);
        double chance = q.Recipe.GetSuccessChance(q.Owner, null, DefInscription.CraftSystem, false, out bool skills);
        if (!skills || Math.Clamp(chance, 0, 1) != q.Chance) { return false; }
        var output = ItemOrigins.Create(ItemOrigin.Crafted, () => new InscribedRune(q.Pattern, CraftQuality.Average));
        int freed = 1, freedWeight = q.MouldInput.FreedWeight;
        foreach (var input in q.Bark) { if (input.Take == input.Amount) { freed++; } freedWeight += input.FreedWeight; }
        // Plan capacity after successful input consumption; a rejected request never rolls or spends anything.
        int weight = output.PileWeight - freedWeight;
        if (q.Owner.Backpack.MaxItems != 0 && q.Owner.Backpack.TotalItems + 1 - freed > q.Owner.Backpack.MaxItems
            || weight > 0 && (q.Owner.TotalWeight + weight > q.Owner.MaxWeight
                || q.Owner.Backpack.MaxWeight != 0 && q.Owner.Backpack.TotalWeight + weight > q.Owner.Backpack.MaxWeight))
        { output.Delete(); q.Owner.SendMessage("Make room for the rune. Nothing was consumed."); return false; }
        random ??= Random.Shared;
        bool success = random.NextDouble() < q.Chance;
        q.Mould.Consume(); int needed = success ? q.Row.Bark : (q.Row.Bark + 1) / 2;
        foreach (var input in q.Bark) { int take = Math.Min(needed, input.Take); input.Item.Consume(take); needed -= take; if (needed == 0) { break; } }
        CraftTransactions.SpendUse(q.Owner, q.Tool, random);
        bool broken = q.Tool.UsesRemaining < 1 && q.Tool.BreakOnDepletion;
        if (broken) { q.Tool.Delete(); }
        if (success)
        {
            var quality = Quality(q.Skill / 10.0, q.Row.RequiredSkill, random.Next(100));
            output.SetQuality(quality);
            q.Owner.Backpack.DropItem(output);
            q.Owner.SendMessage($"You crafted {CraftTransactions.QualityName(quality)} {output.Name}.");
        }
        else { output.Delete(); q.Owner.SendMessage("Rune crafting failed: the mould, half the bark rounded up, and a pen use were spent. Crafted-tool saving applies."); }
        q.Recipe.GetSuccessChance(q.Owner, null, DefInscription.CraftSystem, true, out _);
        int ending = DefInscription.CraftSystem.PlayEndingEffect(q.Owner, !success, true, broken, 1, false, q.Recipe);
        if (ending > 0) { q.Owner.SendLocalizedMessage(ending); }
        return true;
    }
}
