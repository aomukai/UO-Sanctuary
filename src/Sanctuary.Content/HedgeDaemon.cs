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

[SerializationGenerator(0)]
public partial class HedgeDaemon : HedgeEncounter, IScriptedSpeaker
{
    private const string LastWarning = "Two questions I have suffered. Ask a third, mortal, and thou shalt learn how little patience remains.";
    private const HedgeMazeProgress SeenDaemon = HedgeMazeProgress.DaemonMet | HedgeMazeProgress.DaemonBanished
        | HedgeMazeProgress.DaemonDefeated;
    private static readonly Rectangle2D Chamber = new(1161, 2204, 5, 7);
    private static readonly Dictionary<Serial, Visit> _visits = new();
    private readonly record struct Visit(Serial Daemon, bool FirstConversation);

    private static readonly (string Keyword, string Answer, StoryDiscoveries Discovery)[] _questions =
    [
        ("What Happened", "What happened here is none of thy business.", StoryDiscoveries.None),
        ("Thy Watch", "My master summoned me to watch over the Hedge Maze. He has been gone a long time now, but I was never relieved.", StoryDiscoveries.HedgeOrvath),
        ("Thy Master", "My master's name is none of thy business either.", StoryDiscoveries.None)
    ];

    [SerializableField(0)]
    private bool _hostile;

    public new static void Configure()
    {
        ClassMapHooks.LocationChanged += CheckArrival;
        EventSink.Disconnected += player => _visits.Remove(player.Serial);
    }

    [Constructible]
    public HedgeDaemon() : base(WatchDaemon)
    {
        Direction = Direction.East;
        ResumeDisposition();
    }

    public override bool IsInvulnerable => !_hostile;
    public override bool AlwaysMurderer => _hostile;
    public override bool AlwaysAttackable => _hostile && !PaladinConversion.Active(this);
    internal override bool IsEnemyIgnoringLeash(Mobile target) => _hostile && base.IsEnemyIgnoringLeash(target);

    ushort IScriptedSpeaker.Portrait => (ushort)NpcPortrait.HedgeDaemon;
    bool IScriptedSpeaker.CanTalkTo(PlayerMobile player) => !_hostile && Math.Abs(player.Z - Z) <= 5
        && (!HasMet(player) || _visits.TryGetValue(player.Serial, out var visit) && visit == new Visit(Serial, true));

    void IScriptedSpeaker.BeginConversation(PlayerMobile player)
    {
        ClassCreation.Profiles.AdvanceHedgeMaze(player.Serial, HedgeMazeProgress.DaemonMet);
        _visits[player.Serial] = new(Serial, true);
        ShowQuestions(player, 0, "Who art thou? How didst thou get here?");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player && !_hostile)
            ScriptedDialogue.Open(player, this);
        else
            base.OnDoubleClick(from);
    }

    public override void OnThink()
    {
        if (Map is { } map && map != Map.Internal)
        {
            foreach (var player in map.GetMobilesInRange<PlayerMobile>(Location, RangePerception))
                GreetReturningPlayer(player);
        }

        if (_hostile)
            base.OnThink();
    }

    public override void OnDeath(Container corpse)
    {
        if (ClassLoot.Killer(this) is { Deleted: false } killer)
            ClassCreation.Profiles.AdvanceHedgeMaze(killer.Serial, HedgeMazeProgress.DaemonDefeated);
        base.OnDeath(corpse);
    }

    private static bool HasMet(PlayerMobile player) =>
        ClassCreation.Profiles.HasHedgeMazeProgress(player.Serial, SeenDaemon);

    private static bool InChamber(PlayerMobile player) => player.Map == Map.Trammel
        && Math.Abs(player.Z - HedgeMazeLayout.RitualRoom.Z) <= 5 && Chamber.Contains(player.Location);

    private static void CheckArrival(Mobile mobile)
    {
        if (mobile is not PlayerMobile player)
            return;
        if (!InChamber(player))
        {
            if (_visits.Remove(player.Serial, out var visit))
                ScriptedDialogue.CloseForSpeaker(player, visit.Daemon);
            return;
        }
        if (!HasMet(player))
            return;

        foreach (var daemon in player.Map.GetMobilesInRange<HedgeDaemon>(player.Location, 10))
            daemon.GreetReturningPlayer(player);
    }

    private void GreetReturningPlayer(PlayerMobile player)
    {
        if (Deleted || !Alive || Controlled || Summoned || !InChamber(player) || !HasMet(player)
            || _visits.TryGetValue(player.Serial, out var visit) && visit.Daemon == Serial
            || player is not { Alive: true, NetState: { Running: true } connection } || Math.Abs(player.Z - Z) > 5
            || !InRange(player, RangePerception) || !CanSee(player) || !InLOS(player) || !CanBeHarmful(player, false))
            return;

        _visits[player.Serial] = new(Serial, false);
        string line = ClassCreation.Profiles.HasHedgeMazeProgress(player.Serial, HedgeMazeProgress.DaemonDefeated)
            ? "Didst thou think one death would finish me?"
            : HasBeenBanishedBy(player)
                ? "Thou didst cheat me! The old summons dragged me back from the Abyss. Now thou shalt die."
                : "Thou again. This time, mortal, I mean to kill thee.";
        PrivateOverheadMessage(MessageType.Regular, 0x22, false, line, connection);
        StartBattle(player);
    }

    [AfterDeserialization]
    private void ResumeDisposition()
    {
        FightMode = _hostile ? FightMode.Closest : FightMode.None;
        CantWalk = !_hostile;
    }

    private static bool IsWarlock(PlayerMobile player) =>
        ClassCreation.Profiles.TryGet(player.Serial, out var profile) && profile.Class == DiabloClass.Warlock;

    private ScriptedDialogue.Reply Challenge =>
        new("Challenge", 0, (player, _) => BeginBattle(player));

    private void ShowQuestions(PlayerMobile player, int asked, string line)
    {
        var replies = new List<ScriptedDialogue.Reply>();
        for (int index = 0; index < _questions.Length; index++)
        {
            int bit = 1 << index;
            if ((asked & bit) != 0)
                continue;
            var (keyword, answer, discovery) = _questions[index];
            replies.Add(new(keyword, 0, (viewer, _) => Answer(viewer, asked | bit, answer, discovery)));
        }

        if (replies.Count == 1)
            line += "\n\n" + LastWarning;
        else if (replies.Count == 0 && !HasBeenBanishedBy(player))
            replies.Add(new("Return to the Abyss", 0, (viewer, _) => OfferDispel(viewer)));
        replies.Add(Challenge);
        ScriptedDialogue.Show(player, this, [new(line, replies.ToArray())]);
    }

    private void Answer(PlayerMobile player, int asked, string answer, StoryDiscoveries discovery)
    {
        if (asked == (1 << _questions.Length) - 1 && !IsWarlock(player))
        {
            BeginBattle(player, "Thou hast asked too many questions. Now thou shalt die.");
            return;
        }

        ShowQuestions(player, asked, answer);
        if (discovery != StoryDiscoveries.None)
            ScriptedDialogue.RecordDiscovery(player, discovery);
    }

    private void BeginBattle(PlayerMobile player, string line = "Very well. Come and see what my master left behind.")
    {
        if (!ScriptedDialogue.CanTalk(player, this))
            return;

        Say(line);
        StartBattle(player);
    }

    private void StartBattle(PlayerMobile player)
    {
        if (_hostile && EliteCombat.Enemy(this, Combatant, RangePerception))
            return;
        if (!_hostile)
        {
            _hostile = true;
            this.MarkDirty();
            ResumeDisposition();
            Delta(MobileDelta.Noto);
        }
        Combatant = player;
        AIObject?.AITimer.Prod();
    }

    private void OfferDispel(PlayerMobile player)
    {
        if (!IsWarlock(player) || HasBeenBanishedBy(player))
            return;
        ScriptedDialogue.Show(player, this,
            [new("The Abyss? Yes. I've watched this place long enough. Speak the words and I will go.",
                [new("Speak the Dispel", 0, (viewer, _) => Dispel(viewer)), Challenge])]);
    }

    private static bool HasBeenBanishedBy(PlayerMobile player) =>
        ClassCreation.Profiles.HasHedgeMazeProgress(player.Serial, HedgeMazeProgress.DaemonBanished);

    private void Dispel(PlayerMobile player)
    {
        if (!IsWarlock(player) || !ScriptedDialogue.CanTalk(player, this)
            || HasBeenBanishedBy(player) || player.Backpack is not { } pack)
            return;

        int level = CombatProfile.Level;
        var candidates = GearDrops.Bases.Where(basis => basis.Tier == GearTier.Normal && basis.Variants.Length > 0
            && basis.QualityLevel <= level && basis.ExcludedReason == null).ToArray();
        var random = Random.Shared;
        var reward = GearDrops.GenerateMagic(candidates[random.Next(candidates.Length)], level, random);
        if (!pack.TryDropItem(player, reward, false))
        {
            reward.Delete();
            ScriptedDialogue.Show(player, this,
                [new("Make room for what I leave behind, and then speak the words.",
                    [new("Speak the Dispel", 0, (viewer, _) => Dispel(viewer)), Challenge])]);
            return;
        }

        ClassCreation.Profiles.AdvanceHedgeMaze(player.Serial, HedgeMazeProgress.DaemonBanished);
        player.PrivateOverheadMessage(MessageType.Regular, 0x0481, false, "An Ort!", player.NetState);
        Effects.SendLocationEffect(Location, Map, 0x3728, 13);
        PlaySound(0x201);
        player.SendMessage("Orvath's magic item has been placed in thy pack.");
        Delete();
    }
}
