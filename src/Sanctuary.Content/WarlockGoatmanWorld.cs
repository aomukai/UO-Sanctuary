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
    private static readonly HashSet<WarlockGoatmanBody> Bodies=new();
    private static readonly Dictionary<WarlockGoatmanBody,GoatmanNativeHostCandidate> Hosts=new();
    internal static void Track(WarlockGoatmanBody body){if(body?.Prepared?.Controller!=null){Bodies.Add(body);}}
    internal static void Forget(WarlockGoatmanBody body){Bodies.Remove(body);Hosts.Remove(body);}
    internal static WarlockGoatmanBody[] For(PlayerMobile owner)=>(owner.AllFollowers??[]).OfType<WarlockGoatmanBody>()
        .Where(b=>b is{Alive:true,Deleted:false}&&b.ControlMaster==owner&&b.Prepared?.LearnedCast==true).ToArray();
    // The source pettype=demon pool is shared by these three species. Bind
    // Demon has a distinct pettype and does not enter this replacement pool.
    internal static SkeletonGuardian[] Demons(PlayerMobile owner)=>(owner.AllFollowers??[]).OfType<SkeletonGuardian>()
        .Where(b=>b is{Alive:true,Deleted:false}&&b.ControlMaster==owner&&WarlockDemonPlacement.Prepared(b))
        .OrderBy(BattleCommand.PetOrder).ThenBy(b=>b.Serial.Value).ToArray();
    internal static int Limit(PlayerMobile owner)=>checked((int)SkillMechanics.Values(owner,CombatAbility.SummonGoatman).Eval("petmax"));
    internal static void Enforce(PlayerMobile owner)
    {
        var bodies=Demons(owner);int limit=Limit(owner);
        if(limit is<1 or>3){throw new NotSupportedException("Unexpected source demon limit");}
        foreach(var b in bodies.Take(Math.Max(0,bodies.Length-limit))){DeathMarkWorld.Remove(b);b.Delete();}
    }
    internal static WarlockGoatmanBody Create(PlayerMobile owner,Point3D point,DateTime at)
    {
        var body=GoatmanNativeHostCandidate.Create(owner,point,at);
        try
        {
            NpcEquipmentRandom.Set(body,SourceRandom.InitLow(NpcEquipmentRandom.SeedLow()));_=NpcEquipmentRandom.Require(body);
            var host=new GoatmanNativeHostCandidate(body,at);body.RestorePrepared(body.Prepared with{Controller=host.Capture()});
            Hosts.Add(body,host);return body;
        }
        catch{Forget(body);DeathMarkWorld.Remove(body);body.Delete();throw;}
    }
    internal static void Admit(WarlockGoatmanBody body)
    {body.RestorePrepared(body.Prepared with{LearnedCast=true});Track(body);}
    internal static void Suspend(WarlockGoatmanBody body)
    {if(Hosts.TryGetValue(body,out var host)){host.Park();Store(body,host);}}
    internal static void Interrupt(WarlockGoatmanBody body)
    {if(Hosts.TryGetValue(body,out var host)){host.Interrupt();Store(body,host);}}
    private static void Store(WarlockGoatmanBody body,GoatmanNativeHostCandidate host)
    {if(body is{Deleted:false}&&body.Prepared!=null){body.RestorePrepared(body.Prepared with{Controller=host.Capture()});}}
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    private static void Fault(WarlockGoatmanBody body,Exception e)
    {
        if(Hosts.Remove(body,out var host)){host.Park();}
        if(body is{Deleted:false}&&body.Prepared?.Controller is{} clock)
        {body.RestorePrepared(body.Prepared with{Controller=clock with{Fault=e.Message}});}
        Log.Error(e,"Goatman native controller refused unsupported context for {Serial}",body.Serial);
    }
    internal static void Tick(DateTime at)
    {
        var active=new List<WarlockGoatmanBody>();var owners=new HashSet<PlayerMobile>();
        foreach(var body in Bodies.ToArray())
        {
            if(body is not{Alive:true,Deleted:false}){Forget(body);DeathMarkWorld.Remove(body);continue;}
            if(body.Prepared.Controller is not{Fault:null} saved){continue;}
            try
            {
                bool loaded=!Hosts.TryGetValue(body,out var host);
                if(loaded){host=new(body,at,saved);Hosts.Add(body,host);}
                if(body.ControlMaster is PlayerMobile owner&&PersistentMinions.Active(owner,body)&&body.Permitted(owner)&&owners.Add(owner)){Enforce(owner);}
                if(body.Deleted){continue;}
                bool online=body.ControlMaster is PlayerMobile{Alive:true,Deleted:false,NetState:{Running:true}} master
                    &&PersistentMinions.Active(master,body)&&body.Permitted(master)&&!MinionSafeAreas.Contains(master);
                // Suspension/load/placement observations skip the whole
                // inactive interval before joining active frame ordering.
                if(loaded||!online||host.NeedsObservation){host.Advance(at);Store(body,host);}
                if(online&&!body.Deleted){active.Add(body);}
            }
            catch(Exception e){Fault(body,e);}
        }
        // Deliver overdue active actors in frame/serial order;one demon must
        // not finish several attacks before another receives an earlier frame.
        var stalled=new HashSet<WarlockGoatmanBody>();
        while(true)
        {
            var due=active.Where(b=>!b.Deleted&&!stalled.Contains(b)&&b.Prepared.Controller is{Fault:null}&&Hosts.ContainsKey(b)
                &&Hosts[b].Capture().Clock.NextUtc<=at).OrderBy(b=>Hosts[b].Capture().Clock.NextUtc).ThenBy(b=>b.Serial.Value).FirstOrDefault();
            if(due==null){break;}var host=Hosts[due];var before=host.Capture().Clock.NextUtc;
            try{host.Advance(before);Store(due,host);if(!due.Deleted&&host.Capture().Clock.NextUtc==before){stalled.Add(due);}}
            catch(Exception e){Fault(due,e);}
        }
    }
}
