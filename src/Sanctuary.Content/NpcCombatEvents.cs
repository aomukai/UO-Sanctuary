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
// ordinary path supports flat return78, Open Wounds135/function15, Crushing136/function16 and
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
    internal static NpcCombatEvents ForSource(BaseCreature actor)
    {
        if(ReviveEquipmentEvents.Handles(actor)){return ReviveEquipmentEvents.Registry(actor);}
        if(actor is not SkeletonGuardian){throw new NotSupportedException("No admitted NPC source combat-event adapter");}
        var item=NpcEquipmentCombat.Context(actor);bool wounds=NpcOpenWounds.Potential(actor),crushing=NpcCrushing.Potential(actor);
        return item!=null||wounds||crushing?new(item,wounds,NpcOpenWounds.Blood(actor),crushing):Empty;
    }
    internal NpcCombatEvents(ItemStatContext item):this(item,false,false,false){}
    private static EquipmentStatValue[] Stats(ItemStatContext? item)
    {item?.RequireOrdinaryNpcDrainInputs();return item?.ActiveNumericProperties()??[];}
    private NpcCombatEvents(ItemStatContext? item,bool inherentWounds,bool blood,bool inherentCrushing):this(Stats(item),inherentWounds,blood,inherentCrushing){}
    internal static NpcCombatEvents FromStats(EquipmentStatValue[] stats)=>new(stats,false,false,false);
    internal int[] Subscriptions()=>Active.Select(r=>r.Stat).ToArray();
    internal static NpcCombatEvents RestoreSubscriptions(int[] stats)
    {
        if(stats==null||stats.Distinct().Count()!=stats.Length||stats.Any(s=>s is not(78 or 135 or 136)))
        {throw new NotSupportedException("Persisted ordinary item callbacks need their admitted subscription set");}
        return FromStats(stats.Select(s=>new EquipmentStatValue(EquipmentStatKey.Pack(s),1)).ToArray());
    }
    private NpcCombatEvents(EquipmentStatValue[] stats,bool inherentWounds,bool blood,bool inherentCrushing)
    {
        var active=new List<NpcItemEventStat>();
        foreach(var value in stats.Where(v=>v.Value!=0))
        {
            // The reference PreventMonsterHeal helper admits player attackers
            // before it even reads stat117. NPC-held items retain the stat, but
            // it creates no NPC event or healing-prevention state.
            if(value.Stat==117)
            {
                if(EquipmentStatValues.Definition(117).Name!="item_preventheal"||Rules.Value.ContainsKey(117))
                {throw new NotSupportedException("NPC Prevent Monster Heal needs its reviewed player-only mapping");}
                continue;
            }
            // These are conditional hit paths outside ItemStatCost's registry.
            if(value.Stat is 66 or 115 or 116 or 120)
            {throw new NotSupportedException("NPC conditional hit stat needs its consumer: "+EquipmentStatValues.Definition(value.Stat).Name);}
            if(!Rules.Value.TryGetValue(value.Stat,out var rule)){continue;}
            bool flat=rule.Stat==78&&rule.Events.Length==1&&rule.Events[0] is{Event:"attackedinmelee",Function:6};
            bool wounds=rule.Stat==135&&rule.Events.Length==2&&rule.Events[0] is{Event:"domeleedamage",Function:15}&&rule.Events[1] is{Event:"domissiledamage",Function:15};
            if(!flat&&!wounds&&!CrushingRule(rule))
            {throw new NotSupportedException("NPC combat event needs its consumer: "+rule.Name+"/"+string.Join(',',rule.Events.Select(e=>e.Event+":"+e.Function)));}
            active.Add(rule);
        }
        // An eligible actor keeps the potential subscription so a live hard-point
        // unlock/respec during windup is observed at the actual damage event.
        if(inherentWounds&&active.All(r=>r.Stat!=135)){active.Add(Rules.Value[135]);}
        if(inherentCrushing&&active.All(r=>r.Stat!=136))
        {var crushing=Rules.Value[136];if(!CrushingRule(crushing)){throw new InvalidDataException("Grizzly requires pinned Crushing Blow registration");}active.Add(crushing);}
        if(blood)
        {
            var tap=Rules.Value[198];
            if(tap.Events.Length!=2||tap.Events[0] is not{Event:"domeleedamage",Function:20}||tap.Events[1] is not{Event:"domissiledamage",Function:20})
            {throw new InvalidDataException("Blood proc requires pinned skill-on-hit registration");}
            active.Add(tap); // Fixed approved slot; original item writer11 remains unsupported.
        }
        Active=active.DistinctBy(r=>r.Stat).ToArray();
    }
    private static bool CrushingRule(NpcItemEventStat rule)=>rule.Stat==136&&rule.Events.Length==2
        &&rule.Events[0] is{Event:"domeleedamage",Function:16}&&rule.Events[1] is{Event:"domissiledamage",Function:16};
    internal void RequireCrushingOnly(string phase)
    {
        if(Active.Any(rule=>rule.Events.Any(e=>e.Event==phase&&(rule.Stat!=136||e.Function!=16))))
        {throw new NotSupportedException("Grizzly contact requires its additional registered damage-event consumer");}
    }
    internal void DispatchCrushing(string phase,SkeletonGuardian source,BaseCreature target,Func<int,int> limited)
    {
        RequireCrushingOnly(phase);
        // Function16 reads live victim life, not the damage-packet fields.
        Dispatch(phase,null,source,target,null,limited);
    }
    internal void RequireWoundsOnly(string phase)
    {
        if(Active.Any(rule=>rule.Events.Any(e=>e.Event==phase&&(rule.Stat!=135||e.Function!=15))))
        {throw new NotSupportedException("Typed-only NPC contact requires its additional registered damage-event consumer");}
    }
    internal void DispatchWounds(string phase,SkeletonGuardian source,BaseCreature target,DateTime at,Func<int,int> limited)
    {
        RequireWoundsOnly(phase);
        // Function15 does not read the damage packet. No physical lane or
        // synthetic ordinary-monster packet is invented for a cold-only hit.
        Dispatch(phase,null,source,target,at,limited);
    }
    internal PreparedMonsterDamagePacket Dispatch(string phase,PreparedMonsterDamagePacket packet,BaseCreature source=null,BaseCreature target=null,DateTime? at=null,Func<int,int> damageRoll=null)
    {
        foreach(var rule in Active)foreach(var registration in rule.Events)
        {
            if(registration.Event!=phase){continue;}
            if(rule.Stat==78&&registration is{Event:"attackedinmelee",Function:6}){continue;} // Owned by PreparedFlatAttempt.
            if(rule.Stat==135&&registration.Function==15)
            {
                if(source==null||target==null||at==null){throw new InvalidOperationException("NPC wound callback lacks its real damage-event context");}
                NpcOpenWounds.Apply(source,target,at.Value,phase=="domissiledamage",damageRoll);continue;
            }
            if(rule.Stat==136&&registration.Function==16)
            {
                if(source==null||target==null){throw new InvalidOperationException("NPC Crushing Blow lacks its real damage-event context");}
                NpcCrushing.Apply(source,target,phase=="domissiledamage",damageRoll);continue;
            }
            if(rule.Stat==198&&registration.Function==20)
            {
                if(source==null||target==null||at==null){throw new InvalidOperationException("NPC skill proc lacks its real damage-event context");}
                if(source is not SkeletonGuardian guardian){throw new NotSupportedException("Inherent Blood proc requires its original summon body");}
                NpcHitSkills.BloodLifeTap(guardian,target,packet,at.Value);continue;
            }
            throw new NotSupportedException("Unconsumed NPC registered event: "+rule.Name+"/"+phase);
        }
        return packet;
    }
}
