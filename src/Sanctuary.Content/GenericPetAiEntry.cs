// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal sealed record GenericPetAiGame(uint Value108,byte Byte104,byte Selector106,byte Byte101)
{
    internal ulong Snapshot(byte unwrittenTail)=>(ulong)Value108|((ulong)Byte104<<32)|((ulong)Selector106<<40)|((ulong)Byte101<<48)|((ulong)unwrittenTail<<56);
}
internal sealed class GenericPetAiQueued(int token,int command)
{internal readonly int Token=token;internal int Command=command;}
internal sealed class GenericPetAiControl(int state,int function,int field14,bool executing,IEnumerable<GenericPetAiQueued> queue)
{
    internal int State=state,Function=function,Field14=field14;
    internal bool Executing=executing;
    internal readonly List<GenericPetAiQueued> Queue=new(queue??throw new ArgumentNullException(nameof(queue)));
}
internal sealed record GenericPetAiUnit(int Kind,int ClassId,bool HasData,GenericPetAiControl Control,byte Flags30,ulong Snapshot8c);
internal sealed record GenericPetAiOverride(int Flags,int Function);
internal sealed class GenericPetAiTick
{
    internal GenericPetAiControl Control;
    internal ulong Raw08=0,Raw18=0,Snapshot;
    internal int Target=0,Distance=0,Combat=0,RowA,RowB;
    internal GenericPetAiOverride Special;
}
internal sealed record GenericPetAiEntryServices(Func<GenericPetAiGame> Game,Func<int,GenericPetAiUnit> Unit,
    Func<byte,int,int> RowA,Func<byte,int,int> RowB,Func<int,GenericPetAiOverride> Special,
    Func<GenericPetAiTick,bool> Preprocess,Func<int,int,int> AiType,Func<int,GenericPetAiTick,int> Target,
    Func<GenericPetAiTick,bool> PostTarget,Func<GenericPetAiQueued,int,bool> Predicate,
    Action<int,int,GenericPetAiControl> Queued,Action<int> Release,Action<int,GenericPetAiTick,int> CallSpecial,
    Action<int,GenericPetAiTick> CallDefault);

// Complete43b0c0 orchestration. Services keep their actual caller context;
// no target selection,preprocessing,special AI or native defaults are invented.
// Managed FIFO lifetime supports stable head predicates and returning handlers;
// raw functor allocator/move/destructor ABI and exceptions are separate scope.
internal static class GenericPetAiEntry
{
    private static GenericPetAiControl Control(GenericPetAiTick tick)=>tick.Control
        ??throw new NotSupportedException("Current AI entry dereferences missing control");
    internal static GenericPetAiTick Run(int actor,byte stackTail,GenericPetAiEntryServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var unit=actor==0?null:services.Unit(actor);var game=services.Game();
        var tick=new GenericPetAiTick
        {
            Control=unit is{Kind:1,HasData:true}?unit.Control:null,
            Snapshot=GenericPetPackedContext.Read(game,unit,stackTail)
        };
        tick.RowA=services.RowA(game.Selector106,unit?.ClassId??-1);
        tick.RowB=services.RowB(services.Game().Selector106,actor==0?-1:services.Unit(actor).ClassId);
        if(tick.RowA==0||tick.RowB==0){return tick;}
        var initial=Control(tick);
        var special=(uint)initial.Field14<=1?services.Special(initial.Field14):null;tick.Special=special;
        if(services.Preprocess(tick)){return tick;}
        int type=services.AiType(actor,Control(tick).State);
        switch(type)
        {
            case 0:break;
            case 1:case 2:case 3:case 5:case 6:
                tick.Target=services.Target(type,tick);
                if(tick.Target==0&&type is 1 or 3 or 5){Fallback(special,tick,services);return tick;}
                break;
            default:Fallback(special,tick,services);return tick;
        }
        if(services.PostTarget(tick)){return tick;}
        // The source caches this control during FIFO processing,even if a
        // service subsequently changes the control referenced by the tick.
        var current=Control(tick);
        if(current.Queue.Count>0)
        {
            current.Executing=true;
            while(current.Queue.Count>0)
            {
                var first=current.Queue[0];if(!services.Predicate(first,actor)){break;}
                services.Queued(first.Command,actor,Control(tick));
                if(current.Queue.Count==0||!ReferenceEquals(current.Queue[0],first))
                {throw new NotSupportedException("Queued handler invalidated its active head");}
                services.Release(first.Token);current.Queue.RemoveAt(0);
            }
        }
        current.Executing=false;
        if(tick.Special is{Function:not 0} selected)
        {services.CallSpecial(selected.Function,tick,Control(tick).Function);}
        else
        {
            int function=Control(tick).Function;
            if(function==0){throw new NotSupportedException("Current AI entry would call a null default function");}
            services.CallDefault(function,tick);
        }
        return tick;
    }
    private static void Fallback(GenericPetAiOverride special,GenericPetAiTick tick,GenericPetAiEntryServices services)
    {
        // Early fallback uses the originally captured override,not a later
        // tick replacement,and accepts raw flags0/2/6 but excludes4.
        if(special is{Function:not 0}&&(special.Flags&~6)==0&&special.Flags!=4)
        {services.CallSpecial(special.Function,tick,0);}
    }
}
