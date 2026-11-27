// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal interface IClassCompanion
{
    DiabloClass CombatClass { get; }
    CompanionEquipmentStyle EquipmentStyle { get; }
}

internal static class CompanionBuild
{
    internal static SkillValues Values(CombatAbility ability, int level)
    {
        var skill = SkillMechanics.For(ability);
        int Rank(string key) => key == skill.SourceKey ? Math.Min(20, 1 + (level - 1) / 4) : 0;
        return new(skill, Rank, Rank, level);
    }

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

    internal static bool Worn(Item item) => ClassGear.Converted(item)
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
            + (type == ResistanceType.Physical ? 0 : Bonus(actor, "res-all")), -100, CompanionStatProtocol.ResistanceCap);
    }

    internal static int AttackSpeedBonus(BaseCreature actor) =>
        Math.Clamp(Bonus(actor, "swing1") + Bonus(actor, "swing2") + Bonus(actor, "swing3"), 0, 10000);

    internal static ActionTiming Timing(BaseCreature actor, DiabloClass characterClass, CombatAbility ability, BaseWeapon weapon) =>
        CombatTiming.Calculate(characterClass, ability, CombatTiming.Weapon(weapon), AttackSpeedBonus(actor),
            chill: GearEffects.AttackSlowTotal(actor), skillAttackRate: CombatTiming.SkillAttackRate(actor));

    internal static bool AllowedArcherGear(Item item, AttackFamily family) => item switch
    {
        BeltLantern or BaseQuiver => true,
        BaseWeapon weapon => CombatTiming.Weapon(weapon).Family == family,
        _ => ClassGear.Definition(item) is { Slot: GearSlot.Cosmetic or GearSlot.Belt or GearSlot.Ring or GearSlot.Amulet }
            || ClassGear.Definition(item) is { Weight: GearWeight.Light, Slot: GearSlot.Head or GearSlot.Body or GearSlot.Hands or GearSlot.Feet }
    };

    internal static string ArcherGearRefusal(Item item, string weaponType) => item switch
    {
        BaseWeapon => $"I can only use {weaponType}.",
        _ when ClassGear.Definition(item) is { Weight: GearWeight.Medium or GearWeight.Heavy,
            Slot: GearSlot.Head or GearSlot.Body or GearSlot.Hands or GearSlot.Feet } => "I can only wear light armour.",
        _ => "I can't use that."
    };

    internal static (int Min, int Max, int Factor) WeaponDamage(BaseCreature actor, BaseWeapon weapon, int level, int skillPercent = 0)
    {
        var (minimum, maximum) = ClassGear.Damage(weapon, level);
        var basis = ClassGear.Base(weapon);
        int strength = actor.Str + Bonus(actor, "str"), dexterity = actor.Dex + Bonus(actor, "dex");
        int damagePercent = (basis == null ? (weapon is BaseRanged ? 0 : strength)
            : ClassStats.WeaponStatPercent(strength, dexterity, basis.StrengthBonus, basis.DexterityBonus))
            + Bonus(actor, "dmg%", except: weapon) + skillPercent;
        return (minimum, maximum, 100 + Math.Max(-90, damagePercent));
    }

    internal static double BowPhysicalDamage(BaseCreature actor, BaseRanged weapon, int level, int skillPercent = 0)
    {
        var (minimum, maximum, factor) = WeaponDamage(actor, weapon, level, skillPercent);
        return Utility.RandomMinMax(minimum, maximum) * factor / 100.0;
    }

    internal static GearDamage BowHit(BaseCreature actor, Mobile target, GearDamage damage)
    {
        var converted = QuiverEquipment.Convert(damage, QuiverEquipment.Element(actor));
        var parts = DefensiveArmors.Absorb(target, converted with { Poison = converted.Poison + converted.ImpactPoison }, actor);
        parts = parts with
        {
            Physical = GemCombat.Component(target, parts.Physical, target.PhysicalResistance, false),
            Fire = NamedElements.Damage(actor, target, parts.Fire, "fire"),
            Cold = NamedElements.Damage(actor, target, parts.Cold, "cold"),
            Lightning = NamedElements.Damage(actor, target, parts.Lightning, "ltng"),
            Poison = NamedElements.Damage(actor, target, parts.Poison, "pois"),
            Magic = NamedElements.Damage(actor, target, parts.Magic, "mag")
        };
        var settled = DirectDamageParts.From(parts);
        SkillHealth.TypedDamage(actor, target, checked((int)(settled.Total + NamedElements.To256(parts.Poison))), settled);
        return parts;
    }

    internal static int Recovery(BaseCreature actor, DiabloClass characterClass) =>
        HitRecovery.Frames(characterClass, Bonus(actor, "balance1") + Bonus(actor, "balance2") + Bonus(actor, "balance3"),
            actor.Weapon as BaseWeapon);
}
