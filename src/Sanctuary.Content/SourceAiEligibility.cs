// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record SourceAiMonster(int BaseId,bool Walk,bool Boss,bool SwitchAi);
internal sealed record SourceAiUnit(int Kind,int ClassId,bool Attackable,bool Dead,bool Uninterruptible,bool Unique,bool SuperUnique,bool Attached,bool HasMonsterData);
// Dynamic source state is mandatory input. Native body/hue, elite presentation
// and corpse age do not establish these flags or source state lifetime.
public readonly record struct ReviveAiStatus(bool Attackable,bool Uninterruptible,bool Unique,bool SuperUnique,bool Attached,bool HasMonsterData);
internal static class SourceAiEligibility
{
    internal const uint Revived=7,Invalid=19;
    internal const int Imp=492;
    internal static bool CanSwitch(int cls,Func<int,SourceAiMonster> monster)
        =>monster(cls) is{Walk:true,Boss:false,SwitchAi:true};
    internal static bool Think(SourceAiUnit unit,uint state,bool checkSuperUnique,Func<int,SourceAiMonster> monster)
    {
        if(state==0||state is not(10 or 11 or 12)){return true;}
        if(checkSuperUnique&&unit is{Kind:1,HasMonsterData:true,SuperUnique:true}){return false;}
        return CanSwitch(unit?.ClassId??-1,monster);
    }
    internal static bool Utility(SourceAiUnit unit,uint state,Func<int,SourceAiMonster> monster)
    {
        if(unit==null||unit.Kind!=1||state>Invalid||unit.Uninterruptible||!CanSwitch(unit.ClassId,monster)){return false;}
        if(!unit.Attackable&&!unit.Dead||unit.HasMonsterData&&(unit.Unique||unit.SuperUnique)){return false;}
        return state==Invalid||Think(unit,state,true,monster);
    }
    // Complete older skill wrapper plus AIUTIL/AITHINK/UNITS predicates.
    // Lookup availability and each original/base ID remain distinct.
    internal static bool Allowed(SourceAiUnit unit,uint state,int monsterCount,Func<int,SourceAiMonster> monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        if(unit==null||unit.Kind!=1||unit.ClassId<0){return Utility(unit,state,monster);}
        int cls=unit.ClassId;
        if(cls<monsterCount&&monster(cls) is{} row){cls=row.BaseId;}
        if(cls==Imp&&unit.Attached){return false;}
        return Utility(unit,state,monster);
    }
    internal static SourceAiMonster CurrentMonster(int cls)
    {
        var rule=PaladinConversion.SourceRule(GenericPetSearchTables.SourceName(cls));
        if(rule==null){return null;}
        int baseId=GenericPetSearchTables.SourceId(rule.Base)??throw new NotSupportedException("AI rule has no mapped base species");
        return new(baseId,rule.Walk,rule.Boss,rule.SwitchAi);
    }
    internal static bool Current(SourceAiUnit unit,uint state)
        =>Allowed(unit,state,GenericPetSearchTables.MonsterCount,CurrentMonster);
    internal static bool Corpse(SourceCorpseUnit corpse,ReviveAiStatus status)
    {
        ArgumentNullException.ThrowIfNull(corpse);
        return Current(new(1,corpse.ClassId,status.Attackable,true,status.Uninterruptible,status.Unique,status.SuperUnique,status.Attached,status.HasMonsterData),Revived);
    }
}
