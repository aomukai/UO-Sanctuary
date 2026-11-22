// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class FensWatchSentry : BritainSentry
{
    [Constructible]
    public FensWatchSentry(bool female) : base(female)
    {
        Name = female ? "Rhoswen" : "Osric";
        Title = "the Fens watch guard";
    }
}

[SerializationGenerator(0)]
public partial class FensWatchCaptain : ConversationNpc
{
    private static readonly ScriptedDialogue.Reply[] _topics = [new("Fens", 1), new("Graves", 2)];
    private static readonly ScriptedDialogue.Node[] _warning =
    [
        new("Take heed, traveller. Beyond this watch lie the Fens of the Dead. Lizardmen hunt the shallows, and fouler things stir in the deep mire.", _topics),
        new("Keep to firm ground where thou canst. A splash in the reeds may be an alligator. If the reeds themselves begin to walk, turn back.", _topics),
        new("There are old graves on the dry ground before the marsh. Give them a wide berth. I have seen the dead there rise in broad daylight.", _topics)
    ];

    [Constructible]
    public FensWatchCaptain()
    {
        Name = "Gareth";
        Title = "captain of the Fens watch";
        Body = 0x190;
        Hue = 0x8403;
        HairItemID = 0x203B;
        FacialHairItemID = 0x2040;
        HairHue = FacialHairHue = 0x455;
        CantWalk = true;
        Direction = Direction.West;
        AddItem(new PlateChest());
        AddItem(new PlateGorget());
        AddItem(new Cloak(0x66D));
        AddItem(new Broadsword());
        AddItem(new Boots());
    }

    internal override ushort Portrait => (ushort)NpcPortrait.Gareth;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _warning;
}
