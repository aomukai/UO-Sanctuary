// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Buffers;
using System.Text;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class TitleNetwork
{
    public static void Initialize() => EventSink.PaperdollRequest += Send;
    internal static void Send(Mobile beholder, Mobile beheld)
    {
        if (beholder?.NetState?.Running != true || beheld is not PlayerMobile player || EarnedTitles.Display(player) is not { } display) { return; }
        string text = $"{display.Name(player.Name ?? "")}\n{display.Progression}";
        int bytes = Encoding.UTF8.GetByteCount(text);
        if (bytes > TitleProtocol.MaxTextBytes) { return; }
        Span<byte> packet = stackalloc byte[TitleProtocol.HeaderLength + bytes];
        var writer = new SpanWriter(packet);
        writer.Write((byte)0xBF); writer.Write((ushort)packet.Length); writer.Write(TitleProtocol.SubCommand);
        writer.Write(TitleProtocol.Version); writer.Write(player.Serial.Value);
        Encoding.UTF8.GetBytes(text, packet[TitleProtocol.HeaderLength..]);
        beholder.NetState.Send(packet);
    }
    internal static void Broadcast(PlayerMobile player)
    {
        if (player.Map == null || player.Map == Map.Internal || World.Loading) { return; }
        foreach (var viewer in player.Map.GetMobilesInRange<Mobile>(player.Location, CombatProtocol.SightRange))
        { if (viewer.NetState?.Running == true && viewer.CanSee(player)) { Send(viewer, player); } }
    }
}
