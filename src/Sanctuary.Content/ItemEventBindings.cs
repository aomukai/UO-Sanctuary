// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sanctuary;

internal readonly record struct ItemEventBinding(short Event,ushort Function);

internal static class ItemEventDefinitions
{
    private static readonly string[] Phases=["hitbymissile","damagedinmelee","damagedbymissile","attackedinmelee","doactive","domeleedamage","domissiledamage","domeleeattack","domissileattack","kill","killed","absorbdamage","levelup","death"];
    internal static byte Code(string phase)
    {
        int n=Array.IndexOf(Phases,phase);
        return n>=0?(byte)n:throw new NotSupportedException("Unknown source unit-event phase: "+phase);
    }
    internal static readonly Lazy<Dictionary<int,NpcItemEventStat>> Rows=new(()=>
    {
        var rows=BalanceCatalog.Read<NpcItemEventStat>("npc-item-combat-events");
        if(rows.Length!=21||rows.Any(r=>r.Events.Length is <1 or >2||r.Events.Any(e=>e.Function<=0||string.IsNullOrEmpty(e.Event)))
            ||rows.Select(r=>r.Stat).Distinct().Count()!=21)
        {throw new InvalidDataException("Invalid pinned NPC event registration map");}
        foreach(var row in rows){foreach(var entry in row.Events){_=Code(entry.Event);}}
        return rows.ToDictionary(r=>r.Stat);
    });
    internal static ItemEventBinding[] Bindings(int stat)=>Rows.Value.TryGetValue(stat,out var row)
        ?row.Events.Select(e=>new ItemEventBinding(Code(e.Event),checked((ushort)e.Function))).ToArray():null;
}

// Event-binding branch of the complete server stat-change callback. Numeric
// vitals, skill lists, auras, charged skills and states retain separate services;
// callers must not route those non-event effects here and assume they ran.
internal sealed class ItemEventBindings(SourceUnitEvents events,Func<int,ItemEventBinding[]> lookup,bool hasGame=true)
{
    internal void Changed(uint packed,int value)
    {
        if(!hasGame||!events.HasOwner){return;}
        var row=lookup(unchecked((int)packed)>>16);
        if(row==null||row.Length==0||row[0].Event<=0){return;}
        int key=unchecked((int)packed);
        if(value!=0)
        {
            if(events.Get(2,key,key)!=null){return;}
            foreach(var e in row)
            {
                if(e.Event>0){events.RegisterFunction(unchecked((byte)e.Event),key,0,e.Function,2,key);}
            }
        }
        else{events.Unregister(2,key);}
    }
}
