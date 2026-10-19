// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Buffers;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class PotionBeltNetwork
{
    private sealed class Session
    {
        public uint Sequence;
        public long NextPoll = Core.TickCount, NextAction = Core.TickCount;
        public readonly long[] NextUse = new long[4];
    }
    private static readonly Dictionary<Serial, Session> _sessions = new();
    public static void Configure()
    {
        EventSink.Disconnected += mobile => _sessions.Remove(mobile.Serial);
        EventSink.Logout += mobile => _sessions.Remove(mobile.Serial);
    }
    public static unsafe void Initialize() => IncomingExtendedCommandPackets.RegisterExtended(BeltProtocol.SubCommand, true, &Receive);
    private static void Receive(NetState state, SpanReader reader)
    {
        if (state.Mobile is not PlayerMobile player || !ClassCombat.Enabled(player) || reader.Remaining != 7
            || reader.ReadByte() != BeltProtocol.Version)
        {
            return;
        }
        var action = (BeltRequest)reader.ReadByte();
        int cell = reader.ReadByte();
        uint sequence = reader.ReadUInt32();
        if (action > BeltRequest.Fill || cell >= BeltProtocol.Cells)
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
        if (action == BeltRequest.Poll)
        {
            if (now - session.NextPoll < 0)
            {
                return;
            }
            session.NextPoll = now + BeltProtocol.PollMs / 2;
        }
        else
        {
            // Different columns can be used together in a panic. Bound repeats
            // per column, rather than dropping a quick health+mana pair.
            if (action == BeltRequest.Use)
            {
                int column = cell % 4;
                if (now - session.NextUse[column] < 0) { return; }
                session.NextUse[column] = now + 60;
            }
            else
            {
                if (now - session.NextAction < 0) { return; }
                session.NextAction = now + 60;
            }
            switch (action)
            {
                case BeltRequest.Use: PotionBelts.Use(player, cell); break;
                case BeltRequest.Load: PotionBelts.Target(player, cell); break;
                case BeltRequest.Remove: PotionBelts.Remove(player, cell); break;
                case BeltRequest.Fill: PotionBelts.Fill(player); break;
            }
        }
        Send(player);
    }
    public static void Send(PlayerMobile player)
    {
        if (player.NetState?.Running != true)
        {
            return;
        }
        PotionBelts.Reconcile(player);
        var writer = new SpanWriter(stackalloc byte[BeltProtocol.StateLength]);
        writer.Write((byte)0xBF);
        writer.Write((ushort)BeltProtocol.StateLength);
        writer.Write(BeltProtocol.SubCommand);
        writer.Write(BeltProtocol.Version);
        writer.Write(_sessions.GetValueOrDefault(player.Serial)?.Sequence ?? 0);
        writer.Write((byte)PotionBelts.Rows(player));
        writer.Write((byte)(PotionBelts.Ready(player) ? 1 : 0));
        for (int cell = 0; cell < BeltProtocol.Cells; cell++)
        {
            var potion = PotionBelts.Cell(player, cell);
            writer.Write(potion?.Serial.Value ?? 0);
            writer.Write((byte)(potion?.Kind ?? 0));
            writer.Write((byte)(potion?.Tier ?? 0));
            writer.Write((ushort)(potion?.ItemID ?? 0));
            writer.Write((ushort)(potion?.Hue ?? 0));
        }
        player.NetState.Send(writer.Span);
    }
}
