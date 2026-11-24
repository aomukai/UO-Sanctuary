// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary;

// Original-body binding for the explicitly approved native controller. Legacy
// prepared bodies have no Controller and are never adopted by this timer.
public static class WarlockGoatmanWorld
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WarlockGoatmanWorld));
    private static readonly HashSet<SkeletonGuardian> Bodies=new();
    private static readonly Dictionary<SkeletonGuardian,GoatmanNativeHostCandidate> Hosts=new();
    internal static void Track(SkeletonGuardian body){if(body!=null&&DemonNativeState.Controller(body)!=null){Bodies.Add(body);}}
    internal static void Forget(SkeletonGuardian body){Bodies.Remove(body);if(Hosts.Remove(body,out var host)){host.Dispose();}}
    internal static WarlockGoatmanBody[] For(PlayerMobile owner)=>(owner.AllFollowers??[]).OfType<WarlockGoatmanBody>()
        .Where(b=>b is{Alive:true,Deleted:false}&&b.ControlMaster==owner&&b.Prepared?.LearnedCast==true).ToArray();
    internal static WarlockTaintedBody[] ForTainted(PlayerMobile owner)=>(owner.AllFollowers??[]).OfType<WarlockTaintedBody>()
        .Where(b=>b is{Alive:true,Deleted:false}&&b.ControlMaster==owner&&b.Prepared?.LearnedCast==true).ToArray();
    internal static WarlockDefilerBody[] ForDefiler(PlayerMobile owner)=>(owner.AllFollowers??[]).OfType<WarlockDefilerBody>()
        .Where(b=>b is{Alive:true,Deleted:false}&&b.ControlMaster==owner&&b.Prepared?.LearnedCast==true).ToArray();
    // These three implemented species share source pool1. Bind Demon has
    // a distinct pettype,but the same pool1;its unfinished actor/Consume slot
    // integration must eventually join this replacement pool.
    internal static SkeletonGuardian[] Demons(PlayerMobile owner)=>(owner.AllFollowers??[]).OfType<SkeletonGuardian>()
        .Where(b=>b is{Alive:true,Deleted:false}&&b.ControlMaster==owner&&WarlockDemonPlacement.Prepared(b))
        .OrderBy(BattleCommand.PetOrder).ThenBy(b=>b.Serial.Value).ToArray();
    internal static int Limit(PlayerMobile owner)=>checked((int)SkillMechanics.Values(owner,CombatAbility.SummonGoatman).Eval("petmax"));
    internal static void Enforce(PlayerMobile owner)
    {
        var bodies=Demons(owner).Cast<BaseCreature>().Concat(BoundDemonHosts.For(owner)).ToArray();int limit=Limit(owner);
        ConsumeEffects.Enforce(owner,bodies,limit);
    }
    internal static WarlockGoatmanBody Create(PlayerMobile owner,Point3D point,DateTime at)
        =>(WarlockGoatmanBody)Create(owner,point,at,CombatAbility.SummonGoatman);
    internal static SkeletonGuardian Create(PlayerMobile owner,Point3D point,DateTime at,CombatAbility ability)
    {
        SkeletonGuardian body=ability switch
        {CombatAbility.SummonGoatman=>GoatmanNativeHostCandidate.Create(owner,point,at),CombatAbility.SummonTainted=>GoatmanNativeHostCandidate.CreateTainted(owner,point,at),CombatAbility.SummonDefiler=>GoatmanNativeHostCandidate.CreateDefiler(owner,point,at),_=>throw new NotSupportedException("No ground demon birth factory")};
        try
        {
            NpcEquipmentRandom.Set(body,SourceRandom.InitLow(NpcEquipmentRandom.SeedLow()));_=NpcEquipmentRandom.Require(body);
            var host=new GoatmanNativeHostCandidate(body,at);DemonNativeState.Controller(body,host.Capture());
            Hosts.Add(body,host);return body;
        }
        catch{Forget(body);DeathMarkWorld.Remove(body);body.Delete();throw;}
    }
    internal static void Admit(SkeletonGuardian body)
    {DemonNativeState.Admit(body);Track(body);}
    internal static void Suspend(SkeletonGuardian body)
    {
        if (Hosts.TryGetValue(body, out var host))
        {
            host.Park();
            Store(body, host);
        }
        else if (DemonNativeState.Controller(body) is { Fault: { } fault } saved
            && (fault.StartsWith(DeathMarkWorld.BlockedWalk, StringComparison.Ordinal)
                || fault == ReviveMeleeGeometry.StaleDemonPosition
                || body is WarlockTaintedBody && fault == TaintedFireBallCasting.LegacyLostTargetFault))
        {
            // These failures can be repaired by rebinding the original body on return.
            DemonNativeState.Controller(body, saved with { Fault = null });
        }
    }
    internal static void Interrupt(SkeletonGuardian body)
    {if(Hosts.TryGetValue(body,out var host)){host.Interrupt();Store(body,host);}}
    private static void Store(SkeletonGuardian body,GoatmanNativeHostCandidate host)
    {if(body is{Deleted:false}){DemonNativeState.Controller(body,host.Capture());}}
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    private static void Fault(SkeletonGuardian body,Exception e)
    {
        if(Hosts.Remove(body,out var host)){host.Dispose();}
        if(body is{Deleted:false}&&DemonNativeState.Controller(body)is{} clock)
        {DemonNativeState.Controller(body,clock with{Fault=e.Message});}
        Log.Error(e,"Ground demon native controller refused unsupported context for {Serial}",body.Serial);
    }
    internal static void Tick(DateTime at)
    {
        DefilerHealthLink.Tick(at);
        DefilerHealthLink.PresentMarks(at);
        var active=new List<SkeletonGuardian>();var owners=new HashSet<PlayerMobile>();
        foreach(var body in Bodies.ToArray())
        {
            if(body is not{Alive:true,Deleted:false}){Forget(body);DeathMarkWorld.Remove(body);continue;}
            if(DemonNativeState.Controller(body)is not{Fault:null} saved){continue;}
            try
            {
                bool loaded=!Hosts.TryGetValue(body,out var host);
                if(loaded){host=new(body,at,saved);Hosts.Add(body,host);}
                if(body.ControlMaster is PlayerMobile owner&&PersistentMinions.Active(owner,body)&&body.Permitted(owner)&&owners.Add(owner)){Enforce(owner);}
                if(body.Deleted){continue;}
                bool online=body.ControlMaster is PlayerMobile{Alive:true,Deleted:false,NetState:{Running:true}} master
                    &&PersistentMinions.Active(master,body)&&body.Permitted(master)&&!MinionSafeAreas.ShouldPark(master,body);
                // Suspension/load/placement observations skip the whole
                // inactive interval before joining active frame ordering.
                if(loaded||!online||host.NeedsObservation){host.Advance(at);Store(body,host);}
                if(online&&!body.Deleted)
                {
                    active.Add(body);WarlockDemonVisuals.Track(body);
                    WarlockRitualVisuals.Engorge(body,at);
                }
            }
            catch(Exception e){Fault(body,e);}
        }
        // Deliver overdue active actors in frame/serial order;one demon must
        // not finish several attacks before another receives an earlier frame.
        var stalled=new HashSet<SkeletonGuardian>();
        while(true)
        {
            var due=active.Where(b=>!b.Deleted&&!stalled.Contains(b)&&DemonNativeState.Controller(b)is{Fault:null}&&Hosts.ContainsKey(b)
                &&Hosts[b].Capture().Clock.NextUtc<=at).OrderBy(b=>Hosts[b].Capture().Clock.NextUtc).ThenBy(b=>b.Serial.Value).FirstOrDefault();
            if(due==null){break;}var host=Hosts[due];var before=host.Capture().Clock.NextUtc;
            try{host.Advance(before);Store(due,host);if(!due.Deleted&&host.Capture().Clock.NextUtc==before){stalled.Add(due);}}
            catch(Exception e){Fault(due,e);}
        }
    }
}
