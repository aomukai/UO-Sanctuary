// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record DruidAttackCharge(CombatAbility Ability,int Count,int StoredRank,DateTime UntilUtc,
    int DamagePercent,int StunFrames,int AttackRate,int OtherAnimationRate,int VelocityPercent,int LifeDrainMinimum,int LifeDrainMaximum);

// Pure do120aura projection. Combat latch/contact,mana,world ownership,
// form-group cleanup,persistence and actual stat consumers are separate.
internal static class DruidAttackChargeModel
{
    private static SkillDefinition Route(CombatAbility ability)
    {
        if(ability is not(CombatAbility.Maul or CombatAbility.FeralRage)){throw new ArgumentOutOfRangeException(nameof(ability));}
        var row=SkillMechanics.For(ability);bool maul=ability==CombatAbility.Maul;
        if(row.CharacterClass!=DiabloClass.Druid||row.SourceId!=(maul?233:232)||row.Number("srvstfunc")!=56||row.Number("srvdofunc")!=120
            ||row.Text("anim")!="A1"||row.Text("seqtrans")!="A1"||row.Number("UseAttackRate")!=1||row.Number("restrict")!=2
            ||row.Text("State1")!=(maul?"bear":"wolf")||row.Text("aurastate")!=(maul?"maul":"feralrage")
            ||row.Text("calc2")!="lvl/par7 + par8"||row.Parameter(7)!=2||row.Parameter(8)!=3)
        {throw new NotSupportedException("Unmapped charged Druid source route");}
        string[] channels=maul?["damagepercent","stunlength","attackrate"]:["velocitypercent","lifedrainmindam","lifedrainmaxdam"];
        for(int i=0;i<3;i++){if(row.Text("aurastat"+(i+1))!=channels[i]){throw new NotSupportedException("Unmapped Druid charge channel");}}
        for(int i=4;i<=6;i++){if(row.Text("aurastat"+i) is not("0" or "")){throw new NotSupportedException("Additional Druid charge channel");}}
        return row;
    }
    private static SkillValues Values(SkillDefinition row,int rank)=>new(row,key=>key==row.SourceKey?rank:0,_=>0,1);
    internal static DruidAttackCharge Capture(CombatAbility ability,int storedRank,int count,DateTime until)
    {
        var row=Route(ability);
        if(storedRank is <1 or >255||count<1||count>Values(row,storedRank).Eval("calc2")||until.Kind!=DateTimeKind.Utc)
        {throw new ArgumentOutOfRangeException(nameof(count),"Invalid captured Druid charge count/rank/clock");}
        // Shared aura writer evaluates with charge COUNT as formula level.
        var aura=Values(row,count);int A(int i)=>checked((int)aura.Eval("aurastatcalc"+i));bool maul=ability==CombatAbility.Maul;
        int first=A(1),second=A(2),third=A(3);
        return maul?new(ability,count,storedRank,until,first,second,third,third,0,0,0)
            :new(ability,count,storedRank,until,0,0,0,0,first,second,third);
    }
    internal static void Validate(DruidAttackCharge state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if(Capture(state.Ability,state.StoredRank,state.Count,state.UntilUtc)!=state)
        {throw new ArgumentException("Druid charge payload disagrees with source count/rank");}
    }
    // Call only after the successful-latch path. This method does not decide
    // that a lost target,blocked hit or damage packet merits another charge.
    internal static DruidAttackCharge Gain(DruidAttackCharge current,SkillValues values,DateTime at,Func<bool> allocate=null)
    {
        ArgumentNullException.ThrowIfNull(values);var row=Route(values.Ability);
        if(values.Rank is <1 or >255||at.Kind!=DateTimeKind.Utc){throw new ArgumentOutOfRangeException(nameof(values));}
        if(current!=null)
        {Validate(current);if(current.Ability!=values.Ability){throw new ArgumentException("Druid charge slot mismatch");}if(current.UntilUtc<=at){current=null;}}
        if(current==null&&allocate?.Invoke()==false){return null;}
        int count=Math.Min(checked((int)values.Eval("calc2")),(current?.Count??0)+1);
        int duration=checked((int)values.Eval("auralencalc"));
        if(duration!=500||values.Mana256!=768){throw new NotSupportedException("Unmapped Druid charge duration/mana metadata");}
        return Capture(values.Ability,values.Rank,count,at.AddMilliseconds(checked(duration*40)));
    }
}
