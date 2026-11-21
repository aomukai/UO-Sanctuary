// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class BritainStory
{
    internal static readonly Point3D CaptainPost = new(1342, 1746, 20);
    internal static readonly Point3D RecruitPost = new(1341, 1746, 20);
    internal static readonly Point3D Throne = new(1323, 1625, 55);
    internal static readonly Point3D CollegePost = new(1439, 1557, 32);
    internal static readonly Point3D CollegeRune = new(1440, 1553, 32);
    internal static readonly Rectangle2D Bridge = new(1377, 1744, 20, 12);
    private static readonly Point3D[] _guardPosts = [new(1342, 1742, 20), new(1345, 1742, 20)];
    private const StoryDiscoveries Audience = StoryDiscoveries.RoyalAudience | StoryDiscoveries.IoloRetired;

    public static void Configure()
    {
        EventSink.ServerStarted += Install;
        ClassMapHooks.LocationChanged += AtBridge;
    }

    private static void Install()
    {
        var map = Map.Trammel;
        Place<Geoffrey>(CaptainPost, () => new Geoffrey());
        Place<Wolfgang>(RecruitPost, () => new Wolfgang(), actor => !actor.Recruited);
        Place<LordBritish>(Throne, () => new LordBritish());
        Place<CollegeIolo>(CollegePost, () => new CollegeIolo());
        for (int i = 0; i < _guardPosts.Length; i++)
        {
            int post = i;
            Place<BritainSentry>(_guardPosts[post], () => new BritainSentry(post == 1),
                guard => guard.Female == (post == 1));
        }

        PlaceSign(SignType.ArmamentsGuild, new(1345, 1743, 33), "Britain City Guard Headquarters");
        PlaceSign(SignType.BardicGuild, new(1437, 1560, 43), "Britain Bard's College");
        PlaceProp(0x0EB5, new(1437, 1551, 32));
        PlaceProp(0x0EB7, new(1441, 1554, 32));

        void Place<T>(Point3D position, Func<T> create, Predicate<T> match = null) where T : BaseCreature
        {
            if (!World.Mobiles.Values.OfType<T>().Any(actor => !actor.Deleted && (match?.Invoke(actor) ?? true)))
                create().MoveToWorld(position, map);
        }

        void PlaceSign(SignType type, Point3D point, string name)
        {
            foreach (var sign in map.GetItemsAt<BaseSign>(point))
                if (sign.Name == name)
                    return;
            new Sign(type, SignFacing.North) { Name = name }.MoveToWorld(point, map);
        }

        void PlaceProp(int graphic, Point3D point)
        {
            foreach (var prop in map.GetItemsAt<Static>(point))
                if (prop.ItemID == graphic && prop.Z == point.Z)
                    return;
            new Static(graphic).MoveToWorld(point, map);
        }
    }

    private static void AtBridge(Mobile mobile)
    {
        if (mobile is not PlayerMobile { Alive: true } player || player.Map != Map.Trammel
            || !Bridge.Contains(player.Location) || Math.Abs(player.Z) > 8
            || !StatNetwork.SupportsCurrentProtocol(player))
            return;
        var profiles = ClassCreation.Profiles;
        if (profiles.GreetedBritain(player.Serial)
            || Iolo.NearbyCompanion(player) is not { } iolo || player.NetState is not { Running: true } connection)
            return;

        profiles.RecordBritainGreeting(player.Serial);
        iolo.PrivateOverheadMessage(MessageType.Regular, Iolo.VoiceHue, false,
            "Britain at last. It is good to see her again. Come, let us find Lord British.", connection);
    }

    internal static bool Retired(PlayerMobile player) =>
        (ClassCreation.Profiles.Discoveries(player.Serial) & StoryDiscoveries.IoloRetired) != 0;

    internal static bool MobileVisible(Mobile viewer, Mobile target)
    {
        if (viewer is not PlayerMobile player)
            return true;
        if (target is Wolfgang { Recruited: false })
            return !MercenaryCare.HasContract<Wolfgang>(player);
        if (target is Merydwen { Recruited: false })
            return !MercenaryCare.HasContract<Merydwen>(player);
        if (target is CollegeIolo)
            return Retired(player) || (ClassCreation.Profiles.HasDepartedNewHaven(player.Serial)
                && !MercenaryCare.HasContract<Iolo>(player));
        return true;
    }

    internal static void RecordGeoffrey(PlayerMobile player)
    {
        if (ClassCreation.Profiles.Discover(player.Serial, StoryDiscoveries.MetGeoffrey))
            Announce(player, "A new entry has been added to thy journal.");
    }

    internal static void FinishAudience(PlayerMobile player, Mobile speaker)
    {
        if (speaker is not LordBritish king || Retired(player) || !ScriptedDialogue.CanTalk(player, king))
            return;
        if (Iolo.NearbyCompanion(player) is not { } iolo || !MercenaryCare.Retire(player, iolo, keepPotions: true))
        {
            player.SendMessage("Bring Iolo to the throne before concluding the audience.");
            return;
        }

        var profiles = ClassCreation.Profiles;
        profiles.RecordNewHavenDeparture(player.Serial);
        profiles.Discover(player.Serial, Audience);
        Announce(player, "Two new entries have been added to thy journal.");
    }

    private static void Announce(PlayerMobile player, string message)
    {
        player.SendMessage(message);
        ScriptedDialogue.SendStoryState(player);
    }
}

[SerializationGenerator(0)]
public partial class BritainSentry : BaseCreature
{
    [Constructible]
    public BritainSentry() : this(false) { }

    internal BritainSentry(bool female) : base(AIType.AI_Vendor, FightMode.None, 10, 1)
    {
        Name = female ? "Maud" : "Edric";
        Title = "the Britain city guard";
        Female = female;
        Body = female ? 0x191 : 0x190;
        Hue = 0x83EF;
        CantWalk = true;
        Blessed = true;
        Direction = Direction.North;
        AddItem(new RingmailChest());
        AddItem(new Bascinet());
        AddItem(new Halberd());
        AddItem(new Boots());
    }
}

[SerializationGenerator(0)]
public partial class Geoffrey : ConversationNpc
{
    [Constructible]
    public Geoffrey()
    {
        Name = "Geoffrey";
        Title = "captain of the Britain city guard";
        Body = 0x190;
        Hue = 0x83EF;
        CantWalk = true;
        Direction = Direction.East;
        AddItem(new PlateChest());
        AddItem(new CloseHelm());
        AddItem(new Broadsword());
        AddItem(new Cloak(0x0591));
        AddItem(new Boots());
    }

    internal override byte Portrait => 4;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _before;

    private static readonly ScriptedDialogue.Node[] _before =
    [
        new("Avatar. It has been too long. I captain the city guard now, as thou canst see. Go and finish whatever business brought thee to Britain. Come back when thou art free. I have someone I should like thee to meet.", [])
    ];
    private static readonly ScriptedDialogue.Node[] _after =
    [
        new("I heard Iolo has hung up his crossbow. This is Wolfgang, one of my recruits. He's steady behind a shield and green as spring grass. A spell on the road with thee would do him good. Wilt thou take him along?",
            [new("Wolfgang", 1), new("Travel Together", 0, Recruit), new("Later", 2)]),
        new("If the captain thinks I am ready, then I am ready. I should like to come, if thou wilt have me.",
            [new("Travel Together", 0, Recruit), new("Later", 2)], Wolfgang.Voice),
        new("I shall keep him on drill until thou returnest.", [])
    ];
    private static readonly ScriptedDialogue.Node[] _joined =
    [
        new("How fares my recruit? Watch what he does when trouble comes. That will tell thee more than anything he tells thee here.", [])
    ];

    internal override void BeginConversation(PlayerMobile player)
    {
        BritainStory.RecordGeoffrey(player);
        var script = MercenaryCare.HasContract<Wolfgang>(player) ? _joined : BritainStory.Retired(player) ? _after : _before;
        ScriptedDialogue.Show(player, this, script);
    }

    private static void Recruit(PlayerMobile player, Mobile speaker)
    {
        if (speaker is not Geoffrey captain || !BritainStory.Retired(player))
            return;
        if (Wolfgang.Join(player) is not { } wolfgang)
        {
            player.SendMessage("Thou already hast a companion at thy side.");
            return;
        }
        foreach (var recruit in captain.Map.GetMobilesInRange<Wolfgang>(captain.Location, 4))
            if (!recruit.Recruited)
                player.NetState?.SendRemoveEntity(recruit.Serial);
        ScriptedDialogue.SendStoryState(player);
        ScriptedDialogue.Show(player, captain,
        [
            new("I did not send thee with the Avatar to learn how to win fights, lad. I sent thee to learn which fights are worth having.",
                [new("Wolfgang", 1)]),
            new("Aye, Captain. I shall try to remember. I have all my kit, Avatar. I checked twice.", [], Wolfgang.Voice)
        ]);
    }
}

[SerializationGenerator(0)]
public partial class LordBritish : ConversationNpc
{
    private static readonly ScriptedDialogue.Voice _iolo = new("Iolo", 11);

    [Constructible]
    public LordBritish()
    {
        Name = "Lord British";
        Body = 0x190;
        Hue = 0x83EF;
        HairItemID = 0x203C;
        HairHue = 0x0481;
        FacialHairItemID = 0x203E;
        FacialHairHue = 0x0481;
        CantWalk = true;
        Direction = Direction.East;
        AddItem(new Circlet());
        AddItem(new Robe(0x0591));
        AddItem(new Cloak(0x0457));
        AddItem(new Boots());
    }

    internal override byte Portrait => 10;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _waiting;
    private static readonly ScriptedDialogue.Node[] _waiting =
    [
        new("Avatar, it is good to see thee in Britain. I have heard Iolo is looking into the trouble. If he travels with thee, bring him here. I would hear what he has learned.", [])
    ];
    private static readonly ScriptedDialogue.Node[] _after =
    [
        new("Follow what leads thou findest, Avatar. I must see to matters here, but my door is open when thou hast news.", [])
    ];

    internal override void BeginConversation(PlayerMobile player)
    {
        if (BritainStory.Retired(player))
            ScriptedDialogue.Show(player, this, _after);
        else if (Iolo.NearbyCompanion(player) is { } iolo)
            ScriptedDialogue.Show(player, this, AudienceFor(player, iolo));
        else
            base.BeginConversation(player);
    }

    internal static IReadOnlyList<ScriptedDialogue.Node> AudienceFor(PlayerMobile player, Iolo iolo)
    {
        var profiles = ClassCreation.Profiles;
        string report = "I have heard the same story from too many corners of Britannia. Creatures are driving folk from places where they once worked in peace.";
        if (profiles.HasInvestigatedHavenMine(player.Serial, out var witness))
            report += witness == iolo.Serial
                ? " We fought an ogre below the New Haven mine. It had powers I have never seen in one of its kind."
                : " The Avatar tells me an ogre below the New Haven mine wielded powers unknown to me.";

        string farewell = (profiles.Discoveries(player.Serial) & StoryDiscoveries.MetGeoffrey) != 0
            ? "Geoffrey asked thee to return when our business was done. Before thou settest out again, find me at the bard's college. I have something for thee."
            : "Before thou settest out again, find me at the bard's college. I have something for thee.";
        return
        [
            new("Welcome, old friend. Iolo, thou hast brought the Avatar a long way. What news dost thou bring?", [new("Reports", 1)]),
            new(report, [new("How can I help?", 2)], _iolo),
            new("The reports that reach me grow more troubling by the day. Find out what lies behind them, Avatar. My duties bind me to Britain, but I can hear whatever thou learnest.", [new("Iolo", 3)]),
            new("Here we part ways, my friend. I have carried this crossbow further than I meant to. There are young bards at the college who ought to learn these songs while I can still remember the verses.", [new("The Bard's College", 4)], _iolo),
            new(farewell, [new("Until Then", 0, BritainStory.FinishAudience)], _iolo)
        ];
    }
}

[SerializationGenerator(0)]
public partial class CollegeIolo : ConversationNpc
{
    private const string RuneName = "Britain Bard's College";

    [Constructible]
    public CollegeIolo()
    {
        Name = "Iolo";
        Title = "the bard";
        Female = false;
        Body = 0x190;
        Hue = 0x83EF;
        HairItemID = 0x203C;
        HairHue = 0x0481;
        FacialHairItemID = 0x2040;
        FacialHairHue = 0x0481;
        CantWalk = true;
        Direction = Direction.South;
        AddItem(new FancyShirt(0x03E5));
        AddItem(new LongPants(0x0455));
        AddItem(new Cloak(0x0591));
        AddItem(new Boots(0x0590));
    }

    internal override byte Portrait => 11;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _college;
    internal override bool CanTalkTo(PlayerMobile player) => BritainStory.MobileVisible(player, this);
    private static readonly ScriptedDialogue.Reply _identify = new("Identify Pack", 0, IdentifyPack);
    private static readonly ScriptedDialogue.Reply _recover = new("Recover Gear", 0, RecoverGear);
    private static readonly ScriptedDialogue.Reply[] _topics = [new("Runes", 1), _identify, _recover];
    private static readonly ScriptedDialogue.Node[] _college =
    [
        new("My first class knew every tune and none of the words. We shall get there. If thou hast something mysterious in thy pack, let me look it over. For thee, there is no charge.", _topics),
        new("There may be more travel runes out there. If thou findest one already marked, slip it into the book. An old road is worth keeping, even when thou hast no plans to take it.", _topics),
        new("Here is my runebook. I've put a rune to this college inside it. More marked runes may be out there for thee to find. And shouldst thou find something thou canst not identify, bring it here. An old friend pays nothing.", _topics),
        new("I have the runebook here. Make a little room in thy pack, and I shall give it to thee.", [_identify, _recover])
    ];
    private static readonly ScriptedDialogue.Node[] _solo =
    [
        new("I came on ahead to the college. They have given me a room full of eager voices and only one lute between them. I think we shall manage.",
            [_identify])
    ];

    internal override void BeginConversation(PlayerMobile player)
    {
        if (!BritainStory.Retired(player))
        {
            ScriptedDialogue.Show(player, this, _solo);
            return;
        }

        byte node = ClassCreation.Profiles.ReceivedIoloRunebook(player.Serial) ? (byte)0 : GiveRunebook(player) ? (byte)2 : (byte)3;
        ScriptedDialogue.Show(player, this, _college, node);
    }

    internal static bool GiveRunebook(PlayerMobile player)
    {
        var profiles = ClassCreation.Profiles;
        if (!BritainStory.Retired(player) || profiles.ReceivedIoloRunebook(player.Serial) || player.Backpack == null)
            return false;

        var book = new Runebook { Description = "Iolo's Runebook" };
        var entry = new RunebookEntry(book, BritainStory.CollegeRune, Map.Trammel, RuneName);
        book.Entries.Add(entry);
        book.Default = entry;
        if (!player.Backpack.TryDropItem(player, book, false))
        {
            book.Delete();
            return false;
        }
        profiles.RecordIoloRunebook(player.Serial);
        CoveGateRune.FileCarriedRune(player, book);
        return true;
    }

    private static void IdentifyPack(PlayerMobile player, Mobile speaker)
    {
        int count = 0;
        if (player.Backpack is { } pack)
            foreach (var item in pack.FindItems())
            {
                if (Identification.TargetGear(player, item) is not { } state)
                    continue;
                Identification.RevealGear(item, state);
                count++;
            }
        player.SendMessage(count == 0 ? "Iolo found nothing in thy pack that needed identifying."
            : $"Iolo identified {count} {(count == 1 ? "item" : "items")} free of charge.");
    }

    private static void RecoverGear(PlayerMobile player, Mobile speaker)
    {
        if (!BritainStory.Retired(player) || World.FindMobile(ClassCreation.Profiles.IoloCompanion(player.Serial)) is not Iolo iolo)
            return;
        foreach (var item in iolo.Items.ToArray())
            MercenaryCare.RecoverRetiredGear(player, iolo, item);
        if (iolo.Backpack is { } pack)
            foreach (var item in pack.Items.ToArray())
                MercenaryCare.RecoverRetiredGear(player, iolo, item);
        MercenaryCare.RecoverRetiredPotions(player, iolo);
        ScriptedDialogue.Show(player, speaker,
            [new("Thy pack has all it could hold. If there is aught left, I shall keep it here for thee.", _topics)]);
    }
}
