// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class Iolo : ConversationNpc, IClassCompanion
{
    internal const int RequiredLevel = 6;
    internal static readonly ScriptedDialogue.Voice Voice = new("Iolo", (ushort)NpcPortrait.Iolo);
    private const byte JoinedNode = 3;
    [SerializableField(0)]
    private Serial _recruitedBy;

    internal bool Recruited => _recruitedBy.IsMobile;
    internal const DiabloClass CombatClass = DiabloClass.Amazon;
    public override bool WandersWhileGuarding => false;
    DiabloClass IClassCompanion.CombatClass => CombatClass;
    CompanionEquipmentStyle IClassCompanion.EquipmentStyle => CompanionEquipmentStyle.Crossbow;
    public override bool IsInvulnerable => !Recruited;
    public override bool CanRegenMana => false;
    internal override ushort Portrait => Voice.Portrait;
    internal override IReadOnlyList<ScriptedDialogue.Node> Script => _introduction;

    private long _nextSpecialShot = long.MinValue;
    private TimerExecutionToken _shot;

    private const string Summons = "Aye, 'twas I who summoned thee. I would scarce have called thee from thy own world had I thought Britannia could weather this alone.";
    internal const string DungeonReports = "Reports speak of danger coming from the eight dungeons tied to the Anti-Virtues. They agree on little save that the trouble grows.";
    private const string Danger = "There are signs of trouble in the dark places of Britannia. " + DungeonReports
        + " I would have thee find thy footing before we go looking for its source.";
    private const string Graveyard = "There is an old graveyard northeast of town. The dead there have grown restless. Test thy mettle there if thou wilt, while I finish my business in New Haven. Come find me here at the inn when thou art ready.";
    private const string Ready = "Thou art finding thy footing again, I see. My business here is concluded—and this old crossbow has gathered quite enough dust.";

    private static readonly ScriptedDialogue.Reply[] _topics =
        [new("Summons", 1), new("Danger", 2, Learns: StoryDiscoveries.IoloDungeonReports), new("Graveyard", 3)];
    private static readonly ScriptedDialogue.Reply _join = new("Travel Together", 0, AcceptCompany);
    private static readonly ScriptedDialogue.Reply[] _invitation = [_join, new("Travel Alone", 1), new("Summons", 2), new("Graveyard", 4)];
    private static readonly ScriptedDialogue.Reply[] _readyTopics =
        [new("Summons", 2), new("Danger", 3, Learns: StoryDiscoveries.IoloDungeonReports), new("Graveyard", 4), _join];

    private static readonly ScriptedDialogue.Node[] _introduction =
    [
        new("Avatar! 'Twas I who summoned thee. There are signs of danger in Britannia, but thou lookest a little rusty. Get thy bearings while I finish my business. There is an old graveyard northeast of town where thou canst test thy mettle. Come back to the inn when thou feelest ready.", _topics),
        new(Summons, _topics), new(Danger, _topics), new(Graveyard, _topics),
        new("Keep at it, friend. I have business yet to finish here. Come and see me at the inn when thou hast found thy footing again.", _topics),
        new("We are already companions, my friend. Speak to me on the road if thou wouldst look over my kit.", [])
    ];

    private static readonly ScriptedDialogue.Node[] _offer =
    [
        new(Ready + " Wilt thou have my company on the road?", _invitation),
        new("If thou changest thy mind, I shall be here at the inn until thou leavest. Shouldst thou depart without me, seek me next at the bard’s college in Britain. My travelling days are drawing to a close.", _readyTopics),
        new(Summons, _readyTopics), new(Danger, _readyTopics), new(Graveyard, _readyTopics),
        new("Avatar! Aye, 'twas I who summoned thee. Dark signs threaten Britannia, and I would have asked thee to get thy bearings at the graveyard northeast of town while I finished my business. " + Ready + " Wilt thou have my company on the road?", _invitation),
        new("Thou already hast company on the road. Come back alone when there is room for an old bard.", _readyTopics)
    ];

    private static readonly ScriptedDialogue.Node[] _company = CompanyScript(
        "The reports come from all across Britannia, and they grow worse by the day. I have yet to learn what ties them together.");
    private static readonly ScriptedDialogue.Node[] _companyMine = CompanyScript(
        "Look at what befell the mine. Those miners could work in peace until beasts drove them out. I have heard much the same from other parts of Britannia. I begin to wonder how many of these troubles share a cause.",
        "I have faced ogres before, Avatar. I know their strength well enough, but that creature had powers I have never seen in one of its kind.");
    private static readonly ScriptedDialogue.Node[] _companyMineReported = CompanyScript(
        "Thy account of the mine put me in mind of reports from elsewhere in Britannia. Folk are being driven from places they have long worked in peace. I begin to wonder how many of these troubles share a cause.",
        "I have faced ogres before, Avatar, but in all my years I have never seen one wield the likes of the powers thou hast described.");

    private static ScriptedDialogue.Node[] CompanyScript(string danger, string ogre = null)
    {
        var reports = new ScriptedDialogue.Reply("Danger", 2, Learns: StoryDiscoveries.IoloDungeonReports);
        ScriptedDialogue.Reply[] topics = ogre == null
            ? [new("Summons", 1), reports, new("Equipment", 0, OpenEquipment)]
            : [new("Summons", 1), reports, new("Equipment", 0, OpenEquipment), new("Ogre", 4)];
        ScriptedDialogue.Node[] nodes =
        [
            new("What is it, my friend? I've time for a word before we go on.", topics),
            new(Summons, topics), new(danger + " " + DungeonReports, topics),
            new("Come, let us go together.", topics)
        ];
        return ogre == null ? nodes : [..nodes, new(ogre, topics)];
    }

    internal IReadOnlyList<ScriptedDialogue.Node> CompanyFor(PlayerMobile player)
    {
        if (!ClassCreation.Profiles.HasInvestigatedHavenMine(player.Serial, out var witness))
            return _company;

        return witness == Serial ? _companyMine : _companyMineReported;
    }

    private static readonly ScriptedDialogue.Reply[] _departureTopics =
        [new("Lord British", 1), new("Songs", 2), new("Equipment", 0, OpenEquipment)];
    private static readonly ScriptedDialogue.Node[] _departure =
    [
        new("We should seek an audience with Lord British. He must hear what we have found.\n\n"
            + "But first, grant an old man a moment to catch his breath. Once, I could follow thee from dawn until dusk…\n\n"
            + "Of late, I find myself thinking of the bard’s college. There are songs I have yet to teach, and young voices enough to carry them.", _departureTopics),
        new("Thou wilt find him at the castle in Britain. Come, I'll go with thee. He should hear of this from us both.", _departureTopics),
        new("A song is best passed from one voice to another. I should like to teach a few to the young bards at the college, while I can still remember all the verses.", _departureTopics)
    ];

    internal const int VoiceHue = 0x0035;

    [Constructible]
    public Iolo()
    {
        Name = "Iolo";
        Title = "the bard";
        Body = 0x190;
        Hue = 0x83EF;
        HairItemID = 0x203C;
        HairHue = 0x0481;
        FacialHairItemID = 0x2040;
        FacialHairHue = 0x0481;
        CantWalk = true;
        Direction = Direction.East;
        Mana = 0;
        AddItem(new FancyShirt(0x03E5));
        AddItem(new LongPants(0x0455));
        AddItem(new Cloak(0x0591));
        AddItem(new Boots(0x0590));
        AddItem(new Crossbow());
    }

    internal override bool CanTalkTo(PlayerMobile player) =>
        Recruited ? IsOwnedBy(player) : !ClassCreation.Profiles.HasDepartedNewHaven(player.Serial);

    internal override void BeginConversation(PlayerMobile player)
    {
        if (!Recruited && CampaignActors.InnCouncil(player, this))
            return;
        if (Recruited)
        {
            if (IsDeadPet)
                player.SendMessage("Iolo's spirit follows thee still. A healer can call him back.");
            else
                ScriptedDialogue.Show(player, this, CompanyFor(player));
            return;
        }

        var profiles = ClassCreation.Profiles;
        bool first = profiles.MeetIolo(player.Serial);
        if (first)
        {
            player.SendMessage("A new entry has been added to thy journal.");
            ScriptedDialogue.SendStoryState(player);
        }

        var recruited = profiles.IoloCompanion(player.Serial);
        if (recruited.IsMobile)
        {
            if (World.FindMobile(recruited) is Iolo active && ScriptedDialogue.CanTalk(player, active))
                active.BeginConversation(player);
            else
                ScriptedDialogue.Show(player, this, _introduction, 5);
        }
        else if (!profiles.HasReached(player.Serial, RequiredLevel))
            ScriptedDialogue.Show(player, this, _introduction, first ? (byte)0 : (byte)4);
        else
        {
            if (profiles.UnlockNewHavenGate(player.Serial))
                player.SendMessage("The New Haven moongate is now open to thee.");
            ScriptedDialogue.Show(player, this, _offer, first ? (byte)5 : (byte)0);
        }
    }

    private static void OpenEquipment(PlayerMobile player, Mobile speaker)
    {
        if (speaker.CanPaperdollBeOpenedBy(player))
            speaker.DisplayPaperdollTo(player);
    }

    private static void AcceptCompany(PlayerMobile player, Mobile speaker)
    {
        var profiles = ClassCreation.Profiles;
        if (speaker is not Iolo { Recruited: false } innIolo || !profiles.NewHavenGateUnlocked(player.Serial)
            || profiles.HasDepartedNewHaven(player.Serial) || profiles.IoloCompanion(player.Serial).IsMobile)
            return;

        if (Recruit(player) is { } companion)
        {
            player.NetState?.SendRemoveEntity(innIolo.Serial);
            ScriptedDialogue.Show(player, companion, companion.CompanyFor(player), JoinedNode);
        }
        else
            ScriptedDialogue.Show(player, innIolo, _offer, 6);
    }

    private static Iolo Recruit(PlayerMobile player)
    {
        if (MercenaryCare.HasActiveCompanion(player))
            return null;

        var companion = new Iolo { RecruitedBy = player.Serial, CantWalk = false, AI = AIType.AI_Melee };
        // The story companion uses the one-companion slot and travels alongside summoned followers.
        companion.ControlSlots = 0;
        companion.RangeFight = 5;
        if (!companion.SetControlMaster(player) || !MercenaryCare.Register(player, companion, RequiredLevel))
        {
            companion.Delete();
            return null;
        }

        var spot = PersistentMinions.FindPosition(player, out var nearby) ? nearby : player.Location;
        companion.MoveToWorld(spot, player.Map);
        companion.ControlTarget = player;
        companion.ControlOrder = OrderType.Guard;
        ClassCreation.Profiles.SetIoloCompanion(player.Serial, companion.Serial);
        ScriptedDialogue.SendStoryState(player);
        return companion;
    }

    private bool IsOwnedBy(Mobile from) => from is PlayerMobile player && player.Serial == _recruitedBy
        && MercenaryCare.IsCompanion(player, this);

    private static Iolo TravelingCompanion(PlayerMobile player) =>
        World.FindMobile(ClassCreation.Profiles.IoloCompanion(player.Serial)) is Iolo { IsDeadPet: false } companion
        && ScriptedDialogue.CanTalk(player, companion) ? companion : null;

    internal static bool IsTravelingWith(PlayerMobile player) => TravelingCompanion(player) != null;

    internal static Iolo NearbyCompanion(PlayerMobile player) =>
        MercenaryCare.ActiveStoryCompanion(player) is Iolo { Alive: true, IsDeadPet: false } iolo
        && iolo.IsOwnedBy(player) && iolo.Map == player.Map && iolo.InRange(player, 6)
        && Math.Abs(iolo.Z - player.Z) < 16 && player.CanSee(iolo) && player.InLOS(iolo) ? iolo : null;

    internal static void RecordKrulgDefeat(PlayerMobile player, Corpse corpse)
    {
        var profiles = ClassCreation.Profiles;
        var recruited = profiles.IoloCompanion(player.Serial);
        bool witnessed = World.FindMobile(recruited) is Iolo { Alive: true, IsDeadPet: false } companion
            && companion.IsOwnedBy(player) && companion.Map == corpse.Map
            && companion.InRange(corpse.GetWorldLocation(), 12) && companion.InLOS(corpse);
        profiles.RecordKrulgDefeat(player.Serial, witnessed ? recruited : default);
    }

    internal static void OnNewHavenDeparture(PlayerMobile player)
    {
        if (StatNetwork.SupportsCurrentProtocol(player) && TravelingCompanion(player) is { } companion)
            ScriptedDialogue.Show(player, companion, _departure);
    }

    private bool CanHandleGear(Mobile from) => IsOwnedBy(from) && from.Alive && Alive && !IsDeadPet
        && from.Map == Map && from.InRange(this, PortraitProtocol.NpcInteractionRange) && from.CanSee(this) && from.InLOS(this);

    public override bool CanPaperdollBeOpenedBy(Mobile from) => CanHandleGear(from);
    public override bool AllowEquipFrom(Mobile from) => CanHandleGear(from);
    public override bool CheckNonlocalLift(Mobile from, Item item) => CanHandleGear(from);
    public override bool CheckNonlocalDrop(Mobile from, Item item, Item target) => CanHandleGear(from);
    public override bool CanBeRenamedBy(Mobile from) => false;
    public override bool OnDragDrop(Mobile from, Item dropped) => IsOwnedBy(from) && base.OnDragDrop(from, dropped);

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        if (!Recruited || !IsDeadPet)
            base.GetContextMenuEntries(from, ref list);
    }

    public override void OnAfterResurrect()
    {
        base.OnAfterResurrect();
        EndBarkFight();
        if (Recruited && ControlMaster is PlayerMobile owner && IsOwnedBy(owner))
        {
            ControlTarget = owner;
            ControlOrder = OrderType.Guard;
        }
    }

    public override int GetResistance(ResistanceType type) => CompanionBuild.Resistance(this, type, base.GetResistance(type));

    public override bool EquipItem(Item item)
    {
        if (Recruited && !AllowedGear(item))
        {
            ControlMaster?.SendMessage($"{Name}: {CompanionBuild.ArcherGearRefusal(item, "crossbows")}");
            return false;
        }
        return base.EquipItem(item);
    }

    internal static bool AllowedGear(Item item) => CompanionBuild.AllowedArcherGear(item, AttackFamily.Crossbow);

    public override void OnThink()
    {
        if (Recruited)
            RangeFight = Weapon is BaseRanged ranged && !ClassGear.Broken(ranged) ? Math.Min(5, ranged.MaxRange) : 1;
        base.OnThink();
        if (Recruited)
            ObserveCombatBarks();
    }

    internal static int ShotRank(CombatAbility skill, int level)
    {
        int unlock = ShotUnlockLevel(skill);
        return level < unlock ? 0 : Math.Min(20, 1 + (level - unlock) / 4);
    }

    internal static int ShotUnlockLevel(CombatAbility skill) => skill switch
    {
        CombatAbility.MagicArrow => 6, CombatAbility.ColdArrow => 10, CombatAbility.GuidedArrow => 18, _ => 100
    };

    internal static SkillValues ShotValues(CombatAbility skill, int level)
    {
        int Learned(string key) => key switch
        {
            "Magic Arrow" => ShotRank(CombatAbility.MagicArrow, level),
            "Cold Arrow" => ShotRank(CombatAbility.ColdArrow, level),
            "Guided Arrow" => ShotRank(CombatAbility.GuidedArrow, level),
            _ => 0
        };
        return new(SkillMechanics.For(skill), Learned, Learned, level);
    }

    internal static int ArrowConversion(CombatAbility skill, int rank)
    {
        string missile = skill == CombatAbility.ColdArrow ? "coldarrow" : "magicarrow";
        return Math.Clamp(NecromancerSummons.Source(missile, "dParam1")
            + (rank - 1) * NecromancerSummons.Source(missile, "dParam2"), 0, 100);
    }

    internal TimeSpan BeginShot(Mobile target, BaseRanged weapon)
    {
        if (CreatureHitRecovery.Remaining(this) is > 0 and var recovery)
            return TimeSpan.FromMilliseconds(recovery);
        if (_shot.Running || !CanShoot(target, weapon))
            return TimeSpan.FromMilliseconds(100);

        int level = MercenaryCare.CompanionLevel(this) ?? 1;
        var skill = CombatAbility.BasicAttack;
        if (Core.TickCount >= _nextSpecialShot)
        {
            if (ShotRank(CombatAbility.ColdArrow, level) > 0 && GearEffects.ChillPercent(target) == 0)
                skill = CombatAbility.ColdArrow;
            else if (ShotRank(CombatAbility.GuidedArrow, level) > 0 && !InRange(target, 3))
                skill = CombatAbility.GuidedArrow;
            else if (ShotRank(CombatAbility.MagicArrow, level) > 0)
                skill = CombatAbility.MagicArrow;
        }

        var timing = CompanionBuild.Timing(this, CombatClass, skill, weapon);
        Direction = GetDirectionTo(target);
        weapon.PlaySwingAnimation(this);
        if (skill != CombatAbility.BasicAttack)
            _nextSpecialShot = Core.TickCount + 3000;
        Timer.StartTimer(TimeSpan.FromMilliseconds(timing.EffectMs), () => ReleaseShot(target, weapon, skill), out _shot);
        return TimeSpan.FromMilliseconds(timing.DurationMs);
    }

    private bool CanShoot(Mobile target, BaseRanged weapon) => Alive && !Deleted && !IsDeadPet && !Frozen && !Paralyzed
        && ClassCombatHooks.ActionAllowed?.Invoke(this) != false
        && Weapon == weapon && !ClassGear.Broken(weapon) && Combatant == target && ControlMaster is PlayerMobile owner
        && MercenaryCare.IsCompanion(owner, this) && MercenaryCare.WithinCompanionLeash(owner, this, target)
        && ClassCombat.IsHostile(owner, target)
        && InRange(target, Math.Min(5, weapon.MaxRange)) && Math.Abs(Z - target.Z) < 16 && InLOS(target);

    private void ReleaseShot(Mobile target, BaseRanged weapon, CombatAbility skill)
    {
        if (CreatureHitRecovery.Remaining(this) > 0 || !CanShoot(target, weapon))
            return;
        DoHarmful(target);
        MovingParticles(target, weapon.EffectID, 18, 1, false, false, 0, 0, 0, 0, 0, EffectLayer.RightHand, 0);
        int level = MercenaryCare.CompanionLevel(this) ?? 1;
        var values = skill == CombatAbility.BasicAttack ? null : ShotValues(skill, level);
        int attackPercent = CompanionBuild.Bonus(this, "att%") + (int)(values?.Formula("toht") ?? 0);
        if (ClassStats.CheckHit(this, target, attackPercent, projectile: true, guaranteed: skill == CombatAbility.GuidedArrow) != true)
            return;

        int skillPercent = skill == CombatAbility.GuidedArrow ? (int)values.Eval("calc1") : 0;
        double physical = CompanionBuild.BowPhysicalDamage(this, weapon, level, skillPercent);
        double cold = 0, magic = 0;
        if (skill is CombatAbility.ColdArrow or CombatAbility.MagicArrow)
        {
            int conversion = ArrowConversion(skill, values.Rank);
            double element = physical * conversion / 100;
            physical -= element;
            if (skill == CombatAbility.ColdArrow)
                cold = element + Utility.RandomMinMax(values.Damage256(false), values.Damage256(true)) / 256.0;
            else
            {
                magic = element;
                physical += Utility.RandomMinMax(values.PhysicalDamage256(false), values.PhysicalDamage256(true)) / 256.0;
            }
        }

        var parts = CompanionBuild.BowHit(this, target, new(physical, 0, cold, 0, 0, Magic: magic));
        if (skill == CombatAbility.ColdArrow && parts.Cold > 0 && target.Alive)
            GearEffects.ApplyChill(target, Core.TickCount, values.DurationFrames * ActionTiming.FrameMs, this);
    }
}
