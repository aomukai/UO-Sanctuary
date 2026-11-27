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
using Server.Spells;

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
        Register("british-visibility", CheckBritishVisibility);
        Register("blackthorn-relocation", CheckBlackthornRelocation);
        Register("mounted-runebook", CheckMountedRunebook);
        Register("cove-rune", CheckCoveRune);
        Register("island-travel", CheckIslandTravel);
        Register("hythloth-travel", CheckHythlothTravel);
        Register("hythloth-pet-stairs", CheckHythlothPetStairs);
        Register("pet-combat-range", CheckPetCombatRange);
        Register("trash-barrel", () =>
        {
            var post = new Point3D(1424, 1687, 0);
            var barrels = World.Items.Values.OfType<TrashBarrel>().Where(item => item.Location == post
                && (item.Map == Map.Trammel || item.Map == Map.Felucca)).ToArray();
            Require(barrels is [{ Map: var map }] && map == Map.Trammel,
                "Britain's bank should have one trash barrel at its Trammel post");
        });
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

    private static void CheckBritishVisibility()
    {
        var avatar = new PlayerMobile { Body = 0x190, Player = true };
        var other = new PlayerMobile { Body = 0x190, Player = true };
        try
        {
            var king = World.Mobiles.Values.OfType<LordBritish>().Single();
            var audience = new Point3D(1326, 1625, 55);
            avatar.MoveToWorld(audience, king.Map);
            other.MoveToWorld(audience, king.Map);
            Profiles.Assign(avatar, DiabloClass.Paladin);

            Require(avatar.CanSee(king), "Lord British was missing before Leoric");
            BritanniaCampaign.Defeated(avatar, CampaignProgress.SkeletonKing, "King Leoric");
            Require(!avatar.CanSee(king) && other.CanSee(king)
                && BritanniaCampaign.NextSummons(avatar) == BritanniaCampaign.Summons.Blackthorn,
                "Leoric did not hide Lord British for the victor and leave Blackthorn as the next audience");
            BritanniaCampaign.Defeated(avatar, CampaignProgress.Lazarus, "Lazarus");
            Require(!avatar.CanSee(king) && other.CanSee(king), "Lazarus changed Lord British's visibility");
            BritanniaCampaign.Defeated(avatar, CampaignProgress.Diablo, "Diablo");
            Require(avatar.CanSee(king) && other.CanSee(king)
                && BritanniaCampaign.NextSummons(avatar) == BritanniaCampaign.Summons.Victory,
                "Diablo did not restore Lord British and his victory audience");
        }
        finally
        {
            avatar.Delete();
            other.Delete();
        }
    }

    private static void CheckBlackthornRelocation()
    {
        var map = Map.Trammel;
        var player = new PlayerMobile { Body = 0x190, Player = true };
        try
        {
            var blackthorn = World.Mobiles.Values.OfType<CampaignBlackthorn>().Single(npc => npc.Map == map);
            var shamino = World.Mobiles.Values.OfType<CampaignShamino>().Single(npc => npc.Map == map);
            Require(blackthorn.Location == CampaignActors.BlackthornPost && shamino.Location == CampaignActors.ShaminoPost,
                "Blackthorn and Shamino were not moved into Castle Britannia");
            Reachable(player, new(1363, 1625, 50), new(1319, 1608, 50, 40), blackthorn.Location);
            player.MoveToWorld(new Point3D(1326, 1625, 55), map);
            Require(player.CanSee(blackthorn) && player.CanSee(shamino) && player.InLOS(blackthorn) && player.InLOS(shamino),
                "Blackthorn and Shamino cannot be seen from the audience area");

            var doors = World.Items.Values.OfType<MetalDoor>().Where(door => door.Map == map
                && door.Location is { X: 1523 or 1524, Y: 1439, Z: 15 }).ToArray();
            Require(doors.Length == 2 && doors.All(door => door.Locked && !door.Open), "The old keep gate was not sealed");

            var guards = World.Mobiles.Values.OfType<BlackthornBridgeGuard>().Where(guard => guard.Map == map).ToArray();
            Require(guards.Length == 2 && guards.Any(guard => guard.Location == new Point3D(1520, 1506, 5)),
                "Bridge guards were missing");
            foreach (int x in new[] { 1521, 1523, 1525 })
            {
                var approach = new Point3D(x, 1506, 5);
                player.MoveToWorld(approach, map);
                player.Direction = Direction.North;
                Require(player.CheckMovement(Direction.North, out _) && !player.Move(Direction.North) && player.Location == approach,
                    $"A visitor could cross at {approach}");
            }
            Require(!SpellHelper.CheckTravel(player, map, new Point3D(1523, 1441, 0), TravelCheckType.RecallTo, out _),
                "A visitor could recall directly into the closed keep");

            player.MoveToWorld(BlackthornKeepClosure.BridgeApproach, map);
            player.AccessLevel = AccessLevel.GameMaster;
            Require(player.Move(Direction.North) && player.Move(Direction.North) && player.Y == 1504,
                "Staff could not walk past the checkpoint");
            player.AccessLevel = AccessLevel.Player;
            player.Direction = Direction.South;
            Require(player.Move(Direction.South) && player.Move(Direction.South) && player.Y == 1506,
                "A visitor leaving the old keep was blocked at the bridge");
        }
        finally { player.Delete(); }
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

    private static void CheckIslandTravel()
    {
        var map = Map.Trammel;
        var harbourMaster = World.Mobiles.Values.OfType<BritainHarbourMaster>().Single(npc => npc.Map == map);
        var skippers = World.Mobiles.Values.OfType<IslandSkipper>().Where(npc => npc.Map == map).ToArray();
        Require(skippers.Length == 2 && skippers.Count(npc => npc.Dagger) == 1 && skippers[0].Portrait != skippers[1].Portrait,
            "Expected a distinct return skipper on each island");
        Require(!World.Items.Values.OfType<CampaignFerry>().Any(ferry => ferry.Map == map), "An obsolete ferry board is still on Trammel");

        var player = new PlayerMobile { Body = 0x190, Player = true };
        try
        {
            Require(harbourMaster.DialogueFor(player)[0].Replies.All(reply => reply.Select == null),
                "Elowen offered a passage before it was unlocked");
            BritanniaCampaign.Record(player, CampaignProgress.FireIslandPassage);
            Require(harbourMaster.DialogueFor(player)[0].Replies.Where(reply => reply.Select != null).Select(reply => reply.Text)
                .SequenceEqual(["Sail to Fire Island"]), "Elowen did not offer the unlocked Fire Island route");
            BritanniaCampaign.Record(player, CampaignProgress.DaggerIslePassage);
            var travel = harbourMaster.DialogueFor(player)[0].Replies.Where(reply => reply.Select != null).ToArray();
            Require(travel.Select(reply => reply.Text).SequenceEqual(["Sail to Fire Island", "Sail to Dagger Isle"]),
                "Elowen's greeting is missing a ferry destination");
            foreach (var skipper in skippers)
            {
                Require(skipper.Script[0].Replies is [{ Text: "Sail to Britain", Select: not null }],
                    "An island skipper did not offer passage back");
                var harbour = skipper.Dagger ? TownMarkets.Floor(map, new(4110, 439, 0), [], false) : new Point3D(4721, 3822, 0);
                Require(BritanniaCampaign.Sail(player, harbour) && player.InRange(skipper, 3) && player.InLOS(skipper),
                    $"Could not reach {skipper.Name} from the landing at {harbour}");
            }
        }
        finally { player.Delete(); }
    }

    private static void CheckHythlothTravel()
    {
        var map = Map.Trammel;
        var passages = World.Items.Values.OfType<Teleporter>().Where(item => item.Map == map
            && item.Name == "a passage through Hythloth").ToArray();
        Require(passages.Count(item => item.X is >= 4720 and <= 4722 && item.Y == 3814) == 3
            && passages.Count(item => item.PointDest == CampaignDungeonRoutes.HythlothSurfaceLanding) == 3,
            "Obsolete Hythloth entrance or exit lanes were left in the world");

        var player = new PlayerMobile { Body = 0x190, Player = true };
        BaseCreature[] followers = [new Dog(), new SkeletonGuardian(), new SkeletonGuardian()];
        try
        {
            foreach (var pet in followers)
                Require(pet.SetControlMaster(player), "Could not set up a Hythloth follower");
            for (int lane = 0; lane < 3; lane++)
            {
                var ground = new Point3D(4720 + lane, 3814, 0);
                var foot = new Point3D(5904 + lane, 21, 44);
                var exitTile = new Point3D(foot.X, 20, 46);
                var entry = passages.Single(item => item.Location == ground);
                Require(entry.PointDest == foot && map.CanFit(ground, 16, checkMobiles: false)
                    && map.CanFit(foot, 16, checkMobiles: false) && map.CanFit(exitTile, 16, checkMobiles: false),
                    $"Hythloth lane {ground} has a blocked tile");
                player.MoveToWorld(new Point3D(ground.X, 3815, 0), map);
                foreach (var pet in followers)
                    pet.MoveToWorld(new Point3D(ground.X, 3816, 0), map);
                player.Direction = Direction.North;
                player.Move(Direction.North);
                Require(player.Location == foot, $"Walking onto {ground} did not land at the foot of Hythloth's stairs");

                var hall = new Point3D(5905, 24, 44);
                foreach (var pet in followers)
                    Require(pet.Map == map && pet.InRange(foot, 4) && pet.Z <= 46
                        && (pet.InRange(hall, 1) || new MovementPath(pet, hall).Success),
                        $"{pet.Name} could not reach Hythloth's hall from {pet.Location}");

                var exit = passages.Single(item => item.Location == exitTile && item.PointDest == CampaignDungeonRoutes.HythlothSurfaceLanding);
                if (lane == 1)
                    player.Move(Direction.North);
                else
                    exit.OnDoubleClick(player);
                Require(player.Location == CampaignDungeonRoutes.HythlothSurfaceLanding, "The Hythloth exit did not reach the ground");
            }
        }
        finally
        {
            foreach (var pet in followers)
                pet.Delete();
            player.Delete();
        }
    }

    private static void CheckHythlothPetStairs()
    {
        Point3D[] stairs = [new(5960, 42, 0), new(5960, 43, 4), new(5960, 44, 9),
            new(5960, 45, 14), new(5960, 46, 19), new(5960, 47, 22)];
        CheckPetStairRoute([.. stairs, new(5960, 49, 22), new(5960, 52, 22)]);
        Array.Reverse(stairs);
        CheckPetStairRoute([.. stairs, new(5960, 40, 0), new(5960, 38, 0)]);
    }

    private static void CheckPetStairRoute(Point3D[] route)
    {
        var map = Map.Trammel;
        var owner = new PlayerMobile { Body = 0x190, Player = true };
        var dog = new Dog();
        var grizzly = new DruidGrizzlyBody();
        SkeletonWorldBody skeleton = null;
        try
        {
            Profiles.Assign(owner, DiabloClass.Necromancer);
            Profiles.AdvanceLevel(owner, 5);
            ClassSkillChecks.Train(owner, "necromancer.raiseskeleton");
            var at = TimedEffects.Now.AddSeconds(1);
            owner.MoveToWorld(route[^1], map);
            Require(dog.SetControlMaster(owner), "Could not set up the tamed stair fixture");
            dog.MoveToWorld(route[0], map);
            var path = new MovementPath(dog, route[^1]);
            Require(path.Success, $"Tamed pet could not path from {route[0]} to {route[^1]}");
            for (int i = 1; i <= 5; i++)
            {
                var direction = dog.GetDirectionTo(route[i]);
                dog.Direction = direction;
                Require(dog.Move(direction) && dog.Location == route[i], $"Tamed pet could not walk to {route[i]}");
            }
            dog.MoveToWorld(route[0], map);
            dog.IssueOrder(OrderType.Stay, null);
            PetStairTravel.CatchUp(owner, map, route[4], route[5]);
            Require(dog.Location == route[0], "Stair recovery moved a pet ordered to stay");
            dog.IssueOrder(OrderType.Follow, null, owner);
            PetStairTravel.CatchUp(owner, map, route[4], route[5]);
            Require(dog.Z == route[^1].Z && dog.InRange(owner, 4), "Stair recovery left a tamed pet on the other floor");
            dog.Internalize();

            skeleton = SkeletonWorldBody.CreatePrepared(owner, 1, 0, route[0], at, SourceRandom.InitLow(17), 1);
            Require(skeleton != null, "Could not raise the Hythloth stair fixture");

            for (int i = 0; i < route.Length - 3; i++)
            {
                var from = route[i];
                var step = route[i + 1];
                skeleton.MoveToWorld(from, map);
                Require(PetGroundWorld.Point(skeleton, new Point2D(step.X * 2, step.Y * 2), out var projected)
                    && projected == step, $"Pet movement could not resolve the stair tread from {from} to {step}");
                Require(PetGroundWorld.Point(skeleton, new Point2D(step.X * 2, step.Y * 2), out projected, zeroFootprint: true)
                    && projected == step, $"Pet movement without unit collision could not resolve the stair tread at {step}");
                grizzly.MoveToWorld(from, map);
                Require(PetGroundWorld.Point(grizzly, new Point2D(step.X * 2, step.Y * 2), out projected)
                    && projected == step, $"Druid pet movement could not resolve the stair tread at {step}");
            }

            grizzly.Internalize();
            skeleton.MoveToWorld(route[0], map);
            for (int i = 0; i < route.Length; i++)
            {
                owner.MoveToWorld(route[i], map);
                PetOwnerRoutes.Moved(owner, at.AddMilliseconds(200 * (i + 1)));
            }
            IronGolemWorld.Advance(skeleton, at.AddSeconds(8), actions: true);
            PetStairTravel.CatchUp(owner, map, route[4], route[5]);
            Require(skeleton.Map == map && skeleton.Z == route[^1].Z && skeleton.InRange(route[^1], 4)
                && skeleton.WorldState.Controller.Fault == null,
                $"The skeleton did not follow the owner from {route[0]} to {route[^1]}. It stopped at {skeleton.Location}");
        }
        finally
        {
            grizzly.Delete();
            dog.Delete();
            skeleton?.Delete();
            owner.Delete();
        }
    }

    private static void CheckPetCombatRange()
    {
        var owner = new PlayerMobile { Body = 0x190, Player = true };
        var summon = new SkeletonGuardian();
        var dog = new Dog();
        try
        {
            Require(summon.SetControlMaster(owner) && dog.SetControlMaster(owner), "Could not set up combat range fixtures");
            Require(summon.ChaseLeashRange == summon.RangePerception * 2 + 4
                && dog.ChaseLeashRange == dog.RangePerception * 2
                && ClassCombatHooks.PetCombatRangeBonus(dog) == 0,
                "The native combat range bonus was not exclusive to summons");
        }
        finally
        {
            dog.Delete();
            summon.Delete();
            owner.Delete();
        }

        (bool Chases, bool Follows) ThinkAt(int ownerDistance, int targetDistance, int aggroBonus = 0)
        {
            bool chases = false, follows = false;
            var services = new NecromancerPetAiServices(
                request => chases |= request.Kind == NecromancerPetAiKind.Approach,
                () => 1, id => id, id => id == 1 ? ownerDistance : targetDistance,
                _ => default, (_, _) => new(2, targetDistance, false), _ => false, _ => false, id => id == 2,
                () => 0, (_, _, _, _) => false, () => false,
                (mode, _, _, _) => follows = mode == 0);
            NecromancerPetAi.Think(new(new(0, 0, 0), 5, 10, 0, 0, PetCombatRanges.BonusSourceCells, aggroBonus),
                services, responsiveMelee: true);
            return (chases, follows);
        }

        Require(ThinkAt(28, 22) == (true, false) && ThinkAt(20, 23) == (false, false)
            && ThinkAt(29, 22) == (false, true), "The summon acquisition and owner leash did not both gain four tiles");
        Require(ThinkAt(20, 28, PetCombatRanges.GolemAggroBonusSourceCells) == (true, false)
            && ThinkAt(20, 29, PetCombatRanges.GolemAggroBonusSourceCells) == (false, false),
            "Golems did not proactively engage enemies three tiles beyond other melee summons");
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

    private static void CheckCoveRune()
    {
        var player = new PlayerMobile { Body = 0x190, Player = true, Name = "Cove rune fixture" };
        player.AddItem(new Backpack());
        player.MoveToWorld(new Point3D(6560, 3610, 0), Map.Trammel);
        Profiles.Assign(player, DiabloClass.Paladin);
        var iolo = new Iolo { RecruitedBy = player.Serial, ControlSlots = 0 };
        try
        {
            Require(iolo.SetControlMaster(player) && MercenaryCare.Register(player, iolo, ClassStats.Level(player)),
                "Could not set up Iolo before the royal audience");
            iolo.MoveToWorld(player.Location, player.Map);
            KillChief(iolo);
            Require((Profiles.Discoveries(player.Serial) & StoryDiscoveries.CoveChief) == 0,
                "Killing the chief without a runebook consumed the rune award");

            Require(MercenaryCare.Retire(player, iolo), "Could not retire Iolo");
            Profiles.Discover(player.Serial, StoryDiscoveries.IoloRetired);
            Require(CollegeIolo.GiveRunebook(player), "Could not obtain Iolo's runebook after the first kill");
            var book = player.Backpack.FindItemByType<Runebook>();
            Require(book is { Entries.Count: 1 } && book.Default?.Location == BritainStory.CollegeRune,
                "Obtaining a book retroactively filed the skipped rune");

            KillChief(player);
            Require(book.Entries.Count == 2 && book.Entries[1] is { MountedTravel: RuneMountTravel.Allowed } cove
                && cove.Location == CoveGateRune.Gates && cove.Map == Map.Trammel
                && (Profiles.Discoveries(player.Serial) & StoryDiscoveries.CoveChief) != 0,
                "A later chief kill did not file and record the Cove Gates rune");

            KillChief(player);
            Require(book.Entries.Count == 2, "Another chief kill duplicated the entry");
        }
        finally
        {
            MercenaryCare.States.Remove(iolo.Serial);
            iolo.Delete();
            player.Delete();
        }

        void KillChief(Mobile killer)
        {
            var chief = new CoveEncounter("cove-orc-chief", 25);
            try
            {
                chief.MoveToWorld(player.Location, player.Map);
                chief.Damage(100_000, killer);
                Require(chief.Corpse is Corpse { SanctuaryNoRewards: true } corpse
                    && corpse.GetAmount(typeof(OrcEar)) == 1
                    && corpse.FindItemByType<CoveGateRune>() == null
                    && player.Backpack.FindItemByType<CoveGateRune>() == null,
                    "The chief death did not grant one ear or produced a loose rune");
            }
            finally
            {
                chief.Corpse?.Delete();
                chief.Delete();
            }
        }
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
