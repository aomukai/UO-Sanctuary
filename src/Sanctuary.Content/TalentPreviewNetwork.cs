// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Buffers;
using System.Text;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class TalentPreviewNetwork
{
    public static unsafe void Initialize() =>
        IncomingExtendedCommandPackets.RegisterExtended(ClassGumpProtocol.PreviewSubCommand, true, &Receive);

    private static void Receive(NetState state, SpanReader reader)
    {
        if (state.Mobile is not PlayerMobile player || !ClassCombat.Enabled(player)
            || reader.Remaining != ClassGumpProtocol.PreviewRequestLength - 5
            || reader.ReadByte() != ClassGumpProtocol.PreviewVersion
            || player.FindGump<TalentGump>() is not { } gump)
        {
            return;
        }

        uint sequence = reader.ReadUInt32();
        int index = reader.ReadByte();
        string text = gump.Preview(player, index, reader.ReadAscii(ClassGumpProtocol.SkillCount));
        if (text == null)
        {
            return;
        }

        int bytes = Encoding.UTF8.GetByteCount(text);
        if (bytes > ClassGumpProtocol.MaxPreviewBytes)
        {
            return;
        }
        Span<byte> packet = stackalloc byte[ClassGumpProtocol.PreviewReplyHeader + bytes];
        var writer = new SpanWriter(packet);
        writer.Write((byte)0xBF);
        writer.Write((ushort)packet.Length);
        writer.Write(ClassGumpProtocol.PreviewSubCommand);
        writer.Write(ClassGumpProtocol.PreviewVersion);
        writer.Write(sequence);
        Encoding.UTF8.GetBytes(text, packet[ClassGumpProtocol.PreviewReplyHeader..]);
        state.Send(packet);
    }
}
