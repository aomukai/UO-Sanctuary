// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class ShameCaptainGhost : ConversationNpc
{
    [SerializableField(0)] private bool _fleet;

    private static readonly ScriptedDialogue.Node[] _castaway =
    [
        new("Hold there, ye’ve got living feet. What business brings thee to these rocks?",
            [new("What happened to your crew?", 1, TextId: "shame.castaway.ask_crew")],
            LineId: "shame.castaway.greeting", SpeakerId: "shame.castaway"),
        new("We came for gemstones and found a curse. My crew are bound to these rocks, and so am I. An elemental crushed my body years ago. It’s the one thing down here I can’t haul back aboard.",
            [new("Do you regret coming here?", 2, TextId: "shame.castaway.ask_regret"),
             new("What’s on the last isle?", 3, TextId: "shame.castaway.ask_last_isle")],
            LineId: "shame.castaway.curse", SpeakerId: "shame.castaway"),
        new("Regret? Booty is a pirate’s life. I’d sail for it again tomorrow, if I had a ship and hands to hold the wheel.",
            [new("What’s on the last isle?", 3, TextId: "shame.castaway.ask_last_isle")],
            LineId: "shame.castaway.regret", SpeakerId: "shame.castaway"),
        new("The fleet captain. Death made him stronger than he ever was in life. Leave him his rock, if ye have any liking for the surface.",
            [new("How do I get off this island?", 4, TextId: "shame.castaway.ask_exit")],
            LineId: "shame.castaway.warning", SpeakerId: "shame.castaway"),
        new("The shield of Order will put solid ground beneath thy feet. Chaos will take thee to the fleet captain. I’ve given thee directions, not encouragement.", [],
            LineId: "shame.castaway.exit_advice", SpeakerId: "shame.castaway")
    ];
    private static readonly ScriptedDialogue.Node[] _fleetScript =
    [
        new("Expecting thanks? Ye’ve broken nothing but me bones. The curse holds. Take yer plunder and get off my island.", [],
            LineId: "shame.fleet_captain.farewell", SpeakerId: "shame.fleet_captain")
    ];

    internal ShameCaptainGhost(bool fleet)
    {
        Fleet = fleet;
        Name = fleet ? "the fleet captain" : "the castaway captain";
        Blessed = true;
        CantWalk = true;
        ShameEncounter.Dress(this, fleet ? ShameRole.FleetCaptain : ShameRole.BoneCaptain);
    }

    [AfterDeserialization]
    private void RestoreCostume() => ShameEncounter.RestoreGhostCostumeAfterLoad(this);

    public override void SendAppearanceTo(NetState state) =>
        SkeletonAppearancePackets.Send(state, Serial, SkeletonAppearance.ShameGhost);

    internal override ushort Portrait => (ushort)(_fleet ? NpcPortrait.ShameFleetCaptain : NpcPortrait.ShameGhostCaptain);
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _fleet ? _fleetScript : _castaway;
    internal override bool CanTalkTo(PlayerMobile player) => ShameExpedition.MayTalk(player, this);

    internal override void BeginConversation(PlayerMobile player)
    {
        ScriptedDialogue.Show(player, this, Script, onLeave: (viewer, _) => ShameExpedition.Spoke(viewer, this));
    }
}

[SerializationGenerator(0)]
public partial class ShamePlunder : MetalGoldenChest
{
    internal ShamePlunder()
    {
        Name = "the fleet captain’s plunder";
        Movable = false;
    }

    public override bool IsAccessibleTo(Mobile check) =>
        ShameExpedition.MayTakePlunder(check, this) && base.IsAccessibleTo(check);

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject) =>
        ShameExpedition.MayTakePlunder(from, this) && base.CheckLift(from, item, ref reject);

    public override bool OnDragDrop(Mobile from, Item dropped) => false;
    public override bool OnDragDropInto(Mobile from, Item item, Point3D p) => false;
}
