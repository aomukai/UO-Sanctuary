// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Accounting;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class BritainStoryChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(BritainStoryChecks));
    private static PlayerMobile Player => (PlayerMobile)Accounts.GetAccount("classui")[0];
    private static CharacterProfiles Profiles => ClassCreation.Profiles;
    private static Item _unidentified;
    private static int _gold;
    private static HavenEncounter _foe, _secondFoe;
    private static WarlockGoatmanBody _travelSummon;
    private static long _urgentDeadline;
    private static readonly StoryDiscoveries _chapter =
        StoryDiscoveries.MetGeoffrey | StoryDiscoveries.RoyalAudience | StoryDiscoveries.IoloRetired;

    public static void Configure()
    {
        Register("mounted-runebook", CheckMountedRunebook);
        Register("bridge", () =>
        {
            var player = Player;
            var map = Map.Trammel;
            var iolo = (Iolo)MercenaryCare.ActiveStoryCompanion(player);
            var college = World.Mobiles.Values.OfType<CollegeIolo>().Single();
            var recruit = World.Mobiles.Values.OfType<Wolfgang>().Single();
            Require(World.Mobiles.Values.OfType<BritainSentry>().Count() == 2
                && !player.CanSee(college) && !recruit.Recruited && Wolfgang.Join(player) == null,
                "Chapter residents or recruitment stage were wrong");
            var geometry = new PlayerMobile { Body = 0x190 };
            try
            {
                Reachable(geometry, new(1344, 1742, 20), new(1338, 1741, 13, 9), BritainStory.CaptainPost);
                Reachable(geometry, new(1363, 1625, 50), new(1319, 1608, 50, 40), BritainStory.Throne);
                Reachable(geometry, new(1439, 1565, 30), new(1432, 1545, 24, 27), BritainStory.CollegePost);
                Require(map.CanSpawnMobile(BritainStory.CollegeRune), "The marked college landing is blocked");
            }
            finally { geometry.Delete(); }

            iolo.MoveToWorld(new Point3D(1387, 1746, 1), map);
            player.MoveToWorld(new Point3D(1388, 1747, 1), map);
            Require(Profiles.GreetedBritain(player.Serial), "The actual bridge movement did not trigger Iolo's greeting");
        });
        Register("geoffrey", () =>
        {
            var player = Player;
            player.MoveToWorld(new Point3D(1344, 1744, 20), Map.Trammel);
            var geoffrey = World.Mobiles.Values.OfType<Geoffrey>().Single();
            Require(ScriptedDialogue.CanTalk(player, geoffrey), "Geoffrey cannot be reached from the tower entrance");
            ScriptedDialogue.Open(player, geoffrey);
            Require((Profiles.Discoveries(player.Serial) & _chapter) == StoryDiscoveries.MetGeoffrey,
                "Meeting Geoffrey did not unlock exactly his discovery");
        });
        Register("throne", () =>
        {
            var player = Player;
            var map = Map.Trammel;
            var iolo = (Iolo)MercenaryCare.ActiveStoryCompanion(player);
            player.MoveToWorld(new Point3D(1326, 1625, 55), map);
            iolo.MoveToWorld(new Point3D(1326, 1624, 55), map);
            var king = World.Mobiles.Values.OfType<LordBritish>().Single();
            Require(ScriptedDialogue.CanTalk(player, king) && Iolo.NearbyCompanion(player) == iolo,
                "The player and Iolo cannot seek an audience from the dais");
            var before = LordBritish.AudienceFor(player, iolo);
            Require(!before[1].Line.Contains("ogre"), "The report spoiled the mine before the kill");
            Profiles.RecordHavenMineVisit(player.Serial);
            Profiles.RecordKrulgDefeat(player.Serial, iolo.Serial);
            var after = LordBritish.AudienceFor(player, iolo);
            Require(after[1].Line.Contains("We fought an ogre") && after[4].Line.Contains("Geoffrey"),
                "The royal audience did not reflect previous discoveries");
            var potion = new RestorationPotion(RestorationKind.Healing, 1);
            player.Backpack.DropItem(potion);
            Require(MercenaryCare.Deposit(player, iolo, potion), "Could not prepare Iolo's potion stock");
            ScriptedDialogue.Open(player, king);
        });
        Register("retired", () =>
        {
            var player = Player;
            var iolo = (Iolo)World.FindMobile(Profiles.IoloCompanion(player.Serial));
            Require((Profiles.Discoveries(player.Serial) & _chapter) == _chapter
                && !MercenaryCare.HasActiveCompanion(player) && iolo.Map == Map.Internal
                && Profiles.HasDepartedNewHaven(player.Serial) && MercenaryCare.Stock(iolo) == 1,
                "The audience did not journal, retire Iolo, or preserve his stock");
        });
        Register("college", () =>
        {
            var player = Player;
            player.MoveToWorld(new Point3D(1439, 1556, 32), Map.Trammel);
            var bard = World.Mobiles.Values.OfType<CollegeIolo>().Single();
            Require(ScriptedDialogue.CanTalk(player, bard), "College Iolo is inaccessible after retirement");
            player.Backpack.DropItem(new Gold(92));
            _gold = player.Backpack.GetAmount(typeof(Gold));
            _unidentified = IdentificationChecks.Unknown(player, new Longsword());
            ScriptedDialogue.Open(player, bard);
            var book = player.Backpack.FindItemByType<Runebook>();
            Require(book?.Entries is [var rune] && rune.Location == BritainStory.CollegeRune && rune.Map == Map.Trammel
                && book.CurCharges == 0 && book.MaxCharges == 0 && !CollegeIolo.GiveRunebook(player), "Missing, charged, or duplicate runebook gift");
        });
        Register("free-id", () =>
        {
            var player = Player;
            int gold = player.Backpack.GetAmount(typeof(Gold));
            bool identified = ClassGear.State(_unidentified).Identified;
            Require(identified && gold == _gold, $"College Iolo appraisal had identified={identified}, gold={gold} (was {_gold})");
        });
        Register("recover", () =>
        {
            var bard = World.Mobiles.Values.OfType<CollegeIolo>().Single();
            ScriptedDialogue.Open(Player, bard);
        });
        Register("runebook", () =>
        {
            var player = Player;
            player.MoveToWorld(new Point3D(1439, 1565, 30), Map.Trammel);
            OpenRunebook(player);
        });
        Register("runebook-arrived", () => Require(Player.Location == BritainStory.CollegeRune
            && Player.Spell == null && Player.Backpack.FindItemByType<Runebook>()?.CurCharges == 0,
            "Travel did not reach the college without a spell or charges"));
        Register("wolfgang-offer", () =>
        {
            var player = Player;
            var retired = (Iolo)World.FindMobile(Profiles.IoloCompanion(player.Serial));
            Require(MercenaryCare.Stock(retired) == 0
                && player.Backpack.FindItemByType<SkullCap>()?.Name == "Iolo's checked cap"
                && player.Backpack.FindItemByType<RestorationPotion>() is { Amount: 1 },
                "Iolo withheld the player's equipment or stored potion");
            player.MoveToWorld(new Point3D(1344, 1744, 20), Map.Trammel);
            ScriptedDialogue.Open(player, World.Mobiles.Values.OfType<Geoffrey>().Single());
        });
        Register("wolfgang-joined", () =>
        {
            var player = Player;
            var wolfgang = CheckWolfgang(player);
            Require(!Profiles.WolfgangOpenedUp(player.Serial)
                && !player.CanSee(World.Mobiles.Values.OfType<Wolfgang>().Single(actor => !actor.Recruited)),
                "The tower's apprentice remained visible or his past was revealed too early");

            FixtureSpace.Corridor(player, 6560, 14, 3610);
            wolfgang.MoveToWorld(new Point3D(player.X - 1, player.Y, player.Z), player.Map);
            _foe = MakeFoe(player, wolfgang.X, wolfgang.Y + 1);
            _secondFoe = MakeFoe(player, player.X + 1, player.Y);
            wolfgang.Combatant = _foe;
            wolfgang.OnThink();
            Require(BarbarianShout.State(player)?.Supplier == wolfgang.Serial.Value
                && BarbarianShout.Defense(wolfgang) > 0 && _secondFoe.Combatant == wolfgang,
                "Wolfgang did not bolster the player and draw an ordinary foe");
            wolfgang.Damage(wolfgang.HitsMax * 9 / 10, _foe);
            _urgentDeadline = BarkDeadline(wolfgang);
            Require(wolfgang.Hits * 4 < wolfgang.HitsMax && _urgentDeadline > Core.TickCount,
                "Wolfgang did not ask for urgent help after the hit");
        });
        Register("wolfgang-win", () =>
        {
            var wolfgang = CheckWolfgang(Player);
            Require(_foe.Hits < _foe.HitsMax || _secondFoe.Hits < _secondFoe.HitsMax,
                $"Wolfgang's timed melee missed both foes. He is at {wolfgang.Location} fighting {wolfgang.Combatant}.");
            wolfgang.Damage(1, _foe.Alive ? _foe : _secondFoe);
            Require(BarkDeadline(wolfgang) == _urgentDeadline, "Wolfgang repeated his urgent line in the same fight");
            _foe.Damage(10_000, Player);
            _secondFoe.Damage(10_000, Player);
            wolfgang.Combatant = null;
            wolfgang.Hits = wolfgang.HitsMax;
        });
        Register("wolfgang-trust", () =>
        {
            var player = Player;
            Require(Profiles.WolfgangOpenedUp(player.Serial), "A difficult victory did not unlock Wolfgang's past");
            ScriptedDialogue.Open(player, CheckWolfgang(player));
        });
        Register("runebook-followers", () =>
        {
            var player = Player;
            var wolfgang = CheckWolfgang(player);
            player.MoveToWorld(new Point3D(6560, 3610, 0), Map.Trammel);
            wolfgang.MoveToWorld(new Point3D(player.X - 6, player.Y, player.Z), player.Map);
            ClassSkillChecks.Train(player, "warlock.summongoatman");
            _travelSummon = WarlockGoatmanWorld.Create(player, new Point3D(player.X + 6, player.Y, player.Z), TimedEffects.Now);
            WarlockGoatmanWorld.Admit(_travelSummon);
            Require(_travelSummon.ControlMaster == player, "Could not prepare a summon for runebook travel");
            var book = player.Backpack.FindItemByType<Runebook>();
            book.MaxCharges = 12;
            book.CurCharges = 2;
            OpenRunebook(player);
        });
        Register("runebook-party-arrived", () =>
        {
            var player = Player;
            var wolfgang = CheckWolfgang(player);
            Require(player.Location == BritainStory.CollegeRune && wolfgang.Map == player.Map && wolfgang.InRange(player, 4)
                && _travelSummon.ControlMaster == player && !_travelSummon.Deleted
                && (MinionSafeAreas.Parked(_travelSummon) || _travelSummon.Map == player.Map && _travelSummon.InRange(player, 4))
                && player.Backpack.FindItemByType<Runebook>()?.CurCharges == 2, "Runebook travel lost a follower or used legacy charges");
        });
        Register("world-and-pack", () =>
        {
            CheckWorld();
            var player = Player;
            var wolfgang = CheckWolfgang(player);
            var pack = wolfgang.Backpack;
            player.MoveToWorld(new Point3D(6560, 3610, 0), Map.Trammel);
            wolfgang.MoveToWorld(new Point3D(player.X - 1, player.Y, player.Z), player.Map);
            Require(pack is { Movable: false }, "Wolfgang had no equipped backpack");
            player.Use(pack);
            Require(pack.Openers?.Contains(player) == true, "The owner could not open Wolfgang's backpack");
            var outsider = new PlayerMobile { Body = 0x190 };
            try
            {
                outsider.MoveToWorld(player.Location, player.Map);
                Require(!pack.CheckItemUse(outsider) && !wolfgang.CheckNonlocalLift(outsider, pack),
                    "Another player could access Wolfgang's supplies");
            }
            finally { outsider.Delete(); }

            var potion = new RestorationPotion(RestorationKind.Healing, 1) { Amount = 2 };
            player.Backpack.DropItem(potion);
            player.NextActionTime = 0;
            player.Lift(potion, 2, out bool rejected, out _);
            Require(!rejected && player.Drop(wolfgang, Point3D.Zero) && potion.IsChildOf(pack),
                "Dropping a potion on Wolfgang did not pack it");
            var bottled = new GreaterHealPotion();
            player.Backpack.DropItem(bottled);
            player.NextActionTime = 0;
            player.Lift(bottled, 1, out rejected, out _);
            Require(!rejected && player.Drop(pack, new Point3D(32, 32, 0)) && bottled.IsChildOf(pack),
                "Dropping a potion into the open backpack did not pack it");
            wolfgang.Hits = wolfgang.HitsMax / 3;
            Require(MercenaryCare.Stock(wolfgang) == 3 && MercenaryCare.TryDrink(wolfgang, TimedEffects.Now)
                && MercenaryCare.Stock(wolfgang) == 2 && potion.Amount == 1, "Wolfgang did not use a single packed healing dose");
            wolfgang.Hits = wolfgang.HitsMax;

            var foe = MakeFoe(player, player.X + 5, player.Y);
            try
            {
                foe.Combatant = null;
                wolfgang.Combatant = null;
                wolfgang.AIObject.DoOrderGuard();
                Require(wolfgang.Combatant == foe && wolfgang.ActiveMoveSpeed < 0.1,
                    "Wolfgang did not notice and rush the visible enemy before being attacked");
            }
            finally { foe.Delete(); }
        });
        Register("reload", () =>
        {
            var player = Player;
            player.MoveToWorld(player.LogoutLocation, player.LogoutMap);
            player.ClaimAutoStabledPets();
            Require((Profiles.Discoveries(player.Serial) & _chapter) == _chapter && Profiles.WolfgangOpenedUp(player.Serial)
                && Profiles.GreetedBritain(player.Serial) && Profiles.ReceivedIoloRunebook(player.Serial)
                && player.Backpack.FindItemByType<Runebook>()?.Entries.Count == 1, "The saved Britain chapter was incomplete");
            Require(CheckWolfgang(player).ControlOrder == OrderType.Guard, "Wolfgang did not reload with Guard order");
            Require(World.Mobiles.Values.OfType<Geoffrey>().Count() == 1 && World.Mobiles.Values.OfType<LordBritish>().Count() == 1
                && World.Mobiles.Values.OfType<CollegeIolo>().Count() == 1 && World.Mobiles.Values.OfType<BritainSentry>().Count() == 2,
                "Restart duplicated Britain residents");
            if (Environment.GetEnvironmentVariable("SANCTUARY_VERIFY_BRITANNIA_WORLD") == "1")
            {
                CheckWorld();
                Require(MercenaryCare.Stock(CheckWolfgang(player)) == 2, "Wolfgang's physical supplies did not survive restart");
            }
        });
    }

    private static void CheckWorld()
    {
        var map = Map.Trammel;
        var stock = World.Items.Values.Where(item => item.Map == map && !item.Deleted).ToArray();
        int scenery = stock.Count(item => item is BaseDoor or LocalizedSign);
        int population = stock.Count(item => item is BaseSpawner && item.Name == "Britannia town resident");
        Require(scenery > 1_000 && population > 300
            && stock.Any(item => item is LocalizedSign && item.Location == new Point3D(1360, 1776, 15))
            && World.Mobiles.Values.OfType<Banker>().Any(banker => banker.Map == map && banker.Region.IsPartOf("Britain"))
            && World.Mobiles.Values.OfType<BaseVendor>().Any(vendor => vendor.Map == map && vendor.Region.IsPartOf("Trinsic"))
            && stock.Any(item => item is BaseSpawner && item.Name == "Britannia Wildlife"), "Britannia scenery or town life was incomplete");
    }

    private static HavenEncounter MakeFoe(PlayerMobile player, int x, int y)
    {
        var foe = new HavenEncounter("haven-bandit", 5);
        foe.MoveToWorld(new Point3D(x, y, player.Z), player.Map);
        foe.Frozen = true;
        foe.Combatant = player;
        return foe;
    }

    private static void OpenRunebook(PlayerMobile player)
    {
        var book = player.Backpack.FindItemByType<Runebook>();
        NecromancerEffects.SetMana256(player, 0);
        book.OnDoubleClick(player);
        Require(player.FindGump<RunebookGump>()?.Book == book, "The runebook did not open");
    }

    private static void CheckMountedRunebook()
    {
        var map = Map.Trammel;
        var origin = new Point3D(6560, 3610, 0);
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = "Mounted runebook fixture" };
        player.AddItem(new Backpack());
        var book = new Runebook();
        player.Backpack.DropItem(book);
        var copy = new Runebook();
        var horse = new Horse();
        var dog = new Dog();
        var dungeonEntrance = new Teleporter(new Point3D(5518, 674, 20), map);
        try
        {
            ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
            var legacyCollege = new RunebookEntry(book, BritainStory.CollegeRune, map);
            var legacyCove = new RunebookEntry(book, CoveGateRune.Gates, map);
            var dismountCove = new RunebookEntry(book, CoveGateRune.Gates, map, mountedTravel: RuneMountTravel.DismountRequired);
            Require(!RunebookTravel.AllowsMountedTravel(legacyCollege) && RunebookTravel.AllowsMountedTravel(legacyCove)
                && !RunebookTravel.AllowsMountedTravel(dismountCove), "Legacy fallback overrode an authored mounted-travel rule");

            player.MoveToWorld(origin, map);
            var rune = new CoveGateRune();
            player.Backpack.DropItem(rune);
            Require(book.OnDragDrop(player, rune) && book.Entries[0].MountedTravel == RuneMountTravel.Allowed,
                "Filing the Cove rune lost its mounted-travel flag");
            book.OnAfterDuped(copy);
            Require(copy.Entries[0].MountedTravel == RuneMountTravel.Allowed, "Copying the book lost the rune flag");
            book.DropRune(player, book.Entries[0], 0);
            var looseRune = player.Backpack.FindItemByType<RecallRune>();
            Require(looseRune?.MountedTravel == RuneMountTravel.Allowed && book.OnDragDrop(player, looseRune),
                "Removing and refiling a rune lost its mounted-travel flag");

            horse.MoveToWorld(origin, map);
            Require(MountTravel.Adopt(player, horse) && dog.SetControlMaster(player), "Could not set up travel followers");
            horse.OnDoubleClick(player);
            dog.MoveToWorld(new Point3D(origin.X + 6, origin.Y, origin.Z), map);
            Require(player.Mount == horse && !dungeonEntrance.CanTeleport(player), "Mounting or dungeon restriction failed");
            ClassMapHooks.RunebookTravel(player, legacyCollege);
            Require(player.Location == origin && player.Mount == horse, "The indoor college rune let a rider through");
            ClassMapHooks.RunebookTravel(player, book.Entries[0]);
            Require(player.Location == CoveGateRune.Gates && player.Mount == horse && horse.Rider == player
                && dog.Map == player.Map && dog.InRange(player, 4), "Cove travel lost the rider, mount, or follower");
            horse.Rider = null;
            ClassMapHooks.RunebookTravel(player, legacyCollege);
            Require(player.Location == BritainStory.CollegeRune, "The college rune failed on foot");
        }
        finally
        {
            dungeonEntrance.Delete();
            dog.Delete();
            horse.Delete();
            copy.Delete();
            player.Delete();
        }
    }

    private static long BarkDeadline(Wolfgang wolfgang) =>
        (long)typeof(Wolfgang).GetField("_nextBark", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(wolfgang);

    private static Wolfgang CheckWolfgang(PlayerMobile player)
    {
        var wolfgang = MercenaryCare.ActiveStoryCompanion(player) as Wolfgang;
        Require(wolfgang != null && MercenaryCare.IsCompanion(player, wolfgang)
            && CreatureProfiles.Level(wolfgang) == ClassStats.Level(player)
            && wolfgang.RawStr == ClassStats.For(DiabloClass.Barbarian).Strength + 3 * (ClassStats.Level(player) - 1)
            && wolfgang.ManaMax == 0 && wolfgang.DefenseBonus > 0 && wolfgang.Weapon is Broadsword,
            "Wolfgang did not use the level-matched Barbarian build");
        return wolfgang;
    }

    private static void Reachable(PlayerMobile player, Point3D start, Rectangle2D area, Point3D target)
    {
        var closed = new List<BaseDoor>();
        foreach (var door in Map.Trammel.GetItemsInBounds<BaseDoor>(area))
            if (!door.Open && !door.Locked)
                closed.Add(door);
        try
        {
            foreach (var door in closed)
                door.Open = true;
            Walkable(player, start, area, target);
        }
        finally
        {
            foreach (var door in closed)
                door.Open = false;
        }
    }

    private static void Walkable(PlayerMobile player, Point3D start, Rectangle2D area, Point3D target)
    {
        var map = Map.Trammel;
        var todo = new Queue<Point3D>();
        var seen = new HashSet<Point3D> { start };
        todo.Enqueue(start);
        while (todo.TryDequeue(out var point))
        {
            if (Math.Abs(point.Z - target.Z) < 10 && Utility.InRange(point, target, 2)
                && EncounterProfiles.ClearSight(map, point, target))
                return;
            for (int i = 0; i < 8; i++)
            {
                var direction = (Direction)i;
                var next = point;
                Server.Movement.Movement.Offset(direction, ref next);
                if (!area.Contains(next) || !Server.Movement.Movement.CheckMovement(player, map, point, direction, out int z))
                    continue;
                next.Z = z;
                if (seen.Add(next))
                    todo.Enqueue(next);
            }
        }
        throw new InvalidOperationException($"No walkable route from {start} to {target}");
    }

    private static void Register(string suffix, Action check)
    {
        string name = "iolo-story-britain-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Check the Britain story in an isolated test world", _ =>
            Core.LoopContext.Post(() =>
            {
                try { check(); _logger.Information("{Command}: passed", name); }
                catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
            }));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
