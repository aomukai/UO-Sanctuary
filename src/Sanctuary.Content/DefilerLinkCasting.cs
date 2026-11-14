// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record DefilerLinkAction(uint Actor,uint Owner,uint Target,int Speed,DateTime StartedUtc,DateTime ActionUtc,DateTime EndUtc,
    bool Released=false,bool Linked=false,int ReleasedRank=0,int ReleasedSelfRank=0,string Fault=null,string Refusal=null);

// OwnNPC405S1,not playerSC or TaintedA2. Only an explicit caller/frame host
// delivers it;there is no parallel timer or persistent pending-action replay.
internal static class DefilerLinkCasting
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DefilerLinkCasting));
    private static readonly Dictionary<uint,DefilerLinkAction> Actions=new();
    private static readonly HashSet<uint> Owned=new();
    private static readonly Lazy<TaintedCastClock> Clock=new(()=>
    {
        var row=BalanceCatalog.Read<TaintedCastClock>("defiler-link-cast").Single();
        if(row!=new TaintedCastClock(405,8,16,128,12,1)){throw new NotSupportedException("Unknown owned Defiler S1 clock");}
        return row;
    });
    internal static DefilerLinkAction State(WarlockDefilerBody body)=>body==null?null:Actions.GetValueOrDefault(body.Serial.Value);
    internal static void ClaimClock(WarlockDefilerBody body)
    {if(State(body)!=null||!Owned.Add(body.Serial.Value)){throw new NotSupportedException("Defiler S1 already has an action/clock owner");}}
    internal static void ReleaseClock(WarlockDefilerBody body){Cancel(body);if(body!=null){Owned.Remove(body.Serial.Value);}}
    internal static void Cancel(WarlockDefilerBody body)
    {if(body!=null&&Actions.Remove(body.Serial.Value)){DeathMarkWorld.FinishAction(body,8);}}
    private static bool Attached(WarlockDefilerBody b)=>b is{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false,NetState:{Running:true}} p}
        &&b.Prepared?.Owner==p.Serial.Value&&b.Permitted(p)&&PersistentMinions.Active(p,b)&&b.Map==p.Map&&b.Map!=null&&b.Map!=Map.Internal
        &&DeathMarkWorld.State(b)is{Selector:1}&&DefilerFlyingWorld.Profile(b,DeathMarkWorld.Path(b));
    internal static (int Action,int End,int Speed) Timing(WarlockDefilerBody body)
    {
        if(body?.Prepared==null){throw new NotSupportedException("Defiler S1 requires its captured original body");}
        var row=Clock.Value;int speed=SourceOtherAnimation.Speed(row.Speed,SourceOtherAnimation.Rate(body));
        var flags=new int[144];flags[row.Action]=row.Flag;var plan=SourceAnimationEvents.Initial(new(0,speed,row.Frames*256,flags,[]),0);
        return(plan.Events.Single(e=>e.Type==SourceAnimationEvents.ModeChange&&e.Kind==1).Frame,
            plan.Events.Single(e=>e.Type==SourceAnimationEvents.EndAnimation).Frame,speed);
    }
    internal static bool Begin(WarlockDefilerBody body,BaseCreature target,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMinutes(-1)){throw new ArgumentException("Defiler S1 start requires UTC");}
        if(!Attached(body)||State(body)!=null||DeathMarkWorld.State(body).Mode!=1||GenericPetNativeControlStates.Read(body)!=0
            ||CreatureHitRecovery.Remaining(body)>0||!DefilerHealthLink.CanCast(body,target,at)){return false;}
        var clock=Timing(body);DeathMarkWorld.ChangeMode(body,8);
        Actions.Add(body.Serial.Value,new(body.Serial.Value,body.Prepared.Owner,target.Serial.Value,clock.Speed,
            at,at.AddMilliseconds(clock.Action*40),at.AddMilliseconds(clock.End*40)));
        body.Direction=body.GetDirectionTo(target);return true;
    }
    internal static GenericPetActionServices BindRegistered(WarlockDefilerBody body,GenericPetActionServices outer,Func<Mobile,int,bool> admit,Func<DateTime> now)
        =>outer with{ExecuteSkill=(mode,skill,target,x,y,flag)=>
        {
            if(skill!=405){return outer.ExecuteSkill(mode,skill,target,x,y,flag);}
            if(mode!=8||!flag||x!=0||y!=0||target==0){throw new NotSupportedException("Defiler S1 requires its original targeted mode8 request");}
            var unit=World.FindMobile((Serial)unchecked((uint)target));int rank=DefilerLinkNumbers.Capture(body).Rank;
            return admit(unit,rank)&&unit is BaseCreature npc&&Begin(body,npc,now());
        }};
    internal static void Advance(WarlockDefilerBody body,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Defiler S1 clock requires UTC");}
        if(State(body)is not{} state){return;}
        if(!Attached(body)){Cancel(body);return;}
        if(!state.Released&&state.ActionUtc<=at)
        {
            // Claim before selection/mutation. An unsuccessful or unsupported
            // release cannot retry at another observation of the same event.
            state=state with{Released=true};Actions[body.Serial.Value]=state;
            try
            {
                if(World.FindMobile((Serial)state.Target)is not BaseCreature{Alive:true,Deleted:false} target)
                {
                    // Approved link eligibility requires a living first
                    // target. Refuse this release,not every future AI action;
                    // the unverified path-point fallback is still not run.
                    const string reason="Lost Defiler S1 target;current path-point fallback remains unverified";
                    Actions[body.Serial.Value]=state with{Refusal=reason};
                    Log.Warning("Defiler S1 release refused unsupported context for {Actor}: {Reason}",body.Serial,reason);
                }
                else
                {
                    var n=DefilerLinkNumbers.Capture(body);
                    Actions[body.Serial.Value]=state with{Linked=DefilerHealthLink.Cast(body,target,state.ActionUtc),ReleasedRank=n.Rank,ReleasedSelfRank=n.SelfRank};
                }
            }
            catch(NotSupportedException e)
            {Actions[body.Serial.Value]=state with{Fault=e.Message};Log.Warning("Defiler S1 release refused unsupported context for {Actor}: {Reason}",body.Serial,e.Message);}
        }
        if(state.EndUtc<=at){Cancel(body);}
    }
}
