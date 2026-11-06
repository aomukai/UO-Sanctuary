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
// ordinary path supports flat return78, fear112/function08, blind113/function09, lightning128/function10, freeze134/function14,
// Open Wounds135/function15, Crushing136/function16 and
// the approved inherent Blood skill-on-hit198/function20 slot.
// Other active registrations fail preflight before result/damage RNG or HP.
internal sealed class NpcCombatEvents
{
    private static readonly Lazy<Dictionary<int,NpcItemEventStat>> Rules=ItemEventDefinitions.Rows;
    private readonly NpcItemEventStat[] Active;
    private NpcCombatEvents(){Active=[];}
    internal static NpcCombatEvents Empty{get;}=new();
    internal static NpcCombatEvents ForSource(BaseCreature actor)
    {
        var legacy=LegacyForSource(actor);
        if(actor is SkeletonGuardian guardian){NpcItemProcs.Refresh(guardian,legacy);}
        return legacy;
    }
    internal static NpcCombatEvents LegacyForSource(BaseCreature actor)
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
        if(stats==null||stats.Distinct().Count()!=stats.Length||stats.Any(s=>s is not(78 or 112 or 113 or 128 or 134 or 135 or 136)))
        {throw new NotSupportedException("Persisted ordinary item callbacks need their admitted subscription set");}
        return FromStats(stats.Select(s=>new EquipmentStatValue(EquipmentStatKey.Pack(s),1)).ToArray());
    }
    private NpcCombatEvents(EquipmentStatValue[] stats,bool inherentWounds,bool blood,bool inherentCrushing)
    {
        var active=new List<NpcItemEventStat>();
        foreach(var value in stats.Where(v=>v.Value!=0))
        {
            // The reference hit-success routine reads ignore/fractional target
            // defense only in its player branch; PreventMonsterHeal likewise
            // admits players before reading stat117. Retain these NPC item
            // stats without inventing a callback or forwarding through owner.
            if(value.Stat is 115 or 116 or 117)
            {
                string expected=value.Stat switch{115=>"item_ignoretargetac",116=>"item_fractionaltargetac",_=>"item_preventheal"};
                if(EquipmentStatValues.Definition(value.Stat).Name!=expected||Rules.Value.ContainsKey(value.Stat))
                {throw new NotSupportedException("NPC item condition needs its reviewed player-only mapping: "+expected);}
                continue;
            }
            // Flat armor loss is an allocation/setup phase, not an event.
            if(value.Stat==120)
            {
                if((value.Packed&65535)!=0||EquipmentStatValues.Definition(120).Name!="item_damagetargetac"||Rules.Value.ContainsKey(120))
                {throw new NotSupportedException("NPC target-armor setup needs its reviewed unlayered mapping");}
                WeaponStartEffects.RequireArmorDelta(value.Value);continue;
            }
            // Raw stun length needs its packet-fill consumer; no current item
            // property writes it, but supplied values must not be discarded.
            if(value.Stat==66)
            {throw new NotSupportedException("NPC conditional hit stat needs its consumer: "+EquipmentStatValues.Definition(value.Stat).Name);}
            if(!Rules.Value.TryGetValue(value.Stat,out var rule)){continue;}
            bool flat=rule.Stat==78&&rule.Events.Length==1&&rule.Events[0] is{Event:"attackedinmelee",Function:6};
            bool wounds=rule.Stat==135&&rule.Events.Length==2&&rule.Events[0] is{Event:"domeleedamage",Function:15}&&rule.Events[1] is{Event:"domissiledamage",Function:15};
            bool freeze=rule.Stat==134&&rule.Events.Length==2&&rule.Events[0] is{Event:"domeleedamage",Function:14}&&rule.Events[1] is{Event:"domissiledamage",Function:14};
            bool blind=rule.Stat==113&&rule.Events.Length==2&&rule.Events[0] is{Event:"domeleedamage",Function:9}&&rule.Events[1] is{Event:"domissiledamage",Function:9};
            bool fear=rule.Stat==112&&rule.Events.Length==2&&rule.Events[0] is{Event:"domeleedamage",Function:8}&&rule.Events[1] is{Event:"domissiledamage",Function:8};
            bool lightning=rule.Stat==128&&rule.Events.Length==1&&rule.Events[0] is{Event:"damagedinmelee",Function:10};
            if(!flat&&!fear&&!blind&&!lightning&&!freeze&&!wounds&&!CrushingRule(rule))
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
        // Incoming graph phases are produced once by PreparedMonsterDamage,
        // including callers whose legacy event service has no proc context.
        var procs=phase is not("damagedinmelee"or"damagedbymissile")&&source is SkeletonGuardian eventActor?NpcItemProcs.Get(eventActor):null;
        if(procs!=null)
        {
            if(at==null){throw new InvalidOperationException("NPC item event lacks its actual source time");}
            procs.Refresh(LegacyForSource(source));
            procs.Dispatch(phase,packet,(SkeletonGuardian)source,target,at.Value,damageRoll);
        }
        foreach(var rule in Active)foreach(var registration in rule.Events)
        {
            if(registration.Event!=phase){continue;}
            // The live graph aggregates these innate/material stats with item
            // layers and dispatches them in its actual registration order.
            if(procs!=null&&rule.Stat is 112 or 113 or 134 or 135 or 136 or 198){continue;}
            if(rule.Stat==78&&registration is{Event:"attackedinmelee",Function:6}){continue;} // Owned by PreparedFlatAttempt.
            if(rule.Stat==128&&registration.Function==10)
            {
                // Incoming graph dispatch is owned once by the ordered damage
                // receiver, after this legacy event service has returned.
                if(source is SkeletonGuardian g&&NpcItemProcs.Get(g)!=null){continue;}
                if(source==null||target==null||at==null){throw new InvalidOperationException("NPC lightning return lacks its actual source/target/time");}
                NpcLightningReturn.Apply(source,target,at.Value);continue;
            }
            if(rule.Stat==113&&registration.Function==9)
            {
                if(source==null||target==null||at==null){throw new InvalidOperationException("NPC blind lacks actual source/target/time");}
                NpcItemBlind.Apply(source,target,phase=="domissiledamage",at.Value);continue;
            }
            if(rule.Stat==112&&registration.Function==8)
            {
                if(source==null||target==null||at==null){throw new InvalidOperationException("NPC fear lacks actual source/target/time");}
                NpcItemFear.Apply(source,target,at.Value);continue;
            }
            if(rule.Stat==134&&registration.Function==14)
            {
                if(source==null||target==null||at==null){throw new InvalidOperationException("NPC freeze callback lacks its actual source/target/time");}
                NpcItemFreeze.Apply(source,target,phase=="domissiledamage",at.Value);continue;
            }
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
