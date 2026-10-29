// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal sealed record GenericPetStateSeed(short State,byte Flags);
internal sealed class GenericPetStateGroup(int slot,byte flags)
{
    // A source pointer links each group to a live40-byte candidate. Preserve
    // that identity by slot; do not turn it into a stale copied descriptor.
    internal readonly int Slot=slot;
    internal readonly List<int> States=[];
    internal int StateGroup;
    internal bool Flag4=>(flags&4)!=0;
    internal bool Flag2=>(flags&2)!=0;
}
internal sealed record GenericPetStateGroupServices(Func<byte,int,GenericPetStateSeed> Skill,
    Func<byte,bool,int> StateCount,Func<byte,int,short?> State,Func<byte,int,short> RawStateGroup);
internal sealed record GenericPetSearchStateServices(Func<byte,int,short?> SkillWordA2,
    Func<byte,bool,int> StateCount,Func<byte,int,short?> State,Func<byte,int,short> RawStateGroup);
internal static class GenericPetStateGroups
{
    private static void RequireSelector(byte selector)
    {if(selector is <1 or >3){throw new NotSupportedException("Current state grouping requires selector1..3");}}
    internal static List<GenericPetStateGroup> Build(byte selector,IList<GenericPetCombatCandidate> rows,byte type,GenericPetStateGroupServices s,Action<GenericPetStateGroup> added=null)
    {
        var groups=new List<GenericPetStateGroup>();bool expand=false;
        for(int i=0;i<rows.Count;i++)
        {
            var row=rows[i];if(row.Byte36!=type||row.BaseModeFlag!=0){continue;}
            rows[i]=row with{Target=0};var skill=s.Skill(selector,row.SkillId);
            if(skill==null||skill.State<0){continue;}
            RequireSelector(selector);
            if(skill.State>=s.StateCount(selector,false)){continue;}
            var state=s.State(selector,skill.State);if(!state.HasValue){continue;}
            var group=new GenericPetStateGroup(i,skill.Flags);groups.Add(group);added?.Invoke(group);
            if(state.Value!=0){group.StateGroup=state.Value;expand=true;}
            else{group.States.Add(skill.State);}
        }
        if(expand)
        {
            for(int state=0;;state++)
            {
                RequireSelector(selector);
                if(state>=s.StateCount(selector,false)){break;}
                RequireSelector(selector);
                if((long)state>=(long)s.StateCount(selector,true)){continue;}
                short category=s.RawStateGroup(selector,state);if(category==0){continue;}
                foreach(var group in groups){if(group.StateGroup!=0&&group.StateGroup==category){group.States.Add(state);}}
            }
        }
        return groups;
    }
}

internal sealed class GenericPetSearchStates
{
    internal readonly List<GenericPetStateGroup> Groups=[];
    internal void Prepare(byte selector,IList<GenericPetCombatCandidate> candidates,GenericPetSearchStateServices services)
    {
        ArgumentNullException.ThrowIfNull(candidates);ArgumentNullException.ThrowIfNull(services);
        // Managed storage owns no source allocator handles. Clear all previous
        // groups before metadata calls,including an empty/invalid new input.
        Groups.Clear();
        GenericPetStateGroups.Build(selector,candidates,2,new(
            (table,id)=>services.SkillWordA2(table,id) is{} state?new(state,0):null,
            services.StateCount,services.State,services.RawStateGroup),Groups.Add);
    }
}
