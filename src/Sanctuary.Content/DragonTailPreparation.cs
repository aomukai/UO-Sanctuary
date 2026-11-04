// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Prepared suppliers only. Learned action/payment/packet/event integration is
// deliberately separate; this file alone does not publish a usable talent.
internal static class DragonTailPreparation
{
    internal static ActionTiming Timing(int advance)
    {
        if(advance<=0){throw new ArgumentOutOfRangeException(nameof(advance));}
        return new((3328+advance-1)/advance,(1024+advance-1)/advance);
    }
    internal static ActionTiming Timing(PlayerMobile owner)
    {
        var hands=WeaponHands.Pair(owner);
        using var primary=hands is{} pair?WeaponHands.Use(owner,pair.Primary):null;
        int modifier=SkillMechanics.For(CombatAbility.DragonTail).Number("Param4");
        int advance=CombatTiming.FrameAdvance(CombatTiming.Weapon(CombatTiming.Equipped(owner)),CombatTiming.Ias(owner),
            GearEffects.AttackSlowTotal(owner),CombatTiming.SkillAttackRate(owner),rateAdjustment:modifier);
        // Keep the -40 in this action clock. No global stat/list can survive
        // completion, cancellation, equipment changes or a saved world.
        return Timing(advance);
    }
    private sealed class Result(PlayerMobile owner,BaseCreature target,Func<bool> inRange,Func<double> hitRoll,bool charged,bool native):SourceMeleeResultServices
    {
        internal override int? AttackerKind=>0;
        internal override int? DefenderKind=>1;
        internal override bool Hostile()=>ClassCombat.IsHostile(owner,target);
        internal override bool DefenderRunning()=>throw new InvalidOperationException("NPC is not a running player");
        internal override int MeleeReach()=>throw new NotSupportedException("Prepared Dragon Tail uses zero range offset");
        internal override bool InRange(int bonus)=>bonus==1&&inRange();
        internal override bool HitSuccessful(int percent,bool missile)
        {
            if(missile){throw new InvalidOperationException("Dragon Tail is melee");}
            bool hit=ClassStats.CheckHit(owner,target,percent,hitRoll,notifyMelee:false,guaranteed:charged)==true;
            if(hit){WeaponStartEffects.SuccessfulHitRoll(owner,target);}return hit;
        }
        internal override int BlockOrDodge(bool avoid,bool block)
        {
            if(native){return 0;} // Do not invent a D2 shield for an unmapped UO monster.
            if(avoid||!block||!SourceBlocking.TryRoll(target,true,out bool blocked,out _))
            {throw new NotSupportedException("Dragon Tail recipient shield rule is unavailable");}
            return blocked?1:0;
        }
        internal override bool Uninterruptible()=>MonsterUninterruptibles.Active(target);
    }
    internal static int ResultFlags(PlayerMobile owner,BaseCreature target,Func<bool> inRange,Func<double> hitRoll)
    {
        PlayerWeaponContact.RequireOrdinary(owner,target,true);
        var charges=AssassinCharges.Finisher(owner);
        int attack=checked((int)SkillMechanics.Values(owner,CombatAbility.DragonTail).Formula("toht")+charges.Sum(x=>x.AttackPercent));
        // Explicit approved Sanctuary rule: charged accuracy only. The shared
        // result pipeline still enforces hostility, range and block/dodge.
        return SourceMeleeResult.Resolve(new Result(owner,target,inRange,hitRoll,charges.Length!=0,
            PlayerWeaponContact.Metadata(target,true)==null),attack);
    }
}
