// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class CompassionWatch : ConversationNpc
{
    private static readonly ScriptedDialogue.Reply[] _towerTopics =
        [new("Desert", 1), new("Swamp", 2), new("Shrine", 3)];
    private static readonly ScriptedDialogue.Node[] _tower =
    [
        new("Beware the swamp south of the desert, traveller. The sands are worse. If thou must visit the Shrine of Compassion, run straight there and do not stop. The beasts will leave thee be at the ankhs.", _towerTopics),
        new("A harpy can see thee coming from farther off than thou canst see it. And if the sand begins to rise of its own accord, keep running.", _towerTopics),
        new("The marsh south of the sands is foul ground. Mind the lizardmen and keep clear of the deeper pools.", _towerTopics),
        new("The Shrine of Compassion stands on the western side of the desert. Make for the ankhs. Thou canst rest once thou hast reached them.", _towerTopics)
    ];

    private static readonly ScriptedDialogue.Reply[] _crossroadsTopics =
        [new("East", 1), new("South", 2), new("West", 3), new("Covetous", 4)];
    private static readonly ScriptedDialogue.Node[] _crossroads =
    [
        new("East for Cove, Minoc and Vesper. South for the swamp, if thou hast a taste for foul water and poison. West, past the desert, lies the Shrine of Compassion. There is a cave to Covetous off the Vesper road.", _crossroadsTopics),
        new("Follow the road east for Cove, Minoc and Vesper. Stay on the road until it is time to turn for thy destination.", _crossroadsTopics),
        new("South is the swamp. The water is foul, and there are poisonous things in it. I would keep to the road.", _crossroadsTopics),
        new("The road west passes the edge of the desert. The Shrine of Compassion lies beyond the sands. If thou goest in, run until thou reachest the ankhs.", _crossroadsTopics),
        new("Watch the mountains beside the Vesper road. The cave that leads into Covetous opens there.", _crossroadsTopics)
    ];
    private static readonly ScriptedDialogue.Node[] _sentry = [new("Leofric can give thee directions. He keeps watch where the roads meet.", [])];

    [SerializableField(0)] private int _post;

    [Constructible]
    public CompassionWatch(int post)
    {
        _post = post;
        Name = post switch
        {
            0 => "Aldric", 1 => "Tamsin", 2 => "Leofric", 3 => "Edda", 4 => "Hugh",
            _ => throw new ArgumentOutOfRangeException(nameof(post))
        };
        Title = post < 2 ? "the desert watch guard" : "the crossroads guard";
        Female = post is 1 or 3;
        Body = Female ? 0x191 : 0x190;
        Hue = 0x83EF;
        CantWalk = true;
        Direction = post < 2 ? Direction.South : Direction.West;
        AddItem(new RingmailChest());
        AddItem(new Bascinet());
        AddItem(new Boots());
        AddItem(new Halberd());
    }

    internal override ushort Portrait => (ushort)(_post switch
    {
        0 => NpcPortrait.Aldric,
        1 => NpcPortrait.Tamsin,
        2 => NpcPortrait.Leofric,
        3 => NpcPortrait.Edda,
        4 => NpcPortrait.Hugh,
        _ => throw new InvalidOperationException($"Unrecognized Compassion watch post {_post}.")
    });

    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _post < 2 ? _tower : _post == 2 ? _crossroads : _sentry;
}
