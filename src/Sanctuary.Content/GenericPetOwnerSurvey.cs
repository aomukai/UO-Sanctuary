// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace Sanctuary;

// The callback loads +8,not the +4 class field used by the command setter.
// Preserve the raw lookup key;do not silently substitute a monster class ID.
internal sealed record GenericPetSurveyUnit(int Kind,int Id8,bool HasMonsterData,bool HasAi,bool GenericPetAi,GenericPetPathTarget Path);
internal sealed class GenericPetOwnerSurveyState(int pet,int swarmRaw)
{
    internal readonly int Pet=pet,SwarmRaw=swarmRaw;
    internal int Visits;
    internal readonly List<GenericPetSearchSwarm> Targets=[];
}
internal sealed record GenericPetOwnerSurveyServices(Func<int,GenericPetSurveyUnit> Unit,
    Func<byte,int,bool> MonsterRow,Func<int,int> CurrentTarget,
    Func<int,int,byte,int> ValidTarget,Action<int> RefreshPath);

// Complete current initializer callback4b08b0 over explicit unit/target services.
// Visits is cfg+0x70,not an inferred pet cap. The temporary vector feeds the
// existing search swarm records;its allocator/capacity is a managed container.
internal static class GenericPetOwnerSurvey
{
    internal static void Visit(byte selector,int unit,GenericPetOwnerSurveyState state,GenericPetOwnerSurveyServices services)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(services);
        state.Visits=unchecked(state.Visits+1);
        if(state.Pet==unit||state.SwarmRaw<=0||unit==0){return;}
        var info=services.Unit(unit)??throw new NotSupportedException("Missing visited source unit");
        if(info.Kind!=1||!info.HasMonsterData||!info.HasAi||!info.GenericPetAi||!services.MonsterRow(selector,info.Id8)){return;}
        int target=services.CurrentTarget(unit);
        if(target==0||services.ValidTarget(unit,target,1)==0)
        {
            services.RefreshPath(unit);
            info=services.Unit(unit)??throw new NotSupportedException("Visited source unit disappeared during path refresh");
            if(info.Kind==5){throw new NotSupportedException("Current owner survey reaches a tile-path assertion");}
            target=info.Path?.Target??0;
            if(target==unit){target=0;}
            if(target==0||services.ValidTarget(unit,target,1)==0){return;}
        }
        for(int i=0;i<state.Targets.Count;i++)
        {
            var entry=state.Targets[i];if(entry.Target!=target){continue;}
            state.Targets[i]=entry with{Count8=unchecked(entry.Count8+1)};return;
        }
        state.Targets.Add(new(target,1));
    }
    // Use the source iterator's order/membership supplied by its world binding.
    // This is the player-pet iterator35e6d0,not room or monster-owner traversal.
    internal static void Enumerate(byte selector,GenericPetOwnerSurveyState state,
        Action<Action<int>> ownerUnits,GenericPetOwnerSurveyServices services)
    {
        ArgumentNullException.ThrowIfNull(ownerUnits);
        ownerUnits(unit=>Visit(selector,unit,state,services));
    }
    internal static GenericPetFollowExecution Bind(GenericPetFollowExecution follow,GenericPetOwnerSurveyState state)
    {
        ArgumentNullException.ThrowIfNull(follow);ArgumentNullException.ThrowIfNull(state);
        return follow with{Word70=state.Visits};
    }
    internal static GenericPetSearchWrapperContext Bind(GenericPetSearchWrapperContext search,GenericPetOwnerSurveyState state)
    {
        ArgumentNullException.ThrowIfNull(search);ArgumentNullException.ThrowIfNull(state);
        if(search.Pet!=state.Pet||search.Parameters?.SwarmRaw!=state.SwarmRaw)
        {throw new NotSupportedException("Owner survey belongs to a different pet/configuration");}
        return search with{Swarm=state.Targets.ToArray()};
    }
}
