using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class HitboxChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Check()
    {
        var start = new Point3D(100, 100, 0);
        var end = new Point3D(112, 112, 0);
        Require(CombatHitboxes.Intersect(start, end, new Point3D(106, 107, 0), out _),
            "Projectile grazing a body's footprint missed");
        Require(!CombatHitboxes.Intersect(start, end, new Point3D(106, 108, 0), out _),
            "Projectile hit outside the body's footprint");
        Require(!CombatHitboxes.Intersect(start, end, new Point3D(106, 106, 20), out _),
            "Projectile hit a creature on another floor");
        Require(!CombatHitboxes.Intersect(start, end, new Point3D(114, 114, 0), out _),
            "Projectile hit beyond its endpoint");
        var player = new PlayerMobile { Body = 0x190, Name = "HitboxCheck", Player = true };
        var front = new Skeleton { Frozen = true };
        var rear = new Skeleton { Frozen = true };
        var dagger = new Dagger();
        ClassGear.Profiles.MarkStarter(dagger);
        try
        {
            ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
            player.Str = player.Int = 100;
            player.Hits = player.Mana = 100;
            var point = CombatPractice.FindFloor(Map.Felucca, 1382, 1495);
            player.MoveToWorld(point, Map.Felucca);
            player.EquipItem(dagger);
            front.MoveToWorld(new Point3D(point.X + 2, point.Y, point.Z), Map.Felucca);
            rear.MoveToWorld(new Point3D(point.X + 4, point.Y, point.Z), Map.Felucca);
            front.SetHits(100);
            rear.SetHits(100);
            front.Hits = rear.Hits = 100;
            var now = Core.TickCount;
            Require(CombatTestActions.Execute(player, CombatAbility.Teeth, rear, rear.Location, now)
                && front.Hits == 94 && rear.Hits == 100, "A targeted projectile skipped the first body in its path");
            Require(CombatTestActions.Execute(player, CombatAbility.Teeth, null, rear.Location, now + CombatTiming.For(player, CombatAbility.Teeth).DurationMs)
                && front.Hits == 88 && rear.Hits == 100, "Ground and unit aim use different collision rules");
            front.Hits = 1;
            Require(ClassCombat.ContinueCast(player, CombatAbility.Teeth, rear, rear.Location,
                now + 2 * CombatTiming.For(player, CombatAbility.Teeth).DurationMs), "Could not begin intercepted held cast");
            ClassCombat.ResolveAction(player, ClassCombat.For(player).EffectAt);
            Require(!front.Alive && rear.Hits == 100 && ClassCombat.For(player).HeldAbility == CombatAbility.Teeth
                && ClassCombat.For(player).CastTarget == rear.Serial,
                "An intercepting body's death discarded the still-living selected target");
            ClassCombat.ReleaseCast(player);
        }
        finally
        {
            player.Delete();
            front.Delete();
            rear.Delete();
            dagger.Delete();
            ClassCreation.Profiles.PruneDeleted();
        }
    }
}
