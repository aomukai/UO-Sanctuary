// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public readonly record struct GearDamage(double Physical, double Fire, double Cold, double Lightning, double Poison, int PoisonMs = 3000, int ColdMs = 2000)
{
    public double Total => Physical + Fire + Cold + Lightning + Poison;
}

public static class GearEffects
{
    public static void Configure()
    {
        ClassGearHooks.MovementDelay = (mobile, seconds) => seconds * 100 / (100 - ChillPercent(mobile));
        ClassGearHooks.AttackDelay = (mobile, delay) => TimeSpan.FromTicks(delay.Ticks * 100 / (100 - ChillPercent(mobile)));
    }
    [CallPriority(100)]
    public static void Initialize()
    {
        var hits = Mobile.HitsRegenRateHandler;
        Mobile.HitsRegenRateHandler = mobile => RegenerationRate(mobile, hits?.Invoke(mobile) ?? Mobile.DefaultHitsRate, "life-per-five");
        var mana = Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler = mobile => RegenerationRate(mobile, mana?.Invoke(mobile) ?? Mobile.DefaultManaRate, "mana-per-five");
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
    public static int ChillPercent(Mobile mobile, long now) => Math.Max(TimedEffects.Chill(mobile, TimedEffects.TimeAt(now)), EliteAuras.Slow(mobile));
    public static bool ApplyChill(Mobile target, long now, int durationMs = 2000)
    {
        if (target.Deleted || !target.Alive || target.ColdResistance >= 100) { return false; }
        int effect = 50;
        if (target is BaseCreature creature)
        {
            var row = CreatureProfiles.For(creature);
            if (row.Traits.ChillEffectiveness is { } percent) { effect = percent; }
            else if (row.SourceStats.TryGetValue("coldeffect", out var value) && int.TryParse(value, out int source))
            { effect = Math.Abs(source); }
        }
        effect = Math.Clamp(effect, 0, 50);
        int duration = durationMs * Math.Clamp(100 - target.ColdResistance, 0, 200) / 100;
        if (effect == 0 || duration == 0) { return false; }
        TimedEffects.Chill(target, effect, duration, TimedEffects.TimeAt(now));
        target.FixedEffect(0x374A, 10, 15, 0x480, 0);
        return true;
    }
    public static bool ApplyPoison(Mobile source, Mobile target, double total, long now, int durationMs = 3000)
    {
        total *= Math.Clamp(100 - target.PoisonResistance, 0, 200) / 100.0;
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
        var physical = ClassStats.PhysicalDamageRange(player, GemCombat.Undead(target) ? ClassGear.Bonus(player, "dmg-undead%") : 0);
        double value = physical.Min + (physical.Max - physical.Min) * roll;
        if (weapon == null || ClassGear.Base(weapon) is not { } basis) { return new(value, 0, 0, 0, 0); }
        bool two = weapon.Layer == Layer.TwoHanded || basis.TwoHanded && !basis.OneOrTwoHanded;
        int min = two && basis.TwoHandMax > 0 ? basis.TwoHandMin : basis.MinDamage;
        int max = two && basis.TwoHandMax > 0 ? basis.TwoHandMax : basis.MaxDamage;
        if (ClassGear.Definition(weapon).Category == "Throwing" && basis.MissileMax > 0)
        { min = basis.MissileMin; max = basis.MissileMax; }
        double reference = (min + (max - min) * roll) * Math.Max(0, 100 + ClassStats.DamageBonus(player, weapon)) / 10000.0;
        var poison = GemCombat.Poison(weapon, reference * ClassGear.Material(weapon, "damage-poison"));
        double cold = reference * ClassGear.Material(weapon, "damage-cold");
        int coldMs = (cold > 0 ? 2000 : 0) + GemCombat.Duration(weapon, "cold-damage");
        return new(value + reference * ClassGear.Material(weapon, "damage-physical"),
            reference * ClassGear.Material(weapon, "damage-fire") + GemCombat.Value(weapon, "fire-damage", roll),
            cold + GemCombat.Value(weapon, "cold-damage", roll),
            reference * ClassGear.Material(weapon, "damage-lightning") + GemCombat.Value(weapon, "lightning-damage", roll),
            poison.Total, poison.Duration, coldMs);
    }
    public static int Strike(PlayerMobile player, Mobile target, double roll)
    {
        var damage = Damage(player, roll, target);
        double physical = GemCombat.Component(target, damage.Physical, target.PhysicalResistance, false);
        double total = physical + GemCombat.Component(target, damage.Fire, target.FireResistance, true)
            + GemCombat.Component(target, damage.Cold, target.ColdResistance, true)
            + GemCombat.Component(target, damage.Lightning, target.EnergyResistance, true);
        int final = total > 0 ? Math.Max(1, (int)Math.Floor(total)) : 0;
        int before = target.Hits;
        if (final > 0)
        {
            target.Damage(final, player);
            if (target is BaseCreature creature)
            { CreatureHitRecovery.Apply(creature, player, Math.Max(0, before - target.Hits), true); }
            CombatDamage.Finish(target, player);
        }
        if (target.Alive && !target.Deleted)
        {
            if (damage.Cold > 0) { ApplyChill(target, Core.TickCount, damage.ColdMs); }
            if (damage.Poison > 0) { ApplyPoison(player, target, damage.Poison, Core.TickCount, damage.PoisonMs); }
        }
        int loss = Math.Max(0, before - target.Hits);
        GemCombat.Leech(player, target, Math.Min(loss, (int)Math.Floor(physical)));
        var weapon = CombatTiming.Equipped(player);
        bool ranged = CombatTiming.Weapon(weapon).Family is AttackFamily.Bow or AttackFamily.Crossbow or AttackFamily.Throw;
        GemCombat.WeaponHit(player, target, loss, ranged);
        weapon?.WearOnHit(player, target);
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
