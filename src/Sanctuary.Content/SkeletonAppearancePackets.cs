// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers;
using Server;
using Server.Network;

namespace Sanctuary;

internal static class SkeletonAppearancePackets
{
    internal static void Send(NetState state, Serial actor, SkeletonAppearance appearance)
    {
        var packet = new SpanWriter(stackalloc byte[SkeletonAppearanceProtocol.Length]);
        packet.Write((byte)0xBF);
        packet.Write((ushort)SkeletonAppearanceProtocol.Length);
        packet.Write(SkeletonAppearanceProtocol.SubCommand);
        packet.Write(SkeletonAppearanceProtocol.Version);
        packet.Write(actor.Value);
        packet.Write((byte)appearance);
        state.Send(packet.Span);
    }
}
