// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Buffers;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal static class TradeSkillSheet
{
    internal static void Send(PlayerMobile player)
    {
        Span<byte> packet = stackalloc byte[TradeSkillSheetProtocol.PacketLength];
        var writer = new SpanWriter(packet);
        writer.Write((byte)0xBF);
        writer.Write((ushort)packet.Length);
        writer.Write(TradeSkillSheetProtocol.SubCommand);
        writer.Write(TradeSkillSheetProtocol.Version);

        bool managed = ClassProfessions.Affected(player);
        var flags = managed ? TradeSkillSheetFlags.Managed : TradeSkillSheetFlags.None;
        if (managed && TownRecognition.HasAccess(player))
        {
            flags |= TradeSkillSheetFlags.WardrobeUnlocked;
        }
        writer.Write((byte)flags);

        byte craft = TradeSkillSheetProtocol.NoActiveCraft;
        if (managed && ClassProfessions.Profiles.Find(player) is { Confirmed: true } state
            && ClassProfessions.Active(player, state.Craft))
        {
            craft = (byte)state.Craft;
        }
        writer.Write(craft);
        foreach (byte skill in TradeSkills.Gathering)
        {
            writer.Write((ushort)player.Skills[(SkillName)skill].BaseFixedPoint);
        }
        foreach (byte skill in TradeSkills.Crafting)
        {
            writer.Write((ushort)player.Skills[(SkillName)skill].BaseFixedPoint);
        }
        player.NetState.Send(writer.Span);
    }
}
