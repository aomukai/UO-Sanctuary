// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Net;
using ModernUO.Serialization;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class CoveGuard : ConversationNpc
{
    private static readonly ScriptedDialogue.Node[] _report =
    [
        new("Mind yourself outside the walls. The orcs have been testing our gates for weeks. We've held so far, but I'm running out of rested guards, and the messenger we sent to Vesper still hasn't come back.",
            [new("The Raids", 1), new("Vesper", 2), new("Bounty", 3)]),
        new("We drive them off and they come back a few nights later. They can wait. My people cannot. Every guard on this gate has worked more watches than I care to count.",
            [new("Vesper", 2), new("Bounty", 3)]),
        new("Mayor Jaana sent a messenger to Vesper for help. A month now, and no word from either. I'd send someone after them if I had a guard to spare.",
            [new("The Raids", 1), new("Bounty", 3)]),
        new("Cove pays fifty gold per ear from the orcs in these woods or their fort. Take them to Mayor Jaana. She handles the payments herself.", [])
    ];

    [Constructible]
    public CoveGuard(bool thomas)
    {
        Name = thomas ? "Thomas" : "Rowan";
        Title = "the Cove guard";
        Body = 0x190;
        Hue = 0x83EA;
        HairItemID = 0x203B;
        HairHue = 0x045C;
        FacialHairItemID = 0x203F;
        FacialHairHue = HairHue;
        CantWalk = true;
        Blessed = true;
        Direction = Direction.East;
        AddItem(new RingmailChest());
        AddItem(new RingmailLegs());
        AddItem(new Cloak(0x0591));
        AddItem(new Halberd());
        AddItem(new Boots());
    }

    internal override ushort Portrait => PortraitProtocol.ThomasPortrait;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _report;
    internal override bool CanTalkTo(PlayerMobile player) => Name == "Thomas";

    internal override void BeginConversation(PlayerMobile player)
    {
        ScriptedDialogue.RecordDiscovery(player, StoryDiscoveries.CoveSiege);
        base.BeginConversation(player);
    }
}

[SerializationGenerator(0)]
public partial class MayorJaana : ConversationNpc
{
    [Constructible]
    public MayorJaana()
    {
        Name = "Jaana";
        Title = "the mayor of Cove";
        Female = true;
        Body = 0x191;
        Hue = 0x83EF;
        HairItemID = 0x2049;
        HairHue = 0x0459;
        CantWalk = true;
        Blessed = true;
        Direction = Direction.South;
        AddItem(new Robe(0x0591));
        AddItem(new Cloak(0x058F));
        AddItem(new Sandals(0x045C));
    }

    private static readonly ScriptedDialogue.Reply _bounty = new("Orc Bounty", 0, (player, speaker) => ((MayorJaana)speaker).ShowExchange(player));
    private static readonly ScriptedDialogue.Reply _payment = new("Exchange for Gold", 0, (player, speaker) => ((MayorJaana)speaker).PayBounty(player));
    private static readonly ScriptedDialogue.Reply _back = new("Back", 0, (player, speaker) => ((MayorJaana)speaker).BeginConversation(player));
    private static readonly ScriptedDialogue.Reply _letter = new("The Messenger's Letter", 0, (player, speaker) => ((MayorJaana)speaker).ReadLetter(player));
    private static readonly ScriptedDialogue.Reply _ferry = new("The Ferry", 2);
    private static readonly ScriptedDialogue.Reply _orcs = new("The Orcs", 1);
    internal override ushort Portrait => PortraitProtocol.JaanaPortrait;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => [];

    internal override void BeginConversation(PlayerMobile player)
    {
        var flags = ClassCreation.Profiles.Discoveries(player.Serial);
        ScriptedDialogue.RecordDiscovery(player, StoryDiscoveries.CoveJaana);
        bool firstMeeting = (flags & StoryDiscoveries.CoveJaana) == 0;
        if (IsDruid(player))
        {
            Show(player, (firstMeeting
                ? "Avatar! Come, sit with me a while. It does my heart good to see another druid in Cove. I have spent so much time listening for the orcs that I almost missed what the woods were telling me."
                : "Welcome back, my friend. I'm glad thou hast come. I have been out among the trees again, trying to make sense of what I feel there.")
                + "\n\nI cannot put my finger on it, but something feels wrong of late. Even on still days, the woods have the hush that comes before the first frost, as if all of nature were bracing for winter. I cannot tell what it is bracing against.");
            return;
        }
        Show(player, firstMeeting
            ? "Avatar. Oh, it is good to see thee. Come in. I had begun to wonder whom I might ask for help. The orcs have worn the guard thin, and I would be grateful for anything thou canst do to give them some rest."
            : "Welcome back, my friend. How hast thou fared on the road?");
    }

    private static bool IsDruid(PlayerMobile player) =>
        ClassCreation.Profiles.TryGet(player.Serial, out var profile) && profile.Class == DiabloClass.Druid;

    private void Show(PlayerMobile player, string line)
    {
        var replies = new List<ScriptedDialogue.Reply> { _orcs, _bounty };
        if (CoveFerry.Unlocked(player))
            replies.Add(_ferry);
        else if (FindLetter(player) != null)
            replies.Add(_letter);
        if (IsDruid(player))
            replies.Add(new("Signs in the Wild", 0, (viewer, speaker) => ((MayorJaana)speaker).DiscussSigns(viewer)));
        ScriptedDialogue.Show(player, this,
        [
            new(line, [.. replies]),
            new("They raid often enough that the guards hardly sleep. The town will pay fifty gold for each orc ear from this tribe. I had hoped Vesper would send help, but my messenger has yet to return.", [.. replies]),
            new("The ferryman waits at the north end of the docks. Tell him I sent thee. He will take thee to Britain or Vesper whenever thou hast need.", [.. replies])
        ]);
    }

    private void DiscussSigns(PlayerMobile player)
    {
        if (!IsDruid(player) || !ScriptedDialogue.CanTalk(player, this))
            return;

        var profiles = ClassCreation.Profiles;
        var flags = profiles.Discoveries(player.Serial);
        var replies = new List<ScriptedDialogue.Reply>();
        if ((flags & (StoryDiscoveries.RoyalInvestigation | StoryDiscoveries.RoyalAudience)) != 0)
            replies.Add(new("Lord British's Quest", 0, (viewer, speaker) => ((MayorJaana)speaker).DiscussCovetous(viewer,
                "So Lord British has asked thee to learn what lies behind the trouble. I'm glad he is listening. By the time word reaches the throne, folk out here have usually been living with it for weeks.")));
        if ((flags & StoryDiscoveries.IoloDungeonReports) != 0)
            replies.Add(new("Iolo's Reports", 0, (viewer, speaker) => ((MayorJaana)speaker).DiscussCovetous(viewer,
                "All eight dungeons? I trust Iolo to know the difference between a rumour and a warning. If the trouble has stirred in so many places at once, I wonder what else he and I have been overlooking.")));
        if (profiles.DefeatedKrulg(player.Serial))
            replies.Add(new("The Ogre in the Mine", 0, (viewer, speaker) => ((MayorJaana)speaker).DiscussCovetous(viewer,
                "I am glad thou brought down that ogre. Powers like those in the New Haven mine give me more cause for worry than its size or its strength ever could.")));
        replies.Add(new("The Nearest Dungeon", 0, (viewer, speaker) => ((MayorJaana)speaker).DiscussCovetous(viewer, null)));
        ScriptedDialogue.Show(player, this,
            [new("Thou hast been farther afield than I. Tell me what thou hast learned, and perhaps we can make better sense of what the woods are telling us.", [.. replies])]);
    }

    private void DiscussCovetous(PlayerMobile player, string reply)
    {
        if (!IsDruid(player) || !ScriptedDialogue.CanTalk(player, this))
            return;

        const string advice = "Covetous is the nearest dungeon to Cove. It was dangerous long before any of this began, and it sprawls. Go prepared if thou meanest to explore it. A short way down can turn into a long way back.";
        Show(player, (reply == null ? advice : reply + "\n\n" + advice) + CovetousGift(player));
    }

    private static string CovetousGift(PlayerMobile player)
    {
        var profiles = ClassCreation.Profiles;
        var flags = profiles.Discoveries(player.Serial);
        const StoryDiscoveries helpedCove = StoryDiscoveries.CoveChief | StoryDiscoveries.CoveFerry;
        if ((flags & helpedCove) != helpedCove || (flags & StoryDiscoveries.JaanaBeltReceived) != 0)
            return "";

        const string fullPack = "\n\nI have a belt for thee. Make some room in thy pack and ask me about Covetous again before thou goest.";
        if (player.Backpack is not { } pack)
            return fullPack;

        var random = Random.Shared;
        PotionTabard belt = random.Next(2) == 0 ? new PotionSash() : new PotionLightBelt();
        ClassGear.Profiles.Convert(belt, ClassStats.Level(player), GearRarity.Magic, random);
        if (!pack.TryDropItem(player, belt, false))
        {
            belt.Delete();
            return fullPack;
        }

        profiles.Discover(player.Serial, StoryDiscoveries.JaanaBeltReceived);
        return "\n\nCove owes thee, my friend. Take this belt. It holds eight potions, and down there thou wilt need them.";
    }

    private void ShowExchange(PlayerMobile player, string line = null)
    {
        var shop = RegionalGambling.Cove;
        line ??= shop.Unlocked(player)
            ? "Cove still pays fifty gold per ear. I can also offer thee some of the gear we've set aside for those who defend the town."
            : $"Cove pays fifty gold per ear. {RegionalGambling.LockedMessage(player, shop)}";
        ScriptedDialogue.Show(player, this, [new(line, [_payment, RegionalGambling.ExchangeItems, _back])]);
    }

    private void PayBounty(PlayerMobile player)
    {
        if (!ScriptedDialogue.CanTalk(player, this) || player.Backpack is not { } pack)
            return;

        int count = pack.GetAmount(typeof(OrcEar));
        if (count == 0 || !pack.ConsumeTotal(typeof(OrcEar), count))
        {
            ShowExchange(player, "I see no ears from the raiders in thy pack. Come back when thou hast some. Every one makes the guard's next watch a little easier.");
            return;
        }

        long reward = (long)RegionalGambling.CoveGoldPerEar * count;
        for (long left = reward; left > 0; left -= 60_000)
            pack.DropItem(new Gold((int)Math.Min(left, 60_000)));

        bool wasUnlocked = RegionalGambling.Cove.Unlocked(player);
        long total = Gambling.Profiles.RecordCoveTurnIn(player, count);
        string thanks = !wasUnlocked && total >= RegionalGambling.UnlockCount
            ? "You've done our town a great service. I can offer thee gear in exchange from now on."
            : "It will do the guards good to know they have someone to share the work.";
        string ears = total == 1 ? "ear" : "ears";
        ShowExchange(player, $"The town owes thee {reward:N0} gold. Thou hast brought {total:N0} {ears} in all. {thanks}");
    }

    private static CoveDispatch FindLetter(PlayerMobile player)
    {
        if (player.Backpack is { } pack)
            foreach (var letter in pack.FindItemsByType<CoveDispatch>())
                if (letter.BelongsTo(player))
                    return letter;
        return null;
    }

    private void ReadLetter(PlayerMobile player)
    {
        if (!ScriptedDialogue.CanTalk(player, this) || CoveFerry.Unlocked(player) || FindLetter(player) is not { } letter)
            return;

        letter.Delete();
        ScriptedDialogue.RecordDiscovery(player, StoryDiscoveries.CoveFerry);
        Show(player, "I kept hoping they had simply been delayed. Thank thee for finding them, my friend. I cannot bring them home now, but at least I know what became of them. Tell the ferryman I sent thee. Cove will cover thy passage to Britain or Vesper whenever thou needest it.");
    }
}

[SerializationGenerator(0)]
public partial class CoveFerry : ConversationNpc
{
    internal static readonly Point3D Dock = new(2256, 1169, -2);
    internal static readonly Point3D Britain = new(1485, 1755, -2);
    internal static readonly Point3D Vesper = new(3029, 828, -2);

    private static readonly ScriptedDialogue.Node[] _waiting =
        [new("Jaana's got the ferry on town business just now. You'll need her word before I can take you across.", [])];
    private static readonly ScriptedDialogue.Node[] _destinations =
    [
        new("Jaana said you'd come by. Boat should be back any moment, with any luck. Britain or Vesper today?",
            [new("Britain", 0, (player, speaker) => ((CoveFerry)speaker).Depart(player, Britain)),
             new("Vesper", 0, (player, speaker) => ((CoveFerry)speaker).Depart(player, Vesper))])
    ];

    [Constructible]
    public CoveFerry()
    {
        Name = "Alden";
        Title = "the ferryman";
        Body = 0x190;
        Hue = 0x8402;
        HairItemID = 0x2048;
        HairHue = 0x0457;
        FacialHairItemID = 0x2041;
        FacialHairHue = HairHue;
        CantWalk = true;
        Blessed = true;
        Direction = Direction.South;
        AddItem(new FancyShirt(0x03E5));
        AddItem(new Tunic(0x045B));
        AddItem(new LongPants(0x0455));
        AddItem(new Boots());
    }

    internal override ushort Portrait => PortraitProtocol.CoveFerrymanPortrait;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _waiting;
    internal override void BeginConversation(PlayerMobile player) =>
        ScriptedDialogue.Show(player, this, Unlocked(player) ? _destinations : _waiting);

    internal static bool Unlocked(PlayerMobile player) =>
        (ClassCreation.Profiles.Discoveries(player.Serial) & StoryDiscoveries.CoveFerry) != 0;

    private void Depart(PlayerMobile player, Point3D destination)
    {
        if (!Unlocked(player) || !ScriptedDialogue.CanTalk(player, this))
            return;

        var map = Map.Trammel;
        int dockHeight = destination.Z;
        if (!map.CanSpawnMobile(destination) && !PersistentMinions.FindPosition(map, destination,
            point => point.Z == dockHeight, out destination))
        {
            ScriptedDialogue.Show(player, this, [new("The landing is crowded. Give it a minute and ask me again.", [])]);
            return;
        }

        ClassCombat.Stop(player);
        BaseCreature.TeleportPets(player, destination, map);
        player.MoveToWorld(destination, map);
    }
}

[SerializationGenerator(0)]
public partial class CoveCrier : ConversationNpc
{
    private static readonly ScriptedDialogue.Reply[] _topics =
        [new("Graveyard", 1), new("Orcs", 2), new("Cove", 3)];

    private static readonly ScriptedDialogue.Node[] _news =
    [
        new("Perrin, town crier. I used to cry the price of fish in this square. These days folk ask me whether the roads are safe. What wouldst thou hear?", _topics),
        new("The old graveyard lies northeast of town. I used to pass it on my rounds. Then I saw footprints in the mud leading away from the graves, with none leading in. Folk say the ground's cursed. I take the long way round now.", _topics),
        new("The orcs send a few to watch the road, then come back in numbers. Ask Thomas what his guards have seen. I can tell thee what I hear when the alarm goes up. Doors bolt, shutters close, and folk count who's still outside.", _topics),
        new("Cove's small enough that I know who's behind every door. Hardened folk, fierce as any I've met. At the first bell, fishers drop their nets and take up spears. Every raid costs us another night's sleep. I wonder how many we have left to spare.", _topics)
    ];

    [Constructible]
    public CoveCrier()
    {
        Name = "Perrin";
        Title = "the town crier";
        Body = 0x190;
        Hue = 0x83EA;
        SetHairIfMissing();
        CantWalk = true;
        Blessed = true;
        AddItem(new FancyShirt(0x03E5));
        AddItem(new Cloak(0x0591));
        AddItem(new LongPants(0x0455));
        AddItem(new Shoes());
    }

    [AfterDeserialization]
    private void SetHairIfMissing()
    {
        if (HairItemID == 0)
        {
            HairItemID = 0x2048;
            HairHue = 0x0454;
        }
    }

    internal override ushort Portrait => PortraitProtocol.PerrinPortrait;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _news;
}

[SerializationGenerator(0)]
public partial class CoveDispatch : BaseBook
{
    internal const string Heading = "A Letter from Cove";
    internal const string Letter = "Mayor of Vesper,\n\nThe orcs have made three attempts on our gate this month. Each time we drive them off, they return before my guards have slept. If Vesper can spare a few guards, or even supplies for the watch, I ask thee to send them with this messenger. I would owe thee a greater debt than the town can readily repay.\n\nJaana, Mayor of Cove";

    private static readonly BookContent _letter = new(Heading, "Jaana",
        new BookPageInfo("Mayor of Vesper,", "", "The orcs have made three", "attempts on our gate this",
            "month. We drive them off,", "and they return before", "my guards have slept."),
        new BookPageInfo("If Vesper can spare a few", "guards or supplies, send", "them with this messenger.", "",
            "Jaana, Mayor of Cove"));

    [SerializableField(0)] private Serial _recipient;

    [Constructible]
    public CoveDispatch() : base(0xFF1, writable: false) => Name = "Jaana's letter to Vesper";

    internal CoveDispatch(PlayerMobile recipient) : this()
    {
        _recipient = recipient.Serial;
        Movable = false;
        LootType = LootType.Blessed;
    }
    internal bool BelongsTo(PlayerMobile recipient) => _recipient == recipient.Serial;
    public override BookContent DefaultContent => _letter;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile { NetState: { Running: true } } player && IsChildOf(player.Backpack))
        {
            const string id = "cove.jaana.dispatch_copy";
            string text = AuthoredWriting.BookBody(this);
            if (!AuthoredWriting.Show(player, id, AuthoredTextKind.Letter, Title, Author, text, id + ".body"))
                player.SendGump(new CoveMessenger.LetterGump(player, title: Title, text: text));
        }
        else
        {
            base.OnDoubleClick(from);
        }
    }
}

[SerializationGenerator(0)]
public partial class CoveMessenger : Item
{
    [Constructible]
    public CoveMessenger() : base(CoveClue.CorpseGraphic)
    {
        Name = "a fallen messenger";
        Movable = false;
    }

    private bool CanRead(PlayerMobile player) =>
        player is { Alive: true, Backpack: not null, NetState: { Running: true } } && player.Map == Map
        && player.InRange(Location, 2) && Math.Abs(player.Z - Z) < 16 && player.CanSee(this) && player.InLOS(this)
        && ClassCreation.Profiles.TryGet(player.Serial, out _)
        && (ClassCreation.Profiles.Discoveries(player.Serial) & StoryDiscoveries.CoveMessenger) == 0;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player && CanRead(player))
            player.SendGump(new LetterGump(player, this));
    }

    private void TakeLetter(PlayerMobile player)
    {
        if (Deleted || !CanRead(player))
            return;
        var dispatch = new CoveDispatch(player);
        if (!player.Backpack.TryDropItem(player, dispatch, true))
        {
            dispatch.Delete();
            return;
        }
        ScriptedDialogue.RecordDiscovery(player, StoryDiscoveries.CoveMessenger);
    }

    internal sealed class LetterGump(PlayerMobile player, CoveMessenger body = null,
        string title = CoveDispatch.Heading, string text = CoveDispatch.Letter) : DynamicGump(64, 48)
    {
        public override bool Singleton => true;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 430, 335, 2600);
            builder.AddHtml(25, 20, 380, 30, $"<center>{WebUtility.HtmlEncode(title)}</center>");
            builder.AddHtml(32, 60, 366, 210, WebUtility.HtmlEncode(text).Replace("\n", "<br>"), background: true, scrollbar: true);
            if (body != null)
            {
                builder.AddButton(35, 285, 4005, 4007, 1);
                builder.AddHtml(70, 288, 150, 25, "Take Letter");
            }
            builder.AddButton(240, 285, 4017, 4019, 0);
            builder.AddHtml(275, 288, 120, 25, body != null ? "Leave" : "Close");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile == player && info.ButtonID == 1)
                body?.TakeLetter(player);
        }
    }
}
