using System;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class NormalMonsterChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(NormalMonsterChecks));
    private static PlayerMobile _player;
    private static GraveyardUndead _mob;
    private static CombatRandom _random;
    private static Point3D _point;
    private static int _case, _life;
    public static void Configure() => ConsoleInputHandler.RegisterCommand("normal-monster-check",
        "Early D2 Normal stats and real-timer attack checks", _ => Core.LoopContext.Post(() => Run(Setup)));
    private static void Require(bool value, string reason)
    {
        if (!value) throw new InvalidOperationException(reason);
    }
    private static void Run(Action action)
    {
        try { action(); }
        catch (Exception error)
        {
            _logger.Error(error, "normal-monster-check: failed");
            Cleanup();
        }
    }
    private static void Later(int ms, Action action) => Timer.StartTimer(TimeSpan.FromMilliseconds(ms), () => Run(action));
    private static void Setup()
    {
        _case = 0;
        _point = CombatPractice.FindFloor(Map.Trammel, 1440, 1695);
        _player = new PlayerMobile { Player = true, Body = 0x190, Name = "NormalMonsterChecks" };
        ClassCreation.Profiles.Assign(_player, DiabloClass.Necromancer);
        _player.MoveToWorld(_point, Map.Trammel);
        var sk = EarlyNormalMonsters.Skeleton;
        var zm = EarlyNormalMonsters.Zombie;
        Require(sk.Attack1.DurationMs == 760 && sk.Attack1.EffectMs == 480 && sk.Attack2.EffectMs == 440
            && zm.Attack1.DurationMs == 640 && zm.Attack1.EffectMs == 360
            && sk.AiDelayMs == 600 && zm.AiDelayMs == 600,
            "Animation action/end events or post-animation AI delay differ from D2 records");
        Require(sk.AttackChance == 75 && sk.Attack1Chance == 75 && sk.StallMs == 600
            && zm.AttackChance == 100 && zm.Attack1Chance == 20
            && sk.CriticalChance == 5 && zm.CriticalChance == 5 && sk.Experience == 34 && zm.Experience == 33,
            "Normal AI choice, critical or base XP records are wrong");
        for (int i = 0; i < 30; i++)
        {
            var skeleton = new GraveyardSkeleton();
            var zombie = new GraveyardZombie();
            Require(skeleton.HitsMax is >= 7 and <= 11 && zombie.HitsMax is >= 7 and <= 12
                && ClassStats.AttackRating(skeleton) == 12 && ClassStats.AttackRating(zombie) == 8
                && ClassStats.Defense(skeleton) == 10 && ClassStats.Defense(zombie) == 5
                && ClassStats.Level(skeleton) == 2 && ClassStats.Level(zombie) == 1
                && skeleton.PhysicalResistance == 0 && zombie.ColdResistance == 0
                && skeleton.PoisonResistance == 50 && zombie.PoisonResistance == 50,
                "Fresh monster stats differ from vanilla Normal records");
            skeleton.Delete();
            zombie.Delete();
        }
        _mob = new GraveyardSkeleton { CantWalk = true };
        _mob.MoveToWorld(new Point3D(_point.X + 1, _point.Y, _point.Z), Map.Trammel);
        _mob.AIObject.AITimer.Stop();
        using (new CombatRandom(0.99))
        {
            long now = Core.TickCount;
            _life = _player.Hits;
            Require(_mob.BeginMelee(_player, now).TotalMilliseconds == 600
                && _mob.NextMeleeDecision == now + 600 && _player.Hits == _life,
                "Skeleton's failed attack decision did not idle for 15 frames");
            Require(_mob.BeginMelee(_player, now + 599).TotalMilliseconds == 40
                && _mob.NextMeleeDecision == now + 600,
                "A retry bypassed the skeleton's idle decision");
        }
        _mob.Delete();
        CheckRecovery();
        NextCase();
    }
    private static void CheckRecovery()
    {
        var zombie = new GraveyardZombie();
        zombie.MoveToWorld(_point, Map.Trammel);
        zombie.AIObject.AITimer.Stop();
        try
        {
            zombie.SetHits(8);
            zombie.Hits = 6;
            zombie.RecoverLife(1000);
            for (int i = 1; i <= 10; i++) zombie.RecoverLife(1000 + i * 1000);
            Require(zombie.Hits == 6, "Monster recovery rounded fractional life up early");
            zombie.RecoverLife(11240);
            Require(zombie.Hits == 7, "DamageRegen=2 did not recover one life in 256 game frames");
            zombie.RecoverLife(99999999);
            Require(zombie.Hits == 7, "An inactive-sector gap banked recovery");
        }
        finally { zombie.Delete(); }
    }
    private static void NextCase()
    {
        _random?.Dispose();
        _random = null;
        _mob?.Delete();
        if (_case == 7)
        {
            _logger.Information("normal-monster-check: passed");
            Cleanup();
            return;
        }
        _player.MoveToWorld(_point, Map.Trammel);
        _player.Hits = _player.HitsMax;
        _mob = _case < 2 ? new GraveyardSkeleton { CantWalk = true } : new GraveyardZombie { CantWalk = true };
        _mob.MoveToWorld(new Point3D(_point.X + 1, _point.Y, _point.Z), Map.Trammel);
        _mob.AIObject.AITimer.Stop();
        // World AI shares the generator between timer callbacks; critical/miss cases
        // use stable fallbacks after the synchronous A1/A2 choice.
        _random = _case switch
        {
            0 => new CombatRandom(0.5, 0.5, 0.5, 0.5, 0.5),
            1 => new CombatRandom(0.5, 0.5, 0.9, 0.5, 0.5),
            2 => new CombatRandom(0.5, 0.1, 0.5, 0.5),
            4 => new CombatRandom(0, 0.5),
            5 => new CombatRandom(0.99, 0.5),
            _ => new CombatRandom(0.5, 0.5, 0.5, 0.5)
        };
        _life = _player.Hits;
        long now = Core.TickCount;
        int cycle = _case < 2 ? 1360 : 1240;
        int effect = _case == 0 ? 480 : _case == 1 ? 440 : 360;
        Require(_mob.BeginMelee(_player, now).TotalMilliseconds == cycle, "Wrong attack/recovery cycle");
        long deadline = _mob.NextMeleeDecision;
        Require(_mob.BeginMelee(_player, now + 100).TotalMilliseconds == cycle - 100
            && _mob.NextMeleeDecision == deadline && _player.Hits == _life,
            "A retry overlapped a pending strike or extended its cooldown");
        if (_case == 6)
        {
            _player.MoveToWorld(new Point3D(_point.X + 5, _point.Y, _point.Z), Map.Trammel);
            _mob.Combatant = _player;
            var before = _mob.Location;
            _mob.AIObject.Think();
            Require(_mob.Location == before, "An attacking monster pursued its target during the attack/idle lock");
        }
        Later(effect - 70, () =>
        {
            Require(_player.Hits == _life, "Monster damage preceded its action frame");
            Later(140, () =>
            {
                int damage = _case < 3 ? 1 : _case == 3 ? 2 : _case == 4 ? 4 : 0;
                Require(_player.Hits == _life - damage, $"Wrong A1/A2/critical/miss/range result in case {_case}");
                _case++;
                NextCase();
            });
        });
    }
    private static void Cleanup()
    {
        _random?.Dispose();
        _random = null;
        _mob?.Delete();
        _player?.Delete();
        ClassCreation.Profiles.PruneDeleted();
    }
}
