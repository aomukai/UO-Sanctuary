// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;
internal sealed record GenericPetBuffSkill(short State,byte Flags39);
internal sealed record GenericPetPreparationContext(byte GameTableSelector,int Pet,int Owner);
internal sealed record GenericPetPreparationServices(
    Func<byte,int,GenericPetBuffSkill> Skill,
    Func<byte,bool,int> StateCount,
    Func<byte,int,short?> State,
    Func<byte,int,short> RawStateGroup,
    Func<int,int,bool> HasState,
    Func<int,IReadOnlyList<int>> OwnerUnits,
    Func<int,IReadOnlyList<int>> NestedUnits,
    Func<int,GenericPetDecisionUnit> Unit,
    Func<int,int,int,int> Distance);
internal static class GenericPetPreparation
{
    internal static void Apply(GenericPetPreparationContext c,IList<GenericPetCombatCandidate> rows,GenericPetPreparationServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(rows);ArgumentNullException.ThrowIfNull(s);
        var groups=GenericPetStateGroups.Build(c.GameTableSelector,rows,1,new(
            (selector,id)=>s.Skill(selector,id) is{} skill?new(skill.State,skill.Flags39):null,
            s.StateCount,s.State,s.RawStateGroup));
        bool anyFlag4=false,anyFlag2=false;
        foreach(var group in groups){anyFlag4|=group.Flag4;anyFlag2|=group.Flag2;}
        if(groups.Count==0){return;}
        bool Needs(int unit,GenericPetStateGroup group)
        {foreach(int state in group.States){if(s.HasState(unit,state)){return false;}}return true;}
        void Set(GenericPetStateGroup group,int unit){rows[group.Slot]=rows[group.Slot] with{Target=unit};}
        int assigned=0;
        foreach(var group in groups){if(Needs(c.Pet,group)){Set(group,c.Pet);assigned++;}}
        if(assigned==groups.Count||!anyFlag4||c.Owner==0){return;}
        void Visit(int unit)
        {
            if(unit==0){return;}
            var info=s.Unit(unit);var(x,y)=info.Coordinates();if(s.Distance(c.Pet,x,y)>1024){return;}
            if(info.Kind==1)
            {
                foreach(var group in groups)
                {if(rows[group.Slot].Target==0&&Needs(unit,group)){Set(group,unit);}}
                return;
            }
            if(info.Kind!=0){return;}
            bool nested=false;
            foreach(var group in groups)
            {
                int target=rows[group.Slot].Target;
                if(target!=0&&(target==c.Pet||unit==c.Owner)){continue;}
                if(!group.Flag4){continue;}
                if(target!=0&&s.Unit(target).Kind!=1&&unit!=c.Owner){continue;}
                if(Needs(unit,group)){Set(group,unit);}
                if(group.Flag2&&rows[group.Slot].Target==0&&unit==c.Owner){nested=true;}
            }
            if(nested){foreach(int child in s.NestedUnits(unit)){Visit(child);}}
        }
        // Source's separate owner iterator and nested iterator supply order and
        // membership. Their world semantics are not guessed by this policy.
        // The captured context carries aggregate flag2; its callbacks use
        // the per-group flag instead. No extra selection rule is inferred.
        _=anyFlag2;
        foreach(int unit in s.OwnerUnits(c.Owner)){Visit(unit);}
    }
}
