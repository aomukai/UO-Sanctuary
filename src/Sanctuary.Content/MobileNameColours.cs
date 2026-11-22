// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Buffers;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class MobileNameColours
{
    public static void Configure()
    {
        Notoriety.NameStyleHandler = (viewer, target) => (int)Classify(viewer, target);
        Notoriety.SendNameStyle = Send;
    }

    internal static MobileNameStyle Classify(Mobile viewer, Mobile target)
    {
        if (target.Player || target.AccessLevel > AccessLevel.Player)
            return MobileNameStyle.Native;

        if (target is not BaseCreature creature || creature.GetMaster() is { Player: true }
            || PaladinConversion.Friendly(viewer, creature)
            || creature is ConversationNpc or BaseVendor or BaseEscortable or BaseHire)
            return MobileNameStyle.Friendly;

        if (creature.AlwaysMurderer || creature.FightMode is FightMode.Closest or FightMode.Strongest or FightMode.Weakest
            || creature.FightMode == FightMode.Evil && viewer?.Karma < 0)
            return MobileNameStyle.Hostile;

        if (creature.Blessed || creature.IsInvulnerable)
            return MobileNameStyle.Friendly;

        return creature.Body.IsAnimal || creature.FightMode is FightMode.Aggressor or FightMode.Evil && !creature.InitialInnocent
            ? MobileNameStyle.Neutral : MobileNameStyle.Friendly;
    }

    private static void Send(NetState state, Mobile viewer, Mobile target)
    {
        var style = Classify(viewer, target);
        if (style == MobileNameStyle.Native)
            return;

        Span<byte> packet = stackalloc byte[MobileNameProtocol.Length];
        var writer = new SpanWriter(packet);
        writer.Write((byte)0xBF);
        writer.Write((ushort)MobileNameProtocol.Length);
        writer.Write(MobileNameProtocol.SubCommand);
        writer.Write(MobileNameProtocol.Version);
        writer.Write(target.Serial.Value);
        writer.Write((byte)style);
        state.Send(writer.Span);
    }
}
