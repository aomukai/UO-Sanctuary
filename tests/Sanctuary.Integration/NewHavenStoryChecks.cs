using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Server;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
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
    private static HavenEncounter _assistTarget;
    private static HavenEncounter _barkTarget;
    private static MercenaryCareState _barkCare;
    private static long _urgentBarkDeadline;
    private static WarlockGoatmanBody _assistDaemon;
    private static WarlockGoatmanBody _mineDaemon;
    private static Point3D _mineDaemonExit;
    private static Item _ioloWeapon;
    private static Corpse _ropeCorpse;
    private static HavenEncounter _krulg;
    private static Point3D _krulgStart;
    private static Item _appraisalItem, _secondAppraisalItem;
    private static int _serviceGold;
    private static BaseDoor FindDoor(Point3D threshold) => World.Items.Values.OfType<BaseDoor>()
        .Single(door => door.Map == Map.Trammel && door.Location == threshold);

    public static void Configure()
    {
        Register("scholar-setup", () =>
        {
            var player = Avatar;
            var scholar = TownMarkets.Profiles.At("Trammel/New Haven/Scholar") as Scholar;
            Require(scholar != null, "Elara was not installed");
            player.MoveToWorld(new Point3D(3485, 2495, 52), Map.Trammel);
            player.NextActionTime = 0;
            Require(TownMarkets.Trading(scholar, player), $"Scholar at {scholar.Location} is inaccessible");
            player.Backpack.DropItem(new Gold(500));
            _serviceGold = player.Backpack.GetAmount(typeof(Gold));
            _appraisalItem = IdentificationChecks.Unknown(player, new Longsword());
            _secondAppraisalItem = IdentificationChecks.Unknown(player, new LeatherCap());
        });
        Register("npc-speech", () =>
        {
            var player = Avatar;
            var gambler = new TavernGambler();
            var dog = new Dog();
            try
            {
                gambler.MoveToWorld(player.Location, player.Map);
                Require(dog.SetControlMaster(player), "Could not prepare the speech test pet");
                dog.MoveToWorld(player.Location, player.Map);
                dog.ControlOrder = OrderType.Stay;
                player.DoSpeech("vendor buy", [0x3C], MessageType.Regular, 0x3B2);
                player.DoSpeech("all follow me", [0x16C], MessageType.Regular, 0x3B2);
                Require(player.FindGump<GamblingGump>() == null && dog.ControlOrder == OrderType.Stay,
                    "Normal chat still triggered a vendor or pet command");
            }
            finally
            {
                gambler.Delete();
                dog.Delete();
            }
        });
        Register("scholar-paid", () =>
        {
            var player = Avatar;
            Require(_appraisalItem.IsChildOf(player.Backpack) && _secondAppraisalItem.IsChildOf(player.Backpack)
                && ClassGear.State(_appraisalItem).Identified && ClassGear.State(_secondAppraisalItem).Identified,
                $"Appraisal did not identify both selected items in place. Avatar is at {player.Location}, "
                + $"gear identified = {ClassGear.State(_appraisalItem).Identified}/{ClassGear.State(_secondAppraisalItem).Identified}.");
            Require(player.Backpack.GetAmount(typeof(Gold)) == _serviceGold - 2 * Scholar.IdentificationFee,
                "The scholar charged the wrong total");
        });
        Register("training-setup", () =>
        {
            var player = Avatar;
            var alchemist = TownMarkets.Profiles.At("Trammel/New Haven/Alchemist");
            player.MoveToWorld(new Point3D(3483, 2494, 52), Map.Trammel);
            player.NextActionTime = 0;
            Require(player.InLOS(alchemist) && player.InRange(alchemist, 3), "Cannot approach the alchemy trainer");
            Require(!ClassProfessions.Profiles.Find(player).Confirmed, "First craft already chosen");
            _serviceGold = player.Backpack.GetAmount(typeof(Gold));
        });
        Register("training-cancelled", () =>
        {
            var player = Avatar;
            Require(!ClassProfessions.Profiles.Find(player).Confirmed && player.Skills[SkillName.Alchemy].BaseFixedPoint == 0
                && player.Backpack.GetAmount(typeof(Gold)) == _serviceGold, "Cancelling chose a profession or spent gold");
        });
        Register("training-paid", () =>
        {
            var player = Avatar;
            Require(ClassProfessions.Profiles.Find(player) is { Confirmed: true, Craft: SkillName.Alchemy }
                && player.Skills[SkillName.Alchemy].BaseFixedPoint == 300
                && player.Backpack.GetAmount(typeof(Gold)) == _serviceGold - 300,
                "The lesson did not cap skill and payment at 30.0 and 300 gold");
        });
        Register("miner-setup", () =>
        {
            var player = Avatar;
            var camp = NewHavenPopulation.MountainCamp;
            player.MoveToWorld(new Point3D(camp.X, camp.Y + 3, camp.Z), Map.Trammel);
            player.NextActionTime = 0;
            foreach (var miner in World.Mobiles.Values.OfType<MineCampMiner>())
                Require(ScriptedDialogue.CanTalk(player, miner), $"Cannot speak to {miner.Name} at {miner.Location}");
            var companion = CheckCompanion();
            companion.MoveToWorld(new Point3D(camp.X + 2, camp.Y + 5, camp.Z), Map.Trammel);
            Require(Iolo.IsTravelingWith(player), "Iolo is not available to speak at the camp");
        });
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
            CheckIoloMineTopics(player, companion, explored: false);
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
            Require(companion.ControlOrder == OrderType.Guard, "Claiming Iolo left him in passive Follow");
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
            Require(companion.ControlOrder == OrderType.Guard, "Iolo did not resume Guard on reload");
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
        Register("mine-topics", () =>
        {
            var player = Avatar;
            var iolo = CheckCompanion();
            CheckIoloMineTopics(player, iolo, explored: false);
            var map = Map.Trammel;
            iolo.MoveToWorld(new Point3D(5954, 309, 0), map);
            player.MoveToWorld(new Point3D(5954, 310, 0), map);
            CheckIoloMineTopics(player, iolo, explored: false);

            var krulg = new HavenEncounter("haven-krulg", 7);
            krulg.MoveToWorld(new Point3D(5954, 307, 0), map);
            try
            {
                krulg.Damage(krulg.HitsMax * 2, iolo);
                Require(!krulg.Alive || krulg.Deleted, "Iolo's killing blow did not defeat Krulg");
                CheckIoloMineTopics(player, iolo, explored: true);
                var profiles = ClassCreation.Profiles;
                Require(profiles.HasInvestigatedHavenMine(player.Serial, out var witness) && witness == iolo.Serial,
                    "The Avatar did not receive credit for Iolo's killing blow or his presence");
                var solo = new PlayerMobile { Body = 0x190, Player = true };
                try
                {
                    profiles.Assign(solo, DiabloClass.Amazon);
                    profiles.RecordKrulgDefeat(solo.Serial, default);
                    CheckIoloMineTopics(solo, iolo, explored: false);
                    solo.MoveToWorld(player.Location, map);
                    CheckIoloMineTopics(solo, iolo, explored: true);
                    var script = iolo.CompanyFor(solo);
                    var ogre = script[0].Replies.Single(reply => reply.Text == "Ogre");
                    Require(script[ogre.Next].Line.Contains("thou hast described"),
                        "Iolo claimed to have witnessed the Avatar's solo fight");
                }
                finally { solo.Delete(); }
            }
            finally { krulg.Delete(); }
        });
        Register("mine-topics-reload", () =>
        {
            var player = Avatar;
            player.MoveToWorld(player.LogoutLocation, player.LogoutMap);
            player.ClaimAutoStabledPets();
            var companion = CheckCompanion();
            CheckIoloMineTopics(player, companion, explored: true);
            Require(ClassCreation.Profiles.HasInvestigatedHavenMine(player.Serial, out var witness)
                && witness == companion.Serial, "The saved story lost Iolo's witness");
        });
        Register("tower-start", () =>
        {
            var map = Map.Trammel;
            var player = Avatar;
            var approach = new Point3D(1344, 1742, 20);
            player.MoveToWorld(approach, map);
            Require(Server.Movement.Movement.CheckMovement(player, map, approach, Direction.South, out int thresholdZ)
                && thresholdZ == 20
                && Server.Movement.Movement.CheckMovement(player, map, new Point3D(1344, 1743, 20), Direction.South, out int insideZ)
                && insideZ == 20, "The northern opening at the Britain tower is obstructed");
        });
        Register("tower-entered", () => Require(Avatar.X is >= 1343 and <= 1346 && Avatar.Y is >= 1744 and <= 1747
            && Avatar.Z == 20, $"The Avatar stopped at {Avatar.Location}"));
        Register("gate-ui-setup", () =>
        {
            var player = Avatar;
            var profiles = ClassCreation.Profiles;
            profiles.AdvanceLevel(player, 6);
            profiles.MeetIolo(player.Serial);
            profiles.UnlockNewHavenGate(player.Serial);
            var gate = CheckGate();
            player.MoveToWorld(gate.Location, gate.Map);
            Require(gate.UseGate(player) && player.FindGump<MoongateGump>() != null, "The Britannia travel menu did not open");
        });
        Register("gate-ui-arrived", () =>
        {
            var player = Avatar;
            var britain = PMList.Trammel.Entries.Single(entry => entry.Number == 1012004);
            Require(player.Map == Map.Trammel && player.Location == britain.Location
                && ClassCreation.Profiles.HasDepartedNewHaven(player.Serial), "Travel did not land at Britain");
        });
        BaseDoor innDoor = null;
        Register("doorway", () =>
        {
            var player = Avatar;
            var iolo = CheckCompanion();
            innDoor = FindDoor(new(3506, 2526, 27));
            innDoor.Open = false;
            player.MoveToWorld(new Point3D(3506, 2528, 27), Map.Trammel);
            iolo.MoveToWorld(new Point3D(3506, 2526, 27), player.Map);
        });
        Register("crossed-doorway", () =>
        {
            var player = Avatar;
            Require(innDoor.Open && player.Location == new Point3D(3506, 2525, 27),
                $"Holding movement on the closed inn door did not open it and stop on the inside tile: {player.Location}");
        });
        Register("returned-through-doorway", () =>
        {
            var player = Avatar;
            Require(innDoor.Open && player.Location == new Point3D(3506, 2527, 27),
                $"Double-clicking the open inn door did not stop on the outside tile: {player.Location}");
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
            var summon = new Point3D(player.X + 3, player.Y, player.Z);
            _lesserDaemon = SummonLesserDaemon(player, summon);
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
        Register("mine-start", () =>
        {
            var entrance = NewHavenPopulation.MineDoor;
            Avatar.MoveToWorld(new Point3D(entrance.X + 1, entrance.Y + 3, entrance.Z), Map.Trammel);
        });
        Register("mine-shaft-start", () =>
        {
            var shaft = NewHavenPopulation.MineShaft;
            Avatar.MoveToWorld(new Point3D(shaft.X + 2, shaft.End.Y + 1, 0), Map.Trammel);
        });
        Register("mine-entered", () => Require(Avatar.InRange(NewHavenPopulation.MineArrival, 2),
            $"Walking into the mine stopped at {Avatar.Location}"));
        SkeletonGuardian[] castImps = null;
        WarlockTaintedBody firstSuccubus = null;
        Register("succubus-start", () =>
        {
            var player = Avatar;
            if (ClassStats.Level(player) < 27)
                ClassCreation.Profiles.AdvanceLevel(player, 27);
            FixtureSpace.Corridor(player, 6560, 28, 3500);
            ClassSkillChecks.Train(player, "warlock.demonicmastery", 5);
            ClassSkillChecks.Train(player, "warlock.summontainted");
            Require(SkillBindings.Instance.Assign(player, false, CombatAbility.SummonTainted), "Could not bind Succubus to right-click");
            NecromancerEffects.SetMana256(player, player.ManaMax * 256);
            castImps = new SkeletonGuardian[2];
            for (int i = 0; i < castImps.Length; i++)
            {
                var imp = WarlockGoatmanWorld.Create(player, new(player.X + 2 + i, player.Y + 2, player.Z), TimedEffects.Now);
                WarlockGoatmanWorld.Admit(imp);
                imp.Frozen = true;
                castImps[i] = imp;
            }
        });
        Register("succubus-first", () =>
        {
            Require(castImps[0].Deleted && !castImps[1].Deleted, "Right-click did not replace the first imp");
            var player = Avatar;
            firstSuccubus = WarlockGoatmanWorld.ForTainted(player).Single();
            firstSuccubus.Frozen = true;
            firstSuccubus.MoveToWorld(new(player.X + 3, player.Y, player.Z), player.Map);
        });
        Register("succubus-low-mana", () => NecromancerEffects.SetMana256(Avatar,
            SkillMechanics.CastManaCost256(Avatar, CombatAbility.SummonTainted) - 4 * 256));
        Register("succubus-refill", () => NecromancerEffects.SetMana256(Avatar, Avatar.ManaMax * 256));
        Register("succubus-second", () =>
        {
            var roster = WarlockGoatmanWorld.Demons(Avatar);
            Require(castImps[1].Deleted && roster is [WarlockTaintedBody, WarlockTaintedBody] && roster[0] == firstSuccubus,
                "Right-click over a friendly Succubus did not replace the second imp");
            foreach (var demon in roster)
                demon.Delete();
        });
        Register("rope-start", () =>
        {
            var player = Avatar;
            var rope = NewHavenPopulation.RopeDown;
            var elementals = World.Mobiles.Values.OfType<HavenEncounter>().Where(mob => mob.Dungeon.Id == "haven-elemental").ToArray();
            foreach (var elemental in elementals)
            {
                elemental.MoveToWorld(elemental.Home, elemental.Map);
                elemental.Frozen = true;
            }
            var corpseSource = elementals[0];
            _ropeCorpse = GearDropChecks.Body(corpseSource, player, rope);
            corpseSource.MoveToWorld(corpseSource.Home, Map.Trammel);
            var iolo = CheckCompanion();
            iolo.Frozen = true;
            iolo.MoveToWorld(new Point3D(rope.X - 1, rope.Y + 1, rope.Z), Map.Trammel);
            player.MoveToWorld(new Point3D(rope.X - 1, rope.Y, rope.Z), Map.Trammel);
        });
        Register("rope-arrived", () =>
        {
            Require(Avatar.Location == NewHavenPopulation.RopeUp, $"The rope under a corpse could not be used: {Avatar.Location}");
            Require(!World.Mobiles.Values.OfType<HavenEncounter>().Any(mob => mob.Dungeon.Id == "haven-lizardman"
                && mob.Combatant == Avatar), "Lizardmen acquired the Avatar at the rope landing");
            CheckCompanion().Frozen = false;
            foreach (var mob in World.Mobiles.Values.OfType<HavenEncounter>())
                if (mob.Dungeon.Id == "haven-elemental")
                    mob.Frozen = false;
            _ropeCorpse.Delete();
        });
        Register("krulg-start", () =>
        {
            var player = Avatar;
            _krulg = World.Mobiles.Values.OfType<HavenEncounter>().Single(mob => mob.Name == "Boss Krulg");
            player.MoveToWorld(new Point3D(5954, 311, 0), _krulg.Map);
            Require(player.CanSee(_krulg) && player.InLOS(_krulg) && !player.InRange(_krulg, 1), "Krulg's approach fixture is blocked");
            _krulgStart = _krulg.Location;
        });
        ConsoleInputHandler.RegisterCommand("iolo-story-krulg-attacks", "Wait for Krulg to approach and attack", _ =>
            Core.LoopContext.Post(() => CheckKrulgAttack(Core.TickCount + 10000)));
        Register("mountain-mine-start", () =>
        {
            var mouth = NewHavenPopulation.MountainMineMouth;
            int x = mouth.X + 3, y = mouth.Y + 1;
            Avatar.MoveToWorld(new Point3D(x, y, Map.Trammel.GetAverageZ(x, y)), Map.Trammel);
        });
        Register("mountain-mine-entered", () => Require(Avatar.InRange(NewHavenPopulation.MountainMineArrival, 2),
            $"Walking into the mountain stopped at {Avatar.Location}"));
        Register("mountain-mine-minion", () =>
        {
            var player = Avatar;
            var mouth = NewHavenPopulation.MountainMineMouth;
            var map = Map.Trammel;
            int y = mouth.Y + 1;
            FixtureSpace.Corridor(player, 6560, 28, 3500);
            var demon = SummonLesserDaemon(player, new Point3D(player.X + 3, player.Y, player.Z));
            int x = mouth.X + 6;
            bool passed = false;
            try
            {
                player.MoveToWorld(new Point3D(mouth.X + 1, y, map.GetAverageZ(mouth.X + 1, y)), map);
                demon.MoveToWorld(new Point3D(x, mouth.Y, map.GetAverageZ(x, mouth.Y)), map);
                Require(!player.InRange(demon, 3), "The daemon fixture is within the native pet transfer range");
                int slots = player.Followers;
                SkillHealth.SetBalance256(demon, SkillHealth.Balance256(demon) - 257);
                int life = SkillHealth.Balance256(demon);
                player.Direction = Direction.West;
                player.Move(Direction.West);
                Require(player.Location == NewHavenPopulation.MountainMineArrival && !MinionSafeAreas.Contains(player),
                    $"The mountain passage put the Avatar in {player.Region.Name} at {player.Location}");
                CheckDaemon();

                var arrival = NewHavenPopulation.MountainMineArrival;
                player.MoveToWorld(new Point3D(arrival.X + 1, arrival.Y, arrival.Z), map);
                WarlockGoatmanWorld.Forget(demon);
                DemonNativeState.Controller(demon, demon.Prepared.Controller with { Fault = DeathMarkWorld.BlockedWalk + "legacy save" });
                WarlockGoatmanWorld.Track(demon);
                player.Direction = Direction.West;
                player.Move(Direction.West);
                Require(player.Location == mouth, $"The mountain exit returned the Avatar to {player.Location}");
                CheckDaemon();
                WarlockGoatmanWorld.Tick(TimedEffects.Now);
                Require(demon.Prepared.Controller.Fault == null, "The old blocked-step fault survived dungeon travel");
                _mineDaemon = demon;
                _mineDaemonExit = demon.Location;
                passed = true;

                void CheckDaemon() => Require(WarlockGoatmanWorld.For(player).Single() == demon
                    && demon.Map == map && player.InRange(demon, 4) && !MinionSafeAreas.Parked(demon)
                    && player.Followers == slots && SkillHealth.Balance256(demon) == life,
                    $"The daemon was left at {demon.Location} ({demon.Map}, {demon.ControlOrder})");
            }
            finally { if (!passed) demon.Delete(); }
        });
        ConsoleInputHandler.RegisterCommand("iolo-story-mountain-minion-follows", "Walk the Avatar away from the mine",
            _ => Core.LoopContext.Post(() => WalkMineExit(8, Core.TickCount + 10000)));
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
        Register("iolo-ghost-setup", () =>
        {
            var player = Avatar;
            var iolo = CheckCompanion();
            player.MoveToWorld(new Point3D(3462, 2559, 35), Map.Trammel);
            iolo.MoveToWorld(new Point3D(3461, 2559, 35), Map.Trammel);
            Require(player.InLOS(iolo), "Iolo's ghost is not visible from the healer's house");
            _ioloWeapon = iolo.FindItemOnLayer(Layer.TwoHanded);
        });
        Register("iolo-died", () =>
        {
            var player = Avatar;
            var iolo = CheckCompanion();
            iolo.Kill();
            Require(iolo.IsDeadPet && iolo.FindItemOnLayer(Layer.TwoHanded) == _ioloWeapon,
                "Iolo did not leave an equipped spirit");
            using var entries = PooledRefList<ContextMenuEntry>.Create();
            var menu = entries;
            iolo.GetContextMenuEntries(player, ref menu);
            Require(menu.Count == 0, "Iolo's ghost offered the native pet menu");
            var healer = World.Mobiles.Values.OfType<NewHavenHealer>().Single();
            Require(MercenaryCare.Quote(player, iolo, healer) is { Gold: 0, Resurrection: true },
                "Merryn cannot restore the nearby spirit free of charge");
        });
        Register("iolo-healer-offer", () => Require(Avatar.FindGump<MerrynResurrectionGump>() != null,
            "Clicking Merryn did not offer to resurrect Iolo"));
        Register("iolo-healer-declined", () => Require(CheckCompanion().IsDeadPet,
            "Declining Merryn's offer resurrected Iolo"));
        Register("iolo-healer-resurrected", () =>
        {
            var iolo = CheckCompanion();
            Require(!iolo.IsDeadPet && iolo.Hits > 0 && iolo.ControlOrder == OrderType.Guard
                && iolo.ControlTarget == Avatar && iolo.FindItemOnLayer(Layer.TwoHanded) == _ioloWeapon,
                "Merryn did not return Iolo with his gear and guarding order intact");
        });
        Register("slice", CheckSlice);
        Register("tutorial-xp", CheckTutorialExperience);
        Register("assist", () =>
        {
            var player = Avatar;
            var iolo = CheckCompanion();
            FixtureSpace.Corridor(player, 6560, 28, 3500);
            _assistDaemon = WarlockGoatmanWorld.Create(player, new Point3D(player.X + 3, player.Y, player.Z), TimedEffects.Now);
            WarlockGoatmanWorld.Admit(_assistDaemon);
            var map = player.Map;
            iolo.MoveToWorld(new Point3D(player.X, player.Y + 2, player.Z), map);
            Require(iolo.ControlOrder == OrderType.Guard && iolo.ControlTarget == player, "Iolo is not guarding the Witch");
            _assistDaemon.MoveToWorld(new Point3D(player.X + 4, player.Y + 1, player.Z), map);
            _assistTarget = new HavenEncounter("haven-krulg", 7) { Frozen = true, NoKillAwards = true };
            _assistTarget.MoveToWorld(new Point3D(player.X + 4, player.Y + 2, player.Z), map);
            _assistTarget.AIObject.AITimer.Stop();
            _assistTarget.SetHits(5000);
            _assistTarget.Combatant = _assistDaemon;
            Require(ClassCombat.IsHostile(player, _assistTarget) && iolo.InLOS(_assistTarget)
                && _assistTarget.Combatant == _assistDaemon, "The Witch's daemon is not holding the companion test target");
            Require(GearEffects.ApplyChill(_assistTarget, Core.TickCount, 3000, _assistDaemon), "Could not prepare the companion test shot");
        });
        ConsoleInputHandler.RegisterCommand("iolo-story-assist-impact", "Wait for Iolo's autonomous Witch-party shot", _ =>
            Core.LoopContext.Post(() => CheckAssistImpact(Core.TickCount + 6500)));
        Register("bark-stocked", () =>
        {
            var player = Avatar;
            var iolo = CheckCompanion();
            FixtureSpace.Corridor(player, 6560, 14, 3600);
            iolo.MoveToWorld(new Point3D(player.X - 2, player.Y, player.Z), player.Map);
            iolo.Frozen = true;
            iolo.AIObject.AITimer.Stop();
            _barkTarget = new HavenEncounter("haven-bandit", 4) { Frozen = true, NoKillAwards = true };
            _barkTarget.MoveToWorld(new Point3D(player.X - 4, player.Y, player.Z), player.Map);
            _barkTarget.AIObject.AITimer.Stop();
            iolo.Combatant = _barkTarget;
            Require(iolo.Combatant == _barkTarget, "Iolo did not engage the bark test enemy");
            _barkCare = MercenaryCare.States[iolo.Serial];
            MercenaryCare.States[iolo.Serial] = _barkCare with { Healing = [1, 0, 0, 0, 0], NextDrinkUtc = TimedEffects.Now.AddMinutes(1) };
            iolo.Hits = iolo.HitsMax / 3;
            iolo.OnThink();
            Require(BarkWarning(iolo) == 0, "Iolo asked for healing with a potion in his pouch");
        });
        Register("bark-composed", () =>
        {
            var iolo = CheckCompanion();
            MercenaryCare.States[iolo.Serial] = _barkCare with { Healing = [0, 0, 0, 0, 0] };
            iolo.Hits = iolo.HitsMax / 3;
            iolo.OnThink();
            Require(BarkWarning(iolo) == 1, "Empty pouch and low health did not trigger a composed bark");
            Require(BarkDeadline(iolo, "_nextComposedBark") >= Core.TickCount + 119_900,
                "The composed bark did not set a two-minute cooldown");
        });
        Register("bark-urgent", () =>
        {
            var iolo = CheckCompanion();
            Require(Core.TickCount < BarkDeadline(iolo, "_nextBark"), "The ordinary bark cooldown expired before the urgency check");
            iolo.Damage(iolo.Hits - iolo.HitsMax / 5, _barkTarget);
            Require(BarkWarning(iolo) == 2, "The urgent line did not fire immediately during the ordinary cooldown");
            _urgentBarkDeadline = BarkDeadline(iolo, "_nextBark");
        });
        Register("bark-cleanup", () =>
        {
            var iolo = CheckCompanion();
            iolo.Damage(1, _barkTarget);
            Require(BarkDeadline(iolo, "_nextBark") == _urgentBarkDeadline, "Iolo repeated his urgent bark in the same fight");
            _barkTarget.Delete();
            iolo.Combatant = null;
            iolo.Hits = iolo.HitsMax;
            MercenaryCare.States[iolo.Serial] = _barkCare;
            iolo.Frozen = false;
            iolo.AIObject.AITimer.Activate();
        });
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

    private static int BarkWarning(Iolo iolo) =>
        (int)typeof(Iolo).GetField("_warningSpoken", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(iolo);

    private static long BarkDeadline(Iolo iolo, string name) =>
        (long)typeof(Iolo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(iolo);

    private static void CheckKrulgAttack(long deadline)
    {
        var player = Avatar;
        if (_krulg.Location != _krulgStart && player.Aggressors.Any(info => info.Attacker == _krulg))
            _logger.Information("iolo-story-krulg-attacks: passed");
        else if (Core.TickCount >= deadline)
            _logger.Error("iolo-story-krulg-attacks: failed. Krulg moved from {Start} to {Current}, target {Target}",
                _krulgStart, _krulg.Location, _krulg.Combatant?.Name);
        else
            Timer.StartTimer(TimeSpan.FromMilliseconds(100), () => CheckKrulgAttack(deadline));
    }

    private static void WalkMineExit(int steps, long deadline)
    {
        var player = Avatar;
        var demon = _mineDaemon;
        if (steps > 0 && Core.TickCount < deadline)
        {
            player.Direction = Direction.East;
            player.Move(Direction.East);
            Timer.StartTimer(TimeSpan.FromMilliseconds(230), () => WalkMineExit(steps - 1, deadline));
        }
        else if (player.X >= NewHavenPopulation.MountainMineMouth.X + 7 && demon.Location != _mineDaemonExit
            && player.InRange(demon, 4) && demon.Prepared.Controller.Fault == null)
        {
            var point = NewHavenPopulation.MountainMineMouth;
            Teleportation.CommitTransfer(player, point, player.Location, player.Map);
            if (player.Location == point && player.InRange(demon, 4))
                _logger.Information("iolo-story-mountain-minion-follows: passed");
            else
                _logger.Error("iolo-story-mountain-minion-follows: failed. A spell teleport left behind the same daemon");
            demon.Delete();
        }
        else if (Core.TickCount >= deadline)
        {
            _logger.Error("iolo-story-mountain-minion-follows: failed. Daemon {Origin} to {Location}, Avatar {Avatar}, fault {Fault}",
                _mineDaemonExit, demon.Location, player.Location, demon.Prepared.Controller.Fault);
            demon.Delete();
        }
        else
            Timer.StartTimer(TimeSpan.FromMilliseconds(150), () => WalkMineExit(0, deadline));
    }

    private static void CheckAssistImpact(long deadline)
    {
        var iolo = CheckCompanion();
        if (_assistTarget.FindDamageEntryFor(iolo) is { DamageGiven: > 0 })
        {
            _assistTarget.Delete();
            _assistDaemon.Delete();
            _logger.Information("iolo-story-assist-impact: passed");
            return;
        }
        if (Core.TickCount >= deadline)
            _logger.Error("iolo-story-assist-impact: failed. Iolo at {Location} with order {Order} and combatant {Combatant} did not damage the enemy fighting the Witch's daemon",
                iolo.Location, iolo.ControlOrder, iolo.Combatant?.Name);
        else
            Timer.StartTimer(TimeSpan.FromMilliseconds(100), () => CheckAssistImpact(deadline));
    }

    private static WarlockGoatmanBody SummonLesserDaemon(PlayerMobile player, Point3D point)
    {
        ClassSkillChecks.Train(player, "warlock.summongoatman", 1);
        ClassCombat.Stop(player);
        var session = ClassCombat.For(player);
        session.NextAction = session.RecoveryUntil = Core.TickCount;
        NecromancerEffects.SetMana256(player, player.ManaMax * 256);
        Require(ClassCombat.Perform(player, CombatAbility.SummonGoatman, null, point, Core.TickCount)
            && ClassCombat.ResolveAction(player, session.EffectAt), "The Witch could not summon her Lesser Daemon");
        return WarlockGoatmanWorld.For(player).Single();
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

    private static void CheckIoloMineTopics(PlayerMobile player, Iolo iolo, bool explored)
    {
        var script = iolo.CompanyFor(player);
        Require(script.All(node => node.Replies.All(reply => reply.Text != "Graveyard")
            && node.Replies.Any(reply => reply.Text == "Equipment")), "Iolo's companion topics still include Graveyard or omit Equipment");
        Require(script[0].Replies.Any(reply => reply.Text == "Ogre") == explored
            && script[2].Line.Contains("mine") == explored, "Iolo's mine lines did not match the Avatar's discoveries");
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
        companion.MoveToWorld(new Point3D(gate.X + 6, gate.Y, gate.Z), map);
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

        var dog = new Dog();
        try
        {
            Require(dog.SetControlMaster(player), "Could not prepare the moongate test pet");
            dog.MoveToWorld(new Point3D(gate.X + 7, gate.Y, gate.Z), map);
            dog.ControlOrder = OrderType.Stay;
            menu.OnResponse(player.NetState, new RelayInfo(1, [britain], [], [], []));
            player.CloseGump<MoongateGump>();
            Require(profiles.HasDepartedNewHaven(player.Serial) && player.Location == destination
                && companion.Map == player.Map && companion.InRange(player, 4) && MercenaryCare.IsCompanion(player, companion),
                "Iolo did not travel to Britain with the Avatar");
            Require(dog.ControlMaster == player && dog.Map == player.Map && dog.InRange(player, 4),
                "The moongate left a distant tamed pet behind");
            var active = (IDictionary)typeof(ScriptedDialogue).GetField("_sessions", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);
            var conversation = active[player.Serial];
            Require(conversation?.GetType().GetProperty("Speaker")?.GetValue(conversation) == companion
                && active[solo.Serial] == null && !profiles.RecordNewHavenDeparture(player.Serial),
                $"Iolo's dialogue did not open privately after the first journey ({companion.Location} to {player.Location})");
        }
        finally { dog.Delete(); }
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

    private static void CheckTutorialExperience()
    {
        var profiles = ClassCreation.Profiles;
        var player = new PlayerMobile { Name = "Tutorial XP check", Body = 0x190, Player = true };
        player.AddItem(new Backpack());
        profiles.Assign(player, DiabloClass.Necromancer);
        player.MoveToWorld(new Point3D(3598, 2452, 16), Map.Trammel);
        try
        {
            profiles.AwardExperience(player, (int)ExperienceLevels.Threshold(Iolo.RequiredLevel - 1));
            CheckReward("haven-skeleton", 2, 5);
            CheckReward("haven-elemental", 6, 5);
            profiles.TryGet(player.Serial, out var before);
            profiles.AwardExperience(player, (int)(ExperienceLevels.Threshold(Iolo.RequiredLevel) - before.Experience - 1));
            CheckReward("haven-skeleton", 2, 5);
            Require(profiles.HasReached(player.Serial, Iolo.RequiredLevel), "The boosted kill did not reach Iolo's level");
            CheckReward("haven-skeleton", 2, 2);
            CheckReward("haven-elemental", 6, 3);
        }
        finally { player.Delete(); }

        void CheckReward(string species, int level, int multiplier)
        {
            var monster = new HavenEncounter(species, level);
            var corpse = GearDropChecks.Body(monster, player, player.Location);
            try
            {
                profiles.TryGet(player.Serial, out var before);
                int xp = MonsterExperience.Award(monster.Basis.Experience * multiplier, level, before.Level);
                Require(RewardLife.Process(monster, corpse, new Random(42))
                    && profiles.TryGet(player.Serial, out var after) && after.Experience - before.Experience == xp,
                    $"Expected {multiplier}× base XP from {species} at Avatar level {before.Level}");
            }
            finally { corpse.Delete(); monster.Delete(); }
        }
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
            // Combat earlier in the disposable world can leave a pending respawn.
            foreach (var spawner in spawners)
                for (int attempt = 0; attempt < spawner.Count && !spawner.IsFull; attempt++)
                    spawner.Spawn();
            Require(spawners.Length > 0 && spawners.All(s => s.IsFull), $"Unfilled spawners in {areas[i]}");
            var enemies = spawners.SelectMany(s => s.Spawned.Keys).Cast<HavenEncounter>().ToArray();
            var levels = enemies.Select(mob => CreatureProfiles.Level(mob)).ToArray();
            Require(levels.Min() == minimum[i] && levels.Max() == maximum[i], $"Wrong level range in {areas[i]}");
            foreach (var mob in enemies)
            {
                int scaling = i >= 3 ? 3 : 2;
                int eliteLife = EncounterProfiles.LifePermille(mob.Grade, ChampionKind.Champion, MonsterDifficulty.Normal);
                int life = mob.RolledLife * eliteLife / 1000 * scaling;
                int experience = mob.Basis.Experience * scaling * (mob.Leader ? 5 : 1);
                Require(mob.HitsMax == life && CreatureProfiles.Experience(mob) == experience,
                    $"Wrong health or XP scaling for {mob.Name} in {areas[i]}");
            }
        }
        CheckTooltips();

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
            foreach (string role in new[] { "Alchemist", "Scribe", "Scholar" })
            {
                var vendor = TownMarkets.Profiles.At($"Trammel/New Haven/{role}");
                Require(vendor != null && surface.Any(point => point.Z == vendor.Z && Utility.InRange(point, vendor.Location, 2)
                    && EncounterProfiles.ClearSight(map, point, vendor.Location)),
                    $"Town roads cannot reach {role} at the mage tower ({vendor?.Location})");
            }
            Require(TownMarkets.Supplies.Where(supply => supply.Roles.Contains(MarketRole.Scholar))
                .Select(supply => supply.Type).ToHashSet().SetEquals(["IdentifyScroll", "TownPortalScroll"]),
                "The scholar's scroll stock is wrong");
            Require(World.Mobiles.Values.OfType<MineCampMiner>().Where(miner => miner.Map == map
                && miner.InRange(NewHavenPopulation.MountainCamp, 3)).Select(miner => miner.Name).Order().SequenceEqual(["Bess", "Garran"]),
                "Expected two distinct miners around the campfire");
            var upperMine = WalkableFrom(NewHavenPopulation.MineArrival, new Rectangle2D(5986, 299, 16, 47));
            Require(upperMine.Contains(NewHavenPopulation.RopeDown), "The mine entrance does not lead to the rope");
            var mountainMine = WalkableFrom(NewHavenPopulation.MountainMineArrival, new Rectangle2D(5903, 268, 100, 110));
            Require(mountainMine.Contains(NewHavenPopulation.MineArrival), "The mountain entrance does not reach the upper galleries");
            var lowerMine = WalkableFrom(NewHavenPopulation.RopeUp, new Rectangle2D(5939, 304, 26, 32));
            var elementals = World.Mobiles.Values.OfType<HavenEncounter>().Where(mob => mob.Dungeon.Id == "haven-elemental").ToArray();
            Require(elementals.Length == 12 && elementals.All(mob => mountainMine.Contains(mob.Home))
                && elementals.Any(mob => mob.Home.X < 5925) && elementals.Any(mob => mob.Home.Y > 358)
                && elementals.Any(mob => mob.Home.Y < 285), "Elementals do not populate the full upper mine loop");
            var lizardmen = World.Mobiles.Values.OfType<HavenEncounter>().Where(mob => mob.Dungeon.Id == "haven-lizardman").ToArray();
            Require(lizardmen.Length == 8 && lizardmen.All(mob => !Utility.InRange(mob.Home, NewHavenPopulation.RopeUp,
                mob.RangePerception + mob.RangeHome + 1)), "Lizardmen can gather in aggro range of the rope landing");
            var krulg = World.Mobiles.Values.OfType<HavenEncounter>().Single(mob => mob.Name == "Boss Krulg");
            var chamber = krulg.Home;
            Require(lowerMine.Any(p => p.Z == chamber.Z && Utility.InRange(p, chamber, 2)
                && EncounterProfiles.ClearSight(map, p, chamber)), "The rope does not lead to Krulg's chamber");
            ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
            Require(ClassMapHooks.PublicGateWorlds(player) is [{ Name: "Britannia" } world] && world.Map == map,
                "The public selector should offer Britannia");
            for (int offset = -1; offset <= 1; offset++)
            {
                var door = NewHavenPopulation.MineDoor;
                WalkThrough(new Point3D(door.X + offset, door.Y, door.Z), NewHavenPopulation.MineArrival, Direction.North);
            }
            var shaft = NewHavenPopulation.MineShaft;
            for (int x = shaft.X; x < shaft.End.X; x++)
            {
                WalkThrough(new Point3D(x, shaft.Y - 1, 0), NewHavenPopulation.MineArrival, Direction.South);
                WalkThrough(new Point3D(x, shaft.End.Y, 0), NewHavenPopulation.MineArrival, Direction.North);
            }
            for (int y = shaft.Y; y < shaft.End.Y; y++)
            {
                WalkThrough(new Point3D(shaft.X - 1, y, 0), NewHavenPopulation.MineArrival, Direction.East);
                WalkThrough(new Point3D(shaft.End.X, y, 0), NewHavenPopulation.MineArrival, Direction.West);
            }
            ClimbRope(NewHavenPopulation.MineShaftRope, NewHavenPopulation.MineArrival,
                new Point3D(shaft.X + 2, shaft.End.Y, 0));
            ClimbRope(NewHavenPopulation.RopeDown, NewHavenPopulation.RopeUp, NewHavenPopulation.RopeDown);
            ClimbRope(NewHavenPopulation.RopeUp, NewHavenPopulation.RopeDown, NewHavenPopulation.RopeUp);
            WalkThrough(NewHavenPopulation.MineExit, NewHavenPopulation.MineReturn, Direction.South);
            var mouth = NewHavenPopulation.MountainMineMouth;
            int adjacentY = mouth.Y + 1;
            WalkThrough(mouth, NewHavenPopulation.MountainMineArrival, Direction.West);
            WalkThrough(new Point3D(mouth.X, adjacentY, map.GetAverageZ(mouth.X, adjacentY)),
                NewHavenPopulation.MountainMineArrival, Direction.West);
            WalkThrough(NewHavenPopulation.MountainMineArrival, mouth, Direction.West);
            player.MoveToWorld(new Point3D(krulg.X + 1, krulg.Y, krulg.Z), map);
            ClassCreation.Profiles.AdvanceLevel(player, 9);
            var corpse = GearDropChecks.Body(krulg, player, krulg.Location);
            try
            {
                using var properties = new ObjectPropertyList(krulg);
                krulg.GetProperties(properties);
                properties.Terminate();
                Require(krulg.Grade == EncounterGrade.SuperUnique && CreatureProfiles.Level(krulg) == 10
                    && krulg.Modifiers is [var modifier]
                    && Encoding.Unicode.GetString(properties.Buffer.AsSpan(1)).Contains(EncounterProfiles.ModifierLabel(modifier)),
                    "Krulg needs one visible super-unique modifier at level 10");
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
        Require(World.Items.Values.OfType<BaseSpawner>().Count(s => s.Map == map
            && s.Name?.EndsWith("Wildlife") == true && s.Name != "Britannia Wildlife" && s.IsFull) >= 4,
            "Haven wildlife spawners are missing or empty");
        var wildlife = World.Items.Values.OfType<BaseSpawner>().Where(spawner => spawner.Name == "Britannia Wildlife").ToArray();
        Require(wildlife.Length >= 150 && wildlife.Select(spawner => spawner.Guid).Distinct().Count() == wildlife.Length
            && wildlife.All(spawner => spawner.Map == map
                && spawner.Entries.All(entry => entry.SpawnedName is not ("WanderingHealer" or "Orc" or "Mongbat" or "Zombie"))),
            "Expected a single import of animal spawners in Britannia");
        var britainGate = PMList.Trammel.Entries.Single(entry => entry.Number == 1012004).Location;
        Require(wildlife.Any(spawner => spawner.InRange(britainGate, 95) && spawner.Spawned.Count > 0),
            "The woods near Britain have no live wildlife");
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

        void WalkThrough(Point3D from, Point3D to, Direction direction)
        {
            Require(map.CanFit(from, 16, checkMobiles: false) && map.CanFit(to, 16, checkMobiles: false),
                $"Blocked mine passage {from} to {to}");
            var approach = from;
            Server.Movement.Movement.Offset((Direction)(((int)direction + 4) & 7), ref approach);
            if (from == NewHavenPopulation.MountainMineMouth || to == NewHavenPopulation.MountainMineArrival)
                approach.Z = map.GetAverageZ(approach.X, approach.Y);
            player.MoveToWorld(approach, map);
            player.Direction = direction;
            player.Move(direction);
            Require(player.Location == to && player.Map == map, $"Could not walk into mine passage at {from} from {approach}");
        }

        void ClimbRope(Point3D rope, Point3D to, Point3D approach)
        {
            var item = World.Items.Values.OfType<InteractionTeleporter>().Single(item => item.Map == map
                && item.X == rope.X && item.Y == rope.Y && item.Z > rope.Z && item.PointDest == to);
            player.MoveToWorld(approach, map);
            item.OnDoubleClick(player);
            Require(player.Location == to && player.Map == map, $"Could not use the mine rope at {rope}");
        }
    }

    private static void CheckTooltips()
    {
        var boss = new EncounterCreature();
        var immune = new Skeleton();
        var bow = ItemOrigins.Create(ItemOrigin.Drop, () => new MagicalShortbow { Name = "Normal MagicalShortbow" });
        try
        {
            boss.Assign(EncounterProfiles.Profile("crypt-sentinel-60"), 60, MonsterDifficulty.Normal,
                EncounterGrade.Unique, ChampionKind.Champion, [EliteModifier.ExtraStrong, EliteModifier.LightningEnchanted],
                EliteAura.None, "Tooltip Boss", new Random(4));
            string bossText = Encoding.Unicode.GetString(boss.PropertyList.Buffer.AsSpan(1));
            Require(bossText.Contains("Tooltip Boss") && bossText.Contains("Extra Strong") && bossText.Contains("Lightning Enchanted"),
                "Boss tooltip omitted the name or boss mods");

            immune.SetResistance(ResistanceType.Fire, 100);
            immune.SetResistance(ResistanceType.Cold, 120);
            string immuneText = Encoding.Unicode.GetString(immune.PropertyList.Buffer.AsSpan(1));
            Require(immuneText.Contains("Immune to Fire") && immuneText.Contains("Immune to Cold")
                && !immuneText.Contains("Immune to Lightning"), "Ordinary tooltip did not show its exact immunities");

            ClassGear.Profiles.FromMonster(bow, 8, GearRarity.Normal, new Random(3));
            string bowText = Encoding.Unicode.GetString(bow.PropertyList.Buffer.AsSpan(1));
            Require(bowText.Contains(ClassGear.Base(bow).Name) && bowText.Contains("damage") && bowText.Contains("Requires")
                && !bowText.Contains("MagicalShortbow") && !bowText.Contains("affix level") && !bowText.Contains("Base speed modifier"),
                "The converted bow tooltip exposed native or internal roll data");
        }
        finally
        {
            boss.Delete();
            immune.Delete();
            bow.Delete();
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
