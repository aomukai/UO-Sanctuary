using System;
using System.Linq;
using Server;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class DestardChecks
{
    private const string Patron = "Destard bounty fixture";
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(DestardChecks));

    public static void Configure() => ConsoleInputHandler.RegisterCommand("destard-check", "Destard pool, bounty and Diablo aid checks",
        args => Core.LoopContext.Post(() =>
        {
            try
            {
                if (args.Trim() == "reload")
                    Reload();
                else
                    NewCourt();
                _logger.Information("destard-check: passed");
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "destard-check: failed");
            }
        }));

    private static void NewCourt()
    {
        var arena = CampaignArenas.All.Single(entry => entry.Victory == CampaignProgress.Diablo);
        CheckPool(new DestLow(), arena.Center, [34, 33, 33]);
        CheckPool(new DestMed(), arena.Center, [50, 50]);
        CheckPool(new DestHigh(), arena.Center, [50, 50]);
        CheckPool(new DestSpawn(), arena.Center, [50, 50]);

        Require(PortraitProtocol.IsNpcPortrait((ushort)NpcPortrait.DestardQueen)
            && PortraitProtocol.IsNpcPortrait((ushort)NpcPortrait.DestardConsort));

        var player = new PlayerMobile { Body = 0x190, Player = true, Name = Patron };
        ClassCreation.Profiles.Assign(player, DiabloClass.Druid);
        int mid = DiabloCave.Midpoint;
        player.MoveToWorld(arena.Offset(mid - 10, mid), Map.Trammel);
        Require(DestardBounty.For(player) == null);
        var favor = DestardBounty.Record(player, 9);
        Require(favor is { Horns: 9, FirstAt: >= 10 and <= 19, SecondAt: >= 20 and <= 29, First: false, Second: false });
        Require(DestardBounty.Record(player, favor.FirstAt - 9) is { First: true, Second: false });
        Require(DestardBounty.Record(player, favor.SecondAt - favor.FirstAt) is { First: true, Second: true });
        Require(DestardBounty.Record(player, 100).Horns == favor.SecondAt + 100);

        CheckHorn(DestardRole.LesserDaemon, player, arena.Center, expected: true);
        CheckHorn(DestardRole.Daemon, player, arena.Center, expected: true);
        CheckHorn(DestardRole.Archdaemon, player, arena.Center, expected: true);
        CheckHorn(DestardRole.DaemonSpawn, player, arena.Center, expected: false);

        var archdaemon = new DestardEncounter(DestardRole.Archdaemon);
        var hatchling = new DestardEncounter(DestardRole.CorruptedHatchling);
        var spawn = new DestardEncounter(DestardRole.DaemonSpawn);
        archdaemon.MoveToWorld(arena.Offset(mid - 11, mid), Map.Trammel);
        hatchling.MoveToWorld(arena.Offset(mid - 11, mid + 1), Map.Trammel);
        spawn.MoveToWorld(arena.Offset(mid - 10, mid + 1), Map.Trammel);
        Require(EliteAuras.SkillLevel(archdaemon) == 10 && EliteAuras.SkillLevel(hatchling) == 10);
        Require(EliteAuras.Level(player, EliteAura.Conviction, true) == 10);
        EliteAuras.Refresh(player);
        double auraOnly = NamedElements.Damage(archdaemon, player, 100, "fire");
        Require(NecromancerCurseCasts.NpcDamageCurse(spawn, player, CombatAbility.LowerResist, 10));
        Require(NecromancerEffects.Curse(player) is { Rank: 10, NonStackingResistance: true });
        Require(Math.Abs(NamedElements.Damage(archdaemon, player, 100, "fire") - auraOnly) < 0.1);
        spawn.Delete();
        hatchling.Delete();
        archdaemon.Delete();

        CampaignArenas.EnsureBoss(arena);
        var boss = arena.Boss;
        boss.Hits = (boss.HitsMax * 3 + 3) / 4 + 1;
        boss.OnThink();
        Require(boss.DestardChampion == null);
        boss.Hits = boss.HitsMax * 3 / 4;
        boss.OnThink();
        var first = boss.DestardChampion;
        Require(DestardAid.HasPatron(boss, arena, false), $"missing patron at {player.Location}, boss {boss.Location}, {DestardBounty.For(player)}");
        Require(first is { Alive: true, Deleted: false }, $"missing consort at boss {boss.Location}, hp {boss.Hits}/{boss.HitsMax}");
        Require(boss.Combatant == first, $"boss target {boss.Combatant}, consort {first}, harmful {boss.CanBeHarmful(first, false)}");
        Require(!first.CanBeHarmful(boss, false));
        Require(first.Until > Core.Now.AddSeconds(55) && first.Until <= Core.Now.AddMinutes(1));
        Require(EliteCombat.Enemy(boss, first, 1));
        boss.Combatant = player;
        boss.Damage(1, player);
        Require(boss.Combatant == first);
        boss.Combatant = null;
        Require(boss.Combatant == first);
        int life = first.Hits;
        EliteCombat.Hit(boss, first, 50, ResistanceType.Fire);
        Require(first.Hits < life && first.Alive);
        first.Damage(first.Hits * 2, boss);
        Require(first.Alive && first.Hits == 1);
        first.Hits = 200;
        first.Heal(200);
        Require(first.Hits == 1 && !first.CanRegenHits);

        first.Until = Core.Now;
        boss.Hits = boss.HitsMax / 4;
        boss.OnThink();
        Require(first.Deleted && boss.DestardChampion is { } final && final != first && final.Until == DateTime.MaxValue);
        Require(boss.Combatant == boss.DestardChampion);
    }

    private static void CheckPool(Spawner spawner, Point3D point, int[] odds)
    {
        Require(spawner.Count == 1 && spawner.Entries.Select(entry => entry.SpawnedProbability).SequenceEqual(odds));
        spawner.MoveToWorld(point, Map.Trammel);
        spawner.Respawn();
        var creature = spawner.Spawned.Keys.OfType<DestardEncounter>().Single();
        Require(creature.Difficulty == MonsterDifficulty.Hell && creature.Basis.Level >= creature.Dungeon.MinimumLevel
            && creature.Basis.Level <= creature.Dungeon.MaximumLevel);
        Require(EncounterTreasure.Root(creature).StartsWith("Act ", StringComparison.Ordinal));
        spawner.Spawn();
        Require(spawner.Spawned.Count == 1);
        spawner.Delete();
    }

    private static void CheckHorn(DestardRole role, PlayerMobile player, Point3D point, bool expected)
    {
        var creature = new DestardEncounter(role);
        creature.MoveToWorld(point, Map.Trammel);
        creature.Kill();
        var corpse = (Corpse)creature.Corpse;
        var reward = new RewardLife(creature, corpse, player, creature.Basis.Level, 0, 0, MonsterDifficulty.Hell);
        EncounterTreasure.Award(reward, creature, new HornRoll(9));
        Require(corpse.GetAmount(typeof(DaemonHorn)) == 0);
        EncounterTreasure.Award(reward, creature, new HornRoll(8));
        Require(corpse.GetAmount(typeof(DaemonHorn)) == (expected ? 1 : 0));
        corpse.Delete();
    }

    private sealed class HornRoll(int outcome) : Random(17)
    {
        public override int Next(int maxValue) => maxValue == 10 ? outcome : base.Next(maxValue);
    }

    private static void Reload()
    {
        var player = World.Mobiles.Values.OfType<PlayerMobile>().Single(entry => entry.Name == Patron);
        Require(DestardBounty.For(player) is { First: true, Second: true } favor
            && favor.Horns == favor.SecondAt + 100);
        var boss = CampaignArenas.All.Single(entry => entry.Victory == CampaignProgress.Diablo).Boss;
        Require(boss is { DestardFirst: true, DestardFinal: true, DestardChampion: { } tank });
        Require(boss.DestardChampion.Protecting(boss));
        var consort = boss.DestardChampion;
        boss.Delete();
        Require(consort.Deleted);
    }

    private static void Require(bool condition, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string reason = null)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
