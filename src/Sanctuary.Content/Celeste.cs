// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class Celeste : ConversationNpc, IClassCompanion
{
    [SerializableField(0)]
    private bool _recruited;

    [SerializableField(1)]
    private bool _gearRecovered;

    [SerializableField(2)]
    private int _levelsTogether;

    [SerializableField(3, getter: "private", setter: "private")]
    private CampaignProgress _reportVictory;

    [SerializableField(4, getter: "private", setter: "private")]
    private DateTime _levelCapSinceUtc;

    private const int TrustLevels = 3, ReportFallbackLevels = TrustLevels + 5;
    private const int ChestnutHairHue = 0x0470;
    internal static readonly Point3D Cell = new(5515, 1816, 0);
    internal static readonly Point3D Chest = new(5507, 1805, 0);
    internal static readonly Point3D ChestApproach = new(5507, 1806, 0);
    private static readonly StoryDiscoveries _cellResolved = StoryDiscoveries.CelesteRescued | StoryDiscoveries.CelesteAbandoned;
    private static readonly ScriptedDialogue.Node[] _rescue =
    [
        new("I'm Celeste, a paladin. I came to investigate the rumours of an evil stirring in the depths of the anti-virtue dungeons. Is that why thou art here too?",
            [new("Art Thou All Right?", 1)]),
        new("Aye, I am well enough. The man in the next cell fared worse. They dragged him into the torture chamber next door. I can still hear his screams when I close my eyes.",
            [new("Didst Thou Find Anything?", 2)]),
        new("I went after what I took for an ordinary drake. Then it worked magic stronger than anything I was prepared for. I came to in this cell without my armour or weapons. They most likely took them to the torture chamber next door.",
            [new("Canst Thou Get Out Alone?", 0, (player, speaker) => ((Celeste)speaker).SendToInn(player)),
             new("Join My Investigation", 0, (player, speaker) => ((Celeste)speaker).Recruit(player))],
            Learns: StoryDiscoveries.CelesteDrake)
    ];
    private static readonly ScriptedDialogue.Node[] _unarmed =
    [
        new("They carried my kit into the torture chamber next door. I'll keep close until we find it.", [])
    ];
    private static readonly ScriptedDialogue.Node[] _armed =
    [
        new("My father's shield is back where it belongs. I'm ready when thou art.",
            [new("Equipment", 0, (player, speaker) =>
            {
                if (speaker.CanPaperdollBeOpenedBy(player))
                    speaker.DisplayPaperdollTo(player);
            })])
    ];
    private static readonly ScriptedDialogue.Node[] _ready =
    [
        new("My father's shield, and the rest of my kit. I'm ready now. Let's go.", [])
    ];

    DiabloClass IClassCompanion.CombatClass => DiabloClass.Paladin;
    CompanionEquipmentStyle IClassCompanion.EquipmentStyle => CompanionEquipmentStyle.MaceAndShield;
    public override bool IsInvulnerable => !GearRecovered;
    public override bool CanBeRenamedBy(Mobile from) => false;
    public override bool WandersWhileGuarding => false;
    public override bool CanRegenMana => false;
    public override int ManaMax => 0;
    internal override ushort Portrait => (ushort)(GearRecovered ? NpcPortrait.CelesteArmored : NpcPortrait.Celeste);
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => !Recruited ? _rescue : GearRecovered ? _armed : _unarmed;
    internal OrderType TravelOrder => GearRecovered ? OrderType.Guard : OrderType.Follow;

    [Constructible]
    public Celeste()
    {
        Name = "Celeste";
        Title = "the paladin";
        Female = true;
        Body = 0x191;
        Hue = 0x83EF;
        ApplyHair();
        CantWalk = true;
        Direction = Direction.West;
        AddItem(new FancyShirt(0x0591));
        AddItem(new LongPants(0x0455));
        AddItem(new Boots(0x0455));
    }

    [AfterDeserialization]
    private void ApplyHair()
    {
        if (HairItemID != Hair.Long)
            HairItemID = Hair.Long;
        if (HairHue != ChestnutHairHue)
            HairHue = ChestnutHairHue;
    }

    internal static void Install(Map map)
    {
        bool hasChest = false;
        foreach (var chest in map.GetItemsAt<MetalChest>(Chest))
            hasChest |= chest.Location == Chest;
        if (!hasChest)
            new MetalChest { Movable = false }.MoveToWorld(Chest, map);

        foreach (var prisoner in map.GetMobilesInRange<Celeste>(Cell, 0))
            if (!prisoner.Recruited && prisoner.Location == Cell)
                return;

        new Celeste().MoveToWorld(Cell, map);
    }

    internal bool VisibleTo(Mobile viewer) => Recruited || viewer.AccessLevel > AccessLevel.Player
        || viewer is PlayerMobile player && CanRescue(player);

    private static bool CanRescue(PlayerMobile player) => ClassCreation.Profiles.TryGet(player.Serial, out _)
        && (ClassCreation.Profiles.Discoveries(player.Serial) & _cellResolved) == 0
        && !MercenaryCare.HasContract<Celeste>(player);

    private bool OwnedBy(Mobile player) => player is PlayerMobile owner && Recruited && MercenaryCare.IsCompanion(owner, this);

    internal override bool CanTalkTo(PlayerMobile player) => Recruited ? OwnedBy(player) : CanRescue(player);

    internal void RecordLevelsTogether(int gained) =>
        LevelsTogether = Math.Min(ReportFallbackLevels, LevelsTogether + gained);

    internal void RecordLevelCap(int level)
    {
        if (level == 99 && LevelCapSinceUtc == default)
            LevelCapSinceUtc = DateTime.UtcNow;
    }

    internal void RecordCampaignVictory(CampaignProgress victory)
    {
        if (ReportVictory == 0)
            ReportVictory = victory;
    }

    internal override void BeginConversation(PlayerMobile player)
    {
        bool weekAtCap = LevelCapSinceUtc != default && DateTime.UtcNow - LevelCapSinceUtc >= TimeSpan.FromDays(7);
        if (!Recruited || !GearRecovered || !weekAtCap && LevelsTogether < TrustLevels)
        {
            base.BeginConversation(player);
            return;
        }

        string victory = ReportVictory switch
        {
            CampaignProgress.SkeletonKing => "King Leoric has fallen.",
            CampaignProgress.Lazarus => "Lazarus has fallen.",
            CampaignProgress.Diablo => "Diablo has fallen.",
            _ when (weekAtCap || LevelsTogether >= ReportFallbackLevels) && BritanniaCampaign.Complete(player) =>
                "I've had time to put what I saw in Covetous in order. Travelling with thee helped me see where it fits.",
            _ => null
        };
        bool canReport = victory != null;
        var journal = ClassCreation.Profiles.Discoveries(player.Serial);
        bool knowsHerPast = (journal & StoryDiscoveries.CelestePast) != 0;
        bool reportAgreed = (journal & StoryDiscoveries.CelesteTrinsicReport) != 0;
        var equipment = _armed[0].Replies[0];
        var past = new ScriptedDialogue.Reply("Thy Father's Shield", 1);
        var report = new ScriptedDialogue.Reply("Report to Trinsic", reportAgreed ? (byte)3 : (byte)2);
        ScriptedDialogue.Show(player, this,
        [
            _armed[0] with { Replies = canReport && knowsHerPast ? [equipment, past, report] : [equipment, past] },
            new("My father was captain of the paladins of Trinsic. If I earned a place on a patrol, people said he'd put me there. I wanted to show I could earn my place through patience and discipline. Then I followed those rumours to Covetous and took on more than I could handle. It took a while for me to admit that.",
                canReport ? [report, new("Back", 0)] : [new("Back", 0)], Learns: StoryDiscoveries.CelestePast),
            new($"{victory} Now I have something to report. Shall we go back to Trinsic and tell the paladins what we found?",
                [new("We'll Go to Trinsic", 3), new("Another Time", 0)]),
            new("Then we shall make for Trinsic. Thank thee. I'd like to give that report with thee beside me.",
                [], Learns: StoryDiscoveries.CelesteTrinsicReport)
        ]);
    }

    private static Celeste NewCompanion()
    {
        var companion = new Celeste
        {
            Recruited = true, CantWalk = false, AI = AIType.AI_Melee, ControlSlots = 0
        };
        companion.AddItem(new Backpack { Movable = false });
        companion.SetSpeed(0.12, companion.PassiveSpeed, isPassive: true);
        return companion;
    }

    private void SendToInn(PlayerMobile player)
    {
        if (!CanRescue(player))
            return;

        var companion = NewCompanion();
        companion.RecoverGear();
        if (!MercenaryCare.RegisterAtInn(player, companion, ClassStats.Level(player)))
        {
            companion.Delete();
            return;
        }

        ScriptedDialogue.Show(player, this,
        [
            new("Aye. I can slip out once the passage is clear. I'll get my things and make for Ayila's inn in Britain. Come find me there if thou still needest me.", [])
        ]);
        CompleteRescue(player);
        ScriptedDialogue.RecordDiscovery(player, StoryDiscoveries.CelesteWaiting);
    }

    private void Recruit(PlayerMobile player)
    {
        if (!CanRescue(player))
            return;

        var companion = NewCompanion();
        if (!companion.SetControlMaster(player))
        {
            companion.Delete();
            return;
        }

        var previous = MercenaryCare.ActiveStoryCompanion(player);
        if (!MercenaryCare.Register(player, companion, ClassStats.Level(player),
                replacing: previous))
        {
            companion.Delete();
            return;
        }

        var spot = PersistentMinions.FindPosition(player, out var nearby) ? nearby : player.Location;
        companion.MoveToWorld(spot, player.Map);
        companion.IssueOrder(OrderType.Follow, null, player);
        CompleteRescue(player);

        if (previous is IScriptedSpeaker speaker)
        {
            var voice = new ScriptedDialogue.Voice(speaker.DialogueName ?? previous.Name, speaker.Portrait);
            ScriptedDialogue.Show(player, companion,
            [
                new("I'll wait for thee at Ayila's inn in Britain.",
                    [new("Farewell for Now", 1)], voice),
                new("I will join thee. Help me fetch my kit from the torture chamber first, and I'll be of some use in a fight.", [])
            ]);
        }
        else
        {
            ScriptedDialogue.Show(player, companion,
                [new("I will join thee. Help me fetch my kit from the torture chamber first, and I'll be of some use in a fight.", [])]);
        }
    }

    private void CompleteRescue(PlayerMobile player)
    {
        ClassCreation.Profiles.Discover(player.Serial, StoryDiscoveries.CelesteRescued);
        ScriptedDialogue.SendStoryState(player);
        // Remove the shared prisoner from this client's view as soon as their route is chosen.
        player.NetState?.SendRemoveEntity(Serial);
    }

    private bool CanHandleGear(Mobile from) => GearRecovered && OwnedBy(from) && from.Alive && Alive && !IsDeadPet
        && from.Map == Map && Math.Abs(from.Z - Z) < 16 && from.InRange(this, PortraitProtocol.NpcInteractionRange)
        && from.CanSee(this) && from.InLOS(this);

    public override bool CanPaperdollBeOpenedBy(Mobile from) => CanHandleGear(from);
    public override bool AllowEquipFrom(Mobile from) => CanHandleGear(from);
    public override bool IsSnoop(Mobile from) => !CanHandleGear(from) && base.IsSnoop(from);
    public override bool CheckItemUse(Mobile from, Item item) => CanHandleGear(from);
    public override bool CheckNonlocalLift(Mobile from, Item item) => item != Backpack && CanHandleGear(from);
    public override bool CheckNonlocalDrop(Mobile from, Item item, Item target) => CanHandleGear(from);
    public override bool OnDragDrop(Mobile from, Item dropped) =>
        CanHandleGear(from) && (dropped is BasePotion
            ? Backpack?.TryDropItem(from, dropped, true) == true : base.OnDragDrop(from, dropped));

    public override bool EquipItem(Item item)
    {
        if (Recruited && (item is BaseWeapon and not Fists && item is not BaseBashing { Layer: Layer.OneHanded }
            || ClassGear.Definition(item) is { Weight: GearWeight.Heavy, Slot: GearSlot.Head or GearSlot.Body or GearSlot.Hands or GearSlot.Feet }))
        {
            ControlMaster?.SendMessage("Celeste: I use a one-handed mace and can wear up to medium armour.");
            return false;
        }
        return base.EquipItem(item);
    }

    public override int GetResistance(ResistanceType type) => CompanionBuild.Resistance(this, type, base.GetResistance(type));

    public override void OnAfterResurrect()
    {
        base.OnAfterResurrect();
        if (ControlMaster is PlayerMobile owner && OwnedBy(owner))
            IssueOrder(TravelOrder, null, owner);
    }

    private void RecoverGear()
    {
        if (GearRecovered)
            return;

        AddItem(new Mace());
        AddItem(new OrderShield { Name = "Celeste's father's order shield" });
        AddItem(new RingmailChest());
        AddItem(new RingmailArms());
        AddItem(new RingmailGloves());
        FindItemOnLayer(Layer.Pants)?.Delete();
        AddItem(new RingmailLegs());
        GearRecovered = true;
    }

    private static bool InChamber(Mobile mobile) => mobile.Map == Map.Trammel && mobile.Z is >= -2 and <= 4
        && mobile.X is >= 5499 and <= 5508 && mobile.Y is >= 1804 and <= 1814
        && mobile.InLOS(Chest);

    private DateTime _nextChestStep;
    private DateTime _soundDue;
    private int _chestCue;

    private void FollowOrRecover(PlayerMobile owner, DateTime now)
    {
        if (owner.NetState?.Running != true || !owner.Alive || owner.Map != Map || _chestCue == 0 && !InChamber(owner))
        {
            if (_chestCue != 0)
                owner.NetState?.SendSoundEffect(0x2C, Chest);
            _chestCue = 0;
            if (ControlOrder != OrderType.Follow || ControlTarget != owner)
                IssueOrder(OrderType.Follow, null, owner);
            return;
        }

        if (ControlOrder != OrderType.Stay)
        {
            IssueOrder(OrderType.Stay, null);
            Home = Point3D.Zero;
        }

        if (!InRange(Chest, 1) || Math.Abs(Z - Chest.Z) > 2 || !InLOS(Chest))
        {
            if (now >= _nextChestStep)
            {
                _nextChestStep = now.AddMilliseconds(180);
                AIObject?.MoveToPoint(ChestApproach, 0);
            }
            return;
        }

        if (now < _soundDue)
            return;

        Direction = GetDirectionTo(Chest);
        switch (_chestCue++)
        {
            case 0:
                owner.NetState.SendSoundEffect(0x2D, Chest);
                _soundDue = now.AddMilliseconds(480);
                break;
            case 1:
                owner.NetState.SendSoundEffect(0x57, Chest);
                _soundDue = now.AddMilliseconds(360);
                break;
            case 2:
                owner.NetState.SendSoundEffect(0x42, Chest);
                _soundDue = now.AddMilliseconds(440);
                break;
            default:
                owner.NetState.SendSoundEffect(0x2C, Chest);
                _chestCue = 0;
                RecoverGear();
                IssueOrder(OrderType.Guard, null, owner);
                ScriptedDialogue.Show(owner, this, _ready);
                break;
        }
    }

    public override void OnThink()
    {
        base.OnThink();
        if (ControlMaster is not PlayerMobile owner || !OwnedBy(owner) || !Alive || IsDeadPet)
            return;

        var now = TimedEffects.Now;
        if (!GearRecovered)
            FollowOrRecover(owner, now);
        else
            ThinkCombat(owner, now);
    }
}
