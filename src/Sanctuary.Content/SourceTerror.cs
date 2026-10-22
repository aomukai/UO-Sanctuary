// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Optional JSON fields leave older curse saves readable. The curse owns expiry;
// this payload owns the source AI phase and the native movement intent to resume.
public sealed record TerrorAi(CombatAbility Skill,int LaunchRank,int Map,bool Started,uint Override,uint Focus,
    DateTime NextUtc,int VelocityPercent=0,bool Combat=false,Point3D? Goal=null);
public enum TerrorDecision{End,Idle,Attack,Escape}
public static class SourceTerror
{
    private static readonly HashSet<Serial> _tracked=new();
    private static readonly HashSet<Serial> _acquiring=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Expire);
    internal static void Changed(Mobile actor,NecromancerCurse before,NecromancerCurse after)
    {
        if(after?.Terror!=null){_tracked.Add(actor.Serial);}
        else{_tracked.Remove(actor.Serial);}
        if(before?.Terror!=null && (after?.Terror==null || before.Source!=after.Source || before.UntilUtc!=after.UntilUtc))
        {Reset(actor);}
    }
    internal static void AfterLoad(IEnumerable<KeyValuePair<Serial,NecromancerState>> states)
    {
        _tracked.Clear();foreach(var pair in states){if(pair.Value.Curse?.Terror!=null){_tracked.Add(pair.Key);}}
        Expire();
    }
    private static void Reset(Mobile actor)
    {
        if(actor is not BaseCreature creature){return;}
        creature.Combatant=null;creature.FocusMob=null;creature.Warmode=false;
        if(creature.AIObject is{} ai){ai.ClearMoveIntent();ai.ResetApproachState();ai.Action=ActionType.Wander;}
        if(creature is GraveyardUndead undead){undead.InterruptForRecovery();}
    }
    private static void Expire()
    {
        foreach(var serial in _tracked.ToArray())
        {
            var actor=World.FindMobile(serial);var state=actor==null?null:NecromancerEffects.Profiles.Find(actor);
            if(state?.Curse?.Terror==null){_tracked.Remove(serial);if(state==null && actor!=null){NecromancerEffects.Profiles.Clear(actor);}Reset(actor);}
            else if(state.Curse.UntilUtc<=TimedEffects.Now){NecromancerEffects.Profiles.Store(actor,state with{Curse=null});}
        }
    }
    internal static int Distance(int dx,int dy,int size)
    {
        int x=Math.Abs(Math.Abs(dx)-size),y=Math.Abs(Math.Abs(dy)-size);
        return Math.Max(x,y)+Math.Min(x,y)/2;
    }
    internal static int Distance(BaseCreature actor,Mobile target)
    {
        // Target size is subtracted by the source helper. Preserve integer source
        // coordinates around the established two-subtile native projection.
        int size=target is BaseCreature creature?PaladinConversion.SourceRule(creature)?.Size??2:2;
        return Distance((actor.X-target.X)*2,(actor.Y-target.Y)*2,size);
    }
    internal static int Duration(int frames,int resistance)=>resistance>=100?0:frames-(int)((long)frames*resistance/100);
    internal static int Velocity(int walk,int run)=>walk<=0?0:Math.Clamp(100*run/walk-100,0,120);
    internal static TerrorDecision Decide(bool active,bool started,int distance,bool combat,bool canAttack)=>!active?TerrorDecision.End:distance>30?TerrorDecision.Idle:started && combat && canAttack?TerrorDecision.Attack:TerrorDecision.Escape;
    internal static TerrorAi State(Mobile actor)=>NecromancerEffects.Curse(actor)?.Terror;
    internal static bool TargetAllowed(Mobile actor,Mobile target)=>_acquiring.Contains(actor.Serial)
        || State(actor) is{Combat:true} state && target?.Serial.Value==state.Focus;
    internal static bool InCombat(Mobile actor)=>State(actor)?.Combat==true;
    public static double MovementDelay(Mobile actor,double seconds)=>State(actor) is{Goal:not null,Combat:false} state
        ?seconds*100/Math.Max(1,100+state.VelocityPercent):seconds;
    internal static bool Supported(BaseCreature target)=>PaladinConversion.SourceRule(target) is{SwitchAi:true,Walk:true,Boss:false,Npc:false,Attackable:true} row
        // These three source AIs have no previous-state exit callback. Other
        // species/callback mappings remain explicitly unsupported until traced.
        && row.Ai is "Skeleton" or "Zombie" or "Megademon";
    internal static bool Apply(PlayerMobile source,BaseCreature target,int rank,int frames,DateTime at)
    {
        if(!Supported(target) || !NecromancerEffects.AiCurse(target) || NecromancerEffects.Curse(target)?.Ability is CombatAbility.Terror or CombatAbility.Attract || NamedControl.Fear(target)){return false;}
        var state=NecromancerEffects.Profiles.Find(target)??new();
        // Current mapped native monsters have no curse-resistance stat adapter;
        // their source default is zero. The pure source operation is retained.
        frames=Duration(frames,0);if(frames<=0){return false;}
        Reset(target);
        var ai=new TerrorAi(CombatAbility.Howl,rank,target.Map.MapID,false,source.Serial.Value,0,at.AddMilliseconds(40));
        NecromancerEffects.Profiles.Store(target,state with{Curse=new(CombatAbility.Terror,source.Serial.Value,1,at.AddMilliseconds(frames*40L),source.X,source.Y,Terror:ai)});
        if(TimedEffects.TryActor(target,out var actor) && actor.Control is{} control){actor.Control=control with{BlindUntil=default,FearUntil=default};}
        target.AIObject?.AITimer.Prod();target.FixedEffect(0x375A,10,15,0x21,0); // Identified fear marker placeholder.
        source.DoHarmful(target);return true;
    }
    internal static (int X,int Y) Fallback(int branch,int offset,bool flipX,bool flipY)
    {int x=branch!=0?6:offset,y=branch!=0?offset:6;return((flipX?-x:x)/2,(flipY?-y:y)/2);}
    private static Mobile Target(BaseCreature creature,TerrorAi state)
    {
        if(state.Override!=0 && World.FindMobile((Serial)state.Override) is{Alive:true,Deleted:false} source && source.Map==creature.Map){return source;}
        var ai=creature.AIObject;
        // Reuse native visibility/hostility acquisition after the initial source
        // override clears. Exact source scan/vision/ties remain a port boundary.
        _acquiring.Add(creature.Serial);
        try
        {
            if(ai.AcquireFocusMob(18,creature.FightMode,false,false,true)){return creature.FocusMob;}
            // Keep the last valid threat while the native reacquisition clock is
            // cooling down; an unavailable scan must not erase a live escape.
            return World.FindMobile((Serial)state.Focus) is{Alive:true,Deleted:false} prior && prior.Map==creature.Map
                && creature.InRange(prior,18) && creature.CanSee(prior) && creature.InLOS(prior) && creature.IsEnemy(prior)?prior:null;
        }
        finally{_acquiring.Remove(creature.Serial);}
    }
    private static void Store(BaseCreature target,NecromancerCurse curse,TerrorAi ai)
    {
        var state=NecromancerEffects.Profiles.Find(target);if(state!=null){NecromancerEffects.Profiles.Store(target,state with{Curse=curse with{Terror=ai}});}
    }
    internal static bool Think(BaseCreature creature,NecromancerCurse curse)
    {
        if(curse.Terror is not{} state || creature.AIObject is not{} ai){return false;}
        if(creature.Map?.MapID!=state.Map || curse.UntilUtc<=TimedEffects.Now)
        {var profile=NecromancerEffects.Profiles.Find(creature);if(profile!=null){NecromancerEffects.Profiles.Store(creature,profile with{Curse=null});}return false;}
        if(state.NextUtc>TimedEffects.Now){return true;}
        var target=Target(creature,state);var rule=PaladinConversion.SourceRule(creature);
        if(target==null)
        {ai.ClearMoveIntent();creature.Combatant=null;creature.FocusMob=null;Store(creature,curse,state with{Override=0,Focus=0,Combat=false,Goal=null,NextUtc=TimedEffects.Now.AddMilliseconds(400)});return true;}
        int distance=Distance(creature,target);bool combat=creature.InRange(target,1) && creature.InLOS(target);
        var decision=Decide(true,state.Started,distance,combat,rule?.Attack1==true && rule.Interact==false);
        if(decision==TerrorDecision.Idle)
        {ai.ClearMoveIntent();creature.Combatant=null;creature.Warmode=false;Store(creature,curse,state with{Focus=target.Serial.Value,Combat=false,Goal=null,NextUtc=TimedEffects.Now.AddMilliseconds(400)});return true;}
        if(decision==TerrorDecision.Attack)
        {
            ai.ClearMoveIntent();Store(creature,curse,state with{Focus=target.Serial.Value,Combat=true,Goal=null,NextUtc=TimedEffects.Now});
            creature.Combatant=target;creature.FocusMob=null;creature.Warmode=true;creature.Direction=creature.GetDirectionTo(target);
            // Native combat timer owns A1 hit/recovery; do not dispatch species
            // spell/ability AI from this special source state.
            return true;
        }
        int percent=Velocity(rule?.Velocity??0,rule?.RunVelocity??0);
        var goal=new Point3D(creature.X+15*Math.Sign(creature.X-target.X),creature.Y+15*Math.Sign(creature.Y-target.Y),creature.Z);
        if(state.Focus!=target.Serial.Value || state.Combat || state.Goal==null)
        {ai.ClearMoveIntent();goal=new(creature.X+15*Math.Sign(creature.X-target.X),creature.Y+15*Math.Sign(creature.Y-target.Y),creature.Z);}
        creature.Combatant=null;creature.FocusMob=null;creature.Warmode=false;creature.SetCurrentSpeedToActive();
        Store(creature,curse,state with{Started=true,Override=0,Focus=target.Serial.Value,Combat=false,VelocityPercent=percent,Goal=goal,NextUtc=TimedEffects.Now});
        bool progressed=ai.MoveToPoint(goal,0);
        if(!progressed && state.Started)
        {
            // Native pathing is the existing terrain adaptation for source escape
            // and walk-near fallback. Failed first escape has no fallback.
            ai.ClearMoveIntent();var offset=Fallback(Utility.Random(2),Utility.Random(6),Utility.Random(2)!=0,Utility.Random(2)!=0);
            var fallback=new Point3D(creature.X+offset.X,creature.Y+offset.Y,creature.Z);
            var current=State(creature);if(current!=null){Store(creature,curse,current with{Goal=fallback});}ai.MoveToPoint(fallback,0);
        }
        return true;
    }
}
