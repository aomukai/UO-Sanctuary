// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server.Mobiles;

namespace Sanctuary;

internal sealed record DragonClawClockPlan(string Profile,int Advance,int[] Contacts,int EndFrame);

// Distinct original-calculator dualOnly rate, not the claw builders' primary-only
// branch. This supplier does not register a learned finisher or choose its policy.
internal static class DragonClawPreparation
{
    internal const string Profile="original-calculator-b7d7910-dual-dragon-claw-v1";
    private static readonly Lazy<bool> Validated=new(Validate);
    private static bool Validate()
    {
        var row=SkillMechanics.For(CombatAbility.DragonClaw);
        if(row.SourceId!=260||row.Text("anim")!="SQ"||row.Number("seqnum")!=16||row.Number("weapsel")!=3
            ||row.Text("itypea1")!="h2h"||row.Text("itypeb1")!="h2h"||row.Number("SrcDam")!=128
            ||row.Number("srvstfunc")!=25||row.Number("srvdofunc")!=46
            ||row.Text("calc1")!="ln12 + skill('Claw Mastery'.blvl)*par7")
        {throw new InvalidDataException("Unmapped prepared Dragon Claw route");}
        return true;
    }
    private static int Effective(int ias)
    {int bounded=Math.Clamp(ias,0,10000);return 120*bounded/(120+bounded);}
    internal static int Advance(int primaryWsm,int secondaryWsm,int primaryTotalIas,int secondaryTotalIas,int slow,int skillRate)
    {
        // Truncate only after combining both half-valued terms. Averaging WSM
        // or EIAS separately would round differently for odd or negative totals.
        int effective=checked(2*(skillRate-slow-30)-primaryWsm-secondaryWsm+Effective(primaryTotalIas)+Effective(secondaryTotalIas))/2;
        return CombatTiming.FrameAdvance(new(AttackFamily.Thrust,0),0,0,effective);
    }
    internal static bool WeaponAllowed(PlayerMobile owner)
        =>owner!=null&&WeaponHands.Pair(owner) is{} pair&&WeaponHands.Claw(pair.Primary)&&WeaponHands.Claw(pair.Secondary)
            &&ClassGear.Active(owner,pair.Primary)&&ClassGear.Active(owner,pair.Secondary);
    internal static int Advance(PlayerMobile owner)
    {
        if(owner==null||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Assassin
            ||DruidPlayerForms.State(owner)!=null||!WeaponAllowed(owner))
        {throw new NotSupportedException("Prepared Dragon Claw requires an ordinary Assassin with two active claws");}
        var pair=WeaponHands.Pair(owner).Value;int first,second;
        using(WeaponHands.Use(owner,pair.Primary)){first=CombatTiming.Ias(owner);}
        using(WeaponHands.Use(owner,pair.Secondary)){second=CombatTiming.Ias(owner);}
        return Advance(CombatTiming.Weapon(pair.Primary).Modifier,CombatTiming.Weapon(pair.Secondary).Modifier,first,second,
            GearEffects.AttackSlowTotal(owner),CombatTiming.SkillAttackRate(owner));
    }
    internal static DragonClawClockPlan Plan(int advance)
    {
        _=Validated.Value;
        if(advance is <38 or >448){throw new NotSupportedException("Unmapped prepared dual-claw frame advance");}
        var schedule=AssassinClaws.Schedule(true,advance);
        return new(Profile,advance,schedule.Events,schedule.End);
    }
    internal static DragonClawClockPlan Plan(PlayerMobile owner)=>Plan(Advance(owner));
    internal static int PhysicalPercent(PlayerMobile owner)
    {
        if(owner==null||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Assassin
            ||DruidPlayerForms.State(owner)!=null||!WeaponAllowed(owner)||SkillMechanics.Rank(owner,CombatAbility.DragonClaw) is <1 or >255)
        {throw new NotSupportedException("Prepared Dragon Claw damage needs an ordinary trained Assassin with both claws");}
        return checked((int)SkillMechanics.Values(owner,CombatAbility.DragonClaw).Eval("calc1"));
    }
}
