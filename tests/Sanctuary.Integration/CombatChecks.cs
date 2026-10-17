using System;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CombatChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CombatChecks));
    public static void Configure()
    {
        ConsoleInputHandler.RegisterCommand("combat-check", "Check class combat in a disposable world", _ =>
            Core.LoopContext.Post(() =>
            {
                try
                {
                    using var random = new CombatRandom();
                    TimingChecks.Check();
                    Check();
                    CheckHeld();
                    StationaryAttackChecks.Check();
                    MovementChecks.Check();
                    ResourceChecks.Check();
                    HitboxChecks.Check();
                    LootChecks.Check();
                    _logger.Information("combat-check: passed");
                }
                catch (Exception error)
                {
                    _logger.Error(error, "combat-check: failed");
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
    private static void Check()
    {
        var map = Map.Felucca;
        var point = CombatPractice.FindFloor(map, 1382, 1495);
        var player = new PlayerMobile { Body = 0x190, Name = "CombatCheck", Player = true };
        var other = new PlayerMobile { Body = 0x190, Name = "OtherClass", Player = true };
        var legacy = new PlayerMobile { Body = 0x190, Name = "LegacyCombat" };
        var enemy = new Skeleton { Frozen = true };
        var second = new Skeleton { Frozen = true };
        var vendor = new Blacksmith();
        var cow = new Cow { FightMode = FightMode.Aggressor };
        var pet = new Skeleton { Controlled = true, ControlMaster = player };
        var dagger = new Dagger();
        ClassGear.Profiles.MarkStarter(dagger);
        var otherDagger = new Dagger();
        try
        {
            ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
            ClassCreation.Profiles.Assign(other, DiabloClass.Barbarian);
            other.Str = 30;
            player.Str = 100;
            player.Int = 50;
            player.Hits = 100;
            player.Mana = 50;
            foreach (var mobile in new Mobile[] { player, other, legacy, enemy, second, vendor, cow, pet })
            {
                mobile.MoveToWorld(point, map);
            }
            enemy.SetHits(200);
            enemy.Hits = 200;
            second.SetHits(200);
            second.Hits = 200;
            Require(ClassCombat.IsHostile(player, enemy), "Skeleton was not hostile");
            foreach (var friendly in new Mobile[] { player, other, legacy, vendor, cow, pet })
            {
                Require(!ClassCombat.IsHostile(player, friendly), "A friendly/neutral/player/pet was classified hostile");
                Require(!ClassCombat.Begin(player, friendly), "Friendly attack was accepted");
            }
            player.Combatant = enemy;
            player.Warmode = true;
            Require(player.Combatant == null && !player.Warmode, "Legacy player combat leaked into class controls");
            legacy.Combatant = enemy;
            legacy.Warmode = true;
            Require(legacy.Combatant == enemy && legacy.Warmode, "Legacy staff combat was changed");
            legacy.Combatant = null;
            Require(ClassCombat.TeethRank(player) == 0, "Unequipped weapon granted Teeth");
            Require(player.EquipItem(dagger) && ClassCombat.TeethRank(player) == 1, "Equipped dagger did not grant Teeth");
            Require(other.EquipItem(otherDagger) && ClassCombat.TeethRank(other) == 0, "Dagger granted another class Teeth");
            ClassEquipment.Prepare(player, true);
            Require(dagger.Name == ClassEquipment.NecromancerDaggerName, "Starter dagger name is missing");
            using (var properties = new ObjectPropertyList(dagger))
            {
                dagger.GetProperties(properties);
                properties.Terminate();
                Require(properties.Buffer.AsSpan().IndexOf(System.Text.Encoding.Unicode.GetBytes(ClassEquipment.TeethProperty)) >= 0,
                    "Dagger tooltip is missing the Teeth grant");
            }
            otherDagger.Delete();
            Require(other.EquipItem(dagger) && ClassCombat.TeethRank(other) == 0,
                "Barbarian could not wield the Necromancer dagger safely");
            Require(!CombatTestActions.Execute(other, CombatAbility.Teeth, enemy, enemy.Location, Core.TickCount),
                "Barbarian cast Teeth using a transferred dagger");
            Require(CombatTestActions.Execute(other, CombatAbility.BasicAttack, enemy, enemy.Location, Core.TickCount),
                "Barbarian could not use the transferred dagger for melee");
            player.EquipItem(dagger);
            var session = ClassCombat.For(player);
            var now = Core.TickCount;
            enemy.MoveToWorld(CombatPractice.FindFloor(map, point.X + 5, point.Y + 1), map);
            Require(ClassCombat.Begin(player, enemy), "Could not select distant enemy");
            var hp = enemy.Hits;
            var hitDamage = CombatTestActions.HitDamage(player, enemy);
            CombatTestActions.Step(player, now);
            Require(enemy.Hits == hp, "Basic attack hit from outside melee range");
            player.MoveToWorld(enemy.Location, map);
            CombatTestActions.Step(player, now);
            Require(enemy.Hits == hp - hitDamage, "Basic attack failed at zero UO combat skills");
            CombatTestActions.Step(player, now);
            Require(enemy.Hits == hp - hitDamage, "Repeated tick bypassed cooldown");
            ClassCombat.Stop(player);
            ClassCombat.Begin(player, enemy);
            CombatTestActions.Step(player, now);
            Require(enemy.Hits == hp - hitDamage, "Cancel/restart reset cooldown");
            now += CombatTiming.For(player, CombatAbility.BasicAttack).DurationMs;
            CombatTestActions.Step(player, now);
            Require(enemy.Hits == hp - hitDamage * 2, "Selected target was not attacked repeatedly");
            ClassCombat.Stop(player);
            CombatTestActions.Step(player, now + 10000);
            Require(enemy.Hits == hp - hitDamage * 2, "Attack continued after Stop");
            now += CombatTiming.For(player, CombatAbility.BasicAttack).DurationMs;
            var mana = player.Mana;
            Require(CombatTestActions.Execute(player, CombatAbility.Teeth, enemy, enemy.Location, now), "Weapon-granted Teeth failed");
            Require(enemy.Hits == hp - hitDamage * 2 - 6 && player.Mana == mana - 1, "Teeth damage/mana is wrong");
            Require(!CombatTestActions.Execute(player, CombatAbility.Teeth, enemy, enemy.Location, now), "Rapid cast bypassed cooldown");
            player.RemoveItem(dagger);
            Require(ClassCombat.TeethRank(player) == 0, "Unequipping retained the weapon grant");
            Require(!CombatTestActions.Execute(player, CombatAbility.Teeth, enemy, enemy.Location, now + 800), "Unavailable Teeth cast succeeded");
            player.EquipItem(dagger);
            player.Mana = 0;
            Require(!CombatTestActions.Execute(player, CombatAbility.Teeth, enemy, enemy.Location, now + 800), "Cast ignored mana");
            player.Mana = 50;
            player.Frozen = true;
            Require(!CombatTestActions.Execute(player, CombatAbility.Teeth, enemy, enemy.Location, now + 800), "Frozen player cast");
            player.Frozen = false;
            Require(!CombatTestActions.Execute(player, CombatAbility.Teeth, vendor, vendor.Location, now + 800), "Teeth hit a vendor");
            Require(CombatTestActions.Execute(player, CombatAbility.Teeth, null,
                new Point3D(player.X + 12, player.Y, player.Z), now + 800), "Distant aim did not fire");
            ClassCreation.Profiles.AdvanceLevel(player, 2);
            Require(ClassCreation.Profiles.TryTrain(player.Serial, "necromancer.teeth", out _)
                && ClassCombat.TeethRank(player) == 2, "Allocated and weapon ranks did not add");
            player.RemoveItem(dagger);
            Require(ClassCombat.TeethRank(player) == 1, "Unequipping removed an allocated rank");
            enemy.Hits = 1;
            ClassCombat.Begin(player, enemy);
            CombatTestActions.Step(player, now + 1600);
            Require((enemy.Deleted || !enemy.Alive) && session.Target == Serial.Zero, "Target death did not stop combat");
            CombatTestActions.Step(player, now + 10000);
            Require(second.Hits == 200 && session.Target == Serial.Zero, "Combat acquired a replacement enemy");
            Require(player.Combatant == null && !player.Warmode, "An attack enabled stock combat/war mode");
            foreach (var skill in player.Skills)
            {
                Require(skill.BaseFixedPoint == 0, $"Combat awarded a UO skill gain: {skill.SkillName}={skill.BaseFixedPoint}");
            }
        }
        finally
        {
            foreach (var mobile in new Mobile[] { player, other, legacy, enemy, second, vendor, cow, pet })
            {
                mobile.Delete();
            }
            dagger.Delete();
            otherDagger.Delete();
            ClassCreation.Profiles.PruneDeleted();
        }
    }
    private static void CheckHeld()
    {
        var player = new PlayerMobile { Body = 0x190, Name = "HeldCheck", Player = true };
        var enemy = new Skeleton { Frozen = true };
        var far = new Skeleton { Frozen = true };
        var dagger = new Dagger();
        ClassGear.Profiles.MarkStarter(dagger);
        try
        {
            ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
            player.Str = player.Int = 100;
            player.Hits = player.Mana = 100;
            var point = CombatPractice.FindFloor(Map.Felucca, 1382, 1495);
            player.MoveToWorld(point, Map.Felucca);
            enemy.MoveToWorld(point, Map.Felucca);
            far.MoveToWorld(new Point3D(point.X + CombatProtocol.TeethRange + 4, point.Y, point.Z), Map.Felucca);
            enemy.SetHits(1000);
            enemy.Hits = 1000;
            far.SetHits(1000);
            far.Hits = 1000;
            player.EquipItem(dagger);
            var now = Core.TickCount;
            for (var elapsed = 0; elapsed < 2400; elapsed += CombatProtocol.CombatTickMs)
            {
                if (elapsed % CombatProtocol.HoldHeartbeatMs == 0)
                {
                    Require(ClassCombat.ContinueCast(player, CombatAbility.Teeth, enemy, enemy.Location, now + elapsed),
                        "Held cast request was rejected");
                }
                // Extra requests cannot turn the heartbeat into a higher fire rate.
                ClassCombat.ContinueCast(player, CombatAbility.Teeth, enemy, enemy.Location, now + elapsed);
                ClassCombat.UpdateCombat(player, now + elapsed);
            }
            Require(enemy.Hits == 976 && player.Mana == 96, "Held casting bypassed or missed its class casting cadence");
            ClassCombat.ReleaseCast(player);
            ClassCombat.UpdateCombat(player, now + 4000);
            Require(enemy.Hits == 976, "Release did not stop held casting");
            now += 5000;
            ClassCombat.ContinueCast(player, CombatAbility.Teeth, enemy, enemy.Location, now);
            ClassCombat.UpdateCombat(player, now + 800);
            Require(enemy.Hits == 970 && ClassCombat.For(player).HeldAbility == CombatAbility.None,
                "Missing heartbeat did not expire the held cast");
            now += 1600;
            ClassCombat.ContinueCast(player, CombatAbility.Teeth, enemy, enemy.Location, now);
            player.RemoveItem(dagger);
            ClassCombat.UpdateCombat(player, now + 40);
            Require(ClassCombat.For(player).HeldAbility == CombatAbility.None, "Unequip did not stop held casting");
            player.EquipItem(dagger);
            enemy.Hits = 1;
            now += 800;
            ClassCombat.ContinueCast(player, CombatAbility.Teeth, enemy, enemy.Location, now);
            ClassCombat.ResolveAction(player, ClassCombat.For(player).EffectAt);
            Require((enemy.Deleted || !enemy.Alive) && ClassCombat.For(player).HeldAbility == CombatAbility.None,
                "Held target death did not stop casting");
            ClassCombat.UpdateCombat(player, now + 1600);
            Require(far.Hits == 1000, "Held casting acquired a replacement target");
            // Find an unobstructed eastward corridor in a bounded Britain area.
            // World art contains fences and trees; those must still clip projectiles.
            var found = false;
            for (var x = 1320; x <= 1440 && !found; x += 20)
            {
                for (var y = 1470; y <= 1550 && !found; y += 20)
                {
                    try
                    {
                        var candidate = CombatPractice.FindFloor(Map.Felucca, x, y);
                        player.MoveToWorld(candidate, Map.Felucca);
                        var end = ClassCombat.TeethEndpoint(player, new Point3D(candidate.X + 50, candidate.Y, candidate.Z));
                        if (end.X == candidate.X + CombatProtocol.TeethRange && end.Y == candidate.Y)
                        {
                            point = candidate;
                            found = true;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Water and blocked floor candidates are not test corridors.
                    }
                }
            }
            Require(found, "No full-length projectile corridor found near Britain");
            far.MoveToWorld(new Point3D(point.X + CombatProtocol.TeethRange + 4, point.Y, point.Z), Map.Felucca);
            // A short or distant aim describes the same bounded direction.
            var nearAim = new Point3D(point.X + 1, point.Y, point.Z);
            var distantAim = new Point3D(point.X + 50, point.Y, point.Z);
            var endpoint = ClassCombat.TeethEndpoint(player, distantAim);
            Require(endpoint == ClassCombat.TeethEndpoint(player, nearAim)
                && endpoint.X == point.X + CombatProtocol.TeethRange && endpoint.Y == point.Y,
                $"Aim distance changed projectile reach: origin={point}, endpoint={endpoint}, near={ClassCombat.TeethEndpoint(player, nearAim)}");
            now += 2400;
            Require(CombatTestActions.Execute(player, CombatAbility.Teeth, far, far.Location, now) && far.Hits == 1000,
                "Distant unit aim failed or damaged beyond Teeth range");
            var rayTarget = new Skeleton { Frozen = true };
            try
            {
                rayTarget.MoveToWorld(new Point3D(point.X + 12, point.Y, point.Z), Map.Felucca);
                rayTarget.SetHits(100);
                rayTarget.Hits = 100;
                Require(CombatTestActions.Execute(player, CombatAbility.Teeth, null, distantAim, now + 800)
                    && rayTarget.Hits == 94 && far.Hits == 1000, "Far ground aim did not hit the bounded ray target");
                Require(CombatTestActions.Execute(player, CombatAbility.Teeth, null, nearAim, now + 1600)
                    && rayTarget.Hits == 88, "Short ground aim incorrectly shortened the projectile");
                var wall = new Static(0x80);
                try
                {
                    wall.MoveToWorld(new Point3D(point.X + 6, point.Y, point.Z), Map.Felucca);
                    Require(ClassCombat.TeethEndpoint(player, distantAim).X <= wall.X,
                        "Directional projectile passed through a wall");
                    Require(CombatTestActions.Execute(player, CombatAbility.Teeth, null, distantAim, now + 2400)
                        && rayTarget.Hits == 88, "Ground projectile damaged a target behind a wall");
                }
                finally
                {
                    wall.Delete();
                }
            }
            finally
            {
                rayTarget.Delete();
            }
        }
        finally
        {
            player.Delete();
            enemy.Delete();
            far.Delete();
            dagger.Delete();
            ClassCreation.Profiles.PruneDeleted();
        }
    }

}
