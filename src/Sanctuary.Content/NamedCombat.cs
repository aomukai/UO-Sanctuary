// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class NamedCombat
{
    public static bool Ranged(Mobile attacker) => attacker is PlayerMobile player
        ? CombatTiming.Weapon(CombatTiming.Equipped(player)).Family is AttackFamily.Bow or AttackFamily.Crossbow or AttackFamily.Throw
        : attacker.Weapon is BaseRanged || attacker is BaseCreature creature && CreatureProfiles.For(creature).Attack1.Missile != null;
    public static int DefenseFor(Mobile attacker, Mobile target, int defense,bool? ranged=null)
    {
        if (target is PlayerMobile defender)
        { defense += ClassGear.Bonus(defender, (ranged??Ranged(attacker)) ? "ac-miss" : "ac-hth"); }
        if (attacker is not PlayerMobile owner) { return Math.Max(0, defense); }
        bool protectedTarget = target is PlayerMobile || Elite(target) || target is BaseCreature { Controlled: true };
        if (target is BaseCreature && !protectedTarget && ClassGear.Bonus(owner, "ignore-ac") > 0) { return 0; }
        int reduction = Math.Clamp(ClassGear.Bonus(owner, "reduce-ac"), 0, 100);
        if (protectedTarget) { reduction /= 2; }
        return Math.Max(0, defense * (100 - reduction) / 100);
    }
    public static bool Elite(Mobile target) => target is BaseCreature creature &&
        (CreatureProfiles.For(creature).Traits.Boss == true || creature is EncounterCreature { Grade: EncounterGrade.Champion or EncounterGrade.Unique or EncounterGrade.SuperUnique });
    public static double Deadly(PlayerMobile player, Random random)
    {
        // Source weapon mastery critical short-circuits ordinary deadly strike;
        // a physical packet can double once, never stack the two into4x damage.
        int mastery=BarbarianMasteries.Bonus(player,CombatTiming.Equipped(player),3);
        if(mastery>0 && random.Next(100)<mastery){return 2;}
        int chance = Math.Clamp(ClassGear.Bonus(player, "deadly"), 0, 100);
        return chance > 0 && random.Next(100) < chance ? 2 : 1;
    }
    // Missile creation uses a different critical order from ordinary melee.
    // Generic Critical Strike is not yet a supported native passive; its zero
    // chance still consumes the source's unconditional first draw.
    internal static bool ProjectileCritical(PlayerMobile player,Random random)
    {
        _=random.Next(100);
        int deadly=Math.Clamp(ClassGear.Bonus(player,"deadly"),0,100);
        if(deadly>0 && random.Next(100)<deadly){return true;}
        int mastery=BarbarianMasteries.Throwing(player,CombatTiming.Equipped(player),3);
        return mastery>0 && random.Next(100)<mastery;
    }
    public static int CrushingLoss(Mobile target, bool ranged)
    {
        int divisor = target is PlayerMobile || target is BaseCreature { Controlled: true } ? 10 : Elite(target) ? 8 : 4;
        if (ranged) { divisor *= 2; }
        return (int)((long)target.Hits * Math.Clamp(100 - target.PhysicalResistance, 0, 100) / (100 * divisor));
    }
    public static void Crushing(PlayerMobile player, Mobile target, bool ranged, Random random)
    {
        int chance = Math.Clamp(ClassGear.Bonus(player, "crush"), 0, 100);
        if (chance <= 0 || random.Next(100) >= chance) { return; }
        int damage = CrushingLoss(target, ranged);
        if (damage > 0) { target.Damage(damage, player); CombatDamage.Finish(target, player); }
    }
    public static double OpenWoundsDamage(int level, Mobile target, bool ranged)
    {
        int n = Math.Clamp(level, 1, 99) - 1;
        int rate = 40 + 9 * Math.Min(14, n); n = Math.Max(0, n - 14);
        rate += 18 * Math.Min(15, n); n = Math.Max(0, n - 15);
        rate += 27 * Math.Min(15, n); n = Math.Max(0, n - 15);
        rate += 36 * Math.Min(15, n); n = Math.Max(0, n - 15);
        rate += 45 * n;
        if (target is PlayerMobile) { rate /= 4; if (ranged) { rate /= 2; } }
        else if (Elite(target)) { rate /= 2; }
        return rate * 200.0 / 256;
    }
    public static void AfterStrike(PlayerMobile player, Mobile target, bool ranged, Random random,bool smite=false,bool skipKnockback=false)
    {
        if (!target.Alive || target.Deleted) { return; }
        TimedEffects.Slow(target, ClassGear.Bonus(player, "slow"), TimedEffects.Now);
        if (target is BaseCreature defender)
        { TimedEffects.ReduceDefense(defender, Math.Max(0, -ClassGear.Bonus(player, "dmg-ac")), TimedEffects.Now); }
        if(!smite){NamedControl.Freeze(player, target, ClassGear.Bonus(player, "freeze"), ranged, random);}
        NamedControl.ApplyBlind(player, target, ClassGear.Bonus(player, "stupidity"), ranged, random);
        NamedControl.ApplyFear(player, target, ClassGear.Bonus(player, "howl"), random);
        int wounds = Math.Clamp(ClassGear.Bonus(player, "openwounds"), 0, 100);
        if (wounds > 0 && random.Next(100) < wounds)
        { TimedEffects.Bleed(player, target, OpenWoundsDamage(ClassStats.Level(player), target, ranged), TimedEffects.Now); }
        if (target is BaseCreature && ClassGear.Bonus(player, "noheal") > 0)
        { TimedEffects.PreventHealing(target, TimedEffects.Now); }
        if (!smite && !skipKnockback && ClassGear.Bonus(player, "knock") > 0 && !Elite(target) && target.Map == player.Map)
        {
            int size = target is BaseCreature creature ? CreatureProfiles.For(creature).Traits.Size ?? 2 : 2;
            int chance = size <= 1 ? 100 : size >= 3 ? 25 : 50;
            if (random.Next(100) < chance)
            {
                // Use ordinary movement validation, including terrain, collision
                // and private housing entry, for the displaced tile.
                var direction = (Direction)(((int)target.GetDirectionTo(player) + 4) & 7);
                CombatDisplacement.TryStep(target, direction);
            }
        }
    }
    public static bool Demon(Mobile target) => target is BaseCreature creature
        && (CreatureProfiles.For(creature).Traits.Demon ?? SlayerGroup.GetEntryByName(SlayerName.Exorcism)?.Slays(creature) == true);
    public static int ResistanceCap(PlayerMobile player, ResistanceType type)
    {
        if (type == ResistanceType.Physical) { return 50; }
        string code = type switch
        { ResistanceType.Fire => "res-fire-max", ResistanceType.Cold => "res-cold-max", ResistanceType.Poison => "res-pois-max", _ => "res-ltng-max" };
        return Math.Clamp(75 + ClassGear.Bonus(player, code) + ClassGear.Bonus(player, "res-all-max") + PaladinResistance.Maximum(player,type), 0, 95);
    }
    public static void Killed(PlayerMobile player, BaseCreature target)
    {
        if (target.LastKiller != player || !player.Alive) { return; }
        player.Mana += Math.Max(0, ClassGear.Bonus(player, "mana-kill"));
        player.Hits += Math.Max(0, ClassGear.Bonus(player, "heal-kill") + (Demon(target) ? ClassGear.Bonus(player, "demon-heal") : 0));
    }
}
