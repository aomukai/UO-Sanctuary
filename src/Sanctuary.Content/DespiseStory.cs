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

    private static readonly ScriptedDialogue.Voice _linnet = new("Linnet", (ushort)NpcPortrait.PixieLinnet);
    private static readonly ScriptedDialogue.Voice _alder = new("Alder", (ushort)NpcPortrait.PixieAlder);
    private static readonly ScriptedDialogue.Voice _sorrel = new("Sorrel", (ushort)NpcPortrait.PixieSorrel);
    private static readonly ScriptedDialogue.Reply _home = new("Home", 1, TextId: "despise.pixies.ask_home");
    private static readonly ScriptedDialogue.Reply _letter = new("Show them the letter", 0, Reveal,
        TextId: "despise.pixies.show_letter");

    private static readonly ScriptedDialogue.Node[] _pixies =
    [
        new("Careful, traveller. These caves were our home. Then the ratmen came and drove our friends away. We stood no chance against them.",
            [_home], _linnet, LineId: "despise.pixies.greeting", SpeakerId: "despise.linnet"),
        new("We used to know every sound in these caves. Now we keep close to the ankh. If thou findest anything that might explain why the ratmen came, bring it to us here.",
            [], _alder, LineId: "despise.pixies.home", SpeakerId: "despise.alder")
    ];
    private static readonly ScriptedDialogue.Node[] _withLetter =
    [
        new("Thou hast found something? Come closer. Let us see what thou hast brought.",
            [_letter, _home], _linnet, LineId: "despise.pixies.return_with_letter", SpeakerId: "despise.linnet"),
        _pixies[1] with { Replies = [_letter] }
    ];
    private static readonly ScriptedDialogue.Node _sapling =
        new("The thing this letter speaks of was a sapling from the Tree of Life in Ilshenar. It vanished, and shortly after that, the ratmen came. I know nothing more of them, or of the one who carried thy letter.",
            [], _sorrel, LineId: "despise.pixies.sapling", SpeakerId: "despise.sorrel");
    private static readonly ScriptedDialogue.Reply _takeRune =
        new("Continue", 0, GiveRune, TextId: "despise.pixies.continue");
    private static readonly ScriptedDialogue.Node[] _gift =
    [
        new("Thank thee for coming back to us. I have marked the mouth of the caves in thy runebook, shouldst thou need to return.",
            [], _linnet, LineId: "despise.pixies.rune", SpeakerId: "despise.linnet")
    ];
    private static readonly ScriptedDialogue.Node[] _needsRunebook =
    [
        new("Come back with a runebook and I shall mark the mouth of the caves for thee.",
            [], _linnet, LineId: "despise.pixies.rune_needs_runebook", SpeakerId: "despise.linnet")
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
    internal override ushort Portrait => _portraitId;
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
        ScriptedDialogue.Show(player, witness, DungeonEntranceRunes.Despise.WasClaimed(player)
            ? [_sapling] : [_sapling with { Replies = [_takeRune] }]);
    }

    private static void GiveRune(PlayerMobile player, Mobile witness)
    {
        if ((ClassCreation.Profiles.Discoveries(player.Serial) & StoryDiscoveries.StolenSapling) == 0)
            return;
        bool awarded = DungeonEntranceRunes.Award(player, DungeonEntranceRunes.Despise);
        ScriptedDialogue.Show(player, witness, awarded ? _gift : _needsRunebook);
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

        const string id = "despise.guild.folded_letter";
        if (!AuthoredWriting.Show(player, id, AuthoredTextKind.Letter, Title, Author,
            AuthoredWriting.BookBody(this), id + ".body"))
            base.OnDoubleClick(player);
        bool firstReading = (ClassCreation.Profiles.Discoveries(player.Serial) & StoryDiscoveries.DespiseLetter) == 0;
        DespiseStory.Discover(player, StoryDiscoveries.DespiseLetter);
        if (firstReading)
            player.SendMessage("The pixies near the ankh may know what was stolen. Bring the letter back to them.");
    }
}
