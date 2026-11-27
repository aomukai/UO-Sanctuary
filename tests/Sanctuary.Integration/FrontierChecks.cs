using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Movement;

namespace Sanctuary.Integration;

public static class FrontierChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(FrontierChecks));

    public static void Configure() =>
        ConsoleInputHandler.RegisterCommand("frontier-check", "Britannia frontier world and encounter checks",
            args => Core.LoopContext.Post(() => Step(args.Trim() switch
            {
                "deep-despise" => CheckDeepDespise,
                "hedge-sight" => CheckHedgeMazeSight,
                "hoard" => CheckDragonHoard,
                "wind" => CheckWind,
                _ => CheckWorld
            })));

    private static void CheckDragonHoard()
    {
        var map = Map.Trammel;
        var spot = new Point3D(1446, 900, 0);
        var chest = World.Items.Values.OfType<DragonHoardChest>().Single(item => item.Map == map && item.Location == spot);
        var player = new PlayerMobile { Body = 0x190, Player = true };
        var pack = new Backpack();
        player.AddItem(pack);
        player.InitStats(100, 75, 25);
        try
        {
            player.MoveToWorld(new(1446, 901, 0), map);
            var loot = chest.Items.FirstOrDefault();
            if (loot == null)
            {
                loot = new Gold(100);
                chest.DropItem(loot);
            }

            Require(!chest.VerifyMove(player) && !chest.CheckHold(player, pack, false, true),
                "The hoard chest should stay fixed and reject player storage");
            chest.OnDoubleClick(player);
            bool canUse = loot.CheckItemUse(player, loot);
            player.Lift(loot, loot.Amount, out bool rejected, out var reason);
            Require(!rejected && player.Holding == loot, $"The hoard rejected a player's lift: {reason}");
            Require(canUse, "A player cannot use the hoard chest's contents");
            Require(player.Drop(pack, new(20, 20, 0)) && loot.IsChildOf(pack), "The hoard loot could not be put in a backpack");
        }
        finally
        {
            player.Delete();
        }

        _logger.Information("frontier-check hoard: passed");
    }

    private static void CheckHedgeMazeSight()
    {
        var map = Map.Trammel;
        var start = new Point3D(1078, 2259, 0);
        var across = new Point3D(1082, 2259, 0);
        var lane = new Point3D(1079, 2259, 0);
        Require(!EncounterProfiles.ClearSight(map, start, across)
            && !EncounterProfiles.ClearSight(map, across, start)
            && !EncounterProfiles.ClearSight(map, new(1082, 2254, 0), new(1082, 2256, 0)),
            "Both orientations of baked hedge wall should block sight in either direction");
        Require(EncounterProfiles.ClearSight(map, start, lane)
            && map.LineOfSight(new Point3D(start.X, start.Y, 40), new Point3D(across.X, across.Y, 40)),
            "Hedge cover should leave the open lane and upper floors clear");

        var avatar = new PlayerMobile { Body = 0x190, Player = true, Name = "Hedge sight fixture" };
        var gazer = new HedgeEncounter("hedge-gazer");
        try
        {
            ClassCreation.Profiles.Assign(avatar, DiabloClass.Necromancer);
            gazer.MoveToWorld(start, map);
            avatar.MoveToWorld(across, map);
            int life = avatar.Hits;
            Require(!gazer.InLOS(avatar) && !avatar.InLOS(gazer) && !gazer.BeginFirebolt(avatar),
                "A gazer could start casting across a hedge wall");

            EliteBolts.Cast(gazer, avatar, CombatAbility.Lightning);
            for (int step = 0; step < 4; step++)
                EliteBolts.Tick();
            Require(EliteBolts.Inspect(gazer).Length == 0 && avatar.Hits == life,
                "An in-flight gazer bolt crossed the hedge");

            avatar.MoveToWorld(lane, map);
            Require(gazer.InLOS(avatar) && avatar.InLOS(gazer) && gazer.BeginFirebolt(avatar),
                "The same gazer could not cast down a clear lane");
        }
        finally
        {
            gazer.Delete();
            avatar.Delete();
        }
        _logger.Information("frontier-check hedge-sight: passed");
    }

    private static void CheckDeepDespise()
    {
        var map = Map.Trammel;
        CheckDeepDespiseSpawns(map, World.Mobiles.Values.OfType<FrontierEncounter>().Where(m => m.Map == map).ToArray());
        _logger.Information("frontier-check deep-despise: passed");
    }

    private static void CheckWorld()
    {
        var map = Map.Trammel;
        var encounters = World.Mobiles.Values.OfType<FrontierEncounter>().Where(m => m.Map == map && !m.Raised).ToArray();
        var lich = encounters.Single(m => m.Dungeon.Id == "frontier-lich" && m.Home.X < 1400);
        Require(lich.Home == new Point3D(1380, 1480, 10) && lich.Basis.Level == 16, "Cemetery lich spawn");
        var dead = encounters.Where(m => m.Home.X is >= 1373 and <= 1409 && m.Home.Y is >= 1476 and <= 1505
            && m.Dungeon.Id is "frontier-skeleton" or "frontier-zombie").ToArray();
        Require(dead.Length == 14 && dead.Min(m => m.Basis.Level) == 12 && dead.Max(m => m.Basis.Level) == 16,
            "Cemetery undead levels or spawns");
        Require(dead.Select(m => m.Home).Distinct().Count() == 14
            && dead.All(m => m.Home.Z == 10 && m.Z == 10 && m.RangeHome == 1
                && m.RangePerception == 5 && m.HomeLeashRadius == 7)
            && dead.Count(m => Utility.InRange(m.Home, new Point3D(1383, 1494, 10), 6)) <= 2,
            "Cemetery undead clumped near the entrance or spawned above ground");
        var ettins = encounters.Where(m => m.Dungeon.Id == "frontier-ettin").ToArray();
        Require(ettins.GroupBy(m => m.Home).Count(group => group.Count() == 4
            && group.Min(m => m.Basis.Level) == 15 && group.Max(m => m.Basis.Level) == 20) == 3, "Ettin camp population");
        var forest = ettins.Where(m => m.Home.X is >= 1517 and <= 1539 && m.Home.Y is >= 1014 and <= 1087).ToArray();
        Require(forest.Length == 6 && forest.GroupBy(m => m.Home).All(group => group.Count() == 1)
            && forest.All(m => m.Basis.Level is >= 15 and <= 20), "Forest ettins should spawn individually");
        var lower = encounters.Where(m => m.Dungeon.Id == FrontierEncounter.DespiseEttin).ToArray();
        Require(lower.Length == 22 && lower.All(m => m.Basis.Level is >= 18 and <= 22)
            && lower.Count(m => m.Leader && m.Basis.Level == 22) == 2
            && lower.Any(m => m.Leader && m.Home == new Point3D(5439, 679, 20)), "Lower Despise encounters and uniques");
        var ratmen = encounters.Where(m => m.Dungeon.Id == FrontierEncounter.DespiseRatman).ToArray();
        Point3D[] refuges = [new(5474, 526, 72), new(5423, 570, 65), new(5504, 626, 27)];
        Require(ratmen.Length == 19 && ratmen.All(m => m.Basis.Level is >= 12 and <= 16
            && refuges.All(point => !Utility.InRange(point, m.Home, 20))), "Ratman levels or refuge clearance");
        Require(World.Mobiles.Values.OfType<DespiseWitness>().Count(m => m.Map == map) == 4
            && World.Items.Values.OfType<DespiseLetter>().Count(item => item.Map == map) == 1, "Despise story actors");
        var mushrooms = World.Items.Values.OfType<Static>().Where(item => item.Map == map
            && DespiseLighting.IsGlowingMushroom(item.X, item.Y, (ushort)item.ItemID, (ushort)item.Hue)).ToArray();
        Require(mushrooms.Length == 25 && mushrooms.Select(item => item.Location).Distinct().Count() == 25
            && mushrooms.Select(item => item.Hue).Distinct().Count() == 4, "Despise mushroom placement or palette");
        CheckDeepDespiseSpawns(map, encounters);
        CheckWind();
        Require(World.Mobiles.Values.OfType<FrontierSentry>().Count(guard => guard.Map == map
            && guard.InRange(new Point3D(1255, 870, 16), 3) && guard.Script.Count > 0) == 2, "Mountain watch guards");
        CheckSouthernFrontier(map, encounters, ratmen.First(m => m.Basis.Level == 15));
        CheckCompassionFrontier(map, encounters);
        CheckVesperGraveyard(map, encounters);
        CheckCovetousTreasure();

        var avatar = new PlayerMobile { Body = 0x190, Player = true, Name = "FrontierChecks" };
        ClassCreation.Profiles.Assign(avatar, DiabloClass.Necromancer);
        try
        {
            CheckVesperCoast(avatar, map, encounters);
            CheckPassage(avatar, new(1298, 1081, 0), Direction.West, new(5586, 631, 30));
            CheckPassage(avatar, new(5587, 631, 30), Direction.East, new(1299, 1081, 0));
            CheckPassage(avatar, new(5576, 629, 30), Direction.West, new(5501, 570, 59));
            CheckPassage(avatar, new(5501, 570, 59), Direction.East, new(5577, 629, 30));
            CheckPassage(avatar, new(5576, 634, 29), Direction.West, new(5518, 674, 20));
            CheckPassage(avatar, new(5519, 674, 20), Direction.East, new(5578, 635, 30));
            CheckPassage(avatar, new(5521, 674, 27), Direction.East, new(5578, 635, 30));
            CheckPassage(avatar, new(5475, 751, 5), Direction.South, new(5407, 859, 45));
            CheckPassage(avatar, new(5407, 859, 45), Direction.East, new(5475, 750, 5));
            CheckPassage(avatar, new(1361, 887, 0), Direction.North, new(5166, 244, 15));
            CheckPassage(avatar, new(5166, 245, 15), Direction.South, new(1361, 888, 0));
            CheckRefuges(avatar, refuges);
        }
        finally
        {
            avatar.Delete();
        }

        // Retain the corpse and its contents so the test can verify one-time consumption.
        var skeleton = new FrontierEncounter("frontier-skeleton", 14, 0);
        skeleton.MoveToWorld(new Point3D(1382, 1480, 10), map);
        skeleton.Kill();
        var corpse = (Corpse)skeleton.Corpse;
        var item = new Dagger();
        corpse.DropItem(item);
        Require(lich.CanRaise(corpse), "Cemetery lich refused a nearby fallen skeleton");
        Later(3100, () =>
        {
            Require(lich.BeginRaise(corpse), "Cemetery lich did not begin its resurrection");
            Later(950, () =>
            {
                var raised = World.Mobiles.Values.OfType<FrontierEncounter>()
                    .FirstOrDefault(m => m.Map == map && m.Raised && m.InRange(corpse.Location, 1));
                Require(raised is { NoKillAwards: true } && raised.Basis.Level == 14 && corpse.SanctuaryConsumed
                    && item.Parent == corpse && !lich.CanRaise(corpse), "Lich resurrection changed the level, loot or corpse state");
                raised.Delete();
                corpse.Delete();
                CheckAttacks();
            });
        });
    }

    private static void CheckVesperCoast(PlayerMobile avatar, Map map, FrontierEncounter[] encounters)
    {
        var sea = encounters.Where(m => m.Dungeon.Id is "vesper-sea-serpent" or "vesper-kraken").ToArray();
        Point3D[] docks = [CoveFerry.Vesper, new(3046, 840, -2), new(3014, 789, -2), new(2928, 935, -2)];
        Require(sea.Count(m => m.Dungeon.Id == "vesper-sea-serpent") == 7
            && sea.Count(m => m.Dungeon.Id == "vesper-kraken") == 4
            && sea.Min(m => m.Basis.Level) == 15 && sea.Max(m => m.Basis.Level) == 30
            && sea.Select(m => m.Basis.Level).Distinct().Count() >= 8, "Vesper's sea population or level mix is wrong");
        foreach (var mob in sea)
        {
            var tile = map.Tiles.GetLandTile(mob.Home.X, mob.Home.Y);
            Require(mob.CanSwim && mob.CantWalk && mob.Home.Z == -5 && mob.HomeMap == map
                && (TileData.LandTable[tile.ID & TileData.MaxLandValue].Flags & TileFlag.Wet) != 0
                && sea.Count(other => Utility.InRange(mob.Home, other.Home, 18)) == 1
                && docks.All(dock => !Utility.InRange(mob.Home, dock, 24)),
                "A Vesper sea spawn is clumped, on land, or too close to the docks");
            Require(Server.Movement.Movement.CheckMovement(mob, map, mob.Home, Direction.East, out int z) && z == -5,
                "A Vesper sea creature cannot move through water");
            avatar.MoveToWorld(new(mob.Home.X + 1, mob.Home.Y, z), map);
            Require(mob.IsEnemy(avatar), "A Vesper sea creature does not target nearby sailors");
            foreach (var dock in docks)
            {
                avatar.MoveToWorld(dock, map);
                Require(!mob.IsEnemy(avatar), "A Vesper sea creature targets players at the docks");
            }
        }
    }

    private static void CheckSouthernFrontier(Map map, FrontierEncounter[] encounters, FrontierEncounter ratman)
    {
        var bridge = new Point3D(1474, 2161, 6);
        var troll = encounters.Single(m => m.Dungeon.Id == "bridge-troll" && m.Home == bridge);
        Require(troll.Home == bridge && troll.Location == bridge && troll.Basis.Level == 15
            && CreatureProfiles.Experience(troll) == troll.Basis.Experience * 2, "Bridge troll placement, level or Players-3 XP");
        Require(EncounterTreasure.Root(troll) == EncounterTreasure.Root(ratman)
            && EncounterTreasure.Roll(troll, new TreasureTicket()).Count == 0
            && EncounterTreasure.Roll(ratman, new TreasureTicket()) is [{ Leaf: "gld" }],
            "Players-5 should turn the same NoDrop ticket into loot");

        var southernDead = encounters.Where(m => Utility.InRange(m.Home, new Point3D(1475, 2509, 5), 12))
            .Where(m => m.Dungeon.Id is "frontier-skeleton" or "frontier-zombie" or "frontier-lich").ToArray();
        Require(southernDead.Length == 9 && southernDead.All(m => m.Basis.Level == 20)
            && southernDead.Count(m => m.Dungeon.Id == "frontier-lich") == 1, "Southern graveyard levels or population");

        var watch = new Point3D(1689, 2474, 10);
        var captain = World.Mobiles.Values.OfType<FensWatchCaptain>().Single(guard => guard.Map == map);
        Require(captain.Location == watch && captain.Script[0].Line.Contains("Fens of the Dead")
            && World.Mobiles.Values.OfType<FensWatchSentry>().Count(guard => guard.Map == map && guard.InRange(watch, 8)) == 2,
            "Fens watch placement or warning");

        var burialGround = encounters.Where(m => m.Home.X >= 1819 && m.Home.Y is >= 2412 and <= 2426
            && m.Dungeon.Id is "frontier-skeleton" or "frontier-zombie").ToArray();
        Require(burialGround.Length == 10 && burialGround.Min(m => m.Basis.Level) == 14
            && burialGround.Max(m => m.Basis.Level) == 18, "Fens burial ground levels or population");
        var fenCreatures = encounters.Where(m => m.Home.Y >= 2200 && m.Dungeon.Id.StartsWith("fens-", StringComparison.Ordinal)).ToArray();
        Require(fenCreatures.Length == 73 && fenCreatures.All(m => m.Basis.Level is >= 15 and <= 25
            && m.HomeLeashRadius == 10 && map.CanFit(m.Location, 16, false, false))
            && fenCreatures.GroupBy(m => m.Home).Count() == 42
            && fenCreatures.Any(m => m.Home == new Point3D(1823, 2389, 0) && m.Basis.Level == 15)
            && fenCreatures.Any(m => m.Home == new Point3D(1971, 2319, 0) && m.Basis.Level == 25),
            "Fens population is clumped, off its terrain, or missing the level gradient");
    }

    private static void CheckCompassionFrontier(Map map, FrontierEncounter[] encounters)
    {
        var bridge = new Point3D(1866, 740, 6);
        Require(encounters.Any(m => m.Dungeon.Id == "bridge-troll" && m.Basis.Level == 15 && m.Home == bridge && m.Location == bridge),
            "Missing level 15 troll on the Compassion bridge");
        var desert = encounters.Where(m => m.Dungeon.Id.StartsWith("compassion-desert-", StringComparison.Ordinal)).ToArray();
        Require(desert.Length == 68 && desert.Select(m => m.Dungeon.Id).Distinct().Count() == 6
            && desert.All(m => m.Basis.Level is >= 20 and <= 25 && m.Location == m.Home
                && map.CanFit(m.Location, 16, false, false) && !m.RepelledByRefuge(m.Home)
                && desert.Count(other => Utility.InRange(other.Home, m.Home, 13)) == 1),
            "Compassion desert levels, terrain, refuge clearance or individual spawn spacing");
        Require(desert.Any(m => m.Home == new Point3D(2035, 841, 0))
            && desert.All(m => m.RepelledByRefuge(new(1875, 865, -1)) && m.RepelledByRefuge(new(1857, 877, -1)))
            && World.Items.Values.OfType<AnkhNorth>().Any(ankh => ankh.Map == map && ankh.Location == new Point3D(1857, 877, -1)),
            "The Compassion ankh or safe approach is missing");
        int uniques = desert.Count(m => m.Grade == EncounterGrade.Unique);
        Require(uniques <= 1, "Compassion desert spawned multiple Uniques");
        var eliteFixture = uniques == 0 ? new FrontierEncounter("compassion-desert-snake", 23, 1) : null;
        try
        {
            for (int i = 0; i < 100; i++)
            {
                var respawn = new FrontierEncounter("compassion-desert-harpy", 25);
                try { Require(respawn.Grade != EncounterGrade.Unique, "A desert respawn bypassed the existing Unique slot"); }
                finally { respawn.Delete(); }
            }
        }
        finally { eliteFixture?.Delete(); }

        var swamp = encounters.Where(m => m.Home.Y < 1200 && m.Dungeon.Id.StartsWith("fens-", StringComparison.Ordinal)).ToArray();
        Require(swamp.Length == 46 && swamp.Select(m => m.Dungeon.Id).Distinct().Count() == 7
            && swamp.All(m => m.Basis.Level is >= 15 and <= 25 && m.Location == m.Home
                && map.CanFit(m.Location, 16, false, false)
                && map.Tiles.GetLandTile(m.X, m.Y).ID is >= 0x3DC0 and <= 0x3DF0
                && swamp.Count(other => Utility.InRange(other.Home, m.Home, 13)) == 1)
            && swamp.Any(m => m.Home == new Point3D(2007, 952, 0) && m.Basis.Level == 15),
            "Compassion swamp levels, species, terrain or individual spawn spacing");

        var guards = World.Mobiles.Values.OfType<CompassionWatch>().Where(g => g.Map == map).ToArray();
        Require(guards.Count(g => g.InRange(new Point3D(1819, 728, 0), 2) && g.Script[0].Line.Contains("do not stop")) == 2
            && guards.Count(g => g.InRange(new Point3D(2060, 804, 0), 11)) == 3
            && guards.Any(g => g.Name == "Leofric" && g.Location == new Point3D(2060, 804, 0) && g.Script[0].Replies.Length == 4),
            "Compassion guards or road directions are missing");
    }

    private static void CheckVesperGraveyard(Map map, FrontierEncounter[] encounters)
    {
        var undead = encounters.Where(m => m.Home.X is >= 2742 and <= 2782 && m.Home.Y is >= 840 and <= 894).ToArray();
        Require(undead.Length == 18 && undead.Select(m => m.Home).Distinct().Count() == 18
            && undead.All(m => m.Home.Z == 0 && m.Z == 0 && m.RangePerception == 5 && m.HomeLeashRadius == 7)
            && undead.Count(m => m.Grade == EncounterGrade.Unique) <= 1,
            "Vesper graves must have separate ground-level spawns and one shared Unique slot");
        Require(undead.Count(m => m.Basis.Level is >= 12 and <= 16) == 14
            && undead.Any(m => m.Home == new Point3D(2764, 851, 0) && m.Dungeon.Id == "vesper-skeletal-knight" && m.Basis.Level == 18)
            && undead.Any(m => m.Home == new Point3D(2779, 844, 0) && m.Dungeon.Id == "vesper-mummy" && m.Basis.Level == 18)
            && undead.Count(m => m.Dungeon.Id == "frontier-lich" && m.Basis.Level == 19) == 2,
            "Vesper undead levels or house population");

        var passage = new Point3D(2776, 894, -23);
        var rocks = World.Items.Values.OfType<Static>().Where(item => item.Map == map && item.X == passage.X && item.Z == passage.Z);
        Require(rocks.Any(item => item.Location == passage && item.ItemID == 0x1362)
            && !map.CanFit(passage, 16, false, false)
            && !World.Items.Values.OfType<Teleporter>().Any(item => item.Map == map && item.Location == passage && item.Active),
            "Vesper passage to Lost Lands must stay sealed by its boulder");
    }

    private static void CheckCovetousTreasure()
    {
        var creatures = World.Mobiles.Values.OfType<CovetousEncounter>().Where(m => !m.Raised).ToArray();
        Require(creatures.Select(m => m.Dungeon.Id).Distinct().Count() == 18, "Covetous loot check needs the full roster");
        var random = new Random(0);
        foreach (var creature in creatures)
        {
            Require(creature.EncounterPlayers == 5, $"{creature.Dungeon.Id} is not set to Players-5");
            var drops = EncounterTreasure.Roll(creature, random);
            Require(drops.Count <= 6 && (!creature.Leader || drops.Count == 6),
                $"{creature.Dungeon.Id} produced invalid Covetous loot");
        }
    }

    private sealed class TreasureTicket : Random
    {
        // Ticket 25 is in the NoDrop band at Players-3 and in the gold band at Players-5 for this root.
        public override int Next(int maxValue) => 25;
    }

    private static void CheckRefuges(PlayerMobile avatar, Point3D[] refuges)
    {
        foreach (var point in refuges)
            Require(AnkhRefuges.Contains(Map.Trammel, point), $"No working ankh by {point}");
        var ratman = new FrontierEncounter(FrontierEncounter.DespiseRatman, 14, 0);
        var letter = refuges[2];
        try
        {
            var map = Map.Trammel;
            for (int x = letter.X - 8; x <= letter.X + 8; x++)
            for (int y = letter.Y - 8; y <= letter.Y + 8; y++)
            {
                var outside = new Point3D(x, y, map.GetAverageZ(x, y));
                if (!map.CanFit(outside, 16, false, false) || AnkhRefuges.Contains(map, outside))
                    continue;
                ratman.MoveToWorld(outside, map);
                for (int i = 0; i < 8; i++)
                {
                    var direction = (Direction)i;
                    var next = outside;
                    Server.Movement.Movement.Offset(direction, ref next);
                    if (!AnkhRefuges.Contains(map, next)
                        || !Server.Movement.Movement.CheckMovement(ratman, direction, out int z))
                        continue;
                    ratman.Direction = direction;
                    Require(!ratman.Move(direction) && ratman.Location == outside, "A ratman entered the ankh refuge");
                    avatar.MoveToWorld(new Point3D(next.X, next.Y, z), map);
                    Require(!ratman.IsEnemy(avatar), "A ratman targeted the Avatar beside an ankh");
                    return;
                }
            }
            throw new InvalidOperationException("Could not find a walkable ankh refuge boundary");
        }
        finally
        {
            ratman.Delete();
        }
    }

    private static void CheckAttacks()
    {
        var avatar = new PlayerMobile { Body = 0x190, Player = true, Name = "FrontierCombatChecks" };
        ClassCreation.Profiles.Assign(avatar, DiabloClass.Necromancer);
        FixtureSpace.Corridor(avatar, 6560, 14, 3475);
        var origin = avatar.Location;
        var drake = new FrontierEncounter("frontier-drake", 17, 0);
        drake.MoveToWorld(origin, avatar.Map);
        drake.AIObject.AITimer.Stop();
        var aim = new Point3D(origin.X + 3, origin.Y, origin.Z);
        avatar.MoveToWorld(aim, avatar.Map);
        int initial = SkillHealth.Balance256(avatar);
        Require(drake.BeginFirebolt(avatar), "Drake could not start the close-range Inferno cast");
        Later(610, () =>
        {
            int scorched = SkillHealth.Balance256(avatar);
            Require(scorched < initial, "The first Inferno pulse did no fire damage");
            avatar.MoveToWorld(new Point3D(aim.X, aim.Y + 1, aim.Z), avatar.Map);
            Later(600, () =>
            {
                Require(SkillHealth.Balance256(avatar) >= scorched, "Inferno tracked a target that dodged the channel");
                drake.Delete();
                var wyvern = new FrontierEncounter("frontier-wyvern", 18, 0) { MeleeAccuracyRoll = () => 0 };
                wyvern.MoveToWorld(new Point3D(avatar.X + 1, avatar.Y, avatar.Z), avatar.Map);
                wyvern.AIObject.AITimer.Stop();
                wyvern.BeginMelee(avatar);
                Later(wyvern.Basis.EffectMs + 100, () =>
                {
                    Require(PoisonStates.Active(avatar, TimedEffects.Now), "Wyvern's melee did not inflict poison");
                    wyvern.Delete();
                    avatar.Delete();
                    _logger.Information("frontier-check: passed");
                });
            });
        });
    }

    private static void CheckDeepDespiseSpawns(Map map, FrontierEncounter[] encounters)
    {
        Require(encounters.Where(encounter => encounter.Dungeon.Id is FrontierEncounter.DespiseTroll
                or FrontierEncounter.DespiseOgre or FrontierEncounter.DespiseOgreLord)
            .All(encounter => encounter.Spawner is DespiseTroll or DespiseOgre or DespiseOgreLord),
            "Deep Despise still has its preset encounters");
        Require(Enumerable.Range(0, 100).Count(roll => FrontierEncounter.OgreLordGrade(roll) == EncounterGrade.Champion) == 5,
            "Exactly 5% of Ogre Lords should be paragons");

        var point = new Point3D(5407, 859, 45);
        CheckSpawner(map, point, "despisetroll", FrontierEncounter.DespiseTroll, FrontierEncounter.DespiseHarpy, 24, 28);
        CheckSpawner(map, point, "despiseogre", FrontierEncounter.DespiseOgre, FrontierEncounter.DespiseStoneHarpy, 26, 30);
        CheckSpawner(map, point, "despiseogrelord", FrontierEncounter.DespiseOgreLord, FrontierEncounter.DespiseOgreMage, 28, 30, 80, 20);
    }

    private static void CheckWind()
    {
        var map = Map.Trammel;
        var encounters = World.Mobiles.Values.OfType<FrontierEncounter>().Where(m => m.Map == map && !m.Raised).ToArray();
        Require(encounters.Where(m => m.Dungeon.Id is FrontierEncounter.Drake or FrontierEncounter.Wyvern)
            .All(m => m.Spawner is WindOutside or WindInside), "Wind still has preset drakes or wyverns");
        var dragon = encounters.Single(m => m.Dungeon.Id == "frontier-dragon");
        Require(dragon.Home == new Point3D(1442, 903, 0) && dragon.Basis.Level == 30
            && dragon.Modifiers.Length == 2 && dragon.FireResistance == 70, "Cindermaw's lair, level, modifiers or fire resistance");

        CheckSpawner(map, new(1361, 888, 0), "windoutside", FrontierEncounter.Drake, FrontierEncounter.Wyvern, 16, 20);
        CheckSpawner(map, new(5166, 244, 15), "windinside", FrontierEncounter.Drake, FrontierEncounter.Wyvern, 22, 28);
        var wyvern = new FrontierEncounter(FrontierEncounter.Wyvern, 22, 0);
        try
        {
            Require(wyvern.PoisonResistance == 70 && wyvern.EncounterProfile.Attack1.Poison == 100
                && wyvern.HitsMax >= wyvern.Basis.Life.Min * 3 && wyvern.HitsMax <= wyvern.Basis.Life.Max * 3
                && CreatureProfiles.Experience(wyvern) == wyvern.Basis.Experience * 3, "Wind wyvern poison or players-5 scaling");
        }
        finally
        {
            wyvern.Delete();
        }

        var residents = World.Mobiles.Values.OfType<BaseVendor>().Where(vendor => WindTown.Contains(vendor.Home, vendor.Map)).ToArray();
        Require(residents.Any(vendor => vendor is Banker) && residents.Any(vendor => vendor is InnKeeper)
            && residents.Any(vendor => vendor is Healer) && residents.Any(vendor => vendor is ProfessionInstructor),
            "Missing town services in Trammel Wind");
        Require(residents.All(vendor => vendor.Race == Race.Gargoyle && vendor.Body == Race.Gargoyle.AliveBody(vendor.Female)
            && vendor.Items.Any(item => item is GargishClothChestType1)), "Non-gargoyle resident in Trammel Wind");
        CheckWindMarkets(map);

        Require(TownMarkets.Profiles.All.All(market => market.Site?.StartsWith("Felucca/Wind/", StringComparison.Ordinal) != true)
            && !World.Items.Values.OfType<BaseSpawner>().Any(spawner => spawner.Map == Map.Felucca
                && spawner.Name == "Britannia town resident" && Region.Find(spawner.Location, Map.Felucca).IsPartOf("Wind")),
            "Retired Felucca Wind market NPCs or resident spawners are still installed");

        var banker = new Banker();
        try
        {
            banker.OnBeforeSpawn(new(5347, 76, 25), Map.Felucca);
            Require(banker.Race == Race.Human, "The Trammel Wind appearance was applied on Felucca");
            banker.OnBeforeSpawn(new(5347, 76, 25), map);
            Require(banker.Race == Race.Gargoyle, "A respawning Wind banker should be a gargoyle");
        }
        finally
        {
            banker.Delete();
        }

        _logger.Information("frontier-check wind: passed");
    }

    private static void CheckWindMarkets(Map map)
    {
        var avatar = new PlayerMobile { Body = 0x190, Player = true };
        var entrance = new Point3D(5166, 244, 15);
        var visited = new HashSet<Point3D> { entrance };
        var queue = new Queue<Point3D>();
        queue.Enqueue(entrance);
        (int X, int Y)[] offsets = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)];
        try
        {
            while (queue.TryDequeue(out var from))
            {
                for (int dir = 0; dir < offsets.Length; dir++)
                {
                    int x = from.X + offsets[dir].X, y = from.Y + offsets[dir].Y;
                    if (x is < 5120 or >= 5376 || y is < 0 or >= 256
                        || !Movement.CheckMovement(avatar, map, from, (Direction)dir, out int z))
                        continue;

                    var step = new Point3D(x, y, z);
                    if (visited.Add(step))
                        queue.Enqueue(step);
                }
            }
        }
        finally
        {
            avatar.Delete();
        }

        var sites = TownMarkets.Sites.Where(site => site.Town == "Wind" && site.Map == map.Name).ToArray();
        Require(sites.Length == 14 && sites.Select(site => (site.X, site.Y)).Distinct().Count() == sites.Length,
            "Trammel Wind should have 14 separate market posts");
        foreach (var site in sites)
        {
            var post = new Point3D(site.X, site.Y, site.Z);
            var vendor = TownMarkets.Profiles.At(site.Id);
            Require(vendor != null && vendor.Map == map && vendor.Location == post && vendor.Home == post
                && visited.Contains(post), $"{site.Id} failed to use its walkable post at {post}");
            if (TownMarkets.Skill(site.Role) is not { } skill || !TradeSkills.IsCraft((int)skill))
                continue;

            var market = TownMarkets.Profiles.Find(vendor.Serial);
            CheckStation(market.Station);
            if (skill == SkillName.Blacksmith)
                CheckStation(market.Forge);

            void CheckStation(Serial serial)
            {
                var station = World.FindItem(serial) as TownCraftStation;
                Require(station != null && station.Map == map && station.Z == post.Z && station.InRange(post, 3)
                    && offsets.Any(offset =>
                    {
                        var adjacent = new Point3D(station.X + offset.X, station.Y + offset.Y, station.Z);
                        return visited.Contains(adjacent)
                            && map.LineOfSight(new Point3D(adjacent.X, adjacent.Y, adjacent.Z + 14), map.GetPoint(station, false));
                    }), $"{site.Id} has a workshop outside reach of its market floor at {station?.Location}");
            }
        }
    }

    private static void CheckSpawner(Map map, Point3D point, string name, string first, string second, int minimumLevel, int maximumLevel,
        int firstWeight = 100, int secondWeight = 100)
    {
        var gm = new PlayerMobile { Name = $"{name} fixture", AccessLevel = AccessLevel.GameMaster };
        Spawner spawner = null;
        try
        {
            gm.MoveToWorld(point, map);
            CommandSystem.Handle(gm, $"[Add {name}");
            Require(gm.Target != null, $"[Add {name} did not request a target");
            gm.Target.Invoke(gm, gm);
            foreach (var placed in map.GetItemsAt<Spawner>(point))
                if (placed.Location == point && string.Equals(placed.Name, name, StringComparison.OrdinalIgnoreCase))
                    spawner = placed;
            Require(spawner != null, $"[Add {name} did not place a spawner");
            spawner.Respawn();
            spawner.Spawn();
            Require(spawner.Spawned.Count == 1, $"{name} should keep one active monster");
            Require(spawner.Entries.Count == 2 * (maximumLevel - minimumLevel + 1),
                $"{name} should offer two variants at each level from {minimumLevel} to {maximumLevel}");
            for (int i = 0; i < spawner.Entries.Count; i++)
            {
                Require(spawner.Entries[i].SpawnedProbability == (i % 2 == 0 ? firstWeight : secondWeight),
                    $"{name} has the wrong weight for entry {i}");
                spawner.RemoveSpawns();
                Require(spawner.Spawn(i, out _), $"{name} could not spawn entry {i}");
                var monster = (FrontierEncounter)spawner.Spawned.Keys.Single();
                int level = minimumLevel + i / 2;
                string species = i % 2 == 0 ? first : second;
                Require(monster.Location == point && monster.EncounterPlayers == 5 && monster.Basis.Level == level
                    && monster.Dungeon.Id == species, $"{name} spawned the wrong variant, level, floor, or difficulty");
                if (monster.Grade == EncounterGrade.Champion)
                    Require(monster.Name.EndsWith(" (Paragon)", StringComparison.Ordinal)
                        && EncounterTreasure.Root(monster) == $"Act 2 Champ {(level >= 25 ? "B" : "A")}",
                        "Paragons should retain their level bracket and use the Paragon label");
                else
                    Require(monster.Grade == EncounterGrade.Ordinary, $"{name} spawned an unexpected elite");
            }
        }
        finally
        {
            spawner?.Delete();
            gm.Delete();
        }
    }

    private static void CheckPassage(PlayerMobile avatar, Point3D start, Direction direction, Point3D destination)
    {
        avatar.MoveToWorld(start, Map.Trammel);
        avatar.Direction = direction;
        avatar.Move(direction);
        Require(avatar.Map == Map.Trammel && avatar.Location == destination,
            $"Walking from {start} ended at {avatar.Location}, expected {destination}");
    }

    private static void Later(int ms, Action check) => Timer.StartTimer(TimeSpan.FromMilliseconds(ms), () => Step(check));

    private static void Step(Action check)
    {
        try { check(); }
        catch (Exception error) { _logger.Error(error, "frontier-check: failed"); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
