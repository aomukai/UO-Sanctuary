using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Engines.Spawners;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class MonsterChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(MonsterChecks));
    private static readonly List<Mobile> _mobiles = new();
    private static readonly List<Item> _items = new();
    private static PlayerMobile _player;
    private static GraveyardLich _lich;
    private static Corpse _corpse;
    private static Item _loot;
    private static int _life;
    private static Point3D _point;
    private static CombatRandom _random;
    private static Serial _savedMonster, _savedCorpse;
    public static void Configure()
    {
        ConsoleInputHandler.RegisterCommand("monster-check", "Graveyard behavior checks", _ => Core.LoopContext.Post(() => Step(Setup)));
        ConsoleInputHandler.RegisterCommand("monster-reload-check", "Graveyard persistence checks", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                var serials = JsonSerializer.Deserialize<uint[]>(File.ReadAllText(Path.Join(Core.BaseDirectory, "monster-check.json")));
                var monster = World.FindMobile((Serial)serials[0]) as GraveyardZombie;
                var corpse = World.FindItem((Serial)serials[1]) as Corpse;
                Require(monster != null && monster.HitsMax is >= 7 and <= 12 && ClassStats.Level(monster) == 1
                    && ClassStats.AttackRating(monster) == 8 && ClassStats.Defense(monster) == 5
                    && monster.Hits == (monster.HitsMax + 1) / 2 && monster.DamageMin == 1 && monster.DamageMax == 3
                    && monster.PhysicalResistance == 0 && monster.ColdResistance == 0 && monster.PoisonResistance == 50,
                    "Monster profile or serialized type did not survive restart");
                Require(monster.Home.X == 1382 && monster.Home.Y == 1495 && monster.HomeMap == Map.Trammel
                    && ((LeashedMeleeAI)monster.AIObject).UpdateLeash(Core.TickCount),
                    "A distant saved mob lost its home or did not return on reload");
                Require(corpse is { SanctuaryUndeadKind: 1, Animated: true, SanctuaryNoRewards: true, SanctuaryConsumed: true }, "Corpse revival identity was lost with its deleted owner");
                var raised = World.FindMobile((Serial)serials[2]) as GraveyardUndead;
                Require(raised is { Raised: true, NoKillAwards: true }, "Raised state did not survive restart");
                _logger.Information("monster-reload-check: passed");
            }
            catch (Exception error)
            {
                _logger.Error(error, "monster-reload-check: failed");
            }
        }));
    }
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
    private static void Step(Action next)
    {
        try
        {
            next();
        }
        catch (Exception error)
        {
            _logger.Error(error, "monster-check: failed");
            Cleanup();
        }
    }
    private static void Later(int milliseconds, Action next) => Timer.StartTimer(TimeSpan.FromMilliseconds(milliseconds), () => Step(next));
    private static T Track<T>(T mobile) where T : Mobile
    {
        _mobiles.Add(mobile);
        return mobile;
    }
    private static void Setup()
    {
        _point = CombatPractice.FindFloor(Map.Trammel, 1382, 1495);
        _player = Track(new PlayerMobile { Body = 0x190, Player = true, Name = "MonsterChecks" });
        ClassCreation.Profiles.Assign(_player, DiabloClass.Necromancer);
        _player.MoveToWorld(_point, Map.Trammel);
        CheckLeash();
        _lich = Track(new GraveyardLich { CantWalk = true });
        _lich.MoveToWorld(new Point3D(_point.X + 4, _point.Y, _point.Z), Map.Trammel);
        _lich.AIObject.AITimer.Stop();
        var skeleton = Track(new GraveyardSkeleton { CantWalk = true });
        skeleton.MoveToWorld(new Point3D(_point.X + 1, _point.Y, _point.Z), Map.Trammel);
        skeleton.AIObject.AITimer.Stop();
        Require(ClassCombat.IsHostile(_player, skeleton) && skeleton.HitsMax is >= 7 and <= 11
            && ClassStats.Level(skeleton) == 2 && ClassStats.AttackRating(skeleton) == 12 && ClassStats.Defense(skeleton) == 10,
            "Skeleton did not use its explicit profile");
        _random = new CombatRandom(0.5);
        _life = _player.Hits;
        Require(((BaseWeapon)skeleton.Weapon).OnSwing(skeleton, _player).TotalMilliseconds == 1360
            && _player.Hits == _life, "Monster melee cadence or windup is wrong");
        Later(550, () =>
        {
            Require(_player.Hits == _life - 1, "Skeleton used legacy damage or failed its delayed hit");
            _life = _player.Hits;
            Require(_lich.BeginFirebolt(_player) && !_lich.BeginFirebolt(_player) && _player.Hits == _life,
                "Firebolt ignored windup/cadence");
            Later(750, Bolt);
        });
    }
    private static void Bolt()
    {
        Require(_player.Hits == _life - 6, "Lich Firebolt did not deal fire damage after cast and flight");
        _random.Dispose();
        _random = null;
        _player.Hits = _player.HitsMax;
        var dead = Track(new GraveyardSkeleton());
        dead.MoveToWorld(new Point3D(_point.X + 2, _point.Y + 2, _point.Z), Map.Trammel);
        dead.AIObject.AITimer.Stop();
        _loot = new Dagger();
        dead.PackItem(_loot);
        Require(_loot.Deleted, "Suppressed monster accepted a generated drop");
        dead.Kill();
        _corpse = dead.Corpse as Corpse;
        _items.Add(_corpse);
        Require(_corpse is { SanctuaryUndeadKind: 1 } && _lich.CanRaise(_corpse), "Fresh undead corpse was not revivable");
        _corpse.SanctuaryHarvested = true;
        Require(!_lich.CanRaise(_corpse), "Harvested corpse was revivable");
        _corpse.SanctuaryHarvested = false;
        _corpse.Carved = true;
        Require(!_lich.CanRaise(_corpse), "Carved corpse was revivable");
        _corpse.Carved = false;
        _loot = new Dagger();
        _corpse.DropItem(_loot); // Deliberate retained contents exercise corpse mechanics, independently of death loot.
        Later(2000, () =>
        {
            Require(_lich.BeginRaise(_corpse), "Lich did not begin resurrection");
            Later(950, Raised);
        });
    }
    private static GraveyardUndead FindRaised()
    {
        foreach (var undead in Map.Trammel.GetMobilesInRange<GraveyardUndead>(_point, 15))
        {
            if (undead.Raised && !undead.Deleted)
            {
                return undead;
            }
        }
        return null;
    }
    private static void Raised()
    {
        var raised = FindRaised();
        Require(raised != null && _corpse.Animated && _loot.Parent == _corpse && !_corpse.Deleted
            && !_lich.CanRaise(_corpse), "Revival lost original loot or left the corpse reusable");
        raised.AIObject.AITimer.Stop();
        _mobiles.Add(raised);
        raised.Kill();
        var raisedCorpse = raised.Corpse as Corpse;
        _items.Add(raisedCorpse);
        Require(raisedCorpse is { SanctuaryUndeadKind: 1, SanctuaryNoRewards: true } && !ClassLoot.HasContents(raisedCorpse),
            "Revived enemy produced loot or lost its repeatable revival identity");
        Later(8200, () =>
        {
            Require(_lich.BeginRaise(raisedCorpse), "The same enemy could not be resurrected a second time");
            Later(950, () =>
            {
                var again = FindRaised();
                Require(again is { Raised: true, NoKillAwards: true } && raisedCorpse.Deleted,
                    "Repeated revival reset reward eligibility or retained an empty consumed corpse");
                _mobiles.Add(again);
                again.AIObject.AITimer.Stop();
                again.Kill();
                var body = (Corpse)again.Corpse;
                _items.Add(body);
                Require(body.SanctuaryUndeadKind == 1 && body.SanctuaryNoRewards && !ClassLoot.HasContents(body),
                    "Repeated revival created rewards or lost the next usable corpse");
                HarvestRace();
            });
        });
    }
    private static void HarvestRace()
    {
        // A later resurrection races against harvesting; the pending cast must revalidate.
        var dead = Track(new GraveyardZombie());
        dead.MoveToWorld(new Point3D(_point.X + 3, _point.Y + 2, _point.Z), Map.Trammel);
        dead.Kill();
        var harvested = (Corpse)dead.Corpse;
        _items.Add(harvested);
        Later(8200, () =>
        {
            Require(_lich.BeginRaise(harvested), "Second resurrection did not become ready");
            harvested.SanctuaryHarvested = true;
            Later(950, () =>
            {
                Require(!harvested.Animated && FindRaised() == null, "Lich revived a corpse consumed during windup");
                Persistence();
            });
        });
    }
    private static void Persistence()
    {
        var zombie = new GraveyardZombie { CantWalk = true, Home = _point, HomeMap = Map.Trammel };
        zombie.MoveToWorld(new Point3D(_point.X + 25, _point.Y, _point.Z), Map.Trammel);
        zombie.AIObject.AITimer.Stop();
        // Emulate an injured zombie saved before the early-Normal baseline.
        zombie.SetHits(44);
        zombie.Hits = 22;
        zombie.SetDamage(4, 7);
        zombie.SetResistance(ResistanceType.Physical, 5);
        zombie.SetResistance(ResistanceType.Cold, 20);
        zombie.SetResistance(ResistanceType.Poison, 60);
        _savedMonster = zombie.Serial;
        var skeleton = new GraveyardSkeleton();
        skeleton.MoveToWorld(new Point3D(_point.X + 5, _point.Y + 1, _point.Z), Map.Trammel);
        skeleton.Kill();
        _savedCorpse = skeleton.Corpse.Serial;
        ((Corpse)skeleton.Corpse).DropItem(new Dagger { Name = "Retained revival fixture" });
        // Exercise the actual persistent population installer and idempotency.
        var retired = new Spawner("GraveyardLich")
        {
            Guid = new Guid("06dbf592-96fb-4873-9131-2812edff87da")
        };
        retired.MoveToWorld(new Point3D(1393, 1494, 10), Map.Felucca);
        retired.Respawn();
        GraveyardLich retiredLich = null;
        foreach (var spawn in retired.Spawned.Keys)
        {
            retiredLich ??= spawn as GraveyardLich;
        }
        Require(retiredLich != null, "Could not prepare old saved lich spawner");
        retiredLich.MoveToWorld(new Point3D(1450, 1494, 10), Map.Felucca);
        GraveyardPopulation.Install();
        GraveyardPopulation.Install();
        Require(retired.Deleted && retiredLich.Deleted, "Retired lich spawner or its distant spawned lich survived migration");
        int spawners = 0, residents = 0;
        foreach (var spawner in Map.Felucca.GetItemsInRange<BaseSpawner>(new Point3D(1386, 1494, 10), 20))
        {
            if (spawner.Name?.StartsWith("Sanctuary Graveyard", StringComparison.Ordinal) == true)
            {
                spawners++;
                residents += spawner.Spawned.Count;
            }
        }
        Require(spawners == 2 && residents == 5, "Graveyard population was not five creatures across two stable spawners");
        // Save a raised entity as well as its source corpse.
        Later(8200, () =>
        {
            var corpse = (Corpse)World.FindItem(_savedCorpse);
            Require(_lich.BeginRaise(corpse), "Could not prepare saved raised state");
            Later(950, () =>
            {
                var raised = FindRaised();
                Require(raised != null, "No raised entity for persistence check");
                File.WriteAllText(Path.Join(Core.BaseDirectory, "monster-check.json"), JsonSerializer.Serialize(new[]
                    { _savedMonster.Value, _savedCorpse.Value, raised.Serial.Value }));
                _logger.Information("monster-check: passed");
                Cleanup();
            });
        });
    }
    private static void CheckLeash()
    {
        var mob = Track(new GraveyardZombie { Home = _point, HomeMap = Map.Trammel });
        mob.MoveToWorld(new Point3D(_point.X + 4, _point.Y, _point.Z), Map.Trammel);
        mob.AIObject.AITimer.Stop();
        var ai = (LeashedMeleeAI)mob.AIObject;
        mob.Combatant = _player;
        long now = Core.TickCount;
        Require(!ai.UpdateLeash(now) && !ai.UpdateLeash(now + 11999), "Mob abandoned an ordinary short chase");
        mob.Hits = 3;
        Require(ai.UpdateLeash(now + 12000) && mob.Combatant == null && mob.FocusMob == null
            && !mob.Warmode && !mob.IsEnemy(_player) && !ai.OnAggressiveAction(_player),
            "A timed-out mob kept fighting or could be pulled again while returning");
        mob.MoveToWorld(_point, Map.Trammel);
        Require(ai.UpdateLeash(now + 13000) && !ai.UpdateLeash(now + 16000)
            && !mob.ReturningHome && mob.IsEnemy(_player) && mob.Hits == 3,
            "Return did not finish at home after its pause, or reset healed the mob");
        mob.Combatant = _player;
        mob.MoveToWorld(new Point3D(_point.X + 25, _point.Y, _point.Z), Map.Trammel);
        Require(ai.UpdateLeash(now + 17000), "A mob chased outside its home radius");
        mob.Delete();
        var fighter = Track(new GraveyardSkeleton { Home = _point, HomeMap = Map.Trammel });
        fighter.MoveToWorld(new Point3D(_point.X + 1, _point.Y, _point.Z), Map.Trammel);
        fighter.AIObject.AITimer.Stop();
        fighter.Combatant = _player;
        var fighterAi = (LeashedMeleeAI)fighter.AIObject;
        Require(!fighterAi.UpdateLeash(now) && !fighterAi.UpdateLeash(now + 30000),
            "A mob in attack range abandoned an ongoing fight");
        fighter.Delete();
        var zombie = Track(new GraveyardZombie());
        zombie.GetMoveSpeeds(out double active, out double passive);
        Require(active == 0.96 && passive == 0.96 && zombie.HitsMax is >= 7 and <= 12
            && zombie.CombatProfile.MinDamage == 1 && zombie.CombatProfile.MaxDamage == 3,
            "Zombie speed or early-Normal combat baseline is incorrect");
        zombie.Delete();
    }
    private static void Cleanup()
    {
        _random?.Dispose();
        _random = null;
        foreach (var mobile in _mobiles)
        {
            if (!mobile.Deleted)
            {
                mobile.Delete();
            }
        }
        foreach (var item in _items)
        {
            item?.Delete();
        }
        _mobiles.Clear();
        _items.Clear();
        ClassCreation.Profiles.PruneDeleted();
    }
}
