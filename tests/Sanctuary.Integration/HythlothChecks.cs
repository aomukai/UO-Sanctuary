using System;
using System.Linq;
using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class HythlothChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(HythlothChecks));

    public static void Configure() => ConsoleInputHandler.RegisterCommand("hythloth-check", "Hythloth spawn pool and court checks",
        args => Core.LoopContext.Post(() => Check(args.Trim() switch
        {
            "reload" => CheckReload,
            "cleric" => CheckCleric,
            _ => CheckNewCourt
        }, pending: args.Trim() == "cleric")));

    private static void Check(Action test, bool pending = false)
    {
        try
        {
            test();
            if (!pending)
                _logger.Information("hythloth-check: passed");
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "hythloth-check: failed");
        }
    }

    private static readonly Point3D _point = new(6907, 3812, 0);

    private static void CheckNewCourt()
    {
        CheckPool(new HythLow(), [(HythlothRole.Acolyte, 34), (HythlothRole.RoyalSoldier, 33), (HythlothRole.UndeadServant, 33)]);
        CheckPool(new HythMed(), [(HythlothRole.TempleGuard, 25), (HythlothRole.RoyalArbalest, 25),
            (HythlothRole.Apprentice, 25), (HythlothRole.UndeadCleric, 25)]);
        CheckPool(new HythHigh(), [(HythlothRole.RoyalKnight, 50), (HythlothRole.SkeletalMage, 50)]);

        foreach (var (name, role, level) in new[]
        {
            ("Herald", HythlothRole.Herald, 32), ("Jester", HythlothRole.Jester, 35),
            ("Chamberlain", HythlothRole.Chamberlain, 36), ("Executioner", HythlothRole.Executioner, 38)
        })
        {
            var spawner = PlaceSpawner(new HythBoss(name));
            var boss = spawner.Spawned.Keys.OfType<HythlothEncounter>().Single();
            Require(boss.Basis.Level == level && boss.Role == role && boss.Grade == EncounterGrade.SuperUnique);
            spawner.Delete();
        }

        var player = new PlayerMobile { Body = 0x190, Player = true };
        ClassCreation.Profiles.Assign(player, DiabloClass.Druid);
        player.MoveToWorld(_point, Map.Trammel);
        var herald = Place(HythlothRole.Herald, -3, 0);
        herald.Combatant = player;
        herald.OnThink();
        int CountEscort() => Nearby().Count(actor => actor.Role == HythlothRole.RoyalSoldier);
        Require(CountEscort() == 4);
        herald.OnThink();
        Require(CountEscort() == 4);
        herald.Delete();
        Require(CountEscort() == 0);

        var soldier = Place(HythlothRole.RoyalSoldier, 1, 0);
        var acolyte = Place(HythlothRole.Acolyte, 0, 2);
        var servant = Place(HythlothRole.UndeadServant, -1, 0);
        soldier.Hits /= 2;
        acolyte.Hits /= 2;
        int previousSoldier = soldier.Hits, previousAcolyte = acolyte.Hits;
        servant.OnThink();
        Require(soldier.Hits > previousSoldier && acolyte.Hits == previousAcolyte);

        var knight = Place(HythlothRole.RoyalKnight, 2, 0);
        int lifeBefore = soldier.HitsMax;
        knight.Combatant = player;
        knight.OnThink();
        Require(BattleOrders.Percent(knight) > 0 && soldier.HitsMax > lifeBefore);

        var apprentice = Place(HythlothRole.Apprentice, 3, -2);
        Require(apprentice.BeginFirebolt(player));
        var jester = Place(HythlothRole.Jester, -3, -2);
        EliteBolts.CastFrozenOrb(jester, player);
        Require(EliteBolts.Inspect(jester).Length == 1);
        EliteBolts.Tick();
        Require(EliteBolts.Inspect(jester).Length == 2);

        for (int i = 0; i < 200 && !player.Frozen; i++)
            HythlothStun.TryApply(player);
        Require(player.Frozen && !HythlothStun.TryApply(player));
        player.Frozen = false;
        Require(Enumerable.Range(0, 40).All(_ => !HythlothStun.TryApply(player)));

        foreach (var actor in Nearby())
            actor.Delete();
        player.Delete();
        var current = PlaceSpawner(new HythHigh());
        current.Name = "Current HythHigh";
        CurrentCreature(current).Name = current.Name;
        foreach (var legacy in new Spawner[] { new HythLow(), new HythMed(), new HythHigh() })
        {
            legacy.Count = legacy.Entries.Count;
            foreach (var entry in legacy.Entries)
                entry.SpawnedProbability = 100;

            PlaceSpawner(legacy);
            Require(legacy.Spawned.Count == legacy.Count);
            var survivor = legacy.Spawned.Keys.OfType<HythlothEncounter>().MinBy(actor => actor.Serial.Value);
            survivor.Name = legacy.Name;
        }
        Require(CampaignArenas.All.Single(arena => arena.Dungeon == "Hythloth").BossLevel == 40);
    }

    private static void CheckCleric()
    {
        var cleric = Place(HythlothRole.UndeadCleric, 10, -3);
        var fallen = Place(HythlothRole.Acolyte, 11, -3);
        fallen.Kill();
        Require(fallen.Corpse is Corpse);
        var corpse = (Corpse)fallen.Corpse;
        Require(cleric.CanRaise(corpse));
        // Keep the target tile clear during the native Raise wind-up.
        cleric.CantWalk = true;
        Timer.DelayCall(TimeSpan.FromMilliseconds(3100), () => Check(() =>
        {
            Require(cleric.BeginRaise(corpse));
            Timer.DelayCall(TimeSpan.FromMilliseconds(850), () => Check(() =>
            {
                var raised = World.Mobiles.Values.OfType<HythlothEncounter>()
                    .Single(actor => actor.Role == HythlothRole.RoyalSoldier && actor.Raised);
                Require(raised.NoKillAwards && raised.Raised && raised.Body == 147);
                Require(corpse.Deleted || !cleric.CanRaise(corpse));
                raised.Delete();
                cleric.Delete();
            }));
        }, pending: true));
    }

    private static HythlothEncounter Place(HythlothRole role, int x, int y)
    {
        var actor = new HythlothEncounter(role);
        actor.MoveToWorld(new(_point.X + x, _point.Y + y, _point.Z), Map.Trammel);
        return actor;
    }

    private static List<HythlothEncounter> Nearby()
    {
        var actors = new List<HythlothEncounter>();
        foreach (var actor in Map.Trammel.GetMobilesInRange<HythlothEncounter>(_point, 12))
            actors.Add(actor);
        return actors;
    }

    private static void CheckPool(Spawner spawner, (HythlothRole Role, int Chance)[] pool)
    {
        Require(spawner.Count == 1);
        Require(spawner.Entries.Select(entry => (entry.Parameters, entry.SpawnedProbability))
            .SequenceEqual(pool.Select(entry => (entry.Role.ToString(), entry.Chance))));
        PlaceSpawner(spawner);
        for (int draw = 0; draw < 8; draw++)
        {
            var actor = CurrentCreature(spawner);
            Require(pool.Any(entry => entry.Role == actor.Role));
            Require(actor.Basis.Level >= actor.Dungeon.MinimumLevel && actor.Basis.Level <= actor.Dungeon.MaximumLevel);
            Require(actor.Role != HythlothRole.TempleGuard || actor.Aura != EliteAura.None);
            spawner.Spawn();
            Require(CurrentCreature(spawner) == actor);
            spawner.Respawn();
        }

        spawner.Delete();
    }

    private static HythlothEncounter CurrentCreature(Spawner spawner) =>
        (HythlothEncounter)spawner.Spawned.Keys.Single();

    private static Spawner PlaceSpawner(Spawner spawner)
    {
        spawner.MoveToWorld(_point, Map.Trammel);
        spawner.Respawn();
        return spawner;
    }

    private static void CheckReload()
    {
        var spawners = World.Items.Values.OfType<Spawner>().Where(spawner => spawner is HythLow or HythMed or HythHigh).ToArray();
        Require(spawners.Length == 4);
        foreach (var spawner in spawners)
        {
            Require(spawner.Count == 1 && CurrentCreature(spawner).Name == spawner.Name);
            var chances = spawner.Entries.Select(entry => entry.SpawnedProbability);
            Require(chances.SequenceEqual(spawner switch
            {
                HythLow => [34, 33, 33],
                HythMed => [25, 25, 25, 25],
                _ => [50, 50]
            }));
        }
    }

    private static void Require(bool condition, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string reason = null)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
