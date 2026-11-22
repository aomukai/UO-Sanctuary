using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class FrontierChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(FrontierChecks));

    public static void Configure() =>
        ConsoleInputHandler.RegisterCommand("frontier-check", "Britannia frontier world and encounter checks",
            _ => Core.LoopContext.Post(() => Step(CheckWorld)));

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
        var reptiles = encounters.Where(m => m.Dungeon.Id is "frontier-drake" or "frontier-wyvern").ToArray();
        Require(reptiles.Length == 19 && reptiles.Count(m => m.Leader && m.Modifiers.Length == 1) == 2
            && reptiles.All(m => m.Basis.Level is >= 16 and <= 22), "Maze population and unique grades");
        Require(reptiles.GroupBy(m => m.Home).All(group => group.Count() == 1)
            && reptiles.All(m => m.HomeLeashRadius == 10 && m.RangePerception == 7), "Maze defenses clumped at one spawn");
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
        var deep = encounters.Where(m => m.Dungeon.Id is FrontierEncounter.DespiseTroll or FrontierEncounter.DespiseOgre).ToArray();
        var ogreLords = encounters.Where(m => m.Dungeon.Id == FrontierEncounter.DespiseOgreLord).ToArray();
        Require(deep.Length == 20 && deep.All(m => m.Basis.Level is >= 20 and <= 25)
            && ogreLords.Length == 3 && ogreLords.All(m => m.Grade == EncounterGrade.Champion && m.Basis.Level == 30
                && EncounterTreasure.Root(m) == "Act 2 Champ B"), "Deep Despise population and champion loot");
        var dragon = encounters.Single(m => m.Dungeon.Id == "frontier-dragon");
        Require(dragon.Basis.Level == 30 && dragon.Modifiers.Length == 2 && dragon.FireResistance == 70,
            "Cindermaw's level, modifiers or fire resistance");
        var wyvern = reptiles.First(m => m.Dungeon.Id == "frontier-wyvern" && !m.Leader);
        Require(wyvern.PoisonResistance == 70 && wyvern.EncounterProfile.Attack1.Poison == 100
            && wyvern.HitsMax >= wyvern.Basis.Life.Min * 3 && wyvern.HitsMax <= wyvern.Basis.Life.Max * 3
            && CreatureProfiles.Experience(wyvern) == wyvern.Basis.Experience * 3, "Players-5 life or experience scaling");
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
                && map.CanFit(m.Location, 16, false, false) && !m.RepelledByAnkh(m.Home)
                && desert.Count(other => Utility.InRange(other.Home, m.Home, 13)) == 1),
            "Compassion desert levels, terrain, refuge clearance or individual spawn spacing");
        Require(desert.Any(m => m.Home == new Point3D(2035, 841, 0))
            && desert.All(m => m.RepelledByAnkh(new(1875, 865, -1)) && m.RepelledByAnkh(new(1857, 877, -1)))
            && World.Items.Values.OfType<AnkhNorth>().Any(ankh => ankh.Map == map && ankh.Location == new Point3D(1857, 877, -1)),
            "The Compassion ankh or safe approach is missing");
        int elites = desert.Count(m => m.Grade is EncounterGrade.Champion or EncounterGrade.Unique);
        Require(elites <= 1, "Compassion desert spawned multiple elites");
        var eliteFixture = elites == 0 ? new FrontierEncounter("compassion-desert-snake", 23, 1) : null;
        try
        {
            for (int i = 0; i < 100; i++)
            {
                var respawn = new FrontierEncounter("compassion-desert-harpy", 25);
                try { Require(respawn.Grade == EncounterGrade.Ordinary, "A desert respawn bypassed the existing elite slot"); }
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
            && undead.Count(m => m.Grade is EncounterGrade.Champion or EncounterGrade.Unique) <= 1,
            "Vesper graves must have separate ground-level spawns and one shared elite slot");
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
