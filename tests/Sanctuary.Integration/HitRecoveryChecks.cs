using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class HitRecoveryChecks
{
    private static void Require(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
    public static void Check()
    {
        Require(HitRecovery.Chance(0, 160) == 0 && HitRecovery.Chance(9, 160) == 0
            && HitRecovery.Chance(10, 160) == 3 && HitRecovery.Chance(19, 160) == 3
            && HitRecovery.Chance(20, 160) == 6 && HitRecovery.Chance(39, 160) == 6
            && HitRecovery.Chance(40, 160) == 8, "Damage thresholds/probability bands changed");
        int[] fhr = [0, 5, 10, 16, 26, 39, 56, 86, 152, 377];
        for (int i = 0; i < fhr.Length; i++)
        {
            Require(HitRecovery.Frames(DiabloClass.Necromancer, fhr[i]) == 13 - i,
                "Necromancer FHR breakpoint differs from legacy human animation");
        }
        Require(HitRecovery.Frames(DiabloClass.Barbarian) == 9 && HitRecovery.Frames(DiabloClass.Sorceress) == 15,
            "Class recovery profiles collapsed into one timing");
        foreach (DiabloClass characterClass in Enum.GetValues<DiabloClass>())
        {
            int previous = int.MaxValue;
            for (int bonus = 0; bonus < 1000; bonus++)
            {
                int frames = HitRecovery.Frames(characterClass, bonus);
                Require(frames > 0 && frames <= previous, "FHR made recovery slower or unbounded");
                previous = frames;
            }
        }
        var player = new PlayerMobile { Player = true, Body = 0x190, Name = "RecoveryCheck" };
        var legacy = new PlayerMobile { Body = 0x190, Name = "LegacyRecovery" };
        var enemy = new Skeleton { Frozen = true };
        var dagger = new Dagger();
        ClassGear.Profiles.MarkStarter(dagger);
        try
        {
            ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
            player.Str = player.Dex = 100;
            var point = CombatPractice.FindFloor(Map.Trammel, 5450, 1150);
            player.MoveToWorld(point, Map.Trammel);
            enemy.MoveToWorld(new Point3D(point.X + 1, point.Y, point.Z), player.Map);
            player.EquipItem(dagger);
            long now = Core.TickCount;
            Require(ClassCombat.Perform(player, CombatAbility.BasicAttack, enemy, enemy.Location, now), "Cannot start interrupt fixture");
            var session = ClassCombat.For(player);
            long deadline = session.NextAction;
            Require(!HitRecovery.Apply(player, 1, now), "Weak stray hit always interrupted movement");
            Require(HitRecovery.Apply(player, player.HitsMax, now), "Large physical hit did not trigger recovery");
            Require(session.RecoveryUntil == now + 520 && session.PendingAbility == CombatAbility.None
                && session.NextAction == deadline && !player.Frozen && !player.Paralyzed,
                "Recovery failed to interrupt effect, refunded cycle or used native freeze state");
            Require(!player.Move(Direction.East | Direction.Running) && player.Location == point,
                "Server accepted movement during recovery");
            Require(!ClassCombat.Perform(player, CombatAbility.Teeth, enemy, enemy.Location, now + 500)
                && !ClassCombat.CastSummon(player, Serial.Zero, now + 500), "Casting bypassed recovery");
            Require(!HitRecovery.Apply(player, player.HitsMax, now + 300) && session.RecoveryUntil == now + 520,
                "A hit refreshed recovery while already recovering");
            Require(HitRecovery.Active(player, now + 519) && !HitRecovery.Active(player, now + 520),
                "Recovery deadline is off by a tick");
            // Staggered attackers can chain new recoveries; there is no global immunity after a recovery.
            Require(HitRecovery.Apply(player, player.HitsMax, now + 520) && session.RecoveryUntil == now + 1040,
                "A pack cannot start another recovery after the first ends");
            Require(!HitRecovery.Apply(legacy, 1000, now) && ClassCombatHooks.MovementAllowed(legacy),
                "Legacy players received class recovery");
        }
        finally
        {
            player.Delete();
            legacy.Delete();
            enemy.Delete();
            dagger.Delete();
            ClassCreation.Profiles.PruneDeleted();
        }
    }
}
