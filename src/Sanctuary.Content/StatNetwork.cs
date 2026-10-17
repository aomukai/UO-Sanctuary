// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Buffers;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class StatNetwork
{
    private sealed class Session
    {
        public uint Sequence;
        public long NextPoll = Core.TickCount;
        public long NextSpend = Core.TickCount;
    }
    private static readonly Dictionary<Serial, Session> _sessions = new();
    public static void Configure()
    {
        EventSink.Disconnected += player => _sessions.Remove(player.Serial);
        EventSink.Logout += player => _sessions.Remove(player.Serial);
    }
    public static unsafe void Initialize() => IncomingExtendedCommandPackets.RegisterExtended(StatProtocol.SubCommand, true, &Receive);
    private static void Receive(NetState state, SpanReader reader)
    {
        if (state.Mobile is not PlayerMobile player || !ClassCombat.Enabled(player)
            || reader.Remaining != 6 || reader.ReadByte() != StatProtocol.Version)
        {
            return;
        }
        int request = reader.ReadByte();
        uint sequence = reader.ReadUInt32();
        if (request > 4)
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
        long now = Core.TickCount;
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
    public static void Send(PlayerMobile player)
    {
        if (player.NetState?.Running != true)
        {
            return;
        }
        const int length = 6 + StatProtocol.Values * 4;
        var writer = new SpanWriter(stackalloc byte[length]);
        writer.Write((byte)0xBF);
        writer.Write((ushort)length);
        writer.Write(StatProtocol.SubCommand);
        writer.Write(StatProtocol.Version);
        foreach (int value in ClassStats.Snapshot(player))
        {
            writer.Write(value);
        }
        player.NetState.Send(writer.Span);
    }
}
