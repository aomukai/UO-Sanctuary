// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class WesternRoadWarden : ConversationNpc
{
    private static readonly ScriptedDialogue.Reply[] _topics =
        [new("Road", 1), new("Orcs", 2), new("Ogres", 3), new("Bandits", 4)];
    private static readonly ScriptedDialogue.Node[] _script =
    [
        new("Thou art on the main road. Yew lies to the north, Britain to the east past the mountains, and Skara Brae to the south. There are orcs in the forest, talk of ogres in the mountains, and perhaps bandits on the south road.", _topics),
        new("Follow the road north for Yew. To reach Britain, take the east road past the mountains. If thou art bound for Skara Brae, head south.", _topics),
        new("The orcs are in the forest. Keep thine eye on the trees when thou passest. A scout who sees thee may run for help.", _topics),
        new("Travellers speak of ogres somewhere in the mountains. I've heard three tales and three different places. Mind the passes all the same.", _topics),
        new("There may be bandits along the south road. Keep an eye on the roadside, especially where the trees close in.", _topics)
    ];

    [Constructible]
    public WesternRoadWarden()
    {
        Name = "Harlan";
        Title = "warden of the western road";
        Body = 0x190;
        Hue = 0x83EF;
        HairItemID = 0x203B;
        FacialHairItemID = 0x2040;
        HairHue = FacialHairHue = 0x03B2;
        CantWalk = true;
        Direction = Direction.East;
        AddItem(new RingmailChest());
        AddItem(new Cloak(0x0662));
        AddItem(new Boots());
        AddItem(new Broadsword());
    }

    internal override ushort Portrait => (ushort)NpcPortrait.Harlan;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _script;
}

[SerializationGenerator(0)]
public partial class WesternRoadSentry : BritainSentry
{
    [Constructible]
    public WesternRoadSentry(bool female) : base(female)
    {
        Name = "a watch guard";
        Title = null;
        Direction = female ? Direction.West : Direction.East;
    }
}
