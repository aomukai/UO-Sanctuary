// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;

namespace Sanctuary;

internal static class DestardCourt
{
    internal static void Install()
    {
        PlaceGuard(new Point3D(5307, 969, 0), false);
        PlaceGuard(new Point3D(5311, 956, 0), true);

        if (!World.Mobiles.Values.OfType<DestardRoyalServant>().Any(npc => npc.Map == Map.Trammel))
            new DestardRoyalServant().MoveToWorld(new Point3D(5318, 980, 0), Map.Trammel);
    }

    private static void PlaceGuard(Point3D position, bool northernPost)
    {
        if (!World.Mobiles.Values.OfType<DestardRoyalGuard>()
            .Any(guard => guard.Map == Map.Trammel && guard.NorthernPost == northernPost))
            new DestardRoyalGuard(northernPost).MoveToWorld(position, Map.Trammel);
    }
}

[SerializationGenerator(0)]
public partial class DestardRoyalGuard : ConversationNpc
{
    private static readonly ScriptedDialogue.Node[] _watchful =
        [new("I have my eye on thee. Try nothing while thou art in the Queen’s halls.", [])];
    private static readonly ScriptedDialogue.Node[] _grateful =
        [new("Word of thy deeds has reached my post. Thank thee for standing with us.", [])];
    private static readonly ScriptedDialogue.Node[] _trusted =
        [new("Well met, trusted ally. Thou art welcome among the Queen’s people.", [])];

    [SerializableField(0, setter: "private")] private bool _northernPost;

    internal DestardRoyalGuard(bool northernPost)
    {
        NorthernPost = northernPost;
        Name = "a royal dragon guard";
        Body = 12;
        BaseSoundID = 362;
        Hue = northernPost ? 0x59B : 0;
        CantWalk = true;
        Blessed = true;
    }

    internal override ushort Portrait => (ushort)(NorthernPost ? NpcPortrait.DestardGuardNorth : NpcPortrait.DestardGuardWest);
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _watchful;

    internal IReadOnlyList<ScriptedDialogue.Node> DialogueFor(PlayerMobile player) => DestardBounty.For(player)?.Horns switch
    {
        >= 50 => _trusted,
        >= 20 => _grateful,
        _ => Script
    };

    internal override void BeginConversation(PlayerMobile player) => ScriptedDialogue.Show(player, this, DialogueFor(player));
}

[SerializationGenerator(0)]
public partial class DestardRoyalServant : ConversationNpc
{
    private static readonly ScriptedDialogue.Node[] _invasion = BuildStory(false);
    private static readonly ScriptedDialogue.Node[] _aftermath = BuildStory(true);

    internal DestardRoyalServant()
    {
        Name = "royal servant";
        Body = 12;
        BaseSoundID = 362;
        Hue = 0x8A5;
        CantWalk = true;
        Blessed = true;
    }

    internal override ushort Portrait => (ushort)NpcPortrait.DestardServant;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _invasion;

    internal IReadOnlyList<ScriptedDialogue.Node> DialogueFor(PlayerMobile player) =>
        BritanniaCampaign.Has(player, CampaignProgress.Diablo) ? _aftermath : Script;

    internal override void BeginConversation(PlayerMobile player) => ScriptedDialogue.Show(player, this, DialogueFor(player));

    private static ScriptedDialogue.Node[] BuildStory(bool defeated) =>
    [
        new(defeated
                ? "The voice from below has fallen silent. Diablo is dead, though his daemons still prowl our halls. I can tell thee how they came here."
                : "I served these halls before the daemons came. Ask what thou wilt, and I shall tell thee what I saw.",
            [new("The Queen", 1), new("The Consort", 2), new("What Happened?", 3), new("More", 4)],
            Learns: defeated ? StoryDiscoveries.DestardAftermath : StoryDiscoveries.None),
        new("We love Queen Vaelith. She barred the halls against the invaders, but no door could keep out the voice that turned our own kin against us. She could not shield us from its reach.",
            [new("Back", 0)], Learns: StoryDiscoveries.DestardQueen),
        new("Rhazek held the passage while the brood withdrew. None among us doubts his courage. Even he could not stop what came through the gate from driving us out of our home.",
            [new("Back", 0)], Learns: StoryDiscoveries.DestardConsort),
        new(defeated
                ? "It began in the far quarters, in the old nursery. Daemons and corruption poured through a red gate. We held them at first, until the wyrms and wyverns struck from behind. We fell back. Thou hast slain what came through that gate."
                : "Go to the far quarters, where our nursery used to be. That is where the red gate opened. Daemons poured through and brought the corruption with them. We held them at first, until wyrms and wyverns struck from behind. We had to fall back.",
            [new("Back", 0)], Learns: StoryDiscoveries.DestardNursery),
        new("There are some I still hope to see come home. Others chose their side well enough.",
            [new("Betrayal", 5), new("Daemons", 6), new("The Enslaved and the Young", 7), new("Back", 0)]),
        new("The wyrms and wyverns have always coveted power over our shared home. When the daemons came, they saw their chance. They chose to turn on us while we held the line.",
            [new("Back", 4)], Learns: StoryDiscoveries.DestardBetrayal),
        new(defeated
                ? "Diablo’s influence came with his daemons through the red gate. His voice is gone, but some of his servants remain. The Queen still pays for their horns."
                : "The daemons came through the red gate. With them came a voice that bent many to its will. The Queen pays for their horns. Every one thou bringest her is one less down there.",
            [new("Back", 4)], Learns: StoryDiscoveries.DestardDaemons),
        new("Some of our drakes and dragons were overcome and made to serve. The hatchlings in the nursery were corrupted before they could fly. The young never had a choice. Remember that when thou seest what has become of them.",
            [new("Back", 4)], Learns: StoryDiscoveries.DestardEnslaved)
    ];
}
