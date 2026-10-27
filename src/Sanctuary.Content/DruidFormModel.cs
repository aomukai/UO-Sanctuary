// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record DruidFormModifiers(int StateId,int Model,int DurationFrames,int StaminaPercent,
    int AttackRate,int OtherAnimationRate,int AttackPercent,int DamagePercent,int ArmorPercent,int LifePercent,int Concentration);
internal sealed record DruidFormState(CombatAbility Ability,int Rank,int LycanthropyRank,int StartedFrame,int UntilFrame,DruidFormModifiers Modifiers);
internal enum DruidFormOutcome{Refused,Entered,Unshifted,Expired}
internal sealed record DruidFormChange(DruidFormOutcome Outcome,DruidFormState State,int? SkillMode,int DelayFrames)
{
    internal bool NeedsMana=>Outcome==DruidFormOutcome.Entered;
}

// Pure player-form state/stat projection. It does not install a world buff or
// charge mana. The caller must validate/pay/commit, apply stat pools, and own
// expiry, presentation and the outer successful-cast delay separately.
internal static class DruidFormModel
{
    private static SkillDefinition Route(SkillValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if(values.Ability is not(CombatAbility.Werewolf or CombatAbility.Werebear)||values.Rank is <1 or >255)
        {throw new ArgumentOutOfRangeException(nameof(values));}
        var row=SkillMechanics.For(values.Ability);bool wolf=values.Ability==CombatAbility.Werewolf;
        if(row.CharacterClass!=DiabloClass.Druid||row.SourceId!=(wolf?223:228)||row.Passive||row.Left||!row.Right
            ||row.Number("srvdofunc")!=116||row.Text("aurastate")!=(wolf?"wolf":"bear")
            ||row.Number("globaldelay")!=25||row.Number("localdelay")!=0||values.Mana256!=15*256
            ||row.Text("anim")!="SC"||row.Number("seqnum")!=22||row.Number("UseAttackRate")!=1)
        {throw new NotSupportedException("Unsupported player-form source route");}
        return row;
    }
    internal static DruidFormModifiers Capture(SkillValues values)
    {
        var row=Route(values);bool wolf=values.Ability==CombatAbility.Werewolf;
        int duration=checked((int)values.Eval("auralencalc"));
        if(duration<=0){throw new NotSupportedException("Player form needs positive duration");}
        string[] channels=wolf?["skill_staminapercent","attackrate","item_tohit_percent","item_maxhp_percent"]
            :["damagepercent","skill_armor_percent","item_maxhp_percent","skill_concentration"];
        for(int i=0;i<channels.Length;i++)
        {if(row.Text("aurastat"+(i+1))!=channels[i]){throw new NotSupportedException("Unmapped player-form stat channel");}}
        for(int i=5;i<=6;i++)
        {if(row.Text("aurastat"+i)!="0"){throw new NotSupportedException("Additional player-form stat channel");}}
        int A(int i)=>checked((int)values.Eval("aurastatcalc"+i));
        if(wolf)
        {
            int rate=A(2);
            return new(139,430,duration,A(1),rate,rate,A(3),0,0,A(4),0);
        }
        return new(140,431,duration,0,0,0,0,A(1),A(2),A(3),A(4));
    }
    internal static void Validate(DruidFormState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if(state.Ability is not(CombatAbility.Werewolf or CombatAbility.Werebear)||state.Rank is <1 or >255
            ||state.LycanthropyRank is <0 or >255||state.StartedFrame<0||state.UntilFrame<state.StartedFrame||state.Modifiers==null)
        {throw new ArgumentException("Invalid captured player form");}
        var row=SkillMechanics.For(state.Ability);
        var values=new SkillValues(row,key=>key==row.SourceKey?state.Rank:key=="Shape Shifting"?state.LycanthropyRank:0,_=>0,1);
        var modifiers=Capture(values);
        if(modifiers!=state.Modifiers||state.UntilFrame-state.StartedFrame!=modifiers.DurationFrames)
        {throw new ArgumentException("Captured player-form ranks/stats/lifetime disagree");}
    }
    internal static bool CanStart(DruidFormState current,SkillValues values,int mana256)
    {
        Route(values);
        if(mana256<0){throw new ArgumentOutOfRangeException(nameof(mana256));}
        if(current!=null){Validate(current);return true;}
        return mana256>=values.Mana256;
    }
    internal static DruidFormChange Transform(DruidFormState current,SkillValues values,int frame,
        bool hasSkill=true,Func<int,bool> allocate=null)
    {
        var row=Route(values);
        if(frame<0){throw new ArgumentOutOfRangeException(nameof(frame));}
        if(current!=null)
        {
            Validate(current);
            // do116 removes the exclusive group before allocating another
            // form. Cross-form behavior is source-backed, not yet observed in
            // the current executable. Same-form free unshift is user-confirmed.
            return new(DruidFormOutcome.Unshifted,null,hasSkill?7:null,row.Number("globaldelay"));
        }
        int duration=checked((int)values.Eval("auralencalc"));
        int until=checked(frame+duration);
        if(duration<=0){throw new NotSupportedException("Player form needs positive duration");}
        if(allocate!=null&&!allocate(until)){return new(DruidFormOutcome.Refused,null,null,0);}
        int lycan=checked((int)values.Formula("skill('Shape Shifting'.lvl)"));
        var state=new DruidFormState(values.Ability,values.Rank,lycan,frame,until,Capture(values));
        Validate(state);
        // Successful entry's delay and mana belong to the outer handler.
        return new(DruidFormOutcome.Entered,state,hasSkill?10:null,0);
    }
    internal static DruidFormChange Expire(DruidFormState current,int frame,bool hasSkill=true)
    {
        Validate(current);
        if(frame<0){throw new ArgumentOutOfRangeException(nameof(frame));}
        return frame<current.UntilFrame?new(DruidFormOutcome.Refused,current,null,0)
            :new(DruidFormOutcome.Expired,null,hasSkill?7:null,0);
    }
}
