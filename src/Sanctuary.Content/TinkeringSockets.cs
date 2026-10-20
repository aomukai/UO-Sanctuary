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

public sealed record SocketSettings(int[] TierSkills, int[] SocketSkills, int[] BaseChances,
    int[] CraftedToolBonuses, int IntuitionChance);
public enum SocketOutcome { Added, Destroyed, Intuition }
public sealed record SocketResult(SocketOutcome Outcome, int UsesSpent, SocketRequest Retry = null);

public sealed class SocketRequest
{
    internal bool Claimed;
    public PlayerMobile Owner { get; internal init; }
    public Item Item { get; internal init; }
    public TinkerTools Tool { get; internal init; }
    public GearState State { get; internal init; }
    public int Socket { get; internal init; }
    public int RequiredSkill { get; internal init; }
    public int Chance { get; internal init; }
    public bool Retry { get; internal init; }
    internal long Session { get; init; }
    internal NetState Connection { get; init; }
    internal ProfessionState Profession { get; init; }
    internal int Skill { get; init; }
    internal int ToolUses { get; init; }
    internal object ToolParent { get; init; }
    internal CraftToolState ToolState { get; init; }
    internal GearState SavedState { get; init; }
    internal EquipmentSalvage.Snapshot Snapshot { get; init; }
    internal string Name { get; init; }
}

public static class TinkeringSockets
{
    public static SocketSettings Settings { get; private set; }
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static long _nextSession;
    public static void Load()
    {
        var rows = BalanceCatalog.Read<SocketSettings>("tinkering-sockets");
        if (rows.Length != 1) { throw new InvalidDataException("One socketing configuration is required."); }
        Settings = rows[0];
        if (Settings.TierSkills is not { Length: 3 } || Settings.SocketSkills is not { Length: 5 }
            || Settings.BaseChances is not { Length: 5 } || Settings.CraftedToolBonuses is not { Length: 3 }
            || Settings.IntuitionChance is < 0 or > 100)
        { throw new InvalidDataException("Invalid socket progression."); }
        foreach (int value in Settings.TierSkills)
        { if (value is < 0 or > 120) { throw new InvalidDataException("Invalid socket tier gate."); } }
        foreach (int value in Settings.SocketSkills)
        { if (value is < 0 or > 120) { throw new InvalidDataException("Invalid socket index gate."); } }
        foreach (int value in Settings.BaseChances)
        { if (value is < 0 or > 100) { throw new InvalidDataException("Invalid socket chance."); } }
        foreach (int value in Settings.CraftedToolBonuses)
        { if (value is < 0 or > 100) { throw new InvalidDataException("Invalid socket tool bonus."); } }
    }
    public static void Configure()
    {
        EventSink.Disconnected += player => _sessions.Remove(player.Serial);
        EventSink.WorldSave += () =>
        {
            var removed = new List<Serial>();
            foreach (var serial in _sessions.Keys)
            { if (World.FindMobile(serial) is not { Deleted: false }) { removed.Add(serial); } }
            foreach (var serial in removed) { _sessions.Remove(serial); }
        };
    }
    internal static long Begin(PlayerMobile player) => _sessions[player.Serial] = ++_nextSession;
    internal static bool Current(PlayerMobile player, long session) => session != 0 && _sessions.GetValueOrDefault(player.Serial) == session;
    internal static void End(PlayerMobile player, long session)
    { if (Current(player, session)) { _sessions.Remove(player.Serial); } }
    public static int ToolBonus(BaseTool tool) => tool is TinkerTools && tool.Origin == ItemOrigin.Crafted
        && CraftTransactions.Profiles?.Find(tool) is { Crafted: true } state ? Settings.CraftedToolBonuses[(int)state.Quality] : 0;
    public static int Required(GearTier tier, int socket) => socket is >= 1 and <= 5 && Enum.IsDefined(tier)
        ? Math.Max(Settings.TierSkills[(int)tier], Settings.SocketSkills[socket - 1]) : throw new ArgumentOutOfRangeException();
    public static int Chance(TinkerTools tool, int socket) => socket is >= 1 and <= 5
        ? Math.Min(100, Settings.BaseChances[socket - 1] + ToolBonus(tool)) : throw new ArgumentOutOfRangeException(nameof(socket));
    public static bool ToolValid(PlayerMobile player, TinkerTools tool) => ClassProfessions.Affected(player)
        && ClassProfessions.Ready(player) && tool is { Deleted: false, Movable: true, UsesRemaining: > 0 }
        && (tool.Parent == player || tool.IsChildOf(player.Backpack)) && tool.IsAccessibleTo(player) && tool.VerifyMove(player);
    private static bool Ready(PlayerMobile player, TinkerTools tool) => ToolValid(player, tool)
        && ClassProfessions.Active(player, SkillName.Tinkering) && player.CanBeginAction<CraftSystem>()
        && player.NextSkillTime - Core.TickCount <= 0;
    private static bool Eligible(PlayerMobile player, Item item) => item is
        { Deleted: false, Movable: true, Amount: 1, Insured: false, Nontransferable: false }
        && item.Origin is ItemOrigin.Drop or ItemOrigin.Crafted && item.IsChildOf(player.Backpack)
        && item.IsStandardLoot() && item.BlessedFor == null && !item.IsLockedDown && !item.IsSecure
        && item.IsAccessibleTo(player) && item.VerifyMove(player) && ClassGear.Converted(item)
        && GearDetails.Socketable(item) && ClassGear.State(item) is not { StarterTeeth: true }
        && ClassGear.State(item) is not { Identified: false };
    public static SocketRequest Quote(PlayerMobile player, TinkerTools tool, Item item)
        => BuildQuote(player, tool, item, false);
    private static SocketRequest BuildQuote(PlayerMobile player, TinkerTools tool, Item item, bool retry)
    {
        if (!Ready(player, tool) || !Eligible(player, item)) { return null; }
        var state = ClassGear.Profiles.Preview(item);
        int socket = state.SocketCount + 1;
        if (socket > 5 || player.Skills.Tinkering.BaseFixedPoint < Required(state.Tier, socket) * 10) { return null; }
        if (!CraftFacilities.Allows(player, SkillName.Tinkering)) { return null; }
        return new()
        {
            Owner = player, Item = item, Tool = tool, State = state, Retry = retry, SavedState = ClassGear.State(item),
            Socket = socket, RequiredSkill = Required(state.Tier, socket), Chance = Chance(tool, socket),
            Connection = player.NetState, Profession = ClassProfessions.Profiles.Find(player), Skill = player.Skills.Tinkering.BaseFixedPoint,
            ToolUses = tool.UsesRemaining, ToolParent = tool.Parent, ToolState = CraftTransactions.Profiles.Find(tool),
            Snapshot = new EquipmentSalvage.Snapshot(item), Name = item.Name, Session = Begin(player)
        };
    }
    public static void Cancel(PlayerMobile player, SocketRequest request)
    {
        if (request == null || request.Owner != player || request.Claimed) { return; }
        request.Claimed = true; End(player, request.Session);
    }
    internal static bool Commit(PlayerMobile player, SocketRequest request, out SocketResult result, Random random = null)
    {
        result = null;
        if (request == null || request.Owner != player || request.Claimed) { return false; }
        request.Claimed = true;
        if (!Current(player, request.Session)) { return false; }
        End(player, request.Session);
        var item = request.Item; var tool = request.Tool;
        if (!Ready(player, tool) || !Eligible(player, item) || !ReferenceEquals(player.NetState, request.Connection)
            || !ReferenceEquals(ClassProfessions.Profiles.Find(player), request.Profession)
            || player.Skills.Tinkering.BaseFixedPoint != request.Skill || tool.UsesRemaining != request.ToolUses
            || !ReferenceEquals(tool.Parent, request.ToolParent) || !ReferenceEquals(CraftTransactions.Profiles.Find(tool), request.ToolState)
            || !ReferenceEquals(ClassGear.State(item), request.SavedState) || !request.Snapshot.Valid || item.Name != request.Name
            || request.Socket != request.State.SocketCount + 1 || request.Socket > 5
            || request.RequiredSkill != Required(request.State.Tier, request.Socket)
            || request.Skill < request.RequiredSkill * 10 || request.Chance != Chance(tool, request.Socket)
            || !CraftFacilities.Allows(player, SkillName.Tinkering)) { return false; }
        random ??= Random.Shared;
        bool success = random.Next(100) < request.Chance;
        // Intuition happens before acting: the original attempt spends nothing and preserves the item.
        if (!success && !request.Retry && request.Skill >= 1200 && random.Next(100) < Settings.IntuitionChance)
        {
            var retry = BuildQuote(player, tool, item, true);
            result = new(SocketOutcome.Intuition, 0, retry);
            player.SendMessage("Something feels off. You draw back before acting. Retry once or keep the item safely.");
            return true;
        }
        int uses = tool.UsesRemaining;
        CraftTransactions.SpendUse(player, tool, random);
        int spent = uses - tool.UsesRemaining;
        if (tool.UsesRemaining < 1 && tool.BreakOnDepletion) { tool.Delete(); }
        player.NextSkillTime = Core.TickCount + 1000;
        if (success)
        {
            ClassGear.Profiles.Store(item, request.State with { TinkerSockets = request.State.TinkerSockets + 1 });
            player.CheckSkill(SkillName.Tinkering, request.RequiredSkill, request.RequiredSkill + 50);
            player.SendMessage($"Socket {request.Socket} added. The item can no longer be imbued.");
        }
        else
        {
            item.Delete();
            player.SendMessage("Socketing failed. The item and all its contents were destroyed.");
        }
        result = new(success ? SocketOutcome.Added : SocketOutcome.Destroyed, spent);
        return true;
    }
}
