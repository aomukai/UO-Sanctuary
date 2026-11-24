// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class MineCampMiner : ConversationNpc
{
    private static readonly ScriptedDialogue.Node[] _garran =
    [
        new("If thou art bound for the mine, take care. Monsters have overrun the workings. I heard the stone itself moving before we ran.", [])
    ];
    private static readonly ScriptedDialogue.Node[] _bess =
    [
        new("We had to leave our picks behind. The mine is full of monsters now. Watch thy step if thou goest in, and take particular care below the rope.", [])
    ];
    private static readonly ScriptedDialogue.Node _iolo =
        new("Perhaps we should have a look, Avatar. I would sooner know what lurks beneath our feet.", [], Iolo.Voice);

    [Constructible]
    public MineCampMiner() : this(false) { }

    internal MineCampMiner(bool female)
    {
        Name = female ? "Bess" : "Garran";
        Title = "the miner";
        Female = female;
        Body = female ? 0x191 : 0x190;
        Hue = 0x83EA;
        HairItemID = female ? 0x203B : 0x203C;
        HairHue = 0x045E;
        CantWalk = true;
        Direction = female ? Direction.Up : Direction.East;
        AddItem(new Shirt());
        AddItem(new LongPants());
        AddItem(new Boots());
    }

    internal override ushort Portrait => (ushort)(Female ? NpcPortrait.Bess : NpcPortrait.Garran);
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => Female ? _bess : _garran;

    internal override void BeginConversation(PlayerMobile player)
    {
        if (Iolo.IsTravelingWith(player))
        {
            ScriptedDialogue.Show(player, this,
            [
                Script[0] with { Replies = [new("Continue", 1)] },
                _iolo
            ]);
        }
        else
            base.BeginConversation(player);
    }
}
