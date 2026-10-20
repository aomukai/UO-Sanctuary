// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Commands;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public readonly record struct StatAllocation(int Strength, int Dexterity, int Energy, int Vitality)
{
    public bool Valid => Strength is >= 0 and <= 490 && Dexterity is >= 0 and <= 490
        && Energy is >= 0 and <= 490 && Vitality is >= 0 and <= 490;
    public int Total => Strength + Dexterity + Energy + Vitality;
    public StatAllocation Add(CharacterStat stat) => stat switch
    {
        CharacterStat.Strength => this with { Strength = Strength + 1 },
        CharacterStat.Dexterity => this with { Dexterity = Dexterity + 1 },
        CharacterStat.Energy => this with { Energy = Energy + 1 },
        CharacterStat.Vitality => this with { Vitality = Vitality + 1 },
        _ => this
    };
}

// Quarter points preserve Assassin stamina/mana and the half-point level gains.
public sealed record ClassStatProfile(int Strength, int Dexterity, int Vitality, int Energy,
    int Life, int Stamina, int Mana, int LifeLevel4, int StaminaLevel4, int ManaLevel4,
    int LifeVitality4, int StaminaVitality4, int ManaEnergy4, int AttackBonus);

public static class ClassStats
{
    private static readonly ClassStatProfile[] _classes =
    [
        new(20, 25, 20, 15, 50, 84, 15, 8, 4, 6, 12, 4, 6, 5),
        new(10, 25, 10, 35, 40, 74, 35, 4, 4, 8, 8, 4, 8, -15),
        new(15, 25, 15, 25, 45, 79, 25, 6, 4, 8, 8, 4, 8, -10),
        new(25, 20, 25, 15, 55, 89, 15, 8, 4, 6, 12, 4, 6, 20),
        new(30, 20, 25, 10, 55, 92, 10, 8, 4, 4, 16, 4, 4, 20),
        new(15, 20, 25, 20, 55, 84, 20, 6, 4, 8, 8, 4, 8, 5),
        new(20, 20, 20, 25, 50, 95, 25, 8, 5, 6, 12, 5, 7, 15)
    ];
    public static ClassStatProfile For(DiabloClass characterClass) => _classes[(int)characterClass - 101];
    public static void Configure()
    {
        ClassCombatHooks.ResourceMaximum = Maximum;
        ClassCombatHooks.ClassHitCheck = CheckHit;
        CommandSystem.Register("ClassLevel", AccessLevel.GameMaster, args =>
        {
            if (args.Length == 1 && int.TryParse(args.GetString(0), out var level)
                && ClassCreation.Profiles.AdvanceLevel(args.Mobile, level))
            {
                args.Mobile.SendMessage($"Level {level}. Unspent attribute points: {ClassCreation.Profiles.AvailableStatPoints(args.Mobile.Serial)}.");
            }
            else
            {
                args.Mobile.SendMessage("Usage: [ClassLevel <higher level, up to 99>");
            }
        });
    }
    public static void ApplyBase(Mobile mobile, CharacterProfile profile, StatAllocation allocation)
    {
        var basis = For(profile.Class);
        mobile.RawStr = basis.Strength + allocation.Strength;
        mobile.RawDex = basis.Dexterity + allocation.Dexterity;
        mobile.RawInt = basis.Energy + allocation.Energy;
        mobile.StrLock = mobile.DexLock = mobile.IntLock = StatLockType.Locked;
        Refresh(mobile);
    }
    public static void Refresh(Mobile mobile) => mobile.Delta(MobileDelta.Stat | MobileDelta.Hits | MobileDelta.Mana | MobileDelta.Stam);
    public static int Vitality(PlayerMobile player) => ClassCreation.Profiles.TryGet(player.Serial, out var profile)
        ? For(profile.Class).Vitality + ClassCreation.Profiles.Stats(player.Serial).Vitality + ClassGear.Bonus(player, "vit") : 0;
    // Intelligence represents Energy; equipment bonuses come from validated affix rolls.
    public static int? Maximum(PlayerMobile player, int resource)
    {
        if (ClassCreation.Profiles?.TryGet(player.Serial, out var profile) != true)
        {
            return null;
        }
        var basis = For(profile.Class);
        var vit = Vitality(player) - basis.Vitality;
        var energy = player.Int - basis.Energy;
        var levels = profile.Level - 1;
        return Math.Clamp(resource switch
        {
            0 => basis.Life + (levels * basis.LifeLevel4 + vit * basis.LifeVitality4) / 4
                + AosAttributes.GetValue(player, AosAttribute.BonusHits),
            1 => basis.Stamina + (levels * basis.StaminaLevel4 + vit * basis.StaminaVitality4) / 4
                + AosAttributes.GetValue(player, AosAttribute.BonusStam),
            _ => basis.Mana + (levels * basis.ManaLevel4 + energy * basis.ManaEnergy4) / 4
                + AosAttributes.GetValue(player, AosAttribute.BonusMana)
        }, 1, 65000);
    }
    public static int Level(Mobile mobile) => mobile is BaseCreature creature ? CreatureProfiles.Level(creature) : mobile is PlayerMobile && ClassCreation.Profiles.TryGet(mobile.Serial, out var profile)
        ? profile.Level : 1;
    public static int AttackRating(Mobile mobile) => EliteAuras.AttackRating(mobile, BaseAttackRating(mobile));
    private static int BaseAttackRating(Mobile mobile)
    {
        if (mobile is BaseCreature creature)
        {
            return CreatureProfiles.AttackRating(creature);
        }
        if (mobile is not PlayerMobile player || !ClassCreation.Profiles.TryGet(mobile.Serial, out var profile))
        {
            return Math.Max(5, mobile.Dex * 2);
        }
        int baseRating = Math.Max(1, 5 * player.Dex - 35 + For(profile.Class).AttackBonus + ClassGear.Bonus(player, "att"));
        return Math.Max(1, baseRating * (100 + AosAttributes.GetValue(player, AosAttribute.AttackChance)) / 100);
    }
    public static int Defense(Mobile mobile) => EliteAuras.Defense(mobile, BaseDefense(mobile));
    private static int BaseDefense(Mobile mobile)
    {
        if (mobile is BaseCreature creature)
        {
            return CreatureProfiles.Defense(creature);
        }
        if (mobile is PlayerMobile player && ClassCombat.Enabled(player))
        {
            if (ClassMovement.IsRunning(player, Core.TickCount))
            {
                return 0;
            }
            int armor = 0;
            foreach (var item in player.Items)
            {
                if (ClassGear.Active(player, item))
                {
                    armor += ClassGear.Defense(item);
                }
            }
            return Math.Max(0, player.Dex / 4 + armor);
        }
        return Math.Max(0, mobile.Dex / 4 + (int)mobile.ArmorRating);
    }
    public static double HitChance(int attack, int defense, int attackerLevel, int defenderLevel) =>
        Math.Clamp(2.0 * Math.Max(1, attack) / (Math.Max(1, attack) + Math.Max(0, defense))
            * Math.Max(1, attackerLevel) / (Math.Max(1, attackerLevel) + Math.Max(1, defenderLevel)), 0.05, 0.95);
    public static bool? CheckHit(Mobile attacker, Mobile defender)
    {
        bool classAttack = attacker is PlayerMobile a && ClassCombat.Enabled(a);
        bool classDefense = defender is PlayerMobile d && ClassCombat.Enabled(d);
        if (!classAttack && !classDefense && attacker is not (GraveyardUndead or SkeletonGuardian) && defender is not (GraveyardUndead or SkeletonGuardian))
        {
            return null;
        }
        // Project rule: an accepted running step disables avoidance entirely.
        if (classDefense && ClassMovement.IsRunning((PlayerMobile)defender, Core.TickCount))
        {
            return true;
        }
        bool hit = Utility.RandomDouble() < HitChance(AttackRating(attacker), Defense(defender), Level(attacker), Level(defender));
        if (hit && classDefense && ClassGear.BlockChance((PlayerMobile)defender) is > 0 and var chance)
        {
            hit = Utility.RandomDouble() * 100 >= chance;
        }
        return hit;
    }
    public static int DamageBonus(PlayerMobile player, BaseWeapon weapon)
    {
        int strength = player.Str, dexterity = player.Dex;
        var basis = weapon == null ? null : ClassGear.Base(weapon);
        int statBonus = basis == null ? strength : (basis.StrengthBonus * strength + basis.DexterityBonus * dexterity) / 100;
        foreach (var item in player.Items)
        {
            if (item != weapon && ClassGear.Active(player, item))
            {
                statBonus += ClassGear.Bonus(item, "dmg%");
            }
        }
        return statBonus;
    }
    public static (int Min, int Max) DamageRange(PlayerMobile player)
    {
        int min = (int)Math.Floor(GearEffects.Damage(player, 0).Total);
        return (min, Math.Max(min, (int)Math.Floor(GearEffects.Damage(player, 1).Total)));
    }
    public static (int Min, int Max) PhysicalDamageRange(PlayerMobile player, int targetBonus = 0)
    {
        var weapon = CombatTiming.Equipped(player);
        int bonus = Math.Max(0, 100 + DamageBonus(player, weapon) + targetBonus);
        int flatMin = 0, flatMax = 0;
        foreach (var item in player.Items)
        {
            if (item != weapon && ClassGear.Active(player, item))
            {
                flatMin += ClassGear.Bonus(item, "dmg-min");
                flatMax += ClassGear.Bonus(item, "dmg-max");
            }
        }
        int min = Math.Max(1, ((weapon?.MinDamage ?? 1) + flatMin) * bonus / 100);
        int max = Math.Max(min, ((weapon?.MaxDamage ?? 2) + flatMax) * bonus / 100);
        int added = CombatPotions.Bonus(player, "deep-wounds");
        return (min + added, max + added);
    }
    public static int[] Snapshot(PlayerMobile player)
    {
        ClassCreation.Profiles.TryGet(player.Serial, out var profile);
        var basis = For(profile.Class);
        var damage = DamageRange(player);
        return [player.Str, player.Dex, player.Int, Vitality(player), ClassCreation.Profiles.AvailableStatPoints(player.Serial),
            profile.Level, AttackRating(player), Defense(player), damage.Min, damage.Max, CombatTiming.Ias(player), CombatTiming.Fcr(player),
            basis.LifeVitality4, basis.ManaEnergy4, basis.StaminaVitality4, (int)profile.Class,
            basis.LifeLevel4, basis.ManaLevel4, basis.StaminaLevel4, ClassCreation.Profiles.AvailablePoints(player.Serial),
            EquipmentBalance.RunPenalty(player), CombatPotions.RunBonus(player), GearDrops.MagicFind(player), FoodEffects.Bonus(player, "light")];
    }
}
