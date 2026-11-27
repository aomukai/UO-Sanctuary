// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal interface ICampaignRefugee;

internal static class CampaignActors
{
    internal static readonly Point3D BritainInn = new(1595, 1588, 20);
    internal static readonly Point3D HavenInn = new(3503, 2519, 27);
    private static readonly Point3D _britainDock = new(1483, 1754, -2);
    private static readonly Point3D _fireDock = new(4721, 3822, 0);
    private static readonly Point3D _daggerApproach = new(4110, 439, 0);
    private static Point3D _daggerDock;
    internal static readonly Point3D BlackthornPost = new(1324, 1623, 55);
    internal static readonly Point3D ShaminoPost = new(1324, 1626, 55);

    internal static void Install()
    {
        foreach (var messenger in World.Mobiles.Values.OfType<CampaignMessenger>().ToArray())
            messenger.Delete();
        foreach (var ferry in World.Items.Values.OfType<CampaignFerry>().Where(ferry => ferry.Map == Map.Trammel).ToArray())
            ferry.Delete();

        Place(_britainDock, () => new BritainHarbourMaster());
        Place(BlackthornPost, () => new CampaignBlackthorn(), relocate: true);
        Place(ShaminoPost, () => new CampaignShamino(), relocate: true);
        BlackthornKeepClosure.Install();
        Place(new(3503, 2518, 27), () => new CampaignRefugee(RefugeeKind.Jaana), RefugeeKind.Jaana);
        Place(new(3503, 2523, 27), () => new CampaignRefugee(RefugeeKind.Dupre), RefugeeKind.Dupre);
        Place(new(3529, 2529, 20), () => new CampaignRefugee(RefugeeKind.Wolfgang), RefugeeKind.Wolfgang);
        Place(new(3544, 2542, 20), () => new CampaignRefugee(RefugeeKind.Merydwen), RefugeeKind.Merydwen);
        Place(new(3485, 2529, 40), () => new CampaignRefugee(RefugeeKind.Nightwind), RefugeeKind.Nightwind);
        Place(new(3511, 2542, 20), () => new CampaignRefugee(RefugeeKind.Shamino), RefugeeKind.Shamino);

        _daggerDock = TownMarkets.Floor(Map.Trammel, _daggerApproach, [], false);
        PlaceSkipper(new(4720, 3822, 0), _fireDock, false);
        PlaceSkipper(new(_daggerDock.X - 1, _daggerDock.Y, _daggerDock.Z), _daggerDock, true);

        void Place<T>(Point3D point, Func<T> create, RefugeeKind? kind = null, bool relocate = false) where T : Mobile
        {
            var existing = World.Mobiles.Values.OfType<T>().FirstOrDefault(npc => !npc.Deleted && npc.Map == Map.Trammel
                && (!kind.HasValue || npc is CampaignRefugee refugee && refugee.Kind == kind));
            if (existing == null || relocate && existing.Location != point)
                (existing ?? create()).MoveToWorld(TownMarkets.Floor(Map.Trammel, point, [], false), Map.Trammel);
        }

        void PlaceSkipper(Point3D post, Point3D arrival, bool dagger)
        {
            var map = Map.Trammel;
            if (World.Mobiles.Values.OfType<IslandSkipper>().Any(skipper => skipper.Map == map && skipper.Dagger == dagger))
                return;

            var floor = TownMarkets.Floor(map, post, [(map, arrival)], false);
            new IslandSkipper(dagger).MoveToWorld(floor, map);
        }
    }

    internal static void ClearSummons(PlayerMobile player)
    {
        foreach (var messenger in World.Mobiles.Values.OfType<CampaignMessenger>()
                     .Where(npc => npc.Recipient == player.Serial).ToArray())
        {
            ScriptedDialogue.CloseForSpeaker(player, messenger.Serial);
            messenger.Delete();
        }
    }

    internal static void Intercept(PlayerMobile player, BritanniaCampaign.Summons summons)
    {
        if (summons == BritanniaCampaign.Summons.None || World.Mobiles.Values.OfType<CampaignMessenger>()
            .Any(npc => npc.Recipient == player.Serial))
            return;
        if (!PersistentMinions.FindPosition(player, out var position)
            || !player.InRange(position, PortraitProtocol.NpcInteractionRange) || !player.InLOS(position))
            return;
        var messenger = new CampaignMessenger(player);
        messenger.MoveToWorld(position, player.Map);
        messenger.BeginConversation(player);
    }

    internal static bool Geoffrey(PlayerMobile player, Mobile speaker)
    {
        var summons = BritanniaCampaign.NextSummons(player);
        if (BritanniaCampaign.Has(player, CampaignProgress.RoyalProclamation))
        {
            ScriptedDialogue.Show(player, speaker,
                [new("Listen to the streets, Avatar. I had almost forgotten how Britain sounds with her people home.", [],
                    new("Geoffrey", (ushort)NpcPortrait.GeoffreyRelieved))]);
            return true;
        }
        var (line, portrait) = summons switch
        {
            BritanniaCampaign.Summons.British => ("Avatar! At last. I've been looking for thee everywhere. Lord British has called for thee. He has urgent news and needs thy help. Come to the castle as soon as thou canst.", NpcPortrait.Geoffrey),
            BritanniaCampaign.Summons.Blackthorn => ("Avatar, a moment. I've worn myself to the bone trying to keep order. Lord British fell ill, and Lord Blackthorn asks for thee. Half the guard no longer reports. Some who do are drunk. I've caught men taking bribes, and there aren't enough honest hands to replace them. Thou wilt find Blackthorn and Shamino in the throne room at Castle Britannia.", NpcPortrait.GeoffreyDishevelled),
            BritanniaCampaign.Summons.Victory => ("Avatar! Everyone has heard what thou didst. I can hardly walk the bridge without someone wanting to shake my hand on thy behalf. Lord British asks for thee at the castle. I think he has something for thee.", NpcPortrait.GeoffreyRelieved),
            _ => (null, NpcPortrait.Geoffrey)
        };
        if (line == null && BritanniaCampaign.TownsEmpty(player)
            && BritanniaCampaign.Has(player, CampaignProgress.FireIslandPassage))
        {
            line = "The king is gravely ill. Blackthorn too, now. Most folk have gone to ground. There must be one last source of this evil. A dungeon, perhaps, as before. I wish I could tell thee more.";
            portrait = NpcPortrait.GeoffreyExhausted;
        }
        if (line == null)
            return false;
        var discovery = portrait switch
        {
            NpcPortrait.Geoffrey => StoryDiscoveries.CampaignSummons,
            NpcPortrait.GeoffreyDishevelled => StoryDiscoveries.BlackthornSummons,
            NpcPortrait.GeoffreyExhausted => StoryDiscoveries.EmptyTowns,
            _ => StoryDiscoveries.None
        };
        ScriptedDialogue.Show(player, speaker, [new(line, [], new("Geoffrey", (ushort)portrait), discovery)]);
        return true;
    }

    internal static bool British(PlayerMobile player, Mobile speaker)
    {
        if (BritanniaCampaign.Complete(player))
        {
            if (BritanniaCampaign.Has(player, CampaignProgress.RoyalProclamation))
            {
                ScriptedDialogue.Show(player, speaker, [new("The streets have filled again, Avatar. Britannia remembers what thou didst for her.", [])]);
                return true;
            }
            ScriptedDialogue.Show(player, speaker,
            [
                new("When I could scarcely rise from my bed, thou fought on for all of us. By my word and in the hearing of Britannia, I name thee Hero of the People. Thou hast earned use of our wardrobes and the right to make a home here.", [new("Thank thee", 0, Proclaim)])
            ]);
            return true;
        }
        if (BritanniaCampaign.NextSummons(player) != BritanniaCampaign.Summons.British)
            return false;
        ScriptedDialogue.Show(player, speaker,
        [
            new("Mariah and the mages of the Lycaeum have felt waves of magical energy rising from Hythloth. Fire Island trembles with each one. They fear the very ground beneath it will give way. I need thee to find what stirs below and put an end to it.",
                [new("Fire Island", 1)], Learns: StoryDiscoveries.FireIsland),
            new("I will give thee passage by ship. Speak to Elowen Marr at the Britain docks. She has my orders to take thee to the island whenever thou hast need.", [new("I’ll speak to Elowen", 0, GrantPassage)])
        ]);
        return true;
    }

    private static void GrantPassage(PlayerMobile player, Mobile speaker)
    {
        if (speaker is LordBritish && !BritanniaCampaign.Complete(player) && ScriptedDialogue.CanTalk(player, speaker)
            && BritanniaCampaign.Record(player, CampaignProgress.FireIslandPassage))
            ScriptedDialogue.Show(player, speaker, [new("Then go with my blessing, Avatar.", [])]);
    }

    private static void Proclaim(PlayerMobile player, Mobile speaker)
    {
        if (speaker is LordBritish && BritanniaCampaign.Complete(player) && ScriptedDialogue.CanTalk(player, speaker)
            && BritanniaCampaign.Record(player, CampaignProgress.RoyalProclamation))
        {
            TownRecognition.GrantCampaignHero(player);
            ScriptedDialogue.RecordDiscovery(player, StoryDiscoveries.RoyalProclamation);
            ScriptedDialogue.Show(player, speaker, [new("Go where thou wilt, Hero of the People. Thou art always welcome in Britain.", [])]);
        }
    }

    internal static bool InnCouncil(PlayerMobile player, Mobile speaker)
    {
        if (!BritanniaCampaign.TownsEmpty(player))
            return false;
        ScriptedDialogue.Show(player, speaker,
        [
            new("I keep thinking of the mages in Shame. Such power, held for so long beneath the earth. If something has stirred them, it could reach further than we think.",
                [new("Iolo", 1)], new("Jaana", PortraitProtocol.JaanaPortrait)),
            new("I would look in Wrong. A place may keep its doors barred long after the gaolers have fled. What if something is still inside?",
                [new("At the door", 2)], Iolo.Voice),
            new("The tavern door opens and Dupre steps in. Thy guesses are as good as mine, but consider Destard. Dragons and wyrms dwell there, and they suffer no lesser foe to live among them. What could make even those beasts afraid?",
                [new("Iolo", 3)], new("Dupre", (ushort)NpcPortrait.Dupre)),
            new("Destard? I don't know. I would still wager on Wrong.", [new("Jaana", 4)], Iolo.Voice),
            new("Dupre has a point. If evil has settled among such creatures, it had to be stronger than they are. I would begin in Destard.", [],
                new("Jaana", PortraitProtocol.JaanaPortrait), StoryDiscoveries.DestardLead)
        ]);
        return true;
    }

    internal static bool FerryUnlocked(PlayerMobile player, bool dagger = false) =>
        BritanniaCampaign.Has(player, dagger ? CampaignProgress.DaggerIslePassage : CampaignProgress.FireIslandPassage)
        || BritanniaCampaign.Complete(player);

    internal static void SailParty(PlayerMobile player, Point3D destination)
    {
        var fellowTravellers = BritanniaCampaign.NearbyParty(player, player.Location, 4).ToArray();
        if (!BritanniaCampaign.Sail(player, destination))
            return;
        foreach (var companion in fellowTravellers)
            BritanniaCampaign.Sail(companion, destination);
    }

    internal static void SailToFire(PlayerMobile player, Mobile speaker)
    {
        if (FerryUnlocked(player) && ScriptedDialogue.CanTalk(player, speaker))
            SailParty(player, _fireDock);
    }

    internal static void SailToDagger(PlayerMobile player, Mobile speaker)
    {
        if (FerryUnlocked(player, dagger: true) && ScriptedDialogue.CanTalk(player, speaker))
            SailParty(player, _daggerDock);
    }

    internal static void SailToBritain(PlayerMobile player, Mobile speaker)
    {
        if (speaker is IslandSkipper && ScriptedDialogue.CanTalk(player, speaker))
            SailParty(player, _britainDock);
    }
}

[SerializationGenerator(0)]
public partial class CampaignMessenger : ConversationNpc
{
    [SerializableField(0)] private Serial _recipient;

    internal CampaignMessenger(PlayerMobile player)
    {
        Recipient = player.Serial;
        Name = "Geoffrey";
        Title = "captain of the Britain city guard";
        Body = 0x190;
        Hue = 0x83EF;
        CantWalk = true;
        Blessed = true;
        AddItem(new PlateChest());
        AddItem(new Cloak(0x0591));
        AddItem(new Boots());
        Timer.DelayCall(TimeSpan.FromMinutes(2), Delete);
    }

    internal override ushort Portrait => (ushort)NpcPortrait.Geoffrey;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => [];
    internal override bool CanTalkTo(PlayerMobile player) => player.Serial == Recipient;
    internal override void BeginConversation(PlayerMobile player) => CampaignActors.Geoffrey(player, this);
}

[SerializationGenerator(0)]
public partial class BritainHarbourMaster : ConversationNpc
{
    [Constructible]
    public BritainHarbourMaster()
    {
        Name = "Elowen Marr";
        Title = "the Britain harbour master";
        Female = true;
        Body = 0x191;
        Hue = 0x83EA;
        HairItemID = 0x2046;
        HairHue = 0x0455;
        CantWalk = Blessed = true;
        AddItem(new FancyShirt(0x03E5));
        AddItem(new Surcoat(0x051D));
        AddItem(new Skirt(0x0455));
        AddItem(new Boots());
    }

    internal override ushort Portrait => (ushort)NpcPortrait.BritainHarbourMaster;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => [];

    internal override void BeginConversation(PlayerMobile player) => ScriptedDialogue.Show(player, this, DialogueFor(player));

    internal IReadOnlyList<ScriptedDialogue.Node> DialogueFor(PlayerMobile player)
    {
        var opening = new List<ScriptedDialogue.Reply> { new("About the harbour", 1) };
        if (CampaignActors.FerryUnlocked(player))
            opening.Add(new("Sail to Fire Island", 0, CampaignActors.SailToFire));
        if (CampaignActors.FerryUnlocked(player, dagger: true))
            opening.Add(new("Sail to Dagger Isle", 0, CampaignActors.SailToDagger));

        ScriptedDialogue.Reply[] topics = [new("Ships", 2), new("Trade", 3), new("Other harbours", 4), new("Back", 0)];
        return
        [
            new("Elowen Marr. If it takes on cargo, lets down a gangplank, or makes trouble at my docks, I hear about it. What can I do for thee?", opening.ToArray()),
            new("What wouldst thou know about Britain’s harbour?", topics, LineId: "britain.elowen.harbour", SpeakerId: "britain.elowen"),
            new("A stout keel matters more than a fine figurehead. Ships wear out from the bottom. That's where I look first.", topics),
            new("Britain takes in grain, timber and fish. We send out finished goods. Let one cargo run late and you'll hear from every shop on the quay.", topics),
            new("Vesper's built around its water. Jhelom's sailors know every little island to the south. If thou art bound for Moonglow, watch for the light from the Lycaeum.", topics)
        ];
    }
}

[SerializationGenerator(0)]
public partial class IslandSkipper : ConversationNpc
{
    [SerializableField(0)] private bool _dagger;

    internal IslandSkipper(bool dagger)
    {
        Dagger = dagger;
        Name = dagger ? "Halden Rook" : "Nessa Vale";
        Title = "the skipper";
        Female = !dagger;
        Body = Female ? 0x191 : 0x190;
        Hue = dagger ? 0x83EA : 0x8405;
        HairItemID = dagger ? 0x2044 : 0x203B;
        HairHue = dagger ? 0x0386 : 0x0455;
        FacialHairItemID = dagger ? 0x204B : 0;
        FacialHairHue = HairHue;
        CantWalk = Blessed = true;
        AddItem(new FancyShirt(dagger ? 0x03E9 : 0x051D));
        AddItem(new LongPants(0x0455));
        AddItem(new Boots());
        if (dagger)
            AddItem(new Cloak(0x0591));
        else
            AddItem(new Surcoat(0x0456));
    }

    internal override ushort Portrait => (ushort)(Dagger ? NpcPortrait.DaggerIsleSkipper : NpcPortrait.FireIslandSkipper);

    internal override IReadOnlyList<ScriptedDialogue.Node> Script =>
    [
        new(Dagger ? "I’m Halden Rook. The wind’s turning, but we can still make Britain before dark."
                : "Nessa Vale. I’ve a boat ready for Britain if thou hast finished on the island.",
            [new("Sail to Britain", 0, CampaignActors.SailToBritain)],
            LineId: Dagger ? "dagger-isle.halden.passage" : "fire-island.nessa.passage",
            SpeakerId: Dagger ? "dagger-isle.halden" : "fire-island.nessa")
    ];
}

[SerializationGenerator(0)]
public partial class CampaignBlackthorn : ConversationNpc
{
    [Constructible]
    public CampaignBlackthorn()
    {
        Name = "Lord Blackthorn";
        Body = 0x190;
        Hue = 0x83EF;
        HairItemID = 0x203B;
        FacialHairItemID = 0x2041;
        HairHue = FacialHairHue = 0x0455;
        CantWalk = Blessed = true;
        AddItem(new Robe(0x0455));
        AddItem(new Cloak(0x0493));
        AddItem(new Boots());
    }

    internal override ushort Portrait => (ushort)NpcPortrait.Blackthorn;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => [new("The king is resting. If thou hast business with the guard, find Geoffrey at the bridge.", [])];

    internal override void BeginConversation(PlayerMobile player)
    {
        if (BritanniaCampaign.Complete(player))
        {
            ScriptedDialogue.Show(player, this, [new("The king is well enough to hold court again. Have you been to see him?", [])]);
            return;
        }
        if (BritanniaCampaign.NextSummons(player) != BritanniaCampaign.Summons.Blackthorn &&
            !BritanniaCampaign.Has(player, CampaignProgress.BlackthornAudience))
        {
            base.BeginConversation(player);
            return;
        }
        BritanniaCampaign.Record(player, CampaignProgress.BlackthornAudience);
        ScriptedDialogue.Show(player, this,
        [
            new("The king cannot rise from his bed. I have requisitions from every town and scarcely a guard to send them. Where is it coming from? It must be one of the anti-virtue dungeons. Beyond that, I have nothing.", [new("Shamino", 1)]),
            new("I have no proof. But we found one evil nesting on Fire Isle. I would see what another island conceals before dismissing it.", [new("Which island?", 2)], CampaignShamino.Voice),
            new("Dagger Isle. Deceit lies there, and a boat from Britain can take thee across.", [new("Blackthorn", 3)], CampaignShamino.Voice),
            new("Islands and symmetry. Childish reasoning, Shamino. Still, if the Avatar wishes to follow that lead, I can provide passage. Elowen Marr will take thee to Dagger Isle.", [new("I’ll look in Deceit", 0, GrantPassage)], Learns: StoryDiscoveries.DaggerIsle)
        ]);
    }

    private static void GrantPassage(PlayerMobile player, Mobile speaker)
    {
        if (!BritanniaCampaign.Complete(player) && speaker is CampaignBlackthorn && ScriptedDialogue.CanTalk(player, speaker))
        {
            BritanniaCampaign.Record(player, CampaignProgress.DaggerIslePassage);
            ScriptedDialogue.Show(player, speaker, [new("I will send word to the docks. If Shamino is right, I shall be glad to hear it.", [])]);
        }
    }
}

[SerializationGenerator(0)]
public partial class CampaignShamino : ConversationNpc
{
    internal static readonly ScriptedDialogue.Voice Voice = new("Shamino", (ushort)NpcPortrait.Shamino);

    [Constructible]
    public CampaignShamino()
    {
        Name = "Shamino";
        Body = 0x190;
        Hue = 0x83EA;
        HairItemID = 0x203C;
        FacialHairItemID = 0x2041;
        HairHue = FacialHairHue = 0x0455;
        CantWalk = Blessed = true;
        AddItem(new Cloak(0x0591));
        AddItem(new Tunic(0x0455));
        AddItem(new Boots());
    }

    internal override ushort Portrait => (ushort)NpcPortrait.Shamino;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script =>
        [new("Blackthorn is taking every report himself these days. If the captain sent thee, speak with him first.", [])];
}

public enum RefugeeKind : byte { Jaana, Dupre, Wolfgang, Merydwen, Nightwind, Shamino }

[SerializationGenerator(0)]
public partial class CampaignRefugee : ConversationNpc, ICampaignRefugee
{
    [SerializableField(0)] private RefugeeKind _kind;

    internal CampaignRefugee(RefugeeKind kind)
    {
        Kind = kind;
        Name = kind.ToString();
        Female = kind is RefugeeKind.Jaana or RefugeeKind.Merydwen or RefugeeKind.Nightwind;
        Race = kind == RefugeeKind.Nightwind ? Race.Elf : Race.Human;
        Body = Race.AliveBody(Female);
        Hue = 0x83EA;
        HairItemID = Race == Race.Elf ? 0x2FCF : Female ? 0x2046 : 0x203B;
        HairHue = 0x0455;
        CantWalk = Blessed = true;
        AddItem(new Cloak(kind == RefugeeKind.Dupre ? 0x051D : 0x0591));
        AddItem(new Tunic(0x0455));
        AddItem(new Boots());
    }

    internal override ushort Portrait => Kind switch
    {
        RefugeeKind.Jaana => PortraitProtocol.JaanaPortrait,
        RefugeeKind.Dupre => (ushort)NpcPortrait.Dupre,
        RefugeeKind.Wolfgang => PortraitProtocol.WolfgangPortrait,
        RefugeeKind.Nightwind => (ushort)NpcPortrait.Nightwind,
        RefugeeKind.Shamino => (ushort)NpcPortrait.Shamino,
        _ => PortraitProtocol.MerydwenPortrait
    };

    internal override IReadOnlyList<ScriptedDialogue.Node> Script => Kind switch
    {
        RefugeeKind.Wolfgang => [new("I keep thinking about the men who stayed behind. Here, at least, I can keep a watch for those who got out.", [])],
        RefugeeKind.Merydwen => [new("I have walked through empty towns before. Never so many in one journey. I'm glad there's still a fire here.", [])],
        RefugeeKind.Shamino => [new("I’ve walked the roads between villages at dusk. I never heard them this quiet when folk were still about.", [])],
        _ => [new("From the shore I can almost see the mainland. It looks peaceful at this distance.", [])]
    };

    internal override bool CanTalkTo(PlayerMobile player) => BritanniaCampaign.TownsEmpty(player);
    internal override void BeginConversation(PlayerMobile player)
    {
        if (Kind is RefugeeKind.Jaana or RefugeeKind.Dupre)
            CampaignActors.InnCouncil(player, this);
        else
            base.BeginConversation(player);
    }
}

[SerializationGenerator(0)]
public partial class CampaignFerry : Teleporter
{
    [SerializableField(0)] private bool _dagger;

    internal CampaignFerry(Point3D destination, bool dagger) : base(destination, Map.Trammel)
    {
        Dagger = dagger;
        ItemID = 0x1E5E;
        Visible = true;
    }

    public override bool OnMoveOver(Mobile from) => true;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile { Alive: true } player && player.InRange(this, 2) && player.InLOS(this))
        {
            if (CampaignActors.FerryUnlocked(player, Dagger))
                CampaignActors.SailParty(player, PointDest);
            else
                player.SendMessage("The crew awaits a passenger named by the crown.");
        }
    }
}
