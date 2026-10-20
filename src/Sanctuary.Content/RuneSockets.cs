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

public sealed record InstalledRune(RunePattern Pattern, CraftQuality Quality)
{
    public int Percentage => Pattern.Baseline + Pattern.Tier - 1 + (int)Quality;
}
public sealed record RuneInsertion(PlayerMobile Owner, InscribedRune Rune, Item Host, GearState State,
    InstalledRune Content, NetState Connection, long Session)
{
    internal object RuneParent, HostParent;
    internal EquipmentSalvage.Snapshot RuneSnapshot, HostSnapshot;
    public bool Claimed { get; internal set; }
}

public static class RuneSockets
{
    private static readonly Dictionary<Serial, long> _sessions = new();
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
    public static int RequiredLevel(int filled) => filled == 0 ? 0 : filled is >= 1 and <= 5 ? 20 + 10 * filled : throw new ArgumentOutOfRangeException(nameof(filled));
    public static int Filled(GearState state) => state.Runes.Length + state.Gems.Length;
    internal static bool Accessible(PlayerMobile owner, Item item, bool stack = false) => item is { Deleted: false, Movable: true, Amount: > 0,
        Insured: false, Nontransferable: false, QuestItem: false } && item.IsChildOf(owner.Backpack)
        && (stack || item.Amount == 1) && item.IsStandardLoot() && item.BlessedFor == null && !item.IsLockedDown && !item.IsSecure
        && item.IsAccessibleTo(owner) && item.VerifyMove(owner);
    public static bool Ready(PlayerMobile owner) => ClassProfessions.Affected(owner) && ClassProfessions.Ready(owner)
        && owner.CanBeginAction<CraftSystem>();
    public static RuneInsertion Quote(PlayerMobile owner, InscribedRune rune, Item host)
    {
        if (!Ready(owner) || !Accessible(owner, rune) || !Accessible(owner, host) || !ClassGear.Converted(host)
            || !GearDetails.Socketable(host) || host.Origin == ItemOrigin.Vendor) { return null; }
        var state = ClassGear.State(host);
        if (state is not { Identified: true, StarterTeeth: false } || Filled(state) >= state.SocketCount) { return null; }
        return new(owner, rune, host, state, new(rune.Pattern, rune.Quality), owner.NetState, Begin(owner))
        { RuneParent = rune.Parent, HostParent = host.Parent, RuneSnapshot = new(rune), HostSnapshot = new(host) };
    }
    public static bool Commit(RuneInsertion q)
    {
        if (q == null || q.Claimed) { return false; } q.Claimed = true;
        bool current = Current(q.Owner, q.Session); End(q.Owner, q.Session);
        if (!current || !Ready(q.Owner) || !ReferenceEquals(q.Connection, q.Owner.NetState)
            || !Accessible(q.Owner, q.Rune) || !Accessible(q.Owner, q.Host) || !ReferenceEquals(q.Rune.Parent, q.RuneParent)
            || !ReferenceEquals(q.Host.Parent, q.HostParent) || !ReferenceEquals(ClassGear.State(q.Host), q.State)
            || !q.RuneSnapshot.Valid || !q.HostSnapshot.Valid || q.Rune.Pattern != q.Content.Pattern || q.Rune.Quality != q.Content.Quality
            || Filled(q.State) >= q.State.SocketCount || !GearDetails.Socketable(q.Host) || q.Host.Origin == ItemOrigin.Vendor) { return false; }
        var contents = new InstalledRune[q.State.Runes.Length + 1]; q.State.Runes.CopyTo(contents, 0); contents[^1] = q.Content;
        // Store one frozen value record, then consume the rune. No native affix or stat mutation adds a second copy.
        ClassGear.Profiles.Store(q.Host, q.State with { Runes = contents }); q.Rune.Consume();
        q.Owner.SendMessage("The rune is permanently installed. It cannot be removed or replaced.");
        return true;
    }
    internal static void Validate(InstalledRune rune)
    {
        var p = rune?.Pattern;
        if (p == null || p.Tier is < 1 or > 8 || !Enum.IsDefined(rune.Quality) || !Enum.IsDefined(p.AffixKind)
            || p.Baseline is < 1 or > 10000 || !MouldRewards.LegalSavedStat(p.StatId, p.EffectCode)
            || string.IsNullOrWhiteSpace(p.TierName) || string.IsNullOrWhiteSpace(p.StatLabel)
            || string.IsNullOrWhiteSpace(p.SourceAffix) || string.IsNullOrWhiteSpace(p.AffixName))
        { throw new InvalidDataException("Invalid installed rune identity."); }
    }
    internal static void Write(IGenericWriter writer, InstalledRune[] runes)
    {
        writer.Write(runes.Length);
        foreach (var rune in runes)
        {
            var p = rune.Pattern;
            writer.Write(p.Tier); writer.Write(p.TierName); writer.Write(p.StatId); writer.Write(p.EffectCode);
            writer.Write(p.StatLabel); writer.Write(p.Baseline); writer.Write(p.SourceAffix); writer.Write(p.AffixName);
            writer.Write((byte)p.AffixKind); writer.Write((byte)rune.Quality);
        }
    }
    internal static InstalledRune[] Read(IGenericReader reader)
    {
        int count = reader.ReadInt(); if (count is < 0 or > 5) { throw new InvalidDataException("Invalid installed rune count."); }
        var runes = new InstalledRune[count];
        for (int i = 0; i < count; i++)
        {
            var p = new RunePattern(reader.ReadInt(), reader.ReadString(), reader.ReadString(), reader.ReadString(),
                reader.ReadString(), reader.ReadInt(), reader.ReadString(), reader.ReadString(), (AffixKind)reader.ReadByte());
            runes[i] = new(p, (CraftQuality)reader.ReadByte()); Validate(runes[i]);
        }
        return runes;
    }
}
