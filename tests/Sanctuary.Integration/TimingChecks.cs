using System;
using System.Text;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class TimingChecks
{
    private static void Require(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
    private static bool HasProperty(BaseWeapon weapon, string value)
    {
        using var properties = new ObjectPropertyList(weapon);
        weapon.GetProperties(properties);
        properties.Terminate();
        return properties.Buffer.AsSpan().IndexOf(Encoding.Unicode.GetBytes(value)) >= 0;
    }
    public static void Check()
    {
        foreach (DiabloClass characterClass in Enum.GetValues<DiabloClass>())
        {
            foreach (AttackFamily family in Enum.GetValues<AttackFamily>())
            {
                var previous = int.MaxValue;
                for (int ias = 0; ias <= 250; ias++)
                {
                    var speed = CombatTiming.Calculate(characterClass, CombatAbility.BasicAttack, new WeaponTiming(family, 0), ias);
                    Require(speed.Frames <= previous && speed.EffectFrame > 0 && speed.EffectFrame < speed.Frames,
                        "Attack breakpoints regressed or effect escaped its cycle");
                    previous = speed.Frames;
                }
            }
        }
        var daggerProfile = new WeaponTiming(AttackFamily.Thrust, -20);
        Require(CombatTiming.Calculate(DiabloClass.Necromancer, CombatAbility.Teeth, daggerProfile).Frames == 15,
            "Necromancer base casting profile changed");
        Require(CombatTiming.Calculate(DiabloClass.Necromancer, CombatAbility.Teeth, daggerProfile, fcr: 75).Frames == 10
            && CombatTiming.Calculate(DiabloClass.Necromancer, CombatAbility.Teeth, daggerProfile, fcr: 125).Frames == 9,
            "Necromancer FCR breakpoints changed");
        Require(CombatTiming.Calculate(DiabloClass.Necromancer, CombatAbility.Teeth, daggerProfile, ias: 100).Frames == 15,
            "IAS sped up a cast");
        Require(CombatTiming.Calculate(DiabloClass.Necromancer, CombatAbility.BasicAttack, daggerProfile, fcr: 100)
            == CombatTiming.Calculate(DiabloClass.Necromancer, CombatAbility.BasicAttack, daggerProfile), "FCR sped up an attack");
        Require(CombatTiming.Calculate(DiabloClass.Druid, CombatAbility.BasicAttack, new WeaponTiming(AttackFamily.TwoHandedSwing, 0)).Frames
            < CombatTiming.Calculate(DiabloClass.Necromancer, CombatAbility.BasicAttack, new WeaponTiming(AttackFamily.TwoHandedSwing, 0)).Frames,
            "Class/family profiles were ignored");
        var player = new PlayerMobile { Body = 0x190, Name = "TimingCheck", Player = true, Str = 100, Int = 100 };
        var enemy = new Skeleton { Frozen = true };
        var other = new Skeleton { Frozen = true };
        var dagger = new Dagger();
        ClassGear.Profiles.MarkStarter(dagger);
        var halberd = new Halberd();
        var sword = new Broadsword();
        var bow = new Bow();
        try
        {
            ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
            player.Str = player.Dex = player.Int = 100;
            player.Mana = 100;
            var point = CombatPractice.FindFloor(Map.Felucca, 1382, 1495);
            player.MoveToWorld(point, Map.Felucca);
            enemy.MoveToWorld(point, Map.Felucca);
            other.MoveToWorld(point, Map.Felucca);
            enemy.SetHits(1000);
            other.SetHits(1000);
            enemy.Hits = other.Hits = 1000;
            Require(player.EquipItem(dagger), "Could not equip timing dagger");
            Require(CombatTiming.For(player, CombatAbility.BasicAttack).DurationMs == 480, "Dagger base timing changed");
            Require(HasProperty(dagger, "Very Fast (Necromancer, 0.48s)") && !HasProperty(dagger, "2s"),
                "Equipped speed tooltip retained UO seconds or lost class timing");
            player.RemoveItem(dagger);
            Require(HasProperty(dagger, "Very Fast (base)"), "Unequipped tooltip retained wielder timing");
            player.EquipItem(halberd);
            Require(CombatTiming.For(player, CombatAbility.BasicAttack).DurationMs == 800 && HasProperty(halberd, "Slow (Necromancer, 0.80s)"),
                "Halberd timing/tooltip does not differ from dagger");
            player.RemoveItem(halberd);
            player.EquipItem(sword);
            Require(CombatTiming.For(player, CombatAbility.BasicAttack).DurationMs == 680, "Ordinary sword profile changed");
            player.RemoveItem(sword);
            player.EquipItem(bow);
            Require(CombatTiming.Range(player, CombatAbility.BasicAttack) == bow.MaxRange && CombatTiming.Weapon(bow).Family == AttackFamily.Bow,
                "Bow timing family/range is not usable");
            player.RemoveItem(bow);
            player.EquipItem(dagger);
            var hitHp = 1000 - CombatTestActions.HitDamage(player, enemy);
            var session = ClassCombat.For(player);
            long now = Core.TickCount;
            Require(ClassCombat.Perform(player, CombatAbility.BasicAttack, enemy, point, now) && enemy.Hits == 1000, "Attack hit at input time");
            Require(!ClassCombat.ResolveAction(player, session.EffectAt - 1) && enemy.Hits == 1000, "Attack hit before its effect frame");
            Require(ClassCombat.ResolveAction(player, session.EffectAt) && enemy.Hits == hitHp, "Attack did not hit at its effect frame");
            ClassCombat.ResolveAction(player, session.EffectAt);
            Require(enemy.Hits == hitHp && other.Hits == 1000, "Effect resolved twice or acquired another creature");
            Require(!ClassCombat.Perform(player, CombatAbility.Teeth, enemy, point, now + 240), "Switching buttons bypassed remaining action cycle");
            now = session.NextAction;
            player.RemoveItem(dagger);
            player.EquipItem(halberd);
            Require(ClassCombat.Perform(player, CombatAbility.BasicAttack, enemy, point, now), "Halberd could not start after prior cycle");
            var slowEnd = session.NextAction;
            var slowEffect = session.EffectAt;
            player.RemoveItem(halberd);
            player.EquipItem(dagger);
            Require(!ClassCombat.ResolveAction(player, slowEffect) && enemy.Hits == hitHp && session.NextAction == slowEnd,
                "Equipment swap borrowed a slow hit or refunded its cycle");
            Require(!ClassCombat.Perform(player, CombatAbility.BasicAttack, enemy, point, now + 480), "Fast weapon swap shortened slow cycle");
            now = slowEnd;
            Require(ClassCombat.Perform(player, CombatAbility.BasicAttack, enemy, point, now), "Could not start next dagger attack");
            enemy.MoveToWorld(new Point3D(point.X + 5, point.Y, point.Z), Map.Felucca);
            ClassCombat.ResolveAction(player, session.EffectAt);
            Require(enemy.Hits == hitHp, "Target leaving reach was hit at impact");
            now = session.NextAction;
            enemy.MoveToWorld(point, Map.Felucca);
            var mana = player.Mana;
            Require(ClassCombat.Perform(player, CombatAbility.Teeth, enemy, point, now) && player.Mana == mana && enemy.Hits == hitHp,
                "Cast effect/mana happened before release");
            ClassCombat.ReleaseCast(player);
            ClassCombat.ResolveAction(player, session.EffectAt);
            Require(player.Mana == mana - 1 && enemy.Hits == hitHp - 6, "Mouse release cancelled a committed single cast");
            now = session.NextAction;
            ClassCombat.Perform(player, CombatAbility.Teeth, enemy, point, now);
            ClassCombat.Stop(player);
            ClassCombat.ResolveAction(player, session.EffectAt);
            Require(player.Mana == mana - 1 && enemy.Hits == hitHp - 6 && session.NextAction == now + 600,
                "Stop did not cancel windup or reset its deadline");
            now = session.NextAction;
            ClassCombat.Perform(player, CombatAbility.Teeth, enemy, point, now);
            player.MoveToWorld(new Point3D(point.X + 1, point.Y, point.Z), Map.Felucca);
            ClassCombat.ResolveAction(player, session.EffectAt);
            Require(player.Mana == mana - 1 && enemy.Hits == hitHp - 6, "Moving during windup did not interrupt the cast");
            player.MoveToWorld(point, Map.Felucca);
            now = session.NextAction;
            ClassCombat.Perform(player, CombatAbility.Teeth, enemy, point, now);
            player.RemoveItem(dagger);
            ClassCombat.ResolveAction(player, session.EffectAt);
            Require(player.Mana == mana - 1 && enemy.Hits == hitHp - 6, "Unequip retained an unearned pending cast");
            player.EquipItem(dagger);
            dagger.Attributes.WeaponSpeed = 50;
            dagger.Attributes.CastSpeed = 75;
            Require(CombatTiming.For(player, CombatAbility.BasicAttack).DurationMs == 480
                && CombatTiming.For(player, CombatAbility.Teeth).DurationMs == 600,
                "Native UO IAS/FCR leaked into a converted white item");
        }
        finally
        {
            player.Delete();
            enemy.Delete();
            other.Delete();
            dagger.Delete();
            halberd.Delete();
            sword.Delete();
            bow.Delete();
            ClassCreation.Profiles.PruneDeleted();
        }
    }
}
