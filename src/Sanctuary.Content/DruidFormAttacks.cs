// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

// Prepared Druid ordinary A1 only. Human weapon animation lengths normalize
// the modern rate; form metadata supplies the action/end schedule. The legacy
// extra ToHit allocation path is deliberately not asserted current here.
internal static class DruidFormAttacks
{
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static bool Active(PlayerMobile owner)=>DruidPlayerForms.State(owner)!=null;
    internal static int HumanFrames(string code)=>code switch
    {"HTH"=>16,"1HS"=>19,"1HT"=>19,"2HS"=>21,"2HT"=>23,"STF"=>17,"BOW"=>16,"XBW"=>20,_=>0};
    internal static bool Supported(PlayerMobile owner)=>HumanFrames(WeaponAnimationClasses.Equipped(owner))!=0;
    internal static ActionTiming Schedule(CombatAbility form,int advance)
    {
        int frames=form switch{CombatAbility.Werewolf=>13,CombatAbility.Werebear=>12,_=>throw new ArgumentOutOfRangeException(nameof(form))};
        if(advance<=0){throw new ArgumentOutOfRangeException(nameof(advance));}
        // Source ENDANIM is distinct from the supplementary calculator cycle.
        return new(checked((frames*256+advance-1)/advance),checked((7*256+advance-1)/advance));
    }
    internal static ActionTiming Timing(PlayerMobile owner)
    {
        var state=DruidPlayerForms.State(owner)??throw new InvalidOperationException("No Druid form attack context");
        int frames=HumanFrames(WeaponAnimationClasses.Equipped(owner));
        if(frames==0){throw new NotSupportedException("Unmapped Druid human weapon animation");}
        var weapon=CombatTiming.Weapon(CombatTiming.Equipped(owner));
        // The verified modern model is invariant under weapon/gear IAS split.
        // Existing native IAS already supplies the selected equipment total.
        var rate=DruidFormRates.Calculate(new(state.Form.Ability,frames,256,weapon.Modifier,0,CombatTiming.Ias(owner),
            state.Form.Modifiers.AttackRate+DruidAttackCharges.AttackRate(owner)+PaladinOffense.AttackRate(owner)+AssassinBuffs.AttackRate(owner),Math.Max(0,GearEffects.AttackSlow(owner))));
        return Schedule(state.Form.Ability,rate.Progress256);
    }
    internal static int Reach(PlayerMobile owner)
    {
        var weapon=CombatTiming.Equipped(owner);
        // Preserve the project's native melee reach adapter. Bow/throw native
        // projectile distance must not become transformed melee distance.
        if(weapon==null||weapon is BaseRanged||ClassGear.Definition(weapon)?.Category=="Throwing"){return 1;}
        return Math.Clamp(Math.Max(weapon.MaxRange,weapon is Halberd?2:1),1,CombatProtocol.SightRange);
    }
}
