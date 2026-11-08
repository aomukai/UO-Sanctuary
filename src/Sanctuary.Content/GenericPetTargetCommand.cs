// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed class GenericPetCommandData(int target48,int type4c)
{internal int Target48=target48,Type4c=type4c;}
internal sealed class GenericPetCommandUnit(int kind,int classId,byte selector,GenericPetCommandData data)
{
    internal int Kind=kind;
    internal readonly int ClassId=classId;
    internal readonly byte Selector=selector;
    internal GenericPetCommandData Data=data;
}

// Current398a80. Do not infer a monster's raw byte3e capability from its UO
// body or class name;the original source metadata provider must supply it.
internal static class GenericPetTargetCommand
{
    internal static void Clear(GenericPetCommandUnit unit)
    {if(unit is{Kind:1,Data:not null}){unit.Data.Target48=0;unit.Data.Type4c=0;}}
    // Current4b07b0:clear first,including when selection is absent/invalid.
    // A null selection succeeds;the existing setter supplies the exact
    // row/capability/re-read guards for player/monster selections.
    internal static int Finish(GenericPetCommandUnit unit,(int Kind,int Id)? target,Func<byte,int,byte?> monsterByte3e)
    {
        Clear(unit);
        if(target==null){return 1;}
        return target.Value.Kind is 0 or 1?Set(unit,target.Value.Kind+1,target.Value.Id,monsterByte3e):0;
    }
    // Complete current4b06a0. Stored command types1/2 select player/monster
    // registries0/1. Eligibility and path fallback belong to the caller.
    internal static int Get(int game,GenericPetCommandUnit unit,Func<int,int,int,int> resolve)
    {
        if(unit?.Kind!=1||unit.Data==null){return 0;}
        int type=unit.Data.Type4c;
        if(type is not(1 or 2)){return 0;}
        if(game==0){throw new NotSupportedException("Current command lookup requires a game for player/monster targets");}
        ArgumentNullException.ThrowIfNull(resolve);
        return resolve(game,type-1,unit.Data.Target48);
    }
    internal static GenericPetOwnerSurveyServices Bind(GenericPetOwnerSurveyServices services,int game,
        Func<int,GenericPetCommandUnit> unit,Func<int,int,int,int> resolve)
    {
        ArgumentNullException.ThrowIfNull(services);ArgumentNullException.ThrowIfNull(unit);ArgumentNullException.ThrowIfNull(resolve);
        return services with{CurrentTarget=id=>Get(game,unit(id),resolve)};
    }
    internal static int Set(GenericPetCommandUnit unit,int type,int target,Func<byte,int,byte?> monsterByte3e)
    {
        if(unit==null||unit.Kind!=1||type>=5){return 0;}
        var flags=monsterByte3e(unit.Selector,unit.ClassId);
        if(flags==null||(flags.Value&1)==0||unit.Kind!=1||unit.Data==null){return 0;}
        unit.Data.Type4c=type;unit.Data.Target48=target;return 1;
    }
    internal static DeathMarkPetServices Bind(DeathMarkPetServices services,GenericPetCommandUnit unit,Func<byte,int,byte?> monsterByte3e)
        =>services with{TargetCommand=(type,id)=>{_=Set(unit,type,id,monsterByte3e);}};
}
