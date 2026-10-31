// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal sealed record NpcItemEvent(string Event,int Function);
internal sealed record NpcItemEventStat(int Stat,string Name,NpcItemEvent[] Events);

// A validated registry, not a list of silently skipped callbacks. For this
// ordinary path only event6/stat78 has a real consumer (shared flat return).
// Other active registrations fail preflight before result/damage RNG or HP.
internal sealed class NpcCombatEvents
{
    private static readonly Lazy<Dictionary<int,NpcItemEventStat>> Rules=new(()=>
    {
        var rows=BalanceCatalog.Read<NpcItemEventStat>("npc-item-combat-events");
        if(rows.Length!=21||rows.Any(r=>r.Events.Length is <1 or >2||r.Events.Any(e=>e.Function<=0||string.IsNullOrEmpty(e.Event)))
            ||rows.Select(r=>r.Stat).Distinct().Count()!=21)
        {throw new InvalidDataException("Invalid pinned NPC event registration map");}
        return rows.ToDictionary(r=>r.Stat);
    });
    private readonly NpcItemEventStat[] Active;
    private NpcCombatEvents(){Active=[];}
    internal static NpcCombatEvents Empty{get;}=new();
    internal NpcCombatEvents(ItemStatContext item)
    {
        item.RequireOrdinaryNpcDrainInputs();
        var stats=item.ActiveNumericProperties();var active=new List<NpcItemEventStat>();
        foreach(var value in stats.Where(v=>v.Value!=0))
        {
            // These are conditional hit paths outside ItemStatCost's registry.
            if(value.Stat is 66 or 115 or 116 or 117 or 120)
            {throw new NotSupportedException("NPC conditional hit stat needs its consumer: "+EquipmentStatValues.Definition(value.Stat).Name);}
            if(!Rules.Value.TryGetValue(value.Stat,out var rule)){continue;}
            if(rule.Stat!=78||rule.Events.Length!=1||rule.Events[0] is not{Event:"attackedinmelee",Function:6})
            {throw new NotSupportedException("NPC combat event needs its consumer: "+rule.Name+"/"+string.Join(',',rule.Events.Select(e=>e.Event+":"+e.Function)));}
            active.Add(rule);
        }
        Active=active.ToArray();
    }
    internal PreparedMonsterDamagePacket Dispatch(string phase,PreparedMonsterDamagePacket packet)
    {
        foreach(var rule in Active)foreach(var registration in rule.Events)
        {
            if(registration.Event!=phase){continue;}
            if(rule.Stat==78&&registration is{Event:"attackedinmelee",Function:6}){continue;} // Owned by PreparedFlatAttempt.
            throw new NotSupportedException("Unconsumed NPC registered event: "+rule.Name+"/"+phase);
        }
        return packet;
    }
}
