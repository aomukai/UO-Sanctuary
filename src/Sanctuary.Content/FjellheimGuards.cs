// SPDX-License-Identifier: GPL-3.0-or-later
using Server.Mobiles;

namespace Sanctuary;

internal static class FjellheimGuards
{
    private static readonly ScriptedDialogue.Voice _sten = new("Sten", (ushort)NpcPortrait.Sten);
    private static readonly ScriptedDialogue.Voice _ketil = new("Ketil", (ushort)NpcPortrait.Ketil);
    private static readonly ScriptedDialogue.Reply _jarl = new("What sort of man is the Jarl?", 2, TextId: "fjellheim.guards.ask_jarl");
    private static readonly ScriptedDialogue.Reply _town = new("Tell me about Fjellheim.", 3, TextId: "fjellheim.guards.ask_town");
    private static readonly ScriptedDialogue.Reply _thing = new("Why is the Thing closed?", 4, TextId: "fjellheim.guards.ask_thing");

    private static readonly ScriptedDialogue.Node[] _dialogue =
    [
        new("Hold there. The Thing is closed. A few steps back, if you please.",
            [new("May I speak with the Jarl?", 1, TextId: "fjellheim.guards.ask_audience")],
            _sten, LineId: "fjellheim.sten.halt", SpeakerId: "fjellheim.sten"),
        new("Jarl Asbjorn receives no visitors just now. If it’s something we can answer, ask away.",
            [_jarl, _town, _thing], _ketil, LineId: "fjellheim.ketil.no_visitors", SpeakerId: "fjellheim.ketil"),
        new("Asbjorn Isisson. The winter the ice took our pier, he stood waist-deep in the water and held a rope till the last man was ashore. That’s the man whose door I guard.",
            [_town, _thing], _sten, LineId: "fjellheim.sten.asbjorn", SpeakerId: "fjellheim.sten"),
        new("Fjellheim’s an old trading outpost of the Ostmen. The northern tribes bring their furs here, and ships from Britannia meet them at our pier. See the warehouse down the road? You can buy wares there yourself.",
            [_jarl, _thing], _ketil, LineId: "fjellheim.ketil.outpost", SpeakerId: "fjellheim.ketil"),
        new("Something serious has happened. We’ve been asked to hold this door and keep our counsel. That’s all I’m at liberty to say.",
            [_jarl, _town], _sten, LineId: "fjellheim.sten.closed_thing", SpeakerId: "fjellheim.sten")
    ];

    internal static void AtDoor(PlayerMobile player)
    {
        if (!StatNetwork.SupportsCurrentProtocol(player)) return;
        DaggerThingResident nearest = null;
        foreach (var guard in player.Map.GetMobilesInRange<DaggerThingResident>(player.Location, PortraitProtocol.NpcInteractionRange))
        {
            if (!guard.IsGuard || !ScriptedDialogue.CanTalk(player, guard)) continue;
            if (ScriptedDialogue.HasScopedConversation(player, guard)) return;
            if (nearest == null || player.GetDistanceToSqrt(guard) < player.GetDistanceToSqrt(nearest))
                nearest = guard;
        }
        if (nearest != null) Open(player, nearest);
    }

    internal static void Open(PlayerMobile player, DaggerThingResident guard)
    {
        if (!ScriptedDialogue.HasScopedConversation(player, guard))
            ScriptedDialogue.Show(player, guard, _dialogue, canReply: ScriptedDialogue.CanTalk);
    }
}
