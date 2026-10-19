// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public readonly record struct GearDamage(double Physical, double Fire, double Cold, double Lightning, double Poison)
{
    public double Total => Physical + Fire + Cold + Lightning + Poison;
}

public static class GearEffects
{
    private sealed class Chill
    {
        internal long Until = Core.TickCount;
        internal int Percent;
    }
    private sealed class Venom
    {
        internal Serial Source;
        internal long Updated, Until;
        internal double PerMillisecond, Fraction;
    }
    private sealed class Remainder { internal double Value; }
    private static readonly ConditionalWeakTable<Mobile, Chill> _chills = new();
    private static readonly ConditionalWeakTable<Mobile, Remainder> _poisonRemainders = new();
    private static readonly Dictionary<Serial, Venom> _poisons = new();
    private static readonly List<Serial> _expired = new();

    public static void Configure()
    {
        ClassGearHooks.MovementDelay = (mobile, seconds) => seconds * 100 / (100 - ChillPercent(mobile));
        ClassGearHooks.AttackDelay = (mobile, delay) => TimeSpan.FromTicks(delay.Ticks * 100 / (100 - ChillPercent(mobile)));
        EventSink.Disconnected += Clear;
        EventSink.Logout += Clear;
    }
    [CallPriority(100)]
    public static void Initialize()
    {
        Timer.StartTimer(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100), () => Tick(Core.TickCount));
        var hits = Mobile.HitsRegenRateHandler;
        Mobile.HitsRegenRateHandler = mobile => RegenerationRate(mobile, hits?.Invoke(mobile) ?? Mobile.DefaultHitsRate, "life-per-five");
        var mana = Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler = mobile => RegenerationRate(mobile, mana?.Invoke(mobile) ?? Mobile.DefaultManaRate, "mana-per-five");
    }
    public static TimeSpan RegenerationRate(Mobile mobile, TimeSpan baseline, string code)
    {
        int points = mobile is PlayerMobile player && ClassCombat.Enabled(player) && player.Alive
            && (code == "life-per-five" ? player.CanRegenHits : player.CanRegenMana) ? ClassGear.Material(player, code) : 0;
        return points <= 0 || baseline.Ticks <= 0 ? baseline
            : TimeSpan.FromTicks(Math.Max(1, (long)(TimeSpan.TicksPerSecond / (1 / baseline.TotalSeconds + points / 5.0))));
    }
    public static int ChillPercent(Mobile mobile) => ChillPercent(mobile, Core.TickCount);
    public static int ChillPercent(Mobile mobile, long now) => mobile != null && mobile.Alive && !mobile.Deleted
        && _chills.TryGetValue(mobile, out var chill) && now - chill.Until < 0 ? chill.Percent : 0;
    public static bool ApplyChill(Mobile target, long now)
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
        int duration = 2000 * Math.Clamp(100 - target.ColdResistance, 0, 200) / 100;
        if (effect == 0 || duration == 0) { return false; }
        var chill = _chills.GetValue(target, static _ => new Chill());
        chill.Percent = effect;
        chill.Until = now + Math.Max(duration, Math.Max(0, chill.Until - now));
        target.FixedEffect(0x374A, 10, 15, 0x480, 0);
        return true;
    }
    public static bool ApplyPoison(Mobile source, Mobile target, double total, long now)
    {
        total *= Math.Clamp(100 - target.PoisonResistance, 0, 200) / 100.0;
        if (total <= 0 || target.Deleted || !target.Alive || target.Blessed) { return false; }
        double rate = total / 3000;
        if (_poisons.TryGetValue(target.Serial, out var old))
        {
            Advance(target, old, now);
            if (!target.Alive || target.Deleted) { _poisons.Remove(target.Serial); return false; }
            if (now - old.Until < 0 && rate + 1e-12 < old.PerMillisecond) { return false; }
        }
        double remainder = old?.Fraction ?? (_poisonRemainders.TryGetValue(target, out var saved) ? saved.Value : 0);
        _poisonRemainders.Remove(target);
        _poisons[target.Serial] = new Venom
        {
            Source = source.Serial, Updated = now, Until = now + 3000, PerMillisecond = rate,
            Fraction = remainder
        };
        target.FixedEffect(0x374A, 10, 15, 0x3F, 0);
        return true;
    }
    private static void Advance(Mobile target, Venom poison, long now)
    {
        long elapsed = Math.Max(0, now - poison.Updated);
        long remaining = Math.Max(0, poison.Until - poison.Updated);
        poison.Fraction += Math.Min(elapsed, remaining) * poison.PerMillisecond;
        poison.Updated = now;
        int damage = (int)Math.Floor(poison.Fraction + 1e-9);
        poison.Fraction -= damage;
        if (damage > 0)
        {
            var source = World.FindMobile(poison.Source);
            // Periodic poison must not trigger another weapon hit or hit recovery.
            target.Damage(damage, source);
            CombatDamage.Finish(target, source);
        }
    }
    public static void Tick(long now)
    {
        _expired.Clear();
        foreach (var (serial, poison) in _poisons)
        {
            var target = World.FindMobile(serial);
            if (target == null || target.Deleted || !target.Alive || target.Map == Map.Internal)
            { _expired.Add(serial); continue; }
            Advance(target, poison, now);
            if (now - poison.Until >= 0 || !target.Alive || target.Deleted)
            {
                if (target.Alive && !target.Deleted)
                { _poisonRemainders.GetValue(target, static _ => new Remainder()).Value = poison.Fraction; }
                _expired.Add(serial);
            }
        }
        foreach (var serial in _expired) { _poisons.Remove(serial); }
    }
    public static void Clear(Mobile mobile)
    {
        _chills.Remove(mobile);
        _poisonRemainders.Remove(mobile);
        _poisons.Remove(mobile.Serial);
    }
    public static GearDamage Damage(PlayerMobile player, double roll)
    {
        roll = Math.Clamp(roll, 0, 1);
        var weapon = CombatTiming.Equipped(player);
        var physical = ClassStats.PhysicalDamageRange(player);
        double value = physical.Min + (physical.Max - physical.Min) * roll;
        if (weapon == null || ClassGear.Base(weapon) is not { } basis) { return new(value, 0, 0, 0, 0); }
        bool two = weapon.Layer == Layer.TwoHanded || basis.TwoHanded && !basis.OneOrTwoHanded;
        int min = two && basis.TwoHandMax > 0 ? basis.TwoHandMin : basis.MinDamage;
        int max = two && basis.TwoHandMax > 0 ? basis.TwoHandMax : basis.MaxDamage;
        if (ClassGear.Definition(weapon).Category == "Throwing" && basis.MissileMax > 0)
        { min = basis.MissileMin; max = basis.MissileMax; }
        double reference = (min + (max - min) * roll) * Math.Max(0, 100 + ClassStats.DamageBonus(player, weapon)) / 10000.0;
        return new(value + reference * ClassGear.Material(weapon, "damage-physical"),
            reference * ClassGear.Material(weapon, "damage-fire"), reference * ClassGear.Material(weapon, "damage-cold"),
            reference * ClassGear.Material(weapon, "damage-lightning"), reference * ClassGear.Material(weapon, "damage-poison"));
    }
    public static int Strike(PlayerMobile player, Mobile target, double roll)
    {
        var damage = Damage(player, roll);
        double total = damage.Physical * Math.Clamp(100 - target.PhysicalResistance, 0, 200)
            + damage.Fire * Math.Clamp(100 - target.FireResistance, 0, 200)
            + damage.Cold * Math.Clamp(100 - target.ColdResistance, 0, 200)
            + damage.Lightning * Math.Clamp(100 - target.EnergyResistance, 0, 200);
        int final = total > 0 ? Math.Max(1, (int)Math.Floor(total / 100)) : 0;
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
            if (damage.Cold > 0) { ApplyChill(target, Core.TickCount); }
            if (damage.Poison > 0) { ApplyPoison(player, target, damage.Poison, Core.TickCount); }
        }
        CombatTiming.Equipped(player)?.WearOnHit(player, target);
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
