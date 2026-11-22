// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class StatNetwork
{
    private sealed class Session
    {
        public uint Sequence;
        public byte Version;
        public bool PortraitSent;
        public long NextPoll = Core.TickCount;
        public long NextSpend = Core.TickCount;
        public long NextTradeSkills = Core.TickCount;
        public long NextCompanion = Core.TickCount;
        public long NextCompanionOpen = Core.TickCount;
    }
    private static readonly Dictionary<Serial, Session> _sessions = new();
    public static void Configure()
    {
        EventSink.Disconnected += player => _sessions.Remove(player.Serial);
        EventSink.Logout += player => _sessions.Remove(player.Serial);
    }
    public static unsafe void Initialize()
    {
        IncomingExtendedCommandPackets.RegisterExtended(StatProtocol.SubCommand, true, &Receive);
        IncomingExtendedCommandPackets.RegisterExtended(CompanionStatProtocol.SubCommand, true, &ReceiveCompanion);
    }

    private static void ReceiveCompanion(NetState net, SpanReader reader)
    {
        if (net.Mobile is not PlayerMobile owner || reader.Remaining != CompanionStatProtocol.RequestLength - 5
            || reader.ReadByte() != CompanionStatProtocol.Version || !_sessions.TryGetValue(owner.Serial, out var session)
            || session.Version != StatProtocol.Version)
        {
            return;
        }

        var request = (CompanionStatRequest)reader.ReadByte();
        switch (request)
        {
            case CompanionStatRequest.Snapshot when Core.TickCount >= session.NextCompanion:
                session.NextCompanion = Core.TickCount + StatProtocol.PollMs / 2;
                break;
            case CompanionStatRequest.OpenSheet when Core.TickCount >= session.NextCompanionOpen:
                session.NextCompanionOpen = Core.TickCount + StatProtocol.PollMs / 2;
                break;
            default:
                return;
        }
        var serial = (Serial)reader.ReadUInt32();
        if (World.FindMobile(serial) is not BaseCreature companion || companion is not IClassCompanion build
            || !MercenaryCare.IsCompanion(owner, companion) || MercenaryCare.CompanionLevel(companion) is not { } level)
        {
            return;
        }
        if (!companion.CanPaperdollBeOpenedBy(owner))
        {
            if (request == CompanionStatRequest.OpenSheet)
                owner.SendMessage("You cannot reach your companion to open their character sheet.");
            return;
        }
        if (request == CompanionStatRequest.OpenSheet)
            companion.DisplayPaperdollTo(owner);
        else
            CompanionCharacterStats.Send(net, companion, build, level);
    }
    private static void Receive(NetState state, SpanReader reader)
    {
        if (state.Mobile is not PlayerMobile player || !ClassCombat.Enabled(player)
            || reader.Remaining != 6)
        {
            return;
        }
        byte version = reader.ReadByte();
        if (version is not (StatProtocol.Version or StatProtocol.LegacyVersion))
        {
            return;
        }
        int request = reader.ReadByte();
        uint sequence = reader.ReadUInt32();
        if (request > StatProtocol.TradeSkillsRequest || request == StatProtocol.TradeSkillsRequest && version != StatProtocol.Version)
        {
            return;
        }
        if (!_sessions.TryGetValue(player.Serial, out var session))
        {
            _sessions[player.Serial] = session = new Session();
        }
        if (unchecked((int)(sequence - session.Sequence)) <= 0)
        {
            return;
        }
        session.Sequence = sequence;
        session.Version = version;
        if (version == StatProtocol.Version && !session.PortraitSent)
        {
            session.PortraitSent = true;
            ScriptedDialogue.SendAvatarFace(player);
            ScriptedDialogue.TryArrival(player);
        }
        long now = Core.TickCount;
        if (request == StatProtocol.TradeSkillsRequest)
        {
            if (now >= session.NextTradeSkills)
            {
                session.NextTradeSkills = now + TradeSkillSheetProtocol.PollMs / 2;
                TradeSkillSheet.Send(player);
            }
            return;
        }
        if (request == 0)
        {
            if (now - session.NextPoll < 0)
            {
                return;
            }
            session.NextPoll = now + StatProtocol.PollMs / 2;
        }
        else
        {
            if (now - session.NextSpend < 0)
            {
                return;
            }
            session.NextSpend = now + 50;
            ClassCreation.Profiles.TrySpendStat(player, (CharacterStat)(request - 1));
        }
        Send(player);
    }
    internal static bool SupportsCurrentProtocol(PlayerMobile player) =>
        _sessions.TryGetValue(player.Serial, out var session) && session.Version == StatProtocol.Version;

    public static void Send(PlayerMobile player)
    {
        if (player.NetState?.Running != true)
        {
            return;
        }
        bool extended = SupportsCurrentProtocol(player);
        byte version = extended ? StatProtocol.Version : StatProtocol.LegacyVersion;
        const int standardLength = 6 + StatProtocol.Values * 4;
        Span<byte> name = stackalloc byte[StatProtocol.MatchupNameBytes];
        int nameBytes = 0;
        var matchup = extended ? ClassStats.LastMatchup(player) : default;
        if (extended && matchup.Name != null)
        {
            Encoding.UTF8.GetEncoder().Convert(matchup.Name.AsSpan(), name, true, out _, out nameBytes, out _);
        }

        int length = standardLength + (extended ? StatProtocol.MatchupFields * 4 + nameBytes : 0);
        var writer = new SpanWriter(stackalloc byte[length]);
        writer.Write((byte)0xBF);
        writer.Write((ushort)length);
        writer.Write(StatProtocol.SubCommand);
        writer.Write(version);
        foreach (int value in ClassStats.Snapshot(player))
        {
            writer.Write(value);
        }
        if (extended)
        {
            writer.Write(NamedCombat.ResistanceCap(player, ResistanceType.Fire));
            writer.Write(NamedCombat.ResistanceCap(player, ResistanceType.Cold));
            writer.Write(NamedCombat.ResistanceCap(player, ResistanceType.Poison));
            writer.Write(NamedCombat.ResistanceCap(player, ResistanceType.Energy));
            writer.Write(matchup.Attack);
            writer.Write(matchup.Defense);
            writer.Write(name[..nameBytes]);
        }
        player.NetState.Send(writer.Span);
        if (extended && ClassCreation.Profiles.TryGet(player.Serial, out var profile))
        {
            var experience = new SpanWriter(stackalloc byte[StatProtocol.ExperiencePacketLength]);
            experience.Write((byte)0xBF);
            experience.Write((ushort)StatProtocol.ExperiencePacketLength);
            experience.Write(StatProtocol.ExperienceSubCommand);
            experience.Write(StatProtocol.Version);
            experience.Write((uint)profile.Experience);
            experience.Write((uint)ExperienceLevels.Threshold(profile.Level));
            experience.Write((uint)ExperienceLevels.Threshold(Math.Min(99, profile.Level + 1)));
            player.NetState.Send(experience.Span);
        }
    }
}
