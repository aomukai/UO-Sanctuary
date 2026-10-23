// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class HitRecovery
{
    public static bool Active(PlayerMobile player, long now) => player.Alive && ClassCombat.Enabled(player)
        && now - ClassCombat.For(player).RecoveryUntil < 0;

    // The two graveyard melee hit classes (3 and 12) use D2MOO's default divisor 16.
    // Return eighths so the 3/8 and 6/8 probability bands remain exact.
    public static int Chance(int damage, int maximumLife)
    {
        if (damage < 1 || maximumLife < 1 || (long)damage * 16 < maximumLife)
        {
            return 0;
        }
        return (long)damage * 8 < maximumLife ? 3 : (long)damage * 4 < maximumLife ? 6 : 8;
    }

    public static int Fhr(PlayerMobile player) => Math.Clamp(ClassGear.Bonus(player, "balance1")
        + ClassGear.Bonus(player, "balance2") + ClassGear.Bonus(player, "balance3"), 0, 10000);

    public static int BlockFrames(DiabloClass characterClass, int fasterBlock, BaseWeapon weapon = null,bool holyShield=false)
    {
        int frames = characterClass switch
        {
            DiabloClass.Amazon or DiabloClass.Paladin or DiabloClass.Assassin => 3,
            DiabloClass.Barbarian => 4, DiabloClass.Sorceress => 5, _ => 6
        };
        int bonus = Math.Clamp(fasterBlock, 0, 10000);
        var basis = weapon == null ? null : ClassGear.Base(weapon);
        bool amazonSwing = characterClass == DiabloClass.Amazon && weapon != null && !ClassGear.Broken(weapon)
            && (basis != null
                ? EquipmentBalance.IsType(basis.Type, "swor") || EquipmentBalance.IsType(basis.Type, "axe")
                    || EquipmentBalance.IsType(basis.Type, "blun") || EquipmentBalance.IsType(basis.Type, "taxe")
                : CombatTiming.Weapon(weapon).Family == AttackFamily.Swing);
        int speed = amazonSwing ? 88 : 256;
        int advance = Math.Max(1, speed * ((holyShield?100:50) + 120 * bonus / (120 + bonus)) / 100);
        return Math.Max(1, (frames * 256 + advance - 1) / advance - 1);
    }
    public static void Block(PlayerMobile player, long now)
    {
        if(BarbarianWhirlwind.Locked(player)){return;}
        if (!ClassCreation.Profiles.TryGet(player.Serial, out var profile)) { return; }
        if (Active(player, now)) { return; }
        var session = ClassCombat.For(player);
        // Released2.4: a successful block still negates damage but does not interrupt an active skill/attack animation.
        if(session.PendingAbility!=CombatAbility.None || now-session.NextAction<0){return;}
        int frames = BlockFrames(profile.Class, ClassGear.Bonus(player, "block2") + ClassGear.Bonus(player, "block3"), CombatTiming.Equipped(player),HolyShields.State(player)!=null);
        session.RecoveryUntil = Math.Max(session.RecoveryUntil, now + frames * ActionTiming.FrameMs);
        session.PendingAbility = CombatAbility.None; session.PendingTeleport = null; session.Conversion=null;session.BarbarianAttack=null;
        BarbarianDoubleThrow.Cancel(player);
        AmazonJab.Cancel(player);
        AmazonImpale.Cancel(player);
        AmazonFend.Cancel(player);
        AmazonMelee.Cancel(player);
        session.Animation = 20; session.Timing = new ActionTiming(frames, 1);
        ClassCombat.SendState(player, session);
        ClassCombat.SendAnimation(player, 20, frames * ActionTiming.FrameMs);
        player.Animate(20, 5, 1, true, false, 0);
    }

    public static int Frames(DiabloClass characterClass, int fhr = 0, BaseWeapon weapon = null)
    {
        int frames = characterClass switch
        {
            DiabloClass.Amazon => 6,
            DiabloClass.Sorceress => 8,
            DiabloClass.Necromancer or DiabloClass.Druid => 7,
            _ => 5
        };
        var basis = weapon == null ? null : ClassGear.Base(weapon);
        int speed = characterClass switch
        {
            DiabloClass.Paladin when basis?.Type is "pole" or "spea" or "staf" => 192,
            DiabloClass.Druid when weapon?.Animation is WeaponAnimation.Slash1H or WeaponAnimation.Bash1H => 248,
            _ => 256
        };
        int bonus = Math.Clamp(fhr, 0, 10000);
        int effective = 120 * bonus / (120 + bonus);
        int advance = Math.Max(1, speed * (50 + effective) / 100);
        return Math.Max(1, (frames * 256 + advance - 1) / advance - 1);
    }

    public static bool Apply(PlayerMobile player, int damage, long now)
    {
        if(AmazonFend.Locked(player,now) || AmazonImpale.Locked(player,now) || AmazonJab.Locked(player,now) || PaladinZeal.Locked(player,now) || PaladinCharge.Locked(player,now) || BarbarianConcentrate.Locked(player,now) || BarbarianLeap.Uninterruptible(player) || BarbarianWhirlwind.Locked(player)){return false;}
        if (!ClassCombat.Enabled(player) || !player.Alive || player.Hits <= 0 || Active(player, now))
        {
            return false;
        }
        int chance = Chance(damage, player.HitsMax);
        if (chance == 0 || chance < 8 && Utility.Random(8) >= chance)
        {
            return false;
        }
        if(PaladinOffense.PreventRecovery(player,now)){return false;}
        ClassCreation.Profiles.TryGet(player.Serial, out var profile);
        var session = ClassCombat.For(player);
        int duration = Frames(profile.Class, Fhr(player), CombatTiming.Equipped(player)) * ActionTiming.FrameMs;
        session.RecoveryUntil = now + duration;
        // Interrupt the unspent effect, retaining the original shared action deadline and input intent.
        session.PendingAbility = CombatAbility.None;
        session.Conversion=null;session.BarbarianAttack=null;
        BarbarianDoubleThrow.Cancel(player);
        AmazonJab.Cancel(player);
        AmazonImpale.Cancel(player);
        AmazonFend.Cancel(player);
        AmazonMelee.Cancel(player);
        BarbarianBerserk.CancelMode(player);
        session.PendingTeleport = null;
        session.Animation = 20;
        session.Timing = new ActionTiming(duration / ActionTiming.FrameMs, 1);
        ClassCombat.SendState(player, session);
        ClassCombat.SendAnimation(player, 20, duration);
        player.Animate(20, 5, 1, true, false, 0);
        return true;
    }
}
