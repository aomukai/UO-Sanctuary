// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class GemCombat
{
    public static void Configure()
    {
        ClassGearHooks.ResistedDamage = ResistedDamage;
        ClassGearHooks.WeaponHit = WeaponHit;
    }
    public static bool Undead(Mobile target) => target is BaseCreature creature
        && (CreatureProfiles.For(creature).Traits.Undead ?? (creature is GraveyardUndead or SkeletonGuardian || SlayerGroup.GetEntryByName(SlayerName.Silver)?.Slays(creature) == true));
    public static int Reduction(Mobile target) => target is PlayerMobile p && ClassCombat.Enabled(p)
        ? Math.Max(0, ClassGear.Bonus(p, "magic-reduction")) : 0;
    internal static double Component(Mobile target, double damage, int resistance, bool magical)
        => Math.Max(0, damage - (magical ? Reduction(target) : 0)) * Math.Clamp(100 - resistance, 0, 200) / 100.0;
    public static int? ResistedDamage(Mobile target, int damage, int physical, int fire, int cold, int poison, int lightning)
    {
        if (Reduction(target) == 0) { return null; }
        double value = Component(target, damage * physical / 100.0, target.PhysicalResistance, false)
            + Component(target, damage * fire / 100.0, target.FireResistance, true)
            + Component(target, damage * cold / 100.0, target.ColdResistance, true)
            + Component(target, damage * poison / 100.0, target.PoisonResistance, false)
            + Component(target, damage * lightning / 100.0, target.EnergyResistance, true);
        return Math.Max(0, (int)Math.Floor(value));
    }
    internal static double Value(Item host, string code, double roll)
    {
        if (host == null || ClassGear.Broken(host) || ClassGear.State(host) is not { Identified: true } state) { return 0; }
        double total = 0;
        foreach (var gem in state.Gems)
        {
            foreach (var e in GemSockets.Effects(host, gem)) { if (e.Code == code) { total += e.Minimum + (e.Maximum - e.Minimum) * roll; } }
        }
        return total;
    }
    internal static int Duration(Item host, string code)
    {
        int total = 0;
        if (host == null || ClassGear.Broken(host) || ClassGear.State(host) is not { Identified: true } state) { return 0; }
        foreach (var gem in state.Gems)
        { foreach (var e in GemSockets.Effects(host, gem)) { if (e.Code == code) { total += e.DurationMs; } } }
        return total;
    }
    internal static (double Total, int Duration) Poison(Item host, double material)
    {
        double rate = material / 3000.0; int length = material > 0 ? 3000 : 0, count = material > 0 ? 1 : 0;
        if (host != null && !ClassGear.Broken(host) && ClassGear.State(host) is { Identified: true } state)
        {
            foreach (var gem in state.Gems)
            {
                foreach (var e in GemSockets.Effects(host, gem))
                {
                    if (e.Code == "poison-damage" && e.DurationMs > 0)
                    { rate += e.Minimum / (double)e.DurationMs; length += e.DurationMs; count++; }
                }
            }
        }
        int duration = count == 0 ? 3000 : length / count;
        return (rate * duration, duration);
    }
    public static int Drain(BaseCreature creature)
    {
        var row = CreatureProfiles.For(creature);
        if (row.Traits.DrainEffectiveness is { } drain) { return Math.Clamp(drain, 0, 100); }
        if (row.SourceStats.TryGetValue("Drain", out string source)) { return int.TryParse(source, out int value) ? Math.Clamp(value, 0, 100) : 0; }
        return creature is Skeleton or SkeletalKnight or SkeletalMage or GraveyardSkeleton or SkeletonGuardian ? 0 : 100;
    }
    public static void Leech(PlayerMobile owner, Mobile target, int physicalLoss)
    {
        if (!owner.Alive || owner.Deleted || physicalLoss <= 0 || target is not BaseCreature creature) { return; }
        int drain = Drain(creature);
        int divisor = MonsterVariants.Difficulty(creature) switch { MonsterDifficulty.Nightmare => 2, MonsterDifficulty.Hell => 3, _ => 1 };
        int life = (int)((long)physicalLoss * Math.Max(0, ClassGear.Bonus(owner, "life-steal%") + CombatPotions.Bonus(owner, "stealing")) * drain / (10000 * divisor));
        int mana = (int)((long)physicalLoss * Math.Max(0, ClassGear.Bonus(owner, "mana-steal%") + CombatPotions.Bonus(owner, "stealing")) * drain / (10000 * divisor));
        owner.Hits += life; owner.Mana += mana;
    }
    public static void WeaponHit(Mobile attacker, Mobile target, int loss, bool ranged)
    {
        if (ranged || loss <= 0 || attacker == null || attacker == target || attacker.Deleted || !attacker.Alive
            || target is not PlayerMobile p || !ClassCombat.Enabled(p) || attacker.Blessed
            || ClassCombatHooks.Friendly?.Invoke(attacker, target) == true) { return; }
        int thorns = Math.Max(0, ClassGear.Bonus(p, "thorns"));
        int damage = (int)Math.Floor(Component(attacker, thorns, attacker.PhysicalResistance, false));
        if (damage > 0)
        {
            // Reflection is not another weapon strike: no recursive thorns, leech or on-hit effects.
            attacker.Damage(damage, p); CombatDamage.Finish(attacker, p);
        }
    }
}
