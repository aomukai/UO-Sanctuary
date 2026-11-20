// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal interface IClassCompanion
{
    DiabloClass CombatClass { get; }
}

internal static class CompanionBuild
{
    internal static void Apply(BaseCreature actor, DiabloClass characterClass, int level, bool joining)
    {
        var basis = ClassStats.For(characterClass);
        int gained = level - 1;
        int strength = basis.Strength, dexterity = basis.Dexterity, energy = basis.Energy;
        switch (characterClass)
        {
            case DiabloClass.Amazon or DiabloClass.Assassin: dexterity += 3 * gained; break;
            case DiabloClass.Sorceress or DiabloClass.Necromancer or DiabloClass.Warlock: energy += 3 * gained; break;
            default: strength += 3 * gained; break;
        }
        int life = basis.Life + gained * (basis.LifeLevel4 + 2 * basis.LifeVitality4) / 4;
        if (!joining && actor.RawStr == strength && actor.RawDex == dexterity && actor.RawInt == energy && actor.HitsMaxSeed == life)
            return;

        int hits = actor.Hits, maxHits = actor.HitsMax;
        actor.RawStr = strength;
        actor.RawDex = dexterity;
        actor.RawInt = energy;
        actor.HitsMaxSeed = life;
        if (actor.Alive && !actor.IsDeadBondedPet)
            actor.Hits = joining ? actor.HitsMax : Math.Max(1, CharacterProfiles.CarryPool(hits, maxHits, actor.HitsMax));
        ClassStats.Refresh(actor);
    }

    private static bool Worn(Item item) => ClassGear.Converted(item)
        && ClassGear.State(item) is not { Identified: false } && !ClassGear.Broken(item);

    internal static int Bonus(BaseCreature actor, string code, Item except = null)
    {
        int total = 0, level = MercenaryCare.CompanionLevel(actor) ?? 1;
        foreach (var item in actor.Items)
            if (item != except && Worn(item))
                total += ClassGear.Bonus(item, code, level);
        return total;
    }

    internal static int AttackRating(BaseCreature actor, DiabloClass characterClass) =>
        Math.Max(1, 5 * (actor.Dex + Bonus(actor, "dex")) - 35 + ClassStats.For(characterClass).AttackBonus + Bonus(actor, "att"));

    internal static int Defense(BaseCreature actor)
    {
        int level = MercenaryCare.CompanionLevel(actor) ?? 1;
        int defense = (actor.Dex + Bonus(actor, "dex")) / 4;
        foreach (var item in actor.Items)
            if (Worn(item))
                defense += ClassGear.Defense(item, level);
        return defense;
    }

    internal static int Resistance(BaseCreature actor, ResistanceType type, int native)
    {
        if (!MercenaryCare.CompanionLevel(actor).HasValue)
            return native;
        string code = type switch
        {
            ResistanceType.Fire => "res-fire", ResistanceType.Cold => "res-cold", ResistanceType.Energy => "res-ltng",
            ResistanceType.Poison => "res-pois", _ => "res-physical"
        };
        return Math.Clamp(native + Bonus(actor, code)
            + (type == ResistanceType.Physical ? 0 : Bonus(actor, "res-all")), -100, 75);
    }

    internal static ActionTiming Timing(BaseCreature actor, DiabloClass characterClass, CombatAbility ability, BaseWeapon weapon) =>
        CombatTiming.Calculate(characterClass, ability, CombatTiming.Weapon(weapon),
            Math.Clamp(Bonus(actor, "swing1") + Bonus(actor, "swing2") + Bonus(actor, "swing3"), 0, 10000),
            chill: GearEffects.AttackSlowTotal(actor), skillAttackRate: CombatTiming.SkillAttackRate(actor));

    internal static int Recovery(BaseCreature actor, DiabloClass characterClass) =>
        HitRecovery.Frames(characterClass, Bonus(actor, "balance1") + Bonus(actor, "balance2") + Bonus(actor, "balance3"),
            actor.Weapon as BaseWeapon);
}
