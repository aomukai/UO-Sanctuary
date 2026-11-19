using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Server;
using Server.Accounting;
using Server.Engines.Harvest;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Spells;

namespace Sanctuary.Integration;

public static class NewHavenStoryChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(NewHavenStoryChecks));
    private static PlayerMobile Avatar => (PlayerMobile)Accounts.GetAccount("classui")[0];
    private static HavenEncounter _shotTarget;
    private static int _beforeShot;
    private static HavenEncounter _bandit;
    private static WarlockGoatmanBody _lesserDaemon;
    private static int _beforeDaemon;
    private static BaseDoor FindDoor(Point3D threshold) => World.Items.Values.OfType<BaseDoor>()
        .Single(door => door.Map == Map.Trammel && door.Location == threshold);

    public static void Configure()
    {
        Register("setup", () =>
        {
            var player = Avatar;
            var iolo = CheckInn();
            var location = new Point3D(3505, 2524, 27);
            Require(iolo.Map.CanFit(location, 16, checkMobiles: false), "No walkable approach to Iolo's table");
            player.MoveToWorld(location, Map.Trammel);
            Require(player.InLOS(iolo) && player.CanSee(iolo), "The inn table blocks Iolo's dialogue");
            Require(!ClassCreation.Profiles.HasMetIolo(player.Serial), "Journal was unlocked before the meeting");
            Require(!CheckGate().UseGate(player), "A new character could leave through the island gate");
            Require(ClassEquipment.StarterClass(player.Backpack.FindItemByType<Dagger>()) == DiabloClass.Warlock,
                "Witch started without a class dagger");
        });
        Register("met", () =>
        {
            var player = Avatar;
            var profiles = ClassCreation.Profiles;
            Require(profiles.HasMetIolo(player.Serial) && !profiles.IoloCompanion(player.Serial).IsMobile,
                "First greeting did not unlock the journal or recruited Iolo too early");
            Require(!profiles.MeetIolo(player.Serial), "The journal entry can be unlocked twice");
        });
        Register("late", () =>
        {
            var player = Avatar;
            var profiles = ClassCreation.Profiles;
            Require(profiles.AdvanceLevel(player, 6) && profiles.HasReached(player.Serial, Iolo.RequiredLevel),
                "Level six does not satisfy Iolo's threshold");
            Require(profiles.AdvanceLevel(player, 7), "Could not test a late return");
            Require(!CheckGate().UseGate(player), "Level alone unlocked the island gate");
        });
        Register("solo", () =>
        {
            var player = Avatar;
            var profiles = ClassCreation.Profiles;
            Require(profiles.NewHavenGateUnlocked(player.Serial) && !profiles.IoloCompanion(player.Serial).IsMobile
                && ClassMapHooks.PublicGateAllowed(CheckGate(), player), "The gate still requires Iolo's recruitment");
            var previous = profiles.SelectHistory(player, DiabloClass.Amazon);
            try
            {
                Require(ClassStats.Level(player) == 1 && ClassMapHooks.PublicGateAllowed(CheckGate(), player),
                    "Switching to level one closed the gate again");
            }
            finally { profiles.UndoSelection(player, previous); }
        });
        Register("joined", () =>
        {
            var player = Avatar;
            var companion = CheckCompanion();
            var companionSpot = companion.Location;
            Require(companionSpot != player.Location, "Iolo joined on the Avatar's tile");
            player.MoveToWorld(companionSpot, player.Map);
            Require(AlliedOverlap.Allowed(player) && AlliedOverlap.Blocked(companion),
                "Iolo retained priority when the Avatar stepped onto his tile");
            player.MoveToWorld(new Point3D(3505, 2524, 27), player.Map);
            Require(companion.AllowEquipFrom(player) && companion.CanPaperdollBeOpenedBy(player), "The owner cannot manage Iolo's gear");
            var outsider = new PlayerMobile { Body = 0x190 };
            try
            {
                outsider.MoveToWorld(player.Location, player.Map);
                Require(!player.CanSee(CheckInn()) && outsider.CanSee(CheckInn()), "Inn Iolo's visibility is not personal");
                Require(!companion.AllowEquipFrom(outsider) && !companion.CanPaperdollBeOpenedBy(outsider)
                    && !companion.CheckNonlocalLift(outsider, companion.FindItemOnLayer(Layer.TwoHanded)),
                    "An outsider can access Iolo's gear");
            }
            finally
            {
                outsider.Delete();
            }

            var cap = new SkullCap { Name = "Iolo's checked cap" };
            player.Backpack.DropItem(cap);
            player.NextActionTime = 0;
            player.Lift(cap, 1, out var rejected, out _);
            Require(!rejected && player.Holding == cap, "Could not pick up test gear");
            Span<byte> request = stackalloc byte[9];
            BinaryPrimitives.WriteUInt32BigEndian(request, cap.Serial.Value);
            request[4] = (byte)Layer.Helm;
            BinaryPrimitives.WriteUInt32BigEndian(request[5..], companion.Serial.Value);
            IncomingItemPackets.EquipReq(player.NetState, new SpanReader(request));
            Require(companion.FindItemOnLayer(Layer.Helm) == cap, "The normal equipment packet did not equip Iolo");
            Require(CreatureProfiles.Level(companion) == 7 && companion.Dex == 43 && companion.HitsMax == 98,
                "Iolo did not join with the level-seven Amazon stat profile");
            companion.Hits = companion.HitsMax / 2;
            player.AutoStablePets();
            Require(companion.IsStabled && ClassCreation.Profiles.AdvanceLevel(player, 27),
                "Could not level the Avatar while Iolo was away");
            player.ClaimAutoStabledPets();
            Require(MercenaryCare.IsCompanion(player, companion) && CreatureProfiles.Level(companion) == 27
                && companion.HitsMax == 258 && companion.Hits == 129, "Iolo did not catch up by 20 levels and preserve damaged life");
            var plate = new PlateChest();
            var dagger = new Dagger();
            try
            {
                Require(!companion.EquipItem(plate) && !companion.EquipItem(dagger)
                    && Iolo.AllowedGear(companion.FindItemOnLayer(Layer.TwoHanded)), "Iolo accepted restricted equipment");
            }
            finally { plate.Delete(); dagger.Delete(); }
            Require(Iolo.ShotRank(CombatAbility.MagicArrow, 6) == 1 && Iolo.ShotRank(CombatAbility.ColdArrow, 9) == 0
                && Iolo.ShotRank(CombatAbility.GuidedArrow, 27) == 3, "Iolo's skill progression is incorrect");
            var crossbow = (Crossbow)companion.FindItemOnLayer(Layer.TwoHanded);
            Require(CompanionBuild.Timing(companion, Iolo.CombatClass, CombatAbility.MagicArrow, crossbow)
                == CombatTiming.Calculate(DiabloClass.Amazon, CombatAbility.MagicArrow, CombatTiming.Weapon(crossbow))
                && CompanionBuild.Recovery(companion, Iolo.CombatClass) == HitRecovery.Frames(DiabloClass.Amazon, weapon: crossbow),
                "Iolo is not using the Amazon attack and recovery breakpoints");
        });
        Register("reload", () =>
        {
            CheckInn();
            var player = Avatar;
            player.MoveToWorld(player.LogoutLocation, player.LogoutMap);
            player.ClaimAutoStabledPets();
            var companion = CheckCompanion();
            Require(ClassCreation.Profiles.HasMetIolo(player.Serial) && !ClassCreation.Profiles.MeetIolo(player.Serial),
                "The journal discovery was lost on reload");
            Require(CheckGate().UseGate(player), "The island gate unlock did not survive a restart");
            Require(ClassCreation.Profiles.HasDepartedNewHaven(player.Serial), "Iolo's shared departure was lost on reload");
            var solo = (PlayerMobile)Accounts.GetAccount("haven-solo-departure")[0];
            solo.MoveToWorld(NewHavenInn.IoloSeat, Map.Trammel);
            Require(ClassCreation.Profiles.HasDepartedNewHaven(solo.Serial)
                && !solo.CanSee(CheckInn()) && !CheckInn().CanTalkTo(solo),
                "Returning after a restart reopened Iolo's recruitment");
            Require(companion.FindItemOnLayer(Layer.Helm)?.Name == "Iolo's checked cap", "Iolo's gear was lost on reload");
            Require(CreatureProfiles.Level(companion) == 27 && companion.HitsMax == 258, "Iolo's level was lost on reload");
        });
        Register("departure", CheckDeparture);
        BaseDoor innDoor = null;
        Register("doorway", () =>
        {
            var player = Avatar;
            var iolo = CheckCompanion();
            innDoor = FindDoor(new(3506, 2526, 27));
            innDoor.Open = true;
            player.MoveToWorld(new Point3D(3506, 2528, 27), Map.Trammel);
            iolo.MoveToWorld(new Point3D(3506, 2526, 27), player.Map);
        });
        Register("crossed-doorway", () =>
        {
            var player = Avatar;
            Require(player.Y < 2526 && player.InRange(new Point3D(3506, 2525, 27), 2),
                $"Holding movement on the open inn door stopped at {player.Location}");
            innDoor.Open = false;
        });
        Register("bandit-combat", () =>
        {
            var player = Avatar;
            var map = Map.Trammel;
            var start = new Point3D(3714, 2620, 20);
            player.Hidden = false;
            player.MoveToWorld(start, map);
            var dagger = player.Backpack.FindItemByType<Dagger>();
            Require(player.EquipItem(dagger), "The Witch could not equip her starter dagger");
            var origin = new Point3D(3713, 2620, 20);
            _bandit = new HavenEncounter("haven-bandit", 4) { Frozen = true, CantWalk = true, NoKillAwards = true };
            _bandit.Home = origin;
            _bandit.HomeMap = map;
            _bandit.MoveToWorld(origin, map);
            _bandit.AIObject?.AITimer.Stop();
            _bandit.SetHits(1000);
            Require(!_bandit.Items.Any(ClassGear.Converted), "A bandit costume still has combat equipment");
            Require(player.InRange(_bandit, 1) && player.InLOS(_bandit) && ClassCombat.IsHostile(player, _bandit)
                && !SpellHelper.IsTown(_bandit.Location, player),
                $"Bandit fixture at {origin}: LOS={player.InLOS(_bandit)}, hostile={ClassCombat.IsHostile(player, _bandit)}, " +
                $"aware={_bandit.IsEnemy(player)}, visible={player.CanSee(_bandit)}, region={_bandit.Region?.Name}");
            int balance = SkillHealth.Balance256(_bandit);
            var hit = PlayerWeaponContact.PrepareOrdinary(player, _bandit, 0, 0,
                () => player.InRange(_bandit, 1), () => 0, () => 0, _ => 0, _ => 0, Random.Shared, allowNative: true);
            var result = PlayerWeaponContact.Release(player, _bandit, hit, TimedEffects.Now, allowNative: true);
            Require(result.Commit != null && SkillHealth.Balance256(_bandit) < balance,
                $"The starter dagger could not damage a bandit. Result {result.Exit}, flags {hit.Flags}");

            FixtureSpace.Corridor(player, 6560, 28, 3500);
            _bandit.MoveToWorld(new Point3D(player.X + 6, player.Y, player.Z), player.Map);
            _bandit.Home = _bandit.Location;
            _beforeDaemon = SkillHealth.Balance256(_bandit);
            ClassSkillChecks.Train(player, "warlock.summongoatman", 1);
            ClassCombat.Stop(player);
            var session = ClassCombat.For(player);
            session.NextAction = session.RecoveryUntil = Core.TickCount;
            NecromancerEffects.SetMana256(player, player.ManaMax * 256);
            var summon = new Point3D(player.X + 3, player.Y, player.Z);
            Require(ClassCombat.Perform(player, CombatAbility.SummonGoatman, null, summon, Core.TickCount)
                && ClassCombat.ResolveAction(player, session.EffectAt), "The Witch could not summon her Lesser Daemon");
            _lesserDaemon = WarlockGoatmanWorld.For(player).Single();
            Require(_lesserDaemon.Body == 74 && _lesserDaemon.BaseSoundID == 422, "The Lesser Daemon did not use the imp body");
            player.MoveToWorld(ClassCreation.Arrival, Map.Trammel);
            Require(MinionSafeAreas.Parked(_lesserDaemon), "Entering New Haven did not store the Lesser Daemon");
            player.MoveToWorld(new Point3D(summon.X - 3, summon.Y, summon.Z), Map.Trammel);
            Require(_lesserDaemon.Map == player.Map && !MinionSafeAreas.Parked(_lesserDaemon),
                "Leaving New Haven did not return the same Lesser Daemon");
        });
        ConsoleInputHandler.RegisterCommand("iolo-story-bandit-impact", "Wait for the summoned demon to strike a bandit", _ =>
            Core.LoopContext.Post(() => CheckBanditImpact(Core.TickCount + 20000)));
        DruidSpiritWolfBody townWolf = null;
        Point3D outside = default;
        Action restore = null;
        Register("town-summon", () =>
        {
            var player = Avatar;
            outside = player.Location;
            var profiles = ClassCreation.Profiles;
            var previous = profiles.SelectHistory(player, DiabloClass.Druid);
            restore = () => profiles.UndoSelection(player, previous);
            Require(profiles.AdvanceLevel(player, 12), "Could not prepare the Druid for the town summon");
            ClassSkillChecks.Train(player, "druid.summonspiritwolf");
            player.MoveToWorld(ClassCreation.Arrival, Map.Trammel);
            Require(MinionSafeAreas.Contains(player), "The town summon fixture is outside the guarded area");
            ClassCombat.Stop(player);
            var session = ClassCombat.For(player);
            session.NextAction = session.RecoveryUntil = Core.TickCount;
            NecromancerEffects.SetMana256(player, player.ManaMax * 256);
            Require(ClassCombat.Perform(player, CombatAbility.SummonSpiritWolf, null, player.Location, Core.TickCount)
                && ClassCombat.ResolveAction(player, session.EffectAt), "Casting Spirit Wolf in town failed");
            townWolf = DruidSpiritWolves.For(player).Single();
        });
        Register("town-summon-return", () =>
        {
            var player = Avatar;
            Require(MinionSafeAreas.Parked(townWolf), "The in-town summon did not wait outside");
            player.MoveToWorld(outside, Map.Trammel);
            Require(DruidSpiritWolves.For(player).Single() == townWolf
                && townWolf.Map == player.Map && !MinionSafeAreas.Parked(townWolf),
                "The in-town summon did not return when leaving");
            townWolf.Delete();
            restore();
            MercenaryCare.SyncOwner(player);
        });
        Register("ghost", () =>
        {
            var player = Avatar;
            player.Kill();
            Require(!player.Alive, "The player did not die");
            var guard = World.Mobiles.Values.OfType<ArrivalGuard>().Single();
            player.MoveToWorld(ClassCreation.Arrival, Map.Trammel);
            Require(ScriptedDialogue.CanTalk(player, guard), "Elin cannot speak to a ghost at the arrival point");
            guard.OnMovement(player, new Point3D(guard.X + 8, guard.Y, guard.Z));
        });
        Register("resurrect", () =>
        {
            var player = Avatar;
            var healer = World.Mobiles.Values.OfType<NewHavenHealer>().Single();
            var approach = new Point3D(3462, 2558, 35);
            Require(healer.Location == NewHavenHealer.House && healer.Map.CanFit(approach, 16, false, false),
                "The healer house has no clear resurrection approach");
            var before = player.Location;
            player.MoveToWorld(approach, healer.Map);
            healer.OnMovement(player, before);
            Require(player.FindGump<MerrynResurrectionGump>() != null, "Walking to Merryn did not open her resurrection offer");
        });
        Register("resurrection-declined", () =>
        {
            var ghost = Avatar;
            Require(!ghost.Alive && ghost.FindGump<MerrynResurrectionGump>() == null, "Declining did not close Merryn's offer");
            var healer = World.Mobiles.Values.OfType<NewHavenHealer>().Single();
            healer.OnDoubleClick(ghost);
            Require(ghost.FindGump<MerrynResurrectionGump>() != null, "Merryn did not offer resurrection again");
        });
        Register("resurrected", () => Require(Avatar.Alive && Avatar.Hits > 0,
            "Accepting the healer's offer did not resurrect the player"));
        Register("slice", CheckSlice);
        Register("shot", () =>
        {
            var player = Avatar;
            var iolo = CheckCompanion();
            var map = Map.Trammel;
            player.MoveToWorld(new Point3D(5950, 318, 0), map);
            iolo.MoveToWorld(new Point3D(5950, 320, 0), map);
            iolo.AIObject.AITimer.Stop();
            _shotTarget = new HavenEncounter("haven-krulg", 7) { Frozen = true };
            _shotTarget.MoveToWorld(new Point3D(5954, 320, 0), map);
            _beforeShot = _shotTarget.Hits;
            Require(ClassCombat.IsHostile(player, _shotTarget) && iolo.InLOS(_shotTarget), "The companion test shot has no clear target");
            Require(GearEffects.ApplyChill(_shotTarget, Core.TickCount, 3000, player), "Could not set up Guided Arrow selection");
            iolo.Combatant = _shotTarget;
            var weapon = (BaseRanged)iolo.Weapon;
            var duration = CompanionBuild.Timing(iolo, Iolo.CombatClass, CombatAbility.GuidedArrow, weapon).DurationMs;
            Require(weapon.OnSwing(iolo, _shotTarget).TotalMilliseconds == duration, "Iolo's ranged swing did not use his class timing");
        });
        Register("shot-impact", () =>
        {
            Require(_shotTarget != null && _shotTarget.Hits < _beforeShot, "Iolo's crossbow shot did not deal damage");
            Require(CheckCompanion().Mana == 0, "The companion shot requires mana");
            _shotTarget.Delete();
        });
    }

    private static void CheckBanditImpact(long deadline)
    {
        if (_lesserDaemon?.Prepared.Controller.Fault is { } fault)
            _logger.Error("iolo-story-bandit-impact: failed. Lesser Daemon controller stopped: {Fault}", fault);
        else if (_bandit == null || _lesserDaemon == null)
            _logger.Error("iolo-story-bandit-impact: failed. The bandit or daemon is missing");
        else if (_bandit.Deleted || !_bandit.Alive || SkillHealth.Balance256(_bandit) < _beforeDaemon)
        {
            _bandit.Delete();
            _lesserDaemon.Delete();
            _logger.Information("iolo-story-bandit-impact: passed");
        }
        else if (Core.TickCount >= deadline)
            _logger.Error("iolo-story-bandit-impact: failed. Lesser Daemon at {Daemon} did not damage the bandit at {Bandit}",
                _lesserDaemon.Location, _bandit.Location);
        else
            Timer.StartTimer(TimeSpan.FromMilliseconds(100), () => CheckBanditImpact(deadline));
    }

    private static void CheckDeparture()
    {
        var profiles = ClassCreation.Profiles;
        var player = Avatar;
        var companion = CheckCompanion();
        var innIolo = CheckInn();
        var gate = CheckGate();
        var map = Map.Trammel;
        var britain = Array.FindIndex(PMList.Trammel.Entries, entry => entry.Number == 1012004);
        var haven = Array.FindIndex(PMList.Trammel.Entries, entry => entry.Location == gate.Location);
        var destination = PMList.Trammel.Entries[britain].Location;
        var solo = new PlayerMobile { Name = "Solo traveller", Body = 0x190, Player = true };
        var account = new Account("haven-solo-departure", Guid.NewGuid().ToString("N"));
        account[0] = solo;
        profiles.Assign(solo, DiabloClass.Necromancer);
        profiles.AdvanceLevel(solo, 6);
        profiles.MeetIolo(solo.Serial);
        Require(profiles.UnlockNewHavenGate(solo.Serial), "Could not prepare the solo traveller");
        solo.MoveToWorld(gate.Location, map);
        player.MoveToWorld(gate.Location, map);
        companion.MoveToWorld(new Point3D(gate.X + 1, gate.Y, gate.Z), map);
        companion.ControlOrder = OrderType.Guard;
        Require(gate.UseGate(player) && player.FindGump<MoongateGump>() is not null, "The native gate menu did not open");
        var menu = player.FindGump<MoongateGump>();
        var soloReply = (NetState)RuntimeHelpers.GetUninitializedObject(typeof(NetState));
        soloReply.Mobile = solo;
        menu.OnResponse(soloReply, new RelayInfo(0, [britain], [], [], []));
        menu.OnResponse(soloReply, new RelayInfo(1, [haven], [], [], []));
        Require(!profiles.HasDepartedNewHaven(solo.Serial) && innIolo.CanTalkTo(solo),
            "Cancelling or selecting New Haven closed recruitment");

        menu.OnResponse(soloReply, new RelayInfo(1, [britain], [], [], []));
        Require(solo.Location == destination && profiles.HasDepartedNewHaven(solo.Serial)
            && !profiles.IoloCompanion(solo.Serial).IsMobile && !profiles.HasDepartedNewHaven(player.Serial),
            "Solo travel did not record a departure for that character alone");
        solo.MoveToWorld(NewHavenInn.IoloSeat, map);
        Require(!solo.CanSee(innIolo) && !innIolo.CanTalkTo(solo), "Iolo remained recruitable after leaving alone");

        menu.OnResponse(player.NetState, new RelayInfo(1, [britain], [], [], []));
        player.CloseGump<MoongateGump>();
        Require(profiles.HasDepartedNewHaven(player.Serial) && player.Location == destination
            && companion.Map == player.Map && companion.InRange(player, 1) && MercenaryCare.IsCompanion(player, companion),
            "Iolo did not travel to Britain with the Avatar");
    }

    private static PublicMoongate CheckGate()
    {
        var gate = World.Items.Values.OfType<PublicMoongate>().Single(item => item.Map == Map.Trammel
            && item.Location == NewHavenPopulation.IslandGate);
        var britain = PMList.Trammel.Entries.Single(entry => entry.Number == 1012004);
        Require(World.Items.Values.OfType<PublicMoongate>().Count(item => item.Map == Map.Trammel
            && item.Location == britain.Location) == 1, "Britain has no single return gate");
        return gate;
    }

    private static void CheckSlice()
    {
        string[] areas = ["New Haven Graveyard", "South Farm", "Old Haven", "New Haven Mine", "Lower Mine"];
        int[] minimum = [1, 4, 4, 6, 8];
        int[] maximum = [3, 6, 6, 8, 10];
        for (int i = 0; i < areas.Length; i++)
        {
            var spawners = World.Items.Values.OfType<BaseSpawner>().Where(s => s.Map == Map.Trammel
                && s.Name?.StartsWith(areas[i], StringComparison.Ordinal) == true && !s.Name.EndsWith("Wildlife")).ToArray();
            Require(spawners.Length > 0 && spawners.All(s => s.IsFull), $"Unfilled spawners in {areas[i]}");
            var levels = spawners.SelectMany(s => s.Spawned.Keys).Cast<HavenEncounter>().Select(mob => CreatureProfiles.Level(mob)).ToArray();
            Require(levels.Min() == minimum[i] && levels.Max() == maximum[i], $"Wrong level range in {areas[i]}");
        }

        var player = new PlayerMobile { Name = "New Haven check", Body = 0x190, Player = true };
        player.AddItem(new Backpack());
        var map = Map.Trammel;
        try
        {
            // Check map geometry before enabling class movement, which also checks for nearby mobiles.
            CheckDoor(new(3506, 2526, 27), Direction.North, Direction.South);
            CheckDoor(new(3462, 2562, 35), Direction.North, Direction.South);
            CheckDoor(new(3491, 2571, 21), Direction.East, Direction.West);
            CheckDoor(new(3481, 2568, 20), Direction.North, Direction.South);
            CheckDoor(new(3497, 2549, 20), Direction.North, Direction.South);
            CheckDoor(new(3497, 2556, 20), Direction.North, Direction.South);
            foreach (var (id, name) in new[]
            {
                ("haven-bandit", "a farm bandit's corpse"), ("haven-skeleton", "a skeleton's corpse"), ("haven-zombie", "a zombie's corpse")
            })
            {
                var encounter = World.Mobiles.Values.OfType<HavenEncounter>().First(mob => mob.Dungeon.Id == id);
                Require(Corpse.GetCorpseName(encounter) == name, $"Wrong corpse label for {id}");
            }
            var surface = WalkableFrom(new Point3D(3503, 2574, 14), new Rectangle2D(3390, 2438, 370, 252));
            Require(surface.Contains(NewHavenPopulation.MineDoor) && surface.Any(p => p.X == 3710 && p.Y == 2621)
                && surface.Any(p => p.X == 3598 && p.Y == 2452), "The surface route cannot reach the mine, farm, or graveyard");
            Require(surface.Contains(new Point3D(3462, 2558, 35)), "Town roads cannot reach the healer's house");
            var upperMine = WalkableFrom(NewHavenPopulation.MineArrival, new Rectangle2D(5986, 299, 16, 47));
            Require(upperMine.Contains(NewHavenPopulation.RopeDown), "The mine entrance does not lead to the rope");
            var lowerMine = WalkableFrom(NewHavenPopulation.RopeUp, new Rectangle2D(5939, 304, 26, 32));
            var chamber = new Point3D(5947, 311, 0);
            Require(lowerMine.Any(p => p.Z == chamber.Z && Utility.InRange(p, chamber, 2)
                && EncounterProfiles.ClearSight(map, p, chamber)), "The rope does not lead to Krulg's chamber");
            ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
            Follow(NewHavenPopulation.MineDoor, NewHavenPopulation.MineArrival, false);
            Follow(NewHavenPopulation.RopeDown, NewHavenPopulation.RopeUp, true);
            Follow(NewHavenPopulation.RopeUp, NewHavenPopulation.RopeDown, true);
            Follow(NewHavenPopulation.MineExit, NewHavenPopulation.MineReturn, false);
            var krulg = World.Mobiles.Values.OfType<HavenEncounter>().Single(mob => mob.Name == "Boss Krulg");
            player.MoveToWorld(new Point3D(krulg.X + 1, krulg.Y, krulg.Z), map);
            ClassCreation.Profiles.AdvanceLevel(player, 9);
            var corpse = GearDropChecks.Body(krulg, player, krulg.Location);
            try
            {
                Require(krulg.Grade == EncounterGrade.SuperUnique && CreatureProfiles.Level(krulg) == 10,
                    "Krulg is not a level-10 super-unique");
                Require(RewardLife.Process(krulg, corpse, new Random(42))
                    && corpse.Items.OfType<Gold>().Sum(gold => gold.Amount) > 0
                    && corpse.Items.Any(item => ClassGear.State(item) is { Roll.Rarity: not GearRarity.Normal }),
                    "Krulg did not produce gold, magic-or-better equipment, and XP");
            }
            finally { corpse.Delete(); }
        }
        finally { player.Delete(); }

        foreach (var (role, tool) in new[] { (MarketRole.Lumberjack, "Hatchet"), (MarketRole.Miner, "Pickaxe") })
        {
            Require(TownMarkets.Profiles.At($"Trammel/New Haven/{role}") is { CanTeach: true }
                && TownMarkets.Supplies.Any(supply => supply.Type == tool && supply.Roles.Contains(role)),
                $"Missing New Haven {role} tool or training");
        }
        Require(TownMarkets.Profiles.At("Trammel/New Haven/Smith") is { CanTeach: true }
            && TownMarkets.Profiles.At("Trammel/New Haven/Tinker") is { CanTeach: true }, "Missing trade instructors");
        Require(Mining.System.Definitions.Any(definition => definition.Validate(map.Tiles.GetLandTile(5993, 335).ID, true)),
            "The main mine has no mineable land at the entrance gallery");
        Require(World.Items.Values.OfType<BaseSpawner>().Count(s => s.Map == map && s.Name?.EndsWith("Wildlife") == true && s.IsFull) >= 4,
            "Haven wildlife spawners are missing or empty");
        Require(World.Items.Values.OfType<BaseDoor>().Any(door => door.Map == map && door.InRange(new Point3D(3626, 2621, 0), 3)),
            "The Old Haven decoration doors were not installed");

        void CheckDoor(Point3D threshold, Direction first, Direction second)
        {
            var door = FindDoor(threshold);
            var approach = threshold;
            Server.Movement.Movement.Offset(first, ref approach);
            Require(!Server.Movement.Movement.CheckMovement(player, map, approach, second, out _),
                $"Closed door at {threshold} did not block the threshold");
            door.Open = true;
            try
            {
                bool entered = Server.Movement.Movement.CheckMovement(player, map, approach, second, out int z);
                bool crossed = Server.Movement.Movement.CheckMovement(player, map, new Point3D(threshold.X, threshold.Y, z),
                    second, out int farZ);
                Require(entered && z == threshold.Z && crossed,
                    $"Open door at {threshold} had entry {entered} at z={z}, exit {crossed} at z={farZ}");
            }
            finally { door.Open = false; }
        }

        HashSet<Point3D> WalkableFrom(Point3D start, Rectangle2D bounds)
        {
            var visited = new HashSet<Point3D> { start };
            var remaining = new Queue<Point3D>();
            remaining.Enqueue(start);
            while (remaining.TryDequeue(out var point))
                for (int i = 0; i < 8; i++)
                {
                    var direction = (Direction)i;
                    var next = point;
                    Server.Movement.Movement.Offset(direction, ref next);
                    if (!bounds.Contains(next) || !Server.Movement.Movement.CheckMovement(player, map, point, direction, out int z))
                        continue;
                    next.Z = z;
                    if (visited.Add(next))
                        remaining.Enqueue(next);
                }
            return visited;
        }

        void Follow(Point3D from, Point3D to, bool rope)
        {
            Require(map.CanFit(from, 16, checkMobiles: false) && map.CanFit(to, 16, checkMobiles: false),
                $"Blocked mine passage {from} to {to}");
            var item = World.Items.Values.OfType<Teleporter>().Single(item => item.Map == map
                && item.Location == from && item.PointDest == to);
            player.MoveToWorld(from, map);
            if (rope)
                ((InteractionTeleporter)item).OnDoubleClick(player);
            else
                item.OnMoveOver(player);
            Require(player.Location == to && player.Map == map, $"Could not use mine passage from {from}");
        }
    }

    private static Iolo CheckInn()
    {
        var iolo = World.Mobiles.Values.OfType<Iolo>().Single(actor => !actor.Recruited);
        Require(iolo.Location == NewHavenInn.IoloSeat && iolo.Map == Map.Trammel && iolo.CantWalk, "Iolo is not at his table");
        Require(World.Items.Values.OfType<BaseSign>().Count(
            sign => sign.Map == Map.Trammel && sign.Name == NewHavenInn.Name && sign.InRange(NewHavenInn.EntranceSign, 5)) == 1,
            "The inn sign was missing or duplicated");
        Require(World.Mobiles.Values.OfType<HarvestBarkeeper>().Count() == 1
            && World.Mobiles.Values.OfType<HarvestResident>().Count() == 3, "The inn staff or patrons were missing");
        Require(World.Mobiles.Values.OfType<GraveyardUndead>().Any(actor => actor.Map == Map.Trammel
            && actor.InRange(new Point3D(3598, 2450, 16), 12)), "The New Haven graveyard was empty");
        return iolo;
    }

    private static Iolo CheckCompanion()
    {
        var player = Avatar;
        var iolo = World.Mobiles.Values.OfType<Iolo>().Single(actor => actor.Recruited);
        Require(ClassCreation.Profiles.IoloCompanion(player.Serial) == iolo.Serial && MercenaryCare.IsCompanion(player, iolo),
            "The saved companion does not match the owner's active Iolo");
        Require(iolo.FindItemOnLayer(Layer.TwoHanded) is Crossbow, "Iolo did not keep his crossbow");
        return iolo;
    }

    private static void Register(string suffix, Action check)
    {
        string command = $"iolo-story-{suffix}";
        ConsoleInputHandler.RegisterCommand(command, "Check Iolo in the disposable New Haven world", _ =>
            Core.LoopContext.Post(() =>
            {
                try { check(); _logger.Information("{Command}: passed", command); }
                catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
            }));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
