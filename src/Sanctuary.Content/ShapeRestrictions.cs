// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// Source shape eligibility only. Rank,resource,item,town,target and recovery
// checks remain with their existing consumers. Native live state currently
// supplies the prepared Druid forms; no unimplemented transformation is inferred.
internal static class ShapeRestrictions
{
    internal static bool Allowed(SkillDefinition skill,bool restricted,Func<string,bool> activeState)
    {
        ArgumentNullException.ThrowIfNull(skill);ArgumentNullException.ThrowIfNull(activeState);
        int rule=skill.Number("restrict");
        if(rule==0){return !restricted;}
        if(rule==1){return true;}
        if(rule!=2){throw new NotSupportedException("Unmapped source shape restriction");}
        if(!restricted){return false;}
        for(int i=1;i<=3;i++)
        {
            string state=skill.Text("State"+i);
            // Missing table names compile to the negative state sentinel.
            // The source returns immediately,not after checking later slots.
            if(string.IsNullOrEmpty(state)||state=="0"){return false;}
            if(activeState(state)){return true;}
        }
        return false;
    }
    internal static bool Allowed(PlayerMobile owner,CombatAbility ability)
    {
        if(owner==null||ability==CombatAbility.None||!ClassAbilities.Valid(ability)){return false;}
        // Current common Attack0is restrict1; it is outside the240talent rows.
        if(ability==CombatAbility.BasicAttack){return true;}
        var form=DruidPlayerForms.State(owner)?.Form;
        return Allowed(SkillMechanics.For(ability),form!=null,name=>form!=null&&name switch
        {"wolf"=>form.Modifiers.StateId==139,"bear"=>form.Modifiers.StateId==140,_=>false});
    }
}
