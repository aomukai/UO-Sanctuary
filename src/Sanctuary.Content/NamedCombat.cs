// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class NamedCombat
{
    public static bool Ranged(Mobile attacker) => attacker is PlayerMobile player
        ? !DruidFormAttacks.Active(player)&&(CombatTiming.Weapon(CombatTiming.Equipped(player)).Family is AttackFamily.Bow or AttackFamily.Crossbow or AttackFamily.Throw)
        : attacker.Weapon is BaseRanged || attacker is BaseCreature creature && CreatureProfiles.For(creature).Attack1.Missile != null;
    public static int DefenseFor(Mobile attacker, Mobile target, int defense,bool? ranged=null)
    {
        if (target is PlayerMobile defender)
        { defense += ClassGear.Bonus(defender, (ranged??Ranged(attacker)) ? "ac-miss" : "ac-hth"); }
        else if(target is BaseCreature creature)
        {defense=checked(defense+NpcEquipmentCombat.Bonus(creature,(ranged??Ranged(attacker))?"ac-miss":"ac-hth"));}
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
        if(Ranged(player)){return ProjectileCritical(player,random)?2:1;}
        // Normal melee tries mastery, generic critical, then Deadly Strike; double once.
        int mastery=WeaponMasteries.Bonus(player,CombatTiming.Equipped(player),3);
        int chance = Math.Clamp(ClassGear.Bonus(player, "deadly"), 0, 100);
        return MeleeCritical(mastery,AmazonCombat.CriticalChance(player),chance,random)?2:1;
    }
    internal static bool MeleeCritical(int mastery,int generic,int deadly,Random random)
    {
        if(mastery>0 && random.Next(100)<mastery){return true;}
        if(generic>0 && random.Next(100)<generic){return true;}
        return deadly>0 && random.Next(100)<deadly;
    }
    // Missile creation always draws generic critical first, even at zero chance.
    internal static bool ProjectileCritical(PlayerMobile player,Random random)
    {
        int deadly=Math.Clamp(ClassGear.Bonus(player,"deadly"),0,100);
        int mastery=WeaponMasteries.Bonus(player,CombatTiming.Equipped(player),3);
        return ProjectileCritical(AmazonCombat.CriticalChance(player),deadly,mastery,random);
    }
    internal static bool ProjectileCritical(int generic,int deadly,int mastery,Random random)
    {
        if(random.Next(100)<generic){return true;}
        if(deadly>0 && random.Next(100)<deadly){return true;}
        return mastery>0 && random.Next(100)<mastery;
    }
    internal static int CrushingLoss256(int life256,int divisor,int resistance,int hpBonus=0,bool ranged=false)
    {
        if(life256<=0 || divisor<=0){return 0;}
        long adjusted=divisor+(long)divisor*Math.Max(0,hpBonus)/100;
        if(ranged){adjusted*=2;}
        long loss=life256/adjusted;
        // Preserve the two truncations: divide current FP256 life first, then
        // subtract the positive resistance share. Negative resistance adds nothing.
        return (int)(loss-loss*Math.Clamp(resistance,0,100)/100);
    }
    internal static int CrushingLoss256(Mobile target,bool ranged)
    {
        // Preserve the existing published-guide grouping. The older event only
        // halves for Boss/SuperUnique; modern Champion/Unique parity is unresolved.
        int divisor=target is PlayerMobile || target is BaseCreature{Controlled:true}?10:Elite(target)?8:4;
        // Existing Sanctuary encounters have no separate D2 player-count HP bonus.
        return CrushingLoss256(SkillHealth.Balance256(target),divisor,target.PhysicalResistance,ranged:ranged);
    }
    public static int CrushingLoss(Mobile target, bool ranged)=>CrushingLoss256(target,ranged)/256;
    public static void Crushing(PlayerMobile player, Mobile target, bool ranged, Random random)
    {
        if(target is not{Alive:true,Deleted:false} || !target.CanBeDamaged()){return;}
        int chance = Math.Clamp(ClassGear.Bonus(player, "crush"), 0, 100);
        if (chance <= 0 || random.Next(100) >= chance) { return; }
        int damage=CrushingLoss256(target,ranged);
        if(damage>0)
        {
            // The source callback writes life directly before the ordinary packet;
            // it does not dispatch an additional weapon-damage or recovery event.
            SkillHealth.SetBalance256(target,SkillHealth.Balance256(target)-damage);
            target.FixedEffect(0x376A,10,5,0x21,0); // Crushing overlay147 placeholder.
        }
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
        var siphon=HexSiphon.AfterKill(player);
        // The item callbacks add whole points to FP256 balances before capping.
        // Keep existing fractions, including the final fraction below maximum.
        int mana=Math.Max(0,ClassGear.Bonus(player,"mana-kill")+siphon.Mana);
        int life=Math.Max(0,ClassGear.Bonus(player,"heal-kill")+(Demon(target)?ClassGear.Bonus(player,"demon-heal"):0)+siphon.Life);
        NecromancerEffects.AddMana(player,checked(mana*256));SkillHealth.Heal(player,checked(life*256));
    }
}
