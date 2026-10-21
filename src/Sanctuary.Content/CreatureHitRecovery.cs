// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class CreatureHitRecovery
{
    private static readonly Dictionary<string, int> _divisors = new(StringComparer.Ordinal);
    private sealed record WeaponHitClass(string Code, string HitClass, int Divisor);
    public static void Configure()
    {
        foreach (var row in BalanceCatalog.Read<WeaponHitClass>("creature-hit-classes"))
        {
            if (row.Divisor is not (8 or 16 or 32 or 64))
            {
                throw new InvalidOperationException("Invalid D2 weapon hit class divisor.");
            }
            _divisors.Add(row.Code, row.Divisor);
        }
        ClassCombatHooks.CreatureRecoveryRemaining = Remaining;
        ClassCombatHooks.CreatureDamaged = Apply;
    }
    public static int Remaining(BaseCreature creature)
    {
        int frozen = NamedControl.FrozenRemaining(creature);
        if (CombatDisplacement.Active(creature)) { return frozen; }
        CreatureProfiles.Initialize(creature);
        return !creature.Deleted && creature.Alive
            ? Math.Max(frozen, (int)Math.Clamp(CreatureProfiles.Session(creature).RecoveryUntil - Core.TickCount, 0, 10000)) : 0;
    }
    public static int Frames(CreatureRecovery row)
    {
        int effective = 120 * row.Fhr / (120 + row.Fhr);
        int advance = Math.Max(1, row.Speed * (50 + effective) / 100);
        return Math.Max(1, (row.Frames * 256 + advance - 1) / advance);
    }
    public static int Chance(int damage, int maximumLife, int divisor = 16)
    {
        if (damage < 1 || maximumLife < 1 || (long)damage * divisor < maximumLife)
        {
            return 0;
        }
        return (long)damage * (divisor / 2) < maximumLife ? 3
            : (long)damage * (divisor / 4) < maximumLife ? 6 : 8;
    }
    public static int Divisor(Mobile attacker) => attacker?.Weapon is BaseWeapon weapon
        && ClassGear.Base(weapon) is { } basis && _divisors.TryGetValue(basis.Code, out int divisor) ? divisor : 16;
    // Called after final damage is applied; pure poison and periodic damage are excluded by callers.
    public static void Apply(BaseCreature creature, Mobile attacker, int damage, bool weaponDamage = false)
    {
        EliteCombat.OnStruck(creature, attacker, damage);
        if (creature.Deleted || !creature.Alive || creature.Hits <= 0 || creature.Blessed || creature.IsDeadBondedPet
            || Remaining(creature) > 0)
        {
            return;
        }
        var row = CreatureProfiles.For(creature).HitRecovery;
        if (!row.Enabled)
        {
            return;
        }
        int chance = Chance(damage, creature.HitsMax, weaponDamage ? Divisor(attacker) : 16);
        if (chance == 0 || chance < 8 && Utility.Random(8) >= chance)
        {
            return;
        }
        int duration = Frames(row) * ActionTiming.FrameMs;
        CreatureProfiles.Session(creature).RecoveryUntil = Core.TickCount + duration;
        creature.AIObject?.ResetApproachState();
        if (creature is GraveyardUndead undead)
        {
            undead.InterruptForRecovery();
        }
        else if (creature is SkeletonGuardian pet)
        {
            pet.InterruptForRecovery();
        }
        if (creature.Spell is Spell spell)
        {
            spell.Disturb(DisturbType.Hurt);
        }
        byte animation = (byte)(creature.Body.IsHuman ? 20 : creature.Body.IsAnimal ? 7 : 10);
        if (creature.Map != null && creature.Map != Map.Internal)
        {
            ClassCombat.SendAnimation(creature, animation, duration);
            creature.Animate(animation, 5, 1, true, false, 0);
        }
        creature.AIObject?.AITimer.Prod();
    }
}
