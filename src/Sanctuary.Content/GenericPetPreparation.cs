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
    private sealed class Group(int slot)
    {
        internal readonly int Slot=slot;
        internal readonly List<int> States=new();
        internal int StateGroup;
        internal bool Flag4,Flag2;
    }
    private static void RequireSelector(byte selector)
    {if(selector is <1 or >3){throw new NotSupportedException("Current buff preparation state-table selector requires 1..3");}}
    internal static void Apply(GenericPetPreparationContext c,IList<GenericPetCombatCandidate> rows,GenericPetPreparationServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(rows);ArgumentNullException.ThrowIfNull(s);
        var groups=new List<Group>();bool anyStateGroup=false,anyFlag4=false,anyFlag2=false;
        for(int i=0;i<rows.Count;i++)
        {
            var row=rows[i];if(row.Byte36!=1||row.BaseModeFlag!=0){continue;}
            rows[i]=row with{Target=0};var skill=s.Skill(c.GameTableSelector,row.SkillId);
            if(skill==null||skill.State<0){continue;}
            RequireSelector(c.GameTableSelector);
            if(skill.State>=s.StateCount(c.GameTableSelector,false)){continue;}
            var state=s.State(c.GameTableSelector,skill.State);if(!state.HasValue){continue;}
            var group=new Group(i){Flag4=(skill.Flags39&4)!=0,Flag2=(skill.Flags39&2)!=0};groups.Add(group);
            anyFlag4|=group.Flag4;anyFlag2|=group.Flag2;
            if(state.Value!=0){group.StateGroup=state.Value;anyStateGroup=true;}
            else{group.States.Add(skill.State);}
        }
        if(groups.Count==0){return;}
        if(anyStateGroup)
        {
            for(int state=0;;state++)
            {
                RequireSelector(c.GameTableSelector);
                if(state>=s.StateCount(c.GameTableSelector,false)){break;}
                RequireSelector(c.GameTableSelector);
                if((long)state>=(long)s.StateCount(c.GameTableSelector,true)){continue;}
                short category=s.RawStateGroup(c.GameTableSelector,state);if(category==0){continue;}
                foreach(var group in groups){if(group.StateGroup!=0&&group.StateGroup==category){group.States.Add(state);}}
            }
        }
        bool Needs(int unit,Group group)
        {foreach(int state in group.States){if(s.HasState(unit,state)){return false;}}return true;}
        void Set(Group group,int unit){rows[group.Slot]=rows[group.Slot] with{Target=unit};}
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
