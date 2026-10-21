// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public readonly record struct GearDamage(double Physical, double Fire, double Cold, double Lightning, double Poison, int PoisonMs = 3000, int ColdMs = 2000, double Magic = 0)
{
    public double Total => Physical + Fire + Cold + Lightning + Poison + Magic;
}

public static class GearEffects
{
    public static void Configure()
    {
        ClassGearHooks.MovementDelay = (mobile, seconds) => seconds * 100 / (100 - MovementSlow(mobile));
        ClassGearHooks.AttackDelay = (mobile, delay) => TimeSpan.FromTicks(delay.Ticks * 100 / (100 - AttackSlow(mobile)));
    }
    [CallPriority(100)]
    public static void Initialize()
    {
        var hits = Mobile.HitsRegenRateHandler;
        Mobile.HitsRegenRateHandler = mobile => RegenerationRate(mobile, hits?.Invoke(mobile) ?? Mobile.DefaultHitsRate, "life-per-five");
        var mana = Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler = mobile => RegenerationRate(mobile, mana?.Invoke(mobile) ?? Mobile.DefaultManaRate, "mana-per-five");
        var stamina = Mobile.StamRegenRateHandler;
        Mobile.StamRegenRateHandler = mobile =>
        {
            var baseline = stamina?.Invoke(mobile) ?? Mobile.DefaultStamRate;
            return mobile is PlayerMobile player && ClassCombat.Enabled(player) && player.Alive
                ? TimeSpan.FromTicks(Math.Max(1, baseline.Ticks * 100 / Math.Max(1, 100 + ClassGear.Bonus(player, "regen-stam")))) : baseline;
        };
    }
    public static TimeSpan RegenerationRate(Mobile mobile, TimeSpan baseline, string code)
    {
        if (mobile is not PlayerMobile player || !ClassCombat.Enabled(player) || !player.Alive
            || !(code == "life-per-five" ? player.CanRegenHits : player.CanRegenMana) || baseline.Ticks <= 0) { return baseline; }
        double perSecond = 1 / baseline.TotalSeconds;
        if (code == "life-per-five") { perSecond += ClassGear.Bonus(player, "replenish-life") * 25.0 / 256; }
        else { perSecond *= (100 + Math.Max(0, ClassGear.Bonus(player, "mana-regen%"))) / 100.0; }
        perSecond += ClassGear.Material(player, code) / 5.0;
        return TimeSpan.FromTicks(Math.Max(1, (long)(TimeSpan.TicksPerSecond / perSecond)));
    }
    public static int ChillPercent(Mobile mobile) => ChillPercent(mobile, Core.TickCount);
    public static int ChillPercent(Mobile mobile, long now) => Math.Max(mobile is PlayerMobile player
        && ClassGear.Bonus(player, "nofreeze") > 0 ? 0 : TimedEffects.Chill(mobile, TimedEffects.TimeAt(now)), EliteAuras.Slow(mobile));
    public static int MovementSlow(Mobile mobile) => Math.Clamp(ChillPercent(mobile)
        + TimedEffects.Slow(mobile, TimedEffects.Now), 0, 75);
    public static int AttackSlow(Mobile mobile) => Math.Clamp(ChillPercent(mobile)
        + TimedEffects.Slow(mobile, TimedEffects.Now), 0, 85);
    public static bool ApplyChill(Mobile target, long now, int durationMs = 2000, Mobile attacker = null)
    {
        if (target.Deleted || !target.Alive || target.ColdResistance >= 100) { return false; }
        if (target is PlayerMobile player)
        {
            if (ClassGear.Bonus(player, "nofreeze") > 0) { return false; }
            if (ClassGear.Bonus(player, "half-freeze") > 0) { durationMs /= 2; }
        }
        int effect = 50;
        if (target is BaseCreature creature)
        {
            var row = CreatureProfiles.For(creature);
            if (row.Traits.ChillEffectiveness is { } percent) { effect = percent; }
            else if (row.SourceStats.TryGetValue("coldeffect", out var value) && int.TryParse(value, out int source))
            { effect = Math.Abs(source); }
        }
        effect = Math.Clamp(effect, 0, 50);
        int resistance = NamedElements.Resistance(attacker, target, "cold", target.ColdResistance);
        int duration = durationMs * Math.Clamp(100 - resistance, 0, 200) / 100;
        if (effect == 0 || duration == 0) { return false; }
        TimedEffects.Chill(target, effect, duration, TimedEffects.TimeAt(now));
        target.FixedEffect(0x374A, 10, 15, 0x480, 0);
        return true;
    }
    public static bool ApplyPoison(Mobile source, Mobile target, double total, long now, int durationMs = 3000)
    {
        total = NamedElements.Damage(source, target, total, "pois");
        if (target is PlayerMobile player)
        {
            int reduction = Math.Clamp(ClassGear.Bonus(player, "res-pois-len"), 0, 75);
            durationMs = durationMs * (100 - reduction) / 100;
            total *= (100 - reduction) / 100.0;
        }
        if (total <= 0 || durationMs <= 0 || target.Deleted || !target.Alive || target.Blessed) { return false; }
        if (!TimedEffects.Poison(source, target, total, durationMs, TimedEffects.TimeAt(now))) { return false; }
        target.FixedEffect(0x374A, 10, 15, 0x3F, 0);
        return true;
    }
    public static void Tick(long now) => TimedEffects.Tick(TimedEffects.TimeAt(now));
    public static void Clear(Mobile mobile) => TimedEffects.ClearGear(mobile);
    public static GearDamage Damage(PlayerMobile player, double roll, Mobile target = null)
    {
        roll = Math.Clamp(roll, 0, 1);
        var weapon = CombatTiming.Equipped(player);
        var physical = ClassStats.PhysicalDamageRange(player, GemCombat.Undead(target) ? ClassGear.Bonus(player, "dmg-undead%")
            : NamedCombat.Demon(target) ? ClassGear.Bonus(player, "dmg-demon%") : 0);
        double value = physical.Min + (physical.Max - physical.Min) * roll;
        if (weapon == null || ClassGear.Base(weapon) is not { } basis)
        {
            var unarmed = NamedGear.Poison(player, roll);
            int duration = unarmed.Count == 0 ? 3000 : unarmed.Length / unarmed.Count;
            return new(value, NamedGear.Elemental(player, "fire", roll), NamedGear.Elemental(player, "cold", roll),
                NamedGear.Elemental(player, "lightning", roll), unarmed.Rate * duration, duration, NamedGear.ColdDuration(player), NamedGear.Elemental(player, "magic", roll));
        }
        bool two = weapon.Layer == Layer.TwoHanded || basis.TwoHanded && !basis.OneOrTwoHanded;
        int min = two && basis.TwoHandMax > 0 ? basis.TwoHandMin : basis.MinDamage;
        int max = two && basis.TwoHandMax > 0 ? basis.TwoHandMax : basis.MaxDamage;
        if (ClassGear.Definition(weapon).Category == "Throwing" && basis.MissileMax > 0)
        { min = basis.MissileMin; max = basis.MissileMax; }
        double reference = (min + (max - min) * roll) * Math.Max(0, 100 + ClassStats.DamageBonus(player, weapon)) / 10000.0;
        var poison = GemCombat.PoisonCombined(weapon, reference * ClassGear.Material(weapon, "damage-poison"), NamedGear.Poison(player, roll));
        double cold = reference * ClassGear.Material(weapon, "damage-cold");
        int coldMs = (cold > 0 ? 2000 : 0) + GemCombat.Duration(weapon, "cold-damage") + NamedGear.ColdDuration(player);
        return new(value + reference * ClassGear.Material(weapon, "damage-physical"),
            reference * ClassGear.Material(weapon, "damage-fire") + GemCombat.Value(weapon, "fire-damage", roll) + NamedGear.Elemental(player, "fire", roll),
            cold + GemCombat.Value(weapon, "cold-damage", roll) + NamedGear.Elemental(player, "cold", roll),
            reference * ClassGear.Material(weapon, "damage-lightning") + GemCombat.Value(weapon, "lightning-damage", roll) + NamedGear.Elemental(player, "lightning", roll),
            poison.Total, poison.Duration, coldMs, NamedGear.Elemental(player, "magic", roll));
    }
    public static int Strike(PlayerMobile player, Mobile target, double roll, Random effects = null, bool wearWeapon = true)
    {
        effects ??= Random.Shared;
        var weapon = CombatTiming.Equipped(player);
        bool ranged = CombatTiming.Weapon(weapon).Family is AttackFamily.Bow or AttackFamily.Crossbow or AttackFamily.Throw;
        var damage = Damage(player, roll, target);
        double physical = GemCombat.Component(target, damage.Physical * NamedCombat.Deadly(player, effects), target.PhysicalResistance, false);
        double total = physical + NamedElements.Damage(player, target, damage.Fire, "fire")
            + NamedElements.Damage(player, target, damage.Cold, "cold")
            + NamedElements.Damage(player, target, damage.Lightning, "ltng")
            + NamedElements.Damage(player, target, damage.Magic, "mag");
        int final = total > 0 ? Math.Max(1, (int)Math.Floor(total)) : 0;
        int before = target.Hits; var impact = target.Location; var impactMap = target.Map;
        NamedCombat.Crushing(player, target, ranged, effects);
        int normalBefore = target.Hits;
        if (final > 0 && target.Alive && !target.Deleted)
        {
            target.Damage(final, player);
            if (target is BaseCreature creature)
            { CreatureHitRecovery.Apply(creature, player, Math.Max(0, before - target.Hits), true); }
            CombatDamage.Finish(target, player);
        }
        if (target.Alive && !target.Deleted)
        {
            if (damage.Cold > 0) { ApplyChill(target, Core.TickCount, damage.ColdMs, player); }
            if (damage.Poison > 0) { ApplyPoison(player, target, damage.Poison, Core.TickCount, damage.PoisonMs); }
        }
        int loss = Math.Max(0, before - target.Hits);
        if (loss > 0 && impactMap != null && impactMap != Map.Internal && ClassGear.Bonus(player, "bloody") > 0)
        { new Blood().MoveToWorld(impact, impactMap); }
        GemCombat.Leech(player, target, Math.Min(Math.Max(0, normalBefore - target.Hits), (int)Math.Floor(physical)));
        NamedCombat.AfterStrike(player, target, ranged, effects);
        GemCombat.WeaponHit(player, target, loss, ranged);
        if (wearWeapon && weapon != null && NamedAmmunition.Maximum(weapon) == 0) { weapon.WearOnHit(player, target); }
        return Math.Max(0, before - target.Hits);
    }
    public static void WearArmor(PlayerMobile player, BaseWeapon weapon, int damage)
    {
        double chance = Utility.RandomDouble();
        var item = chance switch
        {
            < .07 => player.NeckArmor, < .14 => player.HandArmor, < .28 => player.ArmsArmor,
            < .43 => player.HeadArmor, < .65 => player.LegsArmor, _ => player.ChestArmor
        };
        switch (item)
        {
            case BaseArmor armor: armor.OnHit(weapon, damage); break;
            case BaseClothing clothing: clothing.OnHit(weapon, damage); break;
        }
    }
}
