// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record BindDemonChannelState(long Id,uint Owner,uint Target,int Map,Point3D Origin,uint Weapon,
    DateTime NextUtc,DateTime LastUtc,int Attempts,int Source,DateTime HoldUntilUtc);
internal sealed record BindDemonChannelStep(bool Active,bool Attempted,bool Bound,string Issue,int Chance=0);

// Prepared complete original-body attempt controller. One-second checks and
// nine-mana payments are an explicit proposed adaptation,not start64 parity.
// No timer,learned input or serialized held intent is installed here.
internal static class BindDemonChannel
{
    private static readonly Dictionary<uint,BindDemonChannelState> Casts=new();
    private static long _nextId;
    internal static Func<int,int> Roll=n=>Random.Shared.Next(n);
    internal const int Cost256=9*256;
    public static void Configure(){EventSink.Disconnected+=Cancel;EventSink.Logout+=Cancel;}
    internal static BindDemonChannelState State(Mobile owner)=>owner==null?null:Casts.GetValueOrDefault(owner.Serial.Value);
    internal static void Cancel(Mobile owner){if(owner!=null){Casts.Remove(owner.Serial.Value);}}
    private static bool Eligible(PlayerMobile owner)=>owner is{Alive:true,Deleted:false,NetState:{Running:true},Map:not null}
        &&owner.NetState.Mobile==owner&&owner.Map!=Map.Internal&&ClassCreation.Profiles.TryGet(owner.Serial,out var p)&&p.Class==DiabloClass.Warlock
        &&SkillMechanics.Rank(owner,CombatAbility.BindDemon)>0&&!owner.Frozen&&!owner.Paralyzed&&owner.Spell==null
        &&!HitRecovery.Active(owner,Core.TickCount)&&AlliedOverlap.Allowed(owner)&&!MinionSafeAreas.Contains(owner)
        &&ClassCombat.Existing(owner) is not{PendingAbility:not CombatAbility.None};
    private static void Clock(DateTime at)
    {if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddSeconds(-1)){throw new ArgumentException("Bind channel clock must be bounded UTC");}}
    internal static bool BeginPrepared(PlayerMobile owner,BaseCreature target,DateTime at,out string issue,int source=-1)
    {
        Clock(at);issue="Bind channel requires a living,uninterrupted trained Warlock and nine mana.";
        if(source is< -1 or>=CombatProtocol.ChannelSources||State(owner)!=null||!Eligible(owner)||NecromancerEffects.ManaBalance256(owner)<Cost256){return false;}
        if(!BoundDemonHosts.Qualify(owner,target,out _,out issue)){return false;}
        Casts.Add(owner.Serial.Value,new(++_nextId,owner.Serial.Value,target.Serial.Value,owner.Map.MapID,owner.Location,
            CombatTiming.Equipped(owner)?.Serial.Value??0,at.AddSeconds(1),at,0,source,at.AddMilliseconds(CombatProtocol.HoldLeaseMs)));
        owner.Direction=owner.GetDirectionTo(target);issue=null;return true;
    }
    internal static bool RenewPrepared(PlayerMobile owner,int source,DateTime at)
    {
        Clock(at);if(State(owner)is not{} state||state.Source!=source||source<0||at<state.LastUtc){return false;}
        if(at>=state.HoldUntilUtc||!Advance(owner,at).Active){Complete(owner,state);return false;}
        if(State(owner)is not{} next||next.Id!=state.Id){return false;}
        Casts[state.Owner]=next with{HoldUntilUtc=at.AddMilliseconds(CombatProtocol.HoldLeaseMs)};return true;
    }
    private static bool Current(PlayerMobile owner,BindDemonChannelState state)=>Eligible(owner)&&owner.Map.MapID==state.Map
        &&owner.Location==state.Origin&&(CombatTiming.Equipped(owner)?.Serial.Value??0)==state.Weapon;
    private static bool Matches(PlayerMobile owner,BindDemonChannelState state)=>State(owner)is{} present&&present.Id==state.Id&&present.Attempts==state.Attempts;
    private static bool Same(PlayerMobile owner,BindDemonChannelState state)=>Matches(owner,state)&&Current(owner,state);
    private static void Complete(PlayerMobile owner,BindDemonChannelState state){if(Matches(owner,state)){Cancel(owner);}}
    private static BindDemonChannelStep Stop(PlayerMobile owner,BindDemonChannelState state,string issue,bool attempted=false,int chance=0)
    {Complete(owner,state);return new(false,attempted,false,issue,chance);}
    internal static BindDemonChannelStep Advance(PlayerMobile owner,DateTime at)
    {
        Clock(at);if(State(owner)is not{} state){return new(false,false,false,"No active Bind channel.");}
        if(at<state.LastUtc){throw new ArgumentException("Bind channel cannot rewind time");}
        if(!Current(owner,state)||state.Source>=0&&at>=state.HoldUntilUtc){return Stop(owner,state,"Bind channel interrupted or held lease expired.");}
        if(World.FindMobile((Serial)state.Target)is not BaseCreature target){return Stop(owner,state,"Bind target is no longer present.");}
        if(!BoundDemonHosts.Qualify(owner,target,out var plan,out var issue)){return Stop(owner,state,issue);}
        if(at<state.NextUtc){Casts[state.Owner]=state with{LastUtc=at};return new(true,false,false,null);}
        if(NecromancerEffects.ManaBalance256(owner)<Cost256){return Stop(owner,state,"Insufficient mana for the next Bind attempt.");}
        // Reserve the deadline before callbacks:neither reentry nor lag can
        // replay this attempt. Transport renewal never adds another payment.
        var next=state with{LastUtc=at,NextUtc=at.AddSeconds(1),Attempts=checked(state.Attempts+1)};Casts[state.Owner]=next;
        if(!NecromancerEffects.Pay(owner,Cost256)){return Stop(owner,next,"Bind payment failed.");}
        if(!Same(owner,next)){return Stop(owner,next,"Bind interrupted during payment.",true);}
        int hp=BindDemonChance.HealthPercent(SkillHealth.Balance256(target),SkillHealth.Maximum256(target));
        var chance=BindDemonChance.Evaluate(BindDemonPrepared.Monster(plan.Monster),plan.Birth,12,hp,0,SkillHealth.Maximum256(target));
        // Only ordinary qualified original bodies reach this host. Native
        // champions/uniques/named encounters require their separate mapping.
        if(chance.Percent<=0){return new(true,true,false,null);}
        int roll=Roll(100);if(roll is<0 or>=100){return Stop(owner,next,"Invalid Bind random result.",true,chance.Percent);}
        if(!Same(owner,next)){return Stop(owner,next,"Bind interrupted during chance callback.",true,chance.Percent);}
        if(roll>=chance.Percent){return new(true,true,false,null,chance.Percent);}
        bool bound=BoundDemonHosts.BindPrepared(owner,target,out issue);Complete(owner,next);
        return new(false,true,bound,issue,chance.Percent);
    }
}
