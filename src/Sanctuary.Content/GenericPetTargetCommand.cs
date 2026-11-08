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
