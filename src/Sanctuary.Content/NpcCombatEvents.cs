// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record NpcItemEvent(string Event,int Function);
internal sealed record NpcItemEventStat(int Stat,string Name,NpcItemEvent[] Events);

// A validated registry, not a list of silently skipped callbacks. For this
// ordinary path supports flat return78, Open Wounds135/function15 and
// the approved inherent Blood skill-on-hit198/function20 slot.
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
    internal static NpcCombatEvents ForSource(SkeletonGuardian actor)=>NpcEquipmentCombat.Context(actor) is{} item
        ?new(item,NpcOpenWounds.Potential(actor)):NpcOpenWounds.Potential(actor)?new(null,true):Empty;
    internal NpcCombatEvents(ItemStatContext item):this(item,false){}
    private NpcCombatEvents(ItemStatContext? item,bool blood)
    {
        item?.RequireOrdinaryNpcDrainInputs();
        var stats=item?.ActiveNumericProperties()??[];var active=new List<NpcItemEventStat>();
        foreach(var value in stats.Where(v=>v.Value!=0))
        {
            // These are conditional hit paths outside ItemStatCost's registry.
            if(value.Stat is 66 or 115 or 116 or 117 or 120)
            {throw new NotSupportedException("NPC conditional hit stat needs its consumer: "+EquipmentStatValues.Definition(value.Stat).Name);}
            if(!Rules.Value.TryGetValue(value.Stat,out var rule)){continue;}
            bool flat=rule.Stat==78&&rule.Events.Length==1&&rule.Events[0] is{Event:"attackedinmelee",Function:6};
            bool wounds=rule.Stat==135&&rule.Events.Length==2&&rule.Events[0] is{Event:"domeleedamage",Function:15}&&rule.Events[1] is{Event:"domissiledamage",Function:15};
            if(!flat&&!wounds)
            {throw new NotSupportedException("NPC combat event needs its consumer: "+rule.Name+"/"+string.Join(',',rule.Events.Select(e=>e.Event+":"+e.Function)));}
            active.Add(rule);
        }
        // A Blood actor keeps the potential subscription so a live hard-point
        // unlock/respec during windup is observed at the actual damage event.
        if(blood&&active.All(r=>r.Stat!=135)){active.Add(Rules.Value[135]);}
        if(blood)
        {
            var tap=Rules.Value[198];
            if(tap.Events.Length!=2||tap.Events[0] is not{Event:"domeleedamage",Function:20}||tap.Events[1] is not{Event:"domissiledamage",Function:20})
            {throw new InvalidDataException("Blood proc requires pinned skill-on-hit registration");}
            active.Add(tap); // Fixed approved slot; original item writer11 remains unsupported.
        }
        Active=active.ToArray();
    }
    internal PreparedMonsterDamagePacket Dispatch(string phase,PreparedMonsterDamagePacket packet,SkeletonGuardian source=null,BaseCreature target=null,DateTime? at=null)
    {
        foreach(var rule in Active)foreach(var registration in rule.Events)
        {
            if(registration.Event!=phase){continue;}
            if(rule.Stat==78&&registration is{Event:"attackedinmelee",Function:6}){continue;} // Owned by PreparedFlatAttempt.
            if(rule.Stat==135&&registration.Function==15)
            {
                if(source==null||target==null||at==null){throw new InvalidOperationException("NPC wound callback lacks its real damage-event context");}
                NpcOpenWounds.Apply(source,target,at.Value,phase=="domissiledamage");continue;
            }
            if(rule.Stat==198&&registration.Function==20)
            {
                if(source==null||target==null||at==null){throw new InvalidOperationException("NPC skill proc lacks its real damage-event context");}
                NpcHitSkills.BloodLifeTap(source,target,packet,at.Value);continue;
            }
            throw new NotSupportedException("Unconsumed NPC registered event: "+rule.Name+"/"+phase);
        }
        return packet;
    }
}
