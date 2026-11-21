// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal static class DespiseStory
{
    internal static void EnsureClue(Map map)
    {
        var location = new Point3D(DespiseClue.X, DespiseClue.Y, DespiseClue.Z);
        if (!AnkhRefuges.Contains(map, location, 3))
            new AnkhNorth().MoveToWorld(new Point3D(5505, 625, 30), map);

        foreach (var item in map.GetItemsAt<DespiseLetter>(location))
            if (item.Location == location)
                return;
        new DespiseLetter().MoveToWorld(location, map);
    }

    internal static void Discover(PlayerMobile player, StoryDiscoveries discovery)
    {
        if (ClassCreation.Profiles.Discover(player.Serial, discovery))
        {
            player.SendMessage("A new entry has been added to thy journal.");
            ScriptedDialogue.SendStoryState(player);
        }
    }
}

[SerializationGenerator(0)]
public partial class DespiseWitness : ConversationNpc
{
    [SerializableField(0, getter: "private", setter: "private")] private byte _portraitId;

    private static readonly ScriptedDialogue.Node[] _pixies =
    [
        new("Careful, traveller. These caves were our home. Then the ratmen came and drove our friends away. We stood no chance against them.",
            [new("Home", 1)]),
        new("We used to know every sound in these caves. Now we keep close to the ankh. Of the ratmen's reasons, I know nothing.", [])
    ];
    private static readonly ScriptedDialogue.Node[] _withLetter =
    [
        _pixies[0] with { Replies = [new("Home", 1), new("Letter", 0, Reveal)] },
        _pixies[1] with { Replies = [new("Letter", 0, Reveal)] },
        new("That letter cannot tell me who took it. The thing it speaks of was a sapling from the Tree of Life in Ilshenar. It vanished, and shortly after that, the ratmen came. I know nothing more of them, or of the one who carried thy letter.", [])
    ];
    private static readonly ScriptedDialogue.Node[] _ratman =
    [
        new("Careful. Something drives my tribe. They do things they never did before. I alone seem free of it. I cannot tell thee what it is, or why it passed me by.", [])
    ];

    [Constructible]
    public DespiseWitness(int portrait)
    {
        if (portrait < (int)NpcPortrait.PixieLinnet || portrait > (int)NpcPortrait.RatmanSkrik)
            throw new ArgumentOutOfRangeException(nameof(portrait));
        _portraitId = (byte)portrait;
        Name = ((NpcPortrait)_portraitId) switch
        {
            NpcPortrait.PixieLinnet => "Linnet",
            NpcPortrait.PixieAlder => "Alder",
            NpcPortrait.PixieSorrel => "Sorrel",
            _ => "Skrik"
        };
        Title = Ratman ? "the ratman" : "the pixie";
        Body = Ratman ? 42 : 128;
        Female = _portraitId is (byte)NpcPortrait.PixieLinnet or (byte)NpcPortrait.PixieSorrel;
        CantWalk = true;
    }

    private bool Ratman => _portraitId == (byte)NpcPortrait.RatmanSkrik;
    internal override byte Portrait => _portraitId;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => Ratman ? _ratman : _pixies;

    internal override void BeginConversation(PlayerMobile player)
    {
        DespiseStory.Discover(player, Ratman ? StoryDiscoveries.UnquietRatman : StoryDiscoveries.DisplacedPixies);
        var foundLetter = (ClassCreation.Profiles.Discoveries(player.Serial) & StoryDiscoveries.DespiseLetter) != 0;
        ScriptedDialogue.Show(player, this, Ratman || !foundLetter ? Script : _withLetter);
    }

    internal static void Reveal(PlayerMobile player, Mobile witness)
    {
        if ((ClassCreation.Profiles.Discoveries(player.Serial) & StoryDiscoveries.DespiseLetter) == 0)
            return;
        DespiseStory.Discover(player, StoryDiscoveries.StolenSapling);
        ScriptedDialogue.Show(player, witness, _withLetter, 2);
    }
}

[SerializationGenerator(0)]
public partial class DespiseLetter : BaseBook
{
    private static readonly BookContent _letter = new("A Folded Letter", "Unsigned",
        new BookPageInfo("Steal the pixie artifact.", "", "Return to the guild.",
            "Speak of this to no one.", "", "Come straight back when", "thou hast it."));

    [Constructible]
    public DespiseLetter() : base(DespiseClue.CorpseGraphic, writable: false)
    {
        Name = "a fallen adventurer";
        Movable = false;
    }

    public override BookContent DefaultContent => _letter;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile { Alive: true, NetState: { Running: true } } player || player.Map != Map
            || !player.InRange(Location, 2) || Math.Abs(player.Z - Z) >= 16 || !player.CanSee(this) || !player.InLOS(this))
            return;

        base.OnDoubleClick(player);
        DespiseStory.Discover(player, StoryDiscoveries.DespiseLetter);
    }
}
