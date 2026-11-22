// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public enum HedgeGhostPart : byte
{
    Girl, Grandfather, EldestDaughter, EldestSon, YoungerSon, Grandmother, Mother, Father
}

[SerializationGenerator(0)]
public partial class HedgeHouseGhost : ConversationNpc
{
    private const string GirlName = "Nella", GrandfatherName = "Aldous", EldestDaughterName = "Maren", EldestSonName = "Bram";
    private const string YoungerSonName = "Tobin", GrandmotherName = "Hester", MotherName = "Edith", FatherName = "Roderic";

    private static readonly ScriptedDialogue.Reply[] _boyTopics = [new("Family", 1), new("Death", 6), new("Ghosts", 7)];
    private static readonly ScriptedDialogue.Reply[] _familyTopics =
        [new("Parents", 2), new("Grandparents", 3), new("Brother", 4), new("Sisters", 5)];
    private static readonly ScriptedDialogue.Voice _grandmother = new(GrandmotherName, (ushort)NpcPortrait.HedgeGrandmother);
    private static readonly ScriptedDialogue.Voice _mother = new(MotherName, (ushort)NpcPortrait.HedgeMother);

    private static readonly ScriptedDialogue.Node[] _girl =
    [
        new("Her eyes drift toward thee slowly, as if thy presence were reaching her from a great distance. For a heartbeat she sees thee clearly. “Monster!” she screams, and vanishes.", [])
    ];

    private static readonly ScriptedDialogue.Node[] _boy =
    [
        new($"Oh, a visitor! I'm {YoungerSonName}. Mother said there'd be company. I can speak a little while, but I must finish this page before supper.", _boyTopics),
        new("We're all about the house somewhere. Who were you looking for?", _familyTopics),
        new($"Father is upstairs with his papers. His name's {FatherName}, if you've come on business. Mother's {MotherName}. She's in the kitchen. If you hear an argument, I'd wait a little before going in.", _boyTopics),
        new($"Grandmother {GrandmotherName} is baking today. It's her birthday. Grandfather {GrandfatherName} is upstairs in his workroom. He says we must keep away from the bottles with the round stoppers.", _boyTopics),
        new($"My brother {EldestSonName} is down by the tree splitting wood. If he says he's nearly done, you'll know he hasn't really started.", _boyTopics),
        new($"{EldestDaughterName} is out with the chickens. Our little sister {GirlName} was inside a moment ago. She goes quiet when there are strangers.", _boyTopics),
        new("I don't know anything about that. Could we speak of something else?", _boyTopics),
        new("Ghosts? I haven't seen any. Please, I don't want to talk about that.", _boyTopics)
    ];

    private static readonly ScriptedDialogue.Node[] _kitchen =
    [
        new($"{GrandmotherName} hums as she stirs a bowl. A dusting of flour lies at her feet.", [new("Listen", 1)], _grandmother),
        new("Mother! Look at the floor. That was the good flour, and I've scarcely enough left to make the cake.", [new("Listen", 2)], _mother),
        new("There's plenty in the sack, love. Sweep it up and give the batter a stir. The visitors will be here any minute.", [new("Listen", 3)], _grandmother),
        new("Your birthday, and somehow I'm the one in a hurry.", [new("Listen", 4)], _mother),
        new("Well, I can't very well turn a year older and sweep at the same time, can I?", [], _grandmother)
    ];

    private static readonly ScriptedDialogue.Node[][] _scripts =
    [
        _girl,
        [new("Where are my ingredients? And the recipe book was right here. Those grandchildren are playing tricks again. Let them try making sense of my notes without it.", [])],
        [new("Come on, hens, no loafing today. We need proper eggs for Grandmother's birthday cake. And you, cock: if you crow after I'm already up, what good are you to me?", [])],
        [new("He brings the hatchet down on a stubborn log. “Of course I have to split the wood. Everyone else gets to bake or read. The fire can have what I've cut and like it.”", [])],
        _boy,
        _kitchen,
        _kitchen,
        [new("He reads a page of figures, then straightens the stack. “Let the royal mage bring every apprentice he has. He may hear me plainly. This farm has been in my family for generations. Under no circumstances will I sell it.”", [])]
    ];

    private static readonly string[] _names =
        [GirlName, GrandfatherName, EldestDaughterName, EldestSonName,
            YoungerSonName, GrandmotherName, MotherName, FatherName];

    [SerializableField(0)]
    private HedgeGhostPart _part;

    private readonly HashSet<Serial> _frightenedViewers = new();
    private long _nextChop = Core.TickCount;

    [Constructible]
    public HedgeHouseGhost(HedgeGhostPart part)
    {
        _part = part;
        ApplyName();
        Female = part is HedgeGhostPart.Girl or HedgeGhostPart.EldestDaughter or HedgeGhostPart.Mother or HedgeGhostPart.Grandmother;
        Body = Female ? 0x191 : 0x190;
        Hue = 0x83EA;
        HairItemID = Female ? 0x203C : 0x203B;
        HairHue = 0x0481;
        CantWalk = true;
        Blessed = true;
        Direction = part == HedgeGhostPart.Grandmother ? Direction.South : Direction.East;
        Dress(part);
    }

    [AfterDeserialization]
    private void ApplyName() => Name = _names[(int)_part];

    public override bool IsDeadBondedPet => true;
    internal override ushort Portrait => (ushort)((int)NpcPortrait.HedgeGirl + (int)_part);
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _scripts[(int)_part];
    internal bool VisibleTo(PlayerMobile player) => !_frightenedViewers.Contains(player.Serial);
    internal override bool CanTalkTo(PlayerMobile player) => VisibleTo(player) && Math.Abs(player.Z - Z) <= 5;

    public override void OnThink()
    {
        base.OnThink();
        if (_part != HedgeGhostPart.EldestSon || Core.TickCount < _nextChop)
            return;

        _nextChop = Core.TickCount + 4000;
        Direction = GetDirectionTo(HedgeMazeStory.Woodpile);
        Animate(13, 5, 1, true, false, 0);
    }

    internal override void BeginConversation(PlayerMobile player)
    {
        if (_part == HedgeGhostPart.Girl)
            ScriptedDialogue.Show(player, this, Script, onLeave: (viewer, speaker) => ((HedgeHouseGhost)speaker).HideFrom(viewer));
        else
        {
            ScriptedDialogue.Show(player, this, Script, _part == HedgeGhostPart.Mother ? (byte)1 : (byte)0);
            if (_part == HedgeGhostPart.YoungerSon)
                ScriptedDialogue.RecordDiscovery(player, StoryDiscoveries.HedgeTobin);
            else if (_part == HedgeGhostPart.Father)
                ScriptedDialogue.RecordDiscovery(player, StoryDiscoveries.HedgeRoderic);
        }
    }

    private void HideFrom(PlayerMobile player)
    {
        if (Deleted || player.Deleted || !_frightenedViewers.Add(player.Serial))
            return;
        player.NetState?.SendRemoveEntity(Serial);
        Timer.StartTimer(TimeSpan.FromMinutes(10), () =>
        {
            _frightenedViewers.Remove(player.Serial);
            if (!Deleted && player.NetState is { Running: true } state && player.Map == Map
                && Utility.InUpdateRange(player.Location, Location) && player.CanSee(this))
            {
                state.SendMobileIncoming(player, this);
                state.SendBondedStatus(Serial, true);
                SendOPLPacketTo(state);
            }
        });
    }

    private void Dress(HedgeGhostPart part)
    {
        switch (part)
        {
            case HedgeGhostPart.Girl:
                Wear(new PlainDress(), 0x0481);
                break;
            case HedgeGhostPart.Grandfather:
                Wear(new Robe(), 0x0482);
                break;
            case HedgeGhostPart.EldestDaughter:
                Wear(new PlainDress(), 0x047E);
                Wear(new HalfApron(), 0x0481);
                break;
            case HedgeGhostPart.Grandmother:
                Wear(new PlainDress(), 0x0482);
                Wear(new Cloak(), 0x0481);
                break;
            case HedgeGhostPart.Mother:
                Wear(new PlainDress(), 0x0482);
                Wear(new FullApron(), 0x0481);
                break;
            default:
                Wear(new Shirt(), 0x0481);
                Wear(new LongPants(), 0x0482);
                if (part == HedgeGhostPart.EldestSon)
                    Wear(new Hatchet(), 0x0481);
                if (part == HedgeGhostPart.Father)
                    Wear(new Doublet(), 0x047E);
                break;
        }
        Wear(new Sandals(), 0x0481);
    }

    private void Wear(Item item, int hue)
    {
        item.Hue = hue;
        item.Movable = false;
        AddItem(item);
    }
}

[SerializationGenerator(0)]
public partial class HedgeFowl : Chicken
{
    [Constructible]
    public HedgeFowl(bool cockerel)
    {
        Name = cockerel ? "a ghostly cockerel" : "a ghostly hen";
        Hue = cockerel ? 0x0482 : 0x0481;
        FightMode = FightMode.None;
        CantWalk = true;
        Blessed = true;
        Tamable = false;
    }

    public override bool IsDeadBondedPet => true;
    public override bool IsInvulnerable => true;
}
