// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.Engines.PartySystem;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public enum ShamePassage { BoneShield, DaemonShield, QuarrelShield, Order, Chaos, ReturnGate }

public sealed class ShameExpedition : Item
{
    private sealed record Participant(int Stage = 0, bool Departed = false, bool Spoke = false);
    private sealed record Island(Point3D Arrival, Rectangle2D Bounds, (ShameRole, Point3D)[] Enemies);

    internal static readonly Point3D ReturnShore = new(5748, 89, 0);
    internal static readonly Point3D Entrance = new(514, 1561, 0);
    private static readonly Island[] _islands =
    [
        new(new(5741, 52, 0), new(5735, 47, 11, 10), [(ShameRole.BoneCaptain, new(5737, 49, 5))]),
        new(new(5727, 71, 0), new(5719, 71, 9, 9), [(ShameRole.DaemonCaptain, new(5723, 75, 0))]),
        new(new(5719, 62, 0), new(5711, 55, 9, 9), [(ShameRole.BickeringMate, new(5717, 57, 5)), (ShameRole.BickeringMage, new(5714, 60, 5))]),
        new(new(5703, 80, 0), new(5695, 79, 9, 4), []),
        new(new(5703, 61, 5), new(5695, 55, 9, 8), [(ShameRole.FleetCaptain, new(5697, 57, 5))])
    ];
    private static readonly (string, string)[] _quarrel =
    [
        ("Ye sold our navigator for a cask of rum!", "And he found his way home, didn’t he?"),
        ("Mind where ye cast, ye bilge rat!", "Mind where ye stand, ye walking kindling!"),
        ("I outrank ye!", "Then die in command and leave me to it!")
    ];

    private static ShameExpedition _wheel;
    private readonly Dictionary<Serial, Participant> _participants = new();
    private readonly HashSet<Serial> _actors = new();
    private readonly HashSet<Serial> _artifacts = new();
    private DateTime _startedAt, _endsAt, _nextBark;
    private int _spawned, _cleared, _warnings, _bark;
    private bool _daemonCalled, _orderReturn;

    internal ShameExpedition() : base(0x46FC)
    {
        Name = "the pirate ship’s wheel";
        Movable = false;
    }

    public ShameExpedition(Serial serial) : base(serial) { }

    public static void Initialize()
    {
        ClassCombatHooks.PlayerDied += OnDeath;
        ClassMapHooks.LocationChanged += CheckArrival;
        EventSink.Connected += mobile => Timer.DelayCall(() => CheckArrival(mobile));
        Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () => _wheel?.Tick(Core.Now), out _);
    }

    internal static void Install(Map map, Point3D at)
    {
        _wheel = ShamePopulation.Ensure(map, at, () => new ShameExpedition());
        _wheel.EnsureBoardingCrew();
        _wheel.Tick(Core.Now);
    }

    private bool Running => _startedAt != default;
    private bool Spawned(int stage) => (_spawned & (1 << stage)) != 0;
    private bool Cleared(int stage) => (_cleared & (1 << stage)) != 0;
    private DateTime AdmissionDeadline => _startedAt.AddMinutes(5);

    private static int IslandAt(Point3D at, Map map)
    {
        if (map != Map.Trammel || at.Z is < -6 or > 26)
            return 0;
        for (int i = 0; i < _islands.Length; i++)
            if (_islands[i].Bounds.Contains(at))
                return i + 1;
        return 0;
    }

    private static int IslandOf(PlayerMobile player) => player.Map == Map.Internal
        ? IslandAt(player.LogoutLocation, player.LogoutMap) : IslandAt(player.Location, player.Map);

    private static bool CanUse(PlayerMobile player, Item item, int range = 2) =>
        player.Alive && player.Map == item.Map && player.InRange(item, range)
        && Math.Abs(player.Z - item.Z) < 14 && player.CanSee(item) && player.InLOS(item);

    private IEnumerable<ShameEncounter> Living(int stage) => _actors.Select(serial => World.FindMobile(serial))
        .OfType<ShameEncounter>().Where(actor => actor.Alive && !actor.Deleted && actor.Stage == stage);

    private void EnsureBoardingCrew()
    {
        if (Spawned(0))
            return;
        _spawned |= 1;
        Add(ShameRole.Deckhand, 0, new(5748, 59, -2));
        Add(ShameRole.Deckhand, 0, new(5750, 61, -2));
        Add(ShameRole.Deckhand, 0, new(5749, 62, -2));
        Add(ShameRole.BoardingCaptain, 0, new(5749, 58, -2));
        this.MarkDirty();
    }

    private ShameEncounter Add(ShameRole role, int stage, Point3D at)
    {
        var actor = new ShameEncounter(role, this, stage)
        {
            Home = at,
            HomeMap = Map,
            RangeHome = stage == 0 ? 4 : 6
        };
        _actors.Add(actor.Serial);
        actor.MoveToWorld(at, Map);
        return actor;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player || !CanUse(player, this)
            || !ShamePopulation.OnMainDeck(player.Location, player.Map))
            return;
        var now = Core.Now;
        Tick(now);
        if (!Running && Living(0).Any())
        {
            player.SendMessage("The crew still holds the ship.");
            return;
        }
        if (Running && (now >= AdmissionDeadline || !_participants.TryGetValue(player.Serial, out var admission)
                || admission is not { Stage: 0, Departed: false }))
        {
            int minutes = Math.Max(1, (int)Math.Ceiling((_endsAt - now).TotalMinutes));
            player.SendMessage($"The wheel will turn again in {minutes} minute(s).");
            return;
        }
        bool leader = !Running;
        if (leader)
        {
            _startedAt = now;
            _endsAt = now.AddMinutes(30);
            _participants.Add(player.Serial, new Participant());
            if (Party.Get(player) is { Active: true } party)
                foreach (var member in party.Members)
                    if (member.Mobile is PlayerMobile avatar)
                        _participants.TryAdd(avatar.Serial, new Participant());
        }
        if (!Go(player, 1))
        {
            if (leader)
            {
                _participants.Clear();
                _startedAt = _endsAt = default;
            }
            player.SendMessage("There is no clear place to land on the islet.");
            return;
        }
        if (_orderReturn && !Cleared(5))
        {
            _endsAt = _startedAt.AddMinutes(30);
            _warnings = 0;
        }
        player.SendMessage("The wheel will admit the original party for five minutes from the first crossing.");
        this.MarkDirty();
    }

    private bool Go(PlayerMobile player, int stage)
    {
        var island = _islands[stage - 1];
        if (!FindLanding(player, island.Arrival, stage, out var at))
            return false;
        var before = _participants[player.Serial];
        _participants[player.Serial] = before with { Stage = stage };
        Travel(player, at);
        EnterStage(stage);
        return true;
    }

    private bool FindLanding(PlayerMobile player, Point3D at, int stage, out Point3D landing)
    {
        if (Teleportation.Space(Map, at, player, allowAllies: true))
        {
            landing = at;
            return true;
        }
        return PersistentMinions.FindPosition(Map, at, point => IslandAt(point, Map) == stage
            && Math.Abs(point.Z - at.Z) < 8, out landing);
    }

    private static void Travel(PlayerMobile player, Point3D landing)
    {
        if (player.Map == Map.Internal)
        {
            BaseCreature.TeleportPets(player, landing, Map.Trammel);
            player.LogoutMap = Map.Trammel;
            player.LogoutLocation = landing;
            player.MarkDirty();
        }
        else if (!BritanniaCampaign.Sail(player, landing))
        {
            // Safety exits must work even when a party crowds the shore.
            ClassCombat.Stop(player);
            BaseCreature.TeleportPets(player, landing, Map.Trammel);
            player.MoveToWorld(landing, Map.Trammel);
        }
    }

    private void EnterStage(int stage)
    {
        if (Spawned(stage))
            return;
        // The main loop commits the stage before exposing any of its entities.
        _spawned |= 1 << stage;
        var island = _islands[stage - 1];
        foreach (var (role, at) in island.Enemies)
            Add(role, stage, at);

        switch (stage)
        {
            case 1: Place(ShamePassage.BoneShield, new(5737, 48, 8)); break;
            case 2: Place(ShamePassage.DaemonShield, new(5721, 76, 3)); break;
            case 3:
                Place(ShamePassage.QuarrelShield, new(5716, 56, 5));
                _nextBark = Core.Now.AddSeconds(2);
                break;
            case 4:
                SpeakOn(island.Arrival, fleet: false);
                Place(ShamePassage.Order, new(5701, 79, 3));
                Place(ShamePassage.Chaos, new(5702, 79, 3));
                break;
        }
        this.MarkDirty();
    }

    private void Place(ShamePassage passage, Point3D at)
    {
        var item = new ShameIslandPassage(passage);
        _artifacts.Add(item.Serial);
        item.MoveToWorld(at, Map);
    }

    private ShameCaptainGhost SpeakOn(Point3D location, bool fleet)
    {
        var npc = new ShameCaptainGhost(fleet);
        _actors.Add(npc.Serial);
        npc.Home = location;
        npc.HomeMap = Map;
        npc.RangeHome = 0;
        npc.MoveToWorld(location, Map);
        return npc;
    }

    internal static void Use(PlayerMobile player, ShameIslandPassage item)
    {
        var run = _wheel;
        if (run == null || !run.Running || !run._artifacts.Contains(item.Serial) || !CanUse(player, item))
            return;
        int from = item.Passage switch
        {
            ShamePassage.BoneShield => 1,
            ShamePassage.DaemonShield => 2,
            ShamePassage.QuarrelShield => 3,
            ShamePassage.Order or ShamePassage.Chaos => 4,
            _ => 5
        };
        if (!run.Present(player, from) || Core.Now >= run._endsAt)
            return;
        if (from != 4 && !run.Cleared(from))
        {
            player.SendMessage("The curse holds this passage shut.");
            return;
        }
        if (item.Passage is ShamePassage.Order or ShamePassage.ReturnGate)
        {
            run._participants[player.Serial] = run._participants[player.Serial] with { Departed = true };
            Travel(player, ReturnShore);
            if (item.Passage == ShamePassage.Order)
                run._orderReturn = true;
            run.SettleDepartures(Core.Now);
        }
        else if (!run.Go(player, from + 1))
            player.SendMessage("There is no clear place to land on the islet.");
        run.MarkDirty();
    }

    private bool Present(PlayerMobile player, int stage) => Running && _participants.TryGetValue(player.Serial, out var visit)
        && !visit.Departed && visit.Stage == stage && IslandAt(player.Location, player.Map) == stage;

    internal static bool AllowsCombat(ShameEncounter actor, Mobile target)
    {
        var run = _wheel;
        var player = target as PlayerMobile ?? (target as BaseCreature)?.GetMaster() as PlayerMobile;
        if (player == null || run == null || actor.Map != player.Map)
            return false;
        return actor.Stage == 0
            ? ShamePopulation.OnMainDeck(player.Location, player.Map)
            : Core.Now < run._endsAt && run.Present(player, actor.Stage);
    }

    internal static void Died(ShameEncounter actor, Container corpse, PlayerMobile credited)
    {
        var run = _wheel;
        if (run == null || !run._actors.Contains(actor.Serial))
            return;
        if (corpse is { Deleted: false })
            run._artifacts.Add(corpse.Serial);
        int stage = actor.Stage;
        if (stage == 2 && actor.Role == ShameRole.DaemonCaptain)
            run.CallDaemon(actor);
        if (stage != 0 && !run.Living(stage).Any(other => other != actor
                && (other.Role != ShameRole.Skeleton || stage != 1)))
        {
            run._cleared |= 1 << stage;
            if (stage == 1)
                foreach (var skeleton in run.Living(stage).Where(other => other.Role == ShameRole.Skeleton).ToArray())
                    skeleton.Delete();
            else if (stage == 5 && actor.Role == ShameRole.FleetCaptain)
                run.EndBattle(credited);
        }
        run.MarkDirty();
    }

    internal static void Summon(ShameEncounter actor)
    {
        if (_wheel is not { Running: true } run || !run._actors.Contains(actor.Serial) || run.Cleared(actor.Stage))
            return;
        if (actor.Role == ShameRole.DaemonCaptain)
            run.CallDaemon(actor);
        else if (run.Living(1).Count(other => other.Role == ShameRole.Skeleton) < 5)
            run.CallMinion(actor, ShameRole.Skeleton);
    }

    private void CallDaemon(ShameEncounter actor)
    {
        if (_daemonCalled)
            return;
        _daemonCalled = true;
        CallMinion(actor, ShameRole.Daemon);
        this.MarkDirty();
    }

    private void CallMinion(ShameEncounter actor, ShameRole role)
    {
        var center = actor.Location;
        if (!PersistentMinions.FindPosition(Map, center, point => IslandAt(point, Map) == actor.Stage
            && Math.Abs(point.Z - center.Z) < 8, out var spot))
            spot = _islands[actor.Stage - 1].Arrival;
        var minion = Add(role, actor.Stage, spot);
        if (actor.Combatant is { Alive: true } enemy && AllowsCombat(minion, enemy))
            minion.Combatant = enemy;
        minion.FixedEffect(0x3728, 10, 10);
        this.MarkDirty();
    }

    private void EndBattle(PlayerMobile credited)
    {
        var at = new Point3D(5700, 55, 0);
        var chest = new ShamePlunder();
        _artifacts.Add(chest.Serial);
        int magicFind = credited == null ? 0 : GearDrops.MagicFind(credited);
        int goldFind = credited == null ? 0 : CreatureGold.GoldFind(credited);
        for (int i = 0; i < 4; i++)
            chest.DropItem(EncounterTreasure.RollEquipment("Diablo", 38, magicFind, Random.Shared));
        chest.DropItem(new Gold(Utility.RandomMinMax(650, 950) * (100 + goldFind) / 100));
        chest.MoveToWorld(at, Map);
        var ghost = SpeakOn(new(5697, 57, 5), fleet: true);
        var gate = new Point3D(5696, 62, 0);
        ghost.Animate(16, 5, 1, true, false, 0);
        Place(ShamePassage.ReturnGate, gate);
        Effects.SendLocationEffect(gate, Map, 0x3728, 15);
        foreach (var serial in _participants.Keys)
            if (World.FindMobile(serial) is PlayerMobile player && Present(player, 5))
                ScriptedDialogue.Open(player, ghost);
        _endsAt = Core.Now.AddMinutes(10);
        _warnings = 0;
    }

    internal static bool MayTakePlunder(Mobile from, ShamePlunder chest) => from is PlayerMobile player
        && _wheel is { Running: true } run && Core.Now < run._endsAt && run.Cleared(5)
        && run._artifacts.Contains(chest.Serial) && run.Present(player, 5);

    internal static bool MayTalk(PlayerMobile player, ShameCaptainGhost ghost) =>
        _wheel is { Running: true } run && Core.Now < run._endsAt && run._actors.Contains(ghost.Serial)
        && run.Present(player, ghost.Fleet ? 5 : 4);

    internal static void Spoke(PlayerMobile player, ShameCaptainGhost ghost)
    {
        if (!MayTalk(player, ghost) || ghost.Fleet)
            return;
        var run = _wheel;
        run._participants[player.Serial] = run._participants[player.Serial] with { Spoke = true };
        run.SettleDepartures(Core.Now);
        run.MarkDirty();
    }

    private static void CheckArrival(Mobile mobile)
    {
        if (mobile is not PlayerMobile player || player.Map == Map.Internal || IslandOf(player) is not (> 0 and var stage)
            || player.AccessLevel > AccessLevel.Player || _wheel is not { } run || run.Present(player, stage))
            return;
        Timer.DelayCall(() =>
        {
            int current = IslandOf(player);
            if (!player.Deleted && player.Map != Map.Internal && current != 0 && !run.Present(player, current))
            {
                player.SendMessage("The curse turns thee back to shore.");
                Travel(player, ReturnShore);
            }
        });
    }

    private static void OnDeath(PlayerMobile player, Container corpse)
    {
        if (_wheel is not { Running: true } run || IslandOf(player) is not (> 0 and var stage) || !run.Present(player, stage))
            return;
        run._participants[player.Serial] = run._participants[player.Serial] with { Departed = true };
        if (corpse is { Deleted: false })
            corpse.MoveToWorld(ReturnShore, Map.Trammel);
        Travel(player, Entrance);
        run.SettleDepartures(Core.Now);
        run.MarkDirty();
    }

    private void Tick(DateTime now)
    {
        ShameCombat.PruneRoots(now);
        if (!Running)
            return;
        if (now >= _endsAt)
        {
            Expire();
            return;
        }
        SettleDepartures(now);
        TimeSpan remaining = _endsAt - now;
        int warning = remaining.TotalMinutes <= 1 ? 2 : remaining.TotalMinutes <= 5 ? 1 : 0;
        if (warning > 0 && (_warnings & warning) == 0)
        {
            _warnings |= warning == 2 ? 3 : 1;
            foreach (var (serial, visit) in _participants)
                if (visit is { Stage: > 0, Departed: false } && World.FindMobile(serial) is PlayerMobile player)
                    player.SendMessage(warning == 2 ? "The pirate islets close in one minute." : "The pirate islets close in five minutes.");
            this.MarkDirty();
        }
        if (Spawned(3) && !Cleared(3) && now >= _nextBark)
        {
            _nextBark = now.AddSeconds(12);
            var captains = Living(3).ToArray();
            var mate = captains.FirstOrDefault(actor => actor.Role == ShameRole.BickeringMate);
            var mage = captains.FirstOrDefault(actor => actor.Role == ShameRole.BickeringMage);
            if (mate == null || mage == null || mate.Combatant == null && mage.Combatant == null)
                return;
            var (first, second) = _quarrel[_bark++ % _quarrel.Length];
            mate.PublicOverheadMessage(MessageType.Regular, 0x3B2, false, first);
            Timer.DelayCall(TimeSpan.FromSeconds(2), () =>
            {
                if (!mate.Deleted && mate.Alive && !mage.Deleted && mage.Alive)
                    mage.PublicOverheadMessage(MessageType.Regular, 0x59, false, second);
            });
        }
    }

    private void SettleDepartures(DateTime now)
    {
        foreach (var (serial, visit) in _participants.ToArray())
            if (visit is { Stage: > 0, Departed: false }
                && (World.FindMobile(serial) is not PlayerMobile player || IslandOf(player) != visit.Stage))
            {
                _participants[serial] = visit with { Departed = true };
                this.MarkDirty();
            }

        bool nobodyLeft = !_participants.Values.Any(visit => visit is { Stage: > 0, Departed: false });
        if (_orderReturn && nobodyLeft && !Cleared(5) && _endsAt == _startedAt.AddMinutes(30))
        {
            _endsAt = now.AddMinutes(10);
            _warnings = 0;
            this.MarkDirty();
        }
        if (Spawned(4) && !_participants.Values.Any(visit => !visit.Departed && !visit.Spoke
                && (visit.Stage > 0 || now < AdmissionDeadline)))
            foreach (var ghost in _actors.Select(serial => World.FindMobile(serial)).OfType<ShameCaptainGhost>().Where(ghost => !ghost.Fleet).ToArray())
                ghost.Delete();
    }

    private void Expire()
    {
        foreach (var (serial, visit) in _participants.ToArray())
            if (visit.Stage > 0 && World.FindMobile(serial) is PlayerMobile player && IslandOf(player) is > 0)
            {
                player.SendMessage("The pirate islets close. You are returned to shore.");
                Travel(player, ReturnShore);
            }
        using var corpses = PooledRefList<Corpse>.Create();
        foreach (var island in _islands)
            foreach (var corpse in Map.GetItemsInBounds<Corpse>(island.Bounds))
                if (corpse.Owner is PlayerMobile player && _participants.ContainsKey(player.Serial)
                    && IslandAt(corpse.Location, corpse.Map) > 0)
                    corpses.Add(corpse);
        foreach (var corpse in corpses)
            corpse.MoveToWorld(ReturnShore, Map.Trammel);
        foreach (var serial in _artifacts)
            World.FindItem(serial)?.Delete();
        foreach (var serial in _actors)
            World.FindMobile(serial)?.Delete();
        _participants.Clear();
        _actors.Clear();
        _artifacts.Clear();
        _startedAt = _endsAt = _nextBark = default;
        _spawned = _cleared = _warnings = _bark = 0;
        _daemonCalled = _orderReturn = false;
        EnsureBoardingCrew();
    }

    public override void Serialize(IGenericWriter writer)
    {
        base.Serialize(writer);
        writer.Write(0);
        writer.Write(_startedAt);
        writer.Write(_endsAt);
        writer.Write(_spawned);
        writer.Write(_cleared);
        writer.Write(_warnings);
        writer.Write(_daemonCalled);
        writer.Write(_orderReturn);
        writer.WriteEncodedInt(_participants.Count);
        foreach (var (serial, state) in _participants)
        {
            writer.Write(serial);
            writer.Write(state.Stage);
            writer.Write(state.Departed);
            writer.Write(state.Spoke);
        }
        WriteSet(_actors);
        WriteSet(_artifacts);

        void WriteSet(HashSet<Serial> entries)
        {
            writer.WriteEncodedInt(entries.Count);
            foreach (var serial in entries)
                writer.Write(serial);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        base.Deserialize(reader);
        _ = reader.ReadInt();
        _startedAt = reader.ReadDateTime();
        _endsAt = reader.ReadDateTime();
        _spawned = reader.ReadInt();
        _cleared = reader.ReadInt();
        _warnings = reader.ReadInt();
        _daemonCalled = reader.ReadBool();
        _orderReturn = reader.ReadBool();
        int count = reader.ReadEncodedInt();
        for (int i = 0; i < count; i++)
            _participants.Add(reader.ReadSerial(), new Participant(reader.ReadInt(), reader.ReadBool(), reader.ReadBool()));
        ReadSet(_actors);
        ReadSet(_artifacts);

        void ReadSet(HashSet<Serial> entries)
        {
            int size = reader.ReadEncodedInt();
            for (int i = 0; i < size; i++)
                entries.Add(reader.ReadSerial());
        }
    }
}

[SerializationGenerator(0)]
public partial class ShameIslandPassage : Item
{
    [SerializableField(0)] private ShamePassage _passage;

    internal ShameIslandPassage(ShamePassage passage) : base(passage switch
    {
        ShamePassage.Order => 0x1BC4,
        ShamePassage.ReturnGate => 0xF6C,
        _ => 0x1BC3
    })
    {
        Passage = passage;
        Name = passage switch
        {
            ShamePassage.Order => "the shield of Order",
            ShamePassage.Chaos => "the shield of Chaos",
            ShamePassage.ReturnGate => "the fleet captain’s gate to shore",
            _ => "a salt-stained pirate shield"
        };
        Movable = false;
        if (passage == ShamePassage.ReturnGate)
            Light = LightType.Circle300;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player)
            ShameExpedition.Use(player, this);
    }

    public override bool OnMoveOver(Mobile from)
    {
        if (Passage == ShamePassage.ReturnGate && from is PlayerMobile player)
            Timer.DelayCall(() => { if (!Deleted && player.Location == Location) ShameExpedition.Use(player, this); });
        return true;
    }
}
