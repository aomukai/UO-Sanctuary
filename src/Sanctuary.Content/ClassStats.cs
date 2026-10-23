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
        new(20, 20, 20, 25, 50, 95, 25, 8, 5, 6, 12, 5, 7, 15),
        new(15, 20, 25, 20, 55, 86, 20, 8, 4, 6, 12, 4, 8, 5)
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
        ? For(profile.Class).Vitality + ClassCreation.Profiles.Stats(player.Serial).Vitality + ClassResources.VitalityBonus(player) : 0;
    // Intelligence represents Energy; equipment bonuses come from validated affix rolls.
    public static int? Maximum(PlayerMobile player, int resource)
    {
        // Mobile deserialization clamps pools before class/gear ledgers are ready.
        // Keep their existing ceiling until PostDeserialize can enforce the actual maximum.
        if (World.Loading)
        {
            return 65000;
        }
        if (ClassCreation.Profiles?.TryGet(player.Serial, out var profile) != true)
        {
            return null;
        }
        return ClassResources.Parts(player,resource).NativeMaximum;
    }
    public static int Level(Mobile mobile) => mobile is BaseCreature creature ? CreatureProfiles.Level(creature) : mobile is PlayerMobile && ClassCreation.Profiles.TryGet(mobile.Serial, out var profile)
        ? profile.Level : 1;
    public static int AttackRating(Mobile mobile) => AttackRating(mobile,0);
    internal static int AttackRating(Mobile mobile,int skillPercent) => EliteAuras.AttackRating(mobile, BaseAttackRating(mobile,skillPercent));
    private static int BaseAttackRating(Mobile mobile,int skillPercent,int targetFlat=0)
    {
        if (mobile is BaseCreature creature)
        {
            int basis=CreatureProfiles.AttackRating(creature),percent=skillPercent+(SorceressEffects.Enchantment(creature)?.AttackPercent??0)+PaladinAuras.AttackPercent(creature)+NecromancerEffects.AttackStatPercent(creature);
            return basis+(int)((long)basis*percent/100);
        }
        if (mobile is not PlayerMobile player || !ClassCreation.Profiles.TryGet(mobile.Serial, out var profile))
        {
            return Math.Max(5, mobile.Dex * 2);
        }
        int baseRating = Math.Max(1, 5 * player.Dex - 35 + For(profile.Class).AttackBonus + ClassGear.Bonus(player, "att")+targetFlat);
        return PlayerAttackPercent(baseRating, skillPercent + AosAttributes.GetValue(player, AosAttribute.AttackChance)
            + BarbarianMasteries.Bonus(player,CombatTiming.Equipped(player),1)
            + AmazonCombat.AttackPercent(player)
            + (SorceressEffects.Enchantment(player)?.AttackPercent ?? 0) + PaladinAuras.AttackPercent(player));
    }
    internal static int PlayerAttackPercent(int basis,int percent)=>Math.Max(1,basis*(100+percent)/100);
    internal static int AttackRatingAgainst(Mobile attacker,Mobile defender,int skillPercent=0)
    {
        int flat=attacker is PlayerMobile player?GemCombat.Undead(defender)?ClassGear.Bonus(player,"att-undead")+PaladinSanctuary.AttackFlat(player,defender)
            :NamedCombat.Demon(defender)?ClassGear.Bonus(player,"att-demon"):0:0;
        return EliteAuras.AttackRating(attacker,BaseAttackRating(attacker,skillPercent,flat));
    }
    public static int Defense(Mobile mobile)
    {
        int defense=EliteAuras.Defense(mobile,Math.Max(0,BaseDefense(mobile)-TimedEffects.DefenseLoss(mobile)));
        return mobile is PlayerMobile player?BarbarianBerserk.ApplyOverride(defense,BarbarianBerserk.DefenseOverride(player)):defense;
    }
    internal static int ScaleDefense(int basis,int percent)=>
        (int)Math.Clamp((long)basis+(long)basis*percent/100,0,int.MaxValue);
    private static int BaseDefense(Mobile mobile)
    {
        if (mobile is BaseCreature creature)
        {
            return AmazonInnerSight.Defense(creature,CreatureProfiles.Defense(creature),PaladinAuras.DefensePercent(creature)+BarbarianShout.Defense(creature)+NecromancerEffects.DefenseStatPercent(creature));
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
            return AmazonInnerSight.Defense(player,player.Dex / 4 + armor + NamedGear.SetBonus(player,"ac"),SorceressEffects.Defense(player)+PaladinAuras.DefensePercent(player)+HolyShields.DefensePercent(player)+BarbarianCombat.Passive(player,CombatAbility.IronSkin)+BarbarianConcentrate.Defense(player)+BarbarianShout.Defense(player)+NecromancerEffects.DefenseStatPercent(player));
        }
        return Math.Max(0, mobile.Dex / 4 + (int)mobile.ArmorRating);
    }
    public static double HitChance(int attack, int defense, int attackerLevel, int defenderLevel) =>
        Math.Clamp(2.0 * Math.Max(1, attack) / (Math.Max(1, attack) + Math.Max(0, defense))
            * Math.Max(1, attackerLevel) / (Math.Max(1, attackerLevel) + Math.Max(1, defenderLevel)), 0.05, 0.95);
    public static bool? CheckHit(Mobile attacker, Mobile defender)=>CheckHit(attacker,defender,0,projectile:
        attacker.Weapon is BaseRanged || attacker is PlayerMobile player && CombatTiming.Weapon(CombatTiming.Equipped(player)).Family==AttackFamily.Throw);
    internal static bool? CheckHit(Mobile attacker,Mobile defender,int skillPercent,Func<double> roll=null,bool projectile=false)
    {
        if(!projectile){SorceressEffects.MeleeAttempt(attacker, defender);}
        bool classAttack = attacker is PlayerMobile a && ClassCombat.Enabled(a);
        bool classDefense = defender is PlayerMobile d && ClassCombat.Enabled(d);
        if (!classAttack && !classDefense && !PaladinAuras.CombatStats(attacker) && !PaladinAuras.CombatStats(defender) && attacker is not (GraveyardUndead or SkeletonGuardian) && defender is not (GraveyardUndead or SkeletonGuardian or AmazonDecoy)
            && SorceressEffects.Enchantment(attacker) == null && NecromancerEffects.DefenseStatPercent(defender)==0 && NecromancerEffects.AttackStatPercent(attacker)==0 && AmazonInnerSight.State(defender)==null)
        {
            return null;
        }
        // Project rule: an accepted running step disables avoidance entirely.
        if (classDefense && ClassMovement.IsRunning((PlayerMobile)defender, Core.TickCount))
        {
            return true;
        }
        int attack = AttackRatingAgainst(attacker,defender,skillPercent);
        roll ??= Utility.RandomDouble;
        bool hit = roll() < HitChance(attack, NamedCombat.DefenseFor(attacker, defender, Defense(defender),projectile?true:null), Level(attacker), Level(defender));
        if (hit && classDefense && ClassGear.BlockChance((PlayerMobile)defender) is > 0 and var chance)
        {
            hit = roll() * 100 >= chance;
            if (!hit) { HitRecovery.Block((PlayerMobile)defender, Core.TickCount); }
        }
        if(hit && classDefense && AmazonAvoidance.Weapon((PlayerMobile)defender,projectile,Core.TickCount)){hit=false;}
        return hit;
    }
    public static int DamageBonus(PlayerMobile player, BaseWeapon weapon)
    {
        int strength = player.Str, dexterity = player.Dex;
        var basis = weapon == null ? null : ClassGear.Base(weapon);
        int statBonus = basis == null ? strength : WeaponStatPercent(strength,dexterity,basis.StrengthBonus,basis.DexterityBonus);
        foreach (var item in player.Items)
        {
            if (item != weapon && ClassGear.Slot(item)!=GearSlot.Weapon && ClassGear.Active(player, item))
            {
                statBonus += ClassGear.Bonus(item, "dmg%");
            }
        }
        return statBonus + NamedGear.SetBonus(player, "dmg%") + PaladinAuras.DamagePercent(player)+BarbarianMasteries.Bonus(player,weapon,2);
    }
    internal static int WeaponStatPercent(int strength,int dexterity,int strengthBonus,int dexterityBonus)
        =>(int)((long)strengthBonus*strength/100+(long)dexterityBonus*dexterity/100);
    internal static int PhysicalDamageBonus(PlayerMobile player,BaseWeapon weapon)=>DamageBonus(player,weapon)+NecromancerEffects.DamageStatPercent(player);
    public static (int Min, int Max) DamageRange(PlayerMobile player)
    {
        int min = (int)Math.Floor(GearEffects.Damage(player, 0).Total);
        return (min, Math.Max(min, (int)Math.Floor(GearEffects.Damage(player, 1).Total)));
    }
    internal static (int Min,int Max) WeaponBaseDamage(PlayerMobile player)
    {
        var weapon = CombatTiming.Equipped(player);
        int flatMin = NamedGear.SetBonus(player, "dmg-min"), flatMax = NamedGear.SetBonus(player, "dmg-max");
        foreach (var item in player.Items)
        {
            if (item != weapon && ClassGear.Slot(item)!=GearSlot.Weapon && ClassGear.Active(player, item))
            {
                flatMin += ClassGear.Bonus(item, "dmg-min");
                flatMax += ClassGear.Bonus(item, "dmg-max");
            }
        }
        return((weapon?.MinDamage ?? 1)+flatMin,(weapon?.MaxDamage ?? 2)+flatMax);
    }
    public static (int Min, int Max) PhysicalDamageRange(PlayerMobile player, int targetBonus = 0)
    {
        var weapon=CombatTiming.Equipped(player);var raw=WeaponBaseDamage(player);
        int bonus=Math.Max(10,100+PhysicalDamageBonus(player,weapon)+targetBonus);
        int min = Math.Max(1, raw.Min * bonus / 100);
        int max = Math.Max(min, raw.Max * bonus / 100);
        int added = CombatPotions.Bonus(player, "deep-wounds");
        return (min + added, max + added);
    }
    internal static (int Min,int Max) PhysicalEndpoints256(int minimum,int maximum,int bonus)
    {
        long min=Math.Max(1,minimum)*256L,max=Math.Max(2,maximum)*256L;
        if(max<=min){max=min+256;}
        bonus=Math.Max(-90,bonus);
        min+=min*bonus/100;max+=max*bonus/100;
        return((int)Math.Clamp(min,0,int.MaxValue-1),(int)Math.Clamp(max,1,int.MaxValue));
    }
    internal static (int Min,int Max) SkillPhysicalRange256(PlayerMobile player,int skillPercent,int targetBonus)
    {
        var raw=WeaponBaseDamage(player);
        return PhysicalEndpoints256(raw.Min,raw.Max,PhysicalDamageBonus(player,CombatTiming.Equipped(player))+skillPercent+targetBonus);
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
