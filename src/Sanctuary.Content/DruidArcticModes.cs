// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal sealed record ArcticModeCast(long Id,uint Owner,int Map,Point3D Origin,uint Weapon,uint Target,
    Point3D Point,DateTime EpochUtc,PlayerInfernoSequenceState Sequence);

// Prepared ordinary Druid SQ18 controller. Sequence256 follows the checked
// source rate route; modern client/cadence acceptance remains separate.
// Calls are explicit accepted intents, not raw
// client heartbeats. No learned handler is registered. Like other pending
// player actions, the mode is transient across disconnect/restart; released
// missiles use their existing independent persistence and ownership policy.
internal static class DruidArcticModes
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidArcticModes));
    private static readonly Dictionary<uint,ArcticModeCast> Casts=new();
    private static long _nextId;
    public static void Configure()
    {
        EventSink.Disconnected+=Cancel;EventSink.Logout+=Cancel;
    }
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Tick);
    internal static ArcticModeCast State(Mobile owner)=>owner==null?null:Casts.GetValueOrDefault(owner.Serial.Value);
    internal static bool Active(Mobile owner)=>State(owner)?.Sequence.UsedSkill==true;
    internal static void Cancel(Mobile owner){if(owner!=null){Casts.Remove(owner.Serial.Value);}}
    internal static void Interrupt(Mobile owner)
    {
        if(State(owner) is{} cast)
        {Commit(cast,PlayerInfernoSequence.Cancel(cast.Sequence,cast.Sequence.Frame));}
    }
    internal static void EquipmentChanged(PlayerMobile owner)
    {if(State(owner) is{} cast&&(CombatTiming.Equipped(owner)?.Serial.Value??0)!=cast.Weapon){Interrupt(owner);}}
    private static bool Connected(PlayerMobile owner)
        =>owner is{Alive:true,Deleted:false,NetState:not null}&&owner.NetState.Running&&owner.NetState.Mobile==owner
            &&owner.Map!=null&&owner.Map!=Map.Internal
            &&ClassCreation.Profiles.TryGet(owner.Serial,out var profile)&&profile.Class==DiabloClass.Druid;
    private static bool Eligible(PlayerMobile owner)
        =>Connected(owner)&&DruidPlayerForms.State(owner)==null&&!SpellHelper.IsTown(owner.Location,owner)
            &&SkillMechanics.Rank(owner,CombatAbility.ArcticBlast) is >0 and <=255
            &&!owner.Frozen&&!owner.Paralyzed&&owner.Spell==null&&!HitRecovery.Active(owner,Core.TickCount)
            &&AlliedOverlap.Allowed(owner);
    private static bool ValidPoint(PlayerMobile owner,Point3D point)
        =>point.X>0&&point.Y>0&&point.X<owner.Map.Width&&point.Y<owner.Map.Height;
    private static int Frame(ArcticModeCast cast,DateTime at)
        =>checked((int)Math.Max(0,(at.Ticks-cast.EpochUtc.Ticks)/TimeSpan.TicksPerMillisecond/40));
    private static PlayerInfernoSequenceServices Services(PlayerMobile owner,ArcticModeCast cast,int rank,DateTime phase)
    {
        Point3D aim=cast.Point;
        bool Resolve()
        {
            // Source helper reads a present target's coordinates, otherwise
            // the retained path point. It does not acquire a nearby target.
            aim=World.FindMobile((Serial)cast.Target) is{Deleted:false} target&&target.Map==owner.Map
                ?target.Location:cast.Point;
            return ValidPoint(owner,aim);
        }
        return new(()=>NecromancerEffects.ManaBalance256(owner),cost=>NecromancerEffects.Pay(owner,cost),
            ()=>true,Resolve,frames=>
            {
                var values=SkillMechanics.Values(owner,CombatAbility.ArcticBlast,rank);
                if(Math.Max(1,(int)values.Eval("calc1"))!=frames){throw new NotSupportedException("Arctic channel/factory lifetime mismatch");}
                return DruidArcticStream.Create(owner,new(aim.X*2,aim.Y*2),values,phase)>0;
            });
    }
    internal static bool BeginPrepared(PlayerMobile owner,Serial selected,Point3D point,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||!Eligible(owner)||State(owner)!=null||!ValidPoint(owner,point)
            ||ClassCombat.Existing(owner) is{PendingAbility:not CombatAbility.None}){return false;}
        var target=World.FindMobile(selected);
        if(selected!=Serial.Zero&&(target==null||!ClassCombat.IsHostile(owner,target)||target.Map!=owner.Map)){return false;}
        int rank=SkillMechanics.Rank(owner,CombatAbility.ArcticBlast);
        var cast=new ArcticModeCast(++_nextId,owner.Serial.Value,owner.Map.MapID,owner.Location,
            CombatTiming.Equipped(owner)?.Serial.Value??0,selected.Value,point,at,null);
        var step=PlayerInfernoSequence.EnterFresh(0,CombatAbility.ArcticBlast,rank,PlayerInfernoCostModel.CurrentTablePerInvocation,Services(owner,cast,rank,at));
        if(!step.Success){return false;}
        Casts.Add(cast.Owner,cast with{Sequence=step.State});return true;
    }
    private static bool Current(PlayerMobile owner,ArcticModeCast cast)
        =>Eligible(owner)&&ClassCombat.Existing(owner) is not{PendingAbility:not CombatAbility.None}&&owner.Map.MapID==cast.Map&&owner.Location==cast.Origin
            &&(CombatTiming.Equipped(owner)?.Serial.Value??0)==cast.Weapon;
    private static void Commit(ArcticModeCast cast,PlayerInfernoSequenceState sequence)
    {
        if(!Casts.TryGetValue(cast.Owner,out var present)||present.Id!=cast.Id){return;}
        if(!sequence.UsedSkill&&!sequence.Channel.Lease&&sequence.Animation.Events.Length==0){Casts.Remove(cast.Owner);}
        else{Casts[cast.Owner]=cast with{Sequence=sequence};}
    }
    internal static bool Advance(PlayerMobile owner,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Arctic mode clock must be UTC");}
        if(State(owner) is not{} cast){return false;}
        if(!Connected(owner)){Cancel(owner);return false;}
        if(cast.Sequence.UsedSkill&&!Current(owner,cast))
        {Interrupt(owner);cast=State(owner);if(cast==null){return false;}}
        if(at<cast.EpochUtc){return true;}
        int frame=Frame(cast,at);
        if(frame<cast.Sequence.Frame){throw new ArgumentException("Prepared Arctic clock moved backwards");}
        int rank=Math.Max(1,SkillMechanics.Rank(owner,CombatAbility.ArcticBlast));
        var sequence=cast.Sequence;
        // Drain older frames in order so every emission receives its actual
        // phase timestamp. Same-frame intents precede only undispatched events;
        // this is a per-owner adapter,not the full game's cross-owner queue.
        for(int phase=sequence.Frame;phase<=frame;phase++)
        {sequence=PlayerInfernoSequence.Advance(sequence,phase,rank,Services(owner,cast,rank,cast.EpochUtc.AddMilliseconds(phase*40L)));}
        Commit(cast,sequence);return true;
    }
    private static bool BeforeIntent(PlayerMobile owner,DateTime at,out ArcticModeCast cast,out int frame)
    {
        cast=State(owner);frame=0;
        if(at.Kind!=DateTimeKind.Utc){return false;}
        if(cast==null||!Connected(owner)){Cancel(owner);return false;}
        if(at<cast.EpochUtc){return false;}
        frame=Frame(cast,at);
        if(frame<cast.Sequence.Frame){throw new ArgumentException("Prepared Arctic intent moved backwards");}
        if(frame-1>=cast.Sequence.Frame){Advance(owner,cast.EpochUtc.AddMilliseconds((frame-1)*40L));}
        cast=State(owner);return cast!=null;
    }
    internal static bool StopIntent(PlayerMobile owner,DateTime at)
    {
        if(!BeforeIntent(owner,at,out var cast,out int frame)){return false;}
        Commit(cast,PlayerInfernoSequence.Stop(cast.Sequence,frame));return true;
    }
    internal static bool StartCallback(PlayerMobile owner,DateTime at)
    {
        if(!Eligible(owner)){return false;}
        if(State(owner) is{} current&&current.Sequence.UsedSkill&&!Current(owner,current))
        {Interrupt(owner);return false;}
        if(!BeforeIntent(owner,at,out var cast,out int frame)){return false;}
        int rank=SkillMechanics.Rank(owner,CombatAbility.ArcticBlast);
        var step=PlayerInfernoSequence.StartCallback(cast.Sequence,frame,rank,Services(owner,cast,rank,at));
        Commit(cast,step.State);return step.Success;
    }
    internal static bool Aim(PlayerMobile owner,Serial selected,Point3D point)
    {
        if(State(owner) is not{} cast||!Current(owner,cast)||!ValidPoint(owner,point)){return false;}
        var target=World.FindMobile(selected);
        if(selected!=Serial.Zero&&(target==null||!ClassCombat.IsHostile(owner,target)||target.Map!=owner.Map)){return false;}
        Casts[cast.Owner]=cast with{Target=selected.Value,Point=point};return true;
    }
    private static void Tick()
    {
        var now=TimedEffects.Now;
        foreach(uint serial in Casts.Keys.ToArray())
        {
            if(World.FindMobile((Serial)serial) is not PlayerMobile owner){Casts.Remove(serial);continue;}
            try{Advance(owner,now);}
            catch(Exception e)
            {Cancel(owner);Log.Warning("Prepared Arctic mode cancelled for {Owner}: {Reason}",serial,e.Message);}
        }
    }
}
