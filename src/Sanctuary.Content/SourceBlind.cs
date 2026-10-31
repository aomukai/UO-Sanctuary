// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary;

// Kept outside the curse so ordinary replacement/expiry does not reroll AI.
// This is an actor-local source-algorithm stream, not the historical D2 seed.
public sealed record BlindAiState(ulong Seed,int Map,DateTime NextUtc,uint Focus=0,
    Point3D? Goal=null,bool Combat=false,long Thinks=0,long Draws=0,string Fault=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] DateTime? NextStepUtc=null);
public sealed record BlindAiRule(string Source,int[] Delay,int[] Range);

public static class SourceBlind
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SourceBlind));
    private static readonly HashSet<Serial> Tracked=new();
    private static readonly HashSet<Serial> Acquiring=new();
    private static readonly HashSet<Type> Unmapped=new();
    private static readonly Dictionary<string,BlindAiRule> Rules=new(StringComparer.OrdinalIgnoreCase);
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    public static void Configure()
    {
        Rules.Clear();foreach(var r in BalanceCatalog.Read<BlindAiRule>("blind-ai-rules"))
        {
            if(r==null||string.IsNullOrWhiteSpace(r.Source)||r.Delay?.Length!=3||r.Range?.Length!=3
                ||r.Delay.Any(x=>x is <0 or >32767)||r.Range.Any(x=>x is <0 or >32767)||!Rules.TryAdd(r.Source,r))
            {throw new InvalidDataException("Invalid blind AI timing rules");}
        }
    }
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Tick);
    internal static bool AcquiringTarget(Mobile actor)=>Acquiring.Contains(actor.Serial);
    internal static bool TargetAllowed(Mobile actor,Mobile target)
    {
        if(AcquiringTarget(actor)){return true;}
        if(target==null||actor.Map!=target.Map||!actor.InRange(target,1)){return false;}
        return actor is not BaseCreature creature||!Supported(creature)
            ||NecromancerEffects.Profiles.Find(actor)?.BlindAi is not{Fault:null} state
            ||state.Combat&&state.Focus==target.Serial.Value;
    }
    internal static bool Supported(BaseCreature actor)=>SourceTerror.Supported(actor)
        &&Rules.ContainsKey(CreatureProfiles.Definition(actor).Reference??"");
    private static BlindAiRule Rule(BaseCreature actor)=>Rules.GetValueOrDefault(CreatureProfiles.Definition(actor).Reference??"")
        ??throw new NotSupportedException("Blind source timing/range metadata is unmapped");
    internal static int NeutralDelay(BaseCreature actor)
    {int n=Rule(actor).Delay[(int)SummonDifficulties.For(actor.Map)];return n<=0?15:n;}
    private static int Range(BaseCreature actor)
    {int n=Rule(actor).Range[(int)SummonDifficulties.For(actor.Map)];return n==0?35:n;}
    internal static void Validate(BlindAiState s)
    {
        if(s.Map<0||s.Map>=Map.Maps.Length||Map.Maps[s.Map]==null||Map.Maps[s.Map]==Map.Internal
            ||s.NextUtc.Kind!=DateTimeKind.Utc||s.NextUtc>DateTime.MaxValue.AddSeconds(-2)
            ||s.NextStepUtc is{} step&&step.Kind!=DateTimeKind.Utc
            ||s.Focus!=0&&!((Serial)s.Focus).IsMobile||s.Combat&&(s.Focus==0||s.Goal!=null)
            ||s.Thinks<0||s.Draws<0||s.Goal is{} p&&(p.X<=0||p.Y<=0||p.X>=Map.Maps[s.Map].Width||p.Y>=Map.Maps[s.Map].Height||p.Z is <-128 or >127))
        {throw new InvalidDataException("Invalid saved blind AI state");}
    }
    internal static void Changed(Mobile actor,NecromancerCurse before,NecromancerCurse after)
    {
        if(after?.Ability==CombatAbility.DimVision){Tracked.Add(actor.Serial);}else{Tracked.Remove(actor.Serial);}
        if(before?.Ability==CombatAbility.DimVision&&after?.Ability!=CombatAbility.DimVision){SourceTerror.Reset(actor);}
    }
    internal static void AfterLoad(IEnumerable<KeyValuePair<Serial,NecromancerState>> states)
    {
        Tracked.Clear();foreach(var row in states)
        {
            if(row.Value.Curse?.Ability!=CombatAbility.DimVision){continue;}Tracked.Add(row.Key);
            if(World.FindMobile(row.Key) is BaseCreature actor&&row.Value.BlindAi is{} ai){RestoreStep(actor,ai);}
        }
    }
    private static void RestoreStep(BaseCreature actor,BlindAiState state)
    {if(actor.AIObject is{} ai&&state.NextStepUtc is{} at){ai.NextMove=Math.Max(ai.NextMove,TimedEffects.TickAt(at));}}
    internal static void CaptureMovement()
    {
        foreach(var serial in Tracked.ToArray())
        {
            if(World.FindMobile(serial) is not BaseCreature{Map:not null} actor||actor.Map==Map.Internal){continue;}
            var state=NecromancerEffects.Profiles.Find(actor);
            if(state?.BlindAi is{} ai&&(ai.Goal!=null||state.Curse?.Motion!=null)&&actor.AIObject is{} native)
            {Store(actor,ai with{NextStepUtc=TimedEffects.TimeAt(native.NextMove)});}
        }
    }
    internal static void Attach(BaseCreature actor,bool fresh)
    {
        if(!Supported(actor)){return;}
        var state=NecromancerEffects.Profiles.Find(actor);if(state==null||actor.Map==null||actor.Map==Map.Internal){return;}
        if(!fresh&&state.BlindAi is{} existing&&existing.Map==actor.Map.MapID){return;}
        var next=new BlindAiState(state.BlindAi?.Seed??SourceRandom.InitLow(SeedLow()),actor.Map.MapID,TimedEffects.Now);
        NecromancerEffects.Profiles.Store(actor,state with{BlindAi=next});
    }
    private static void Store(BaseCreature actor,BlindAiState ai,NecromancerCurse curse=null)
    {
        var state=NecromancerEffects.Profiles.Find(actor);
        if(state!=null){NecromancerEffects.Profiles.Store(actor,state with{BlindAi=ai,Curse=curse??state.Curse});}
    }
    private static void Tick()
    {
        foreach(var serial in Tracked.ToArray())
        {
            var actor=World.FindMobile(serial) as BaseCreature;var state=actor==null?null:NecromancerEffects.Profiles.Find(actor);
            if(state?.Curse is not{Ability:CombatAbility.DimVision} curse){Tracked.Remove(serial);continue;}
            if(curse.UntilUtc<=TimedEffects.Now){NecromancerEffects.Profiles.Store(actor,state with{Curse=null});continue;}
            if(actor.Map==null||actor.Map==Map.Internal||actor.PlayerRangeSensitive&&!actor.Map.GetSector(actor.Location).Active){continue;}
            Think(actor,curse);
        }
    }
    private static bool Fault(BaseCreature actor,BlindAiState state,string issue)
    {
        SourceTerror.Reset(actor);
        if(state!=null){Store(actor,state with{Goal=null,Combat=false,Fault=issue});}
        Log.Warning("Blind world adapter unsupported for {Target}: {Issue}",actor.Serial.Value,issue);
        return false; // Existing blind adapter remains available; no invented retry delay.
    }
    private static Mobile Target(BaseCreature actor,BlindAiState state)
    {
        if(SourceMind.AttractedTarget(actor) is{} attracted){return attracted;}
        int range=Range(actor),tiles=(range+1)/2;
        Acquiring.Add(actor.Serial);
        try
        {
            if(actor.AIObject.AcquireFocusMob(tiles,actor.FightMode,false,false,true)&&actor.FocusMob is{} candidate&&SourceTerror.Distance(actor,candidate)<=range){return candidate;}
            var previous=World.FindMobile((Serial)state.Focus);
            return previous is{Alive:true,Deleted:false}&&previous.Map==actor.Map&&SourceTerror.Distance(actor,previous)<=range
                &&actor.CanSee(previous)&&actor.InLOS(previous)&&actor.IsEnemy(previous)?previous:null;
        }
        finally{Acquiring.Remove(actor.Serial);}
    }
    private static int NearestPlayer(BaseCreature actor)
    {
        int best=int.MaxValue;
        foreach(var player in actor.Map.GetMobilesInRange<PlayerMobile>(actor.Location,28))
        {if(player.Deleted||Math.Abs(player.Z-actor.Z)>=16){continue;}best=Math.Min(best,DruidWolfGeometry.NoSize(new(actor.X*2,actor.Y*2),new(player.X*2,player.Y*2)));}
        return best;
    }
    private static bool Pending(BaseCreature actor,NecromancerCurse curse,BlindAiState state)
    {
        if(curse.Motion is not{} motion){return false;}
        var target=World.FindMobile((Serial)motion.Target);var ai=actor.AIObject;
        RestoreStep(actor,state);
        if(state.NextStepUtc>TimedEffects.Now){return true;}
        if(motion.Flee||target is{Alive:true,Deleted:false}&&target.Map==actor.Map)
        {
            if(!motion.Flee&&actor.InRange(target,1))
            {Store(actor,state with{NextUtc=TimedEffects.Now.AddMilliseconds(NeutralDelay(actor)*40L),NextStepUtc=null},curse with{Motion=null});return false;}
            var destination=motion.Flee?new Point3D(motion.OriginX,motion.OriginY,actor.Z):target.Location;
            var direction=actor.GetDirectionTo(destination);if(motion.Flee){direction=(Direction)(((int)direction+4)&7);}
            if(ai.DoMoveDirect(direction)!=MoveResult.Blocked)
            {Store(actor,state with{NextStepUtc=TimedEffects.TimeAt(ai.NextMove)});return true;}
        }
        ai.ClearMoveIntent();actor.Combatant=null;actor.FocusMob=null;
        Store(actor,state with{NextUtc=TimedEffects.Now.AddMilliseconds(NeutralDelay(actor)*40L),NextStepUtc=null},curse with{Motion=null});return false;
    }
    internal static bool Think(BaseCreature actor,NecromancerCurse curse)
    {
        try{return ThinkCore(actor,curse);}
        catch(Exception e){return Fault(actor,NecromancerEffects.Profiles.Find(actor)?.BlindAi,e.Message);}
    }
    private static bool ThinkCore(BaseCreature actor,NecromancerCurse curse)
    {
        if(!Supported(actor))
        {if(Unmapped.Add(actor.GetType())){Log.Warning("Blind AI exit/mode adapter is unmapped: {Type}",actor.GetType().Name);}return false;}
        var ai=actor.AIObject;if(ai==null||actor.Map==null||actor.Map==Map.Internal){return true;}
        var current=NecromancerEffects.Profiles.Find(actor);if(current?.BlindAi==null){Attach(actor,true);current=NecromancerEffects.Profiles.Find(actor);}
        var state=current?.BlindAi;if(state==null){return false;}if(state.Fault!=null){return false;}
        if(actor.Controlled||actor.Summoned||PaladinConversion.Active(actor))
        {return Fault(actor,state,"ownership/conversion coexistence has no traced source AI transition");}
        if(state.Map!=actor.Map.MapID){return Fault(actor,state,"facet transfer has no traced source mode transition");}
        if(actor.Frozen||actor.Paralyzed||NamedControl.FrozenRemaining(actor)>0||FreezeStates.Waiting(actor)
            ||ClassCombatHooks.CreatureRecoveryRemaining?.Invoke(actor)>0){return true;}
        if(Pending(actor,curse,state)){return true;}
        curse=NecromancerEffects.Curse(actor);if(curse?.Ability!=CombatAbility.DimVision){return false;}
        state=NecromancerEffects.Profiles.Find(actor).BlindAi;
        if(state.Goal is{} goal)
        {
            RestoreStep(actor,state);
            if(state.NextStepUtc>TimedEffects.Now){return true;}
            bool progress=ai.MoveToPoint(goal,0);
            if(actor.Location==goal){ai.ClearMoveIntent();Store(actor,state with{Goal=null,NextUtc=TimedEffects.Now.AddMilliseconds(NeutralDelay(actor)*40L),NextStepUtc=null});return true;}
            if(!progress){return Fault(actor,state,"blocked native walk/mode completion is not source-certified");}
            Store(actor,state with{NextStepUtc=TimedEffects.TimeAt(ai.NextMove)});
            return true;
        }
        if(state.NextUtc>TimedEffects.Now){return true;}
        var target=Target(actor,state);var row=PaladinConversion.SourceRule(actor);
        bool combat=target!=null&&actor.InRange(target,1)&&actor.InLOS(target);
        ulong seed=state.Seed;long draws=state.Draws;
        uint Draw(){draws=checked(draws+1);return SourceRandom.Roll(ref seed);}
        // Native room/collision and target scans are explicit world adapters.
        // There is no native representation of source dwAiState3/19 here.
        bool collision=!PetGroundWorld.ZeroFootprint(actor.Map,actor.Location);
        var request=BlindAiPolicy.Decide(target!=null,combat,true,true,row?.Interact??false,true,row?.Attack1??true,row?.Walk??true,
            false,collision,NearestPlayer(actor),()=>unchecked((int)(Draw()%100)));
        state=state with{Thinks=checked(state.Thinks+1),Focus=target?.Serial.Value??0,Combat=false,Goal=null,NextStepUtc=null};
        if(request.Action==BlindAiAction.Attack)
        {
            state=state with{Combat=true,NextUtc=TimedEffects.Now.AddMilliseconds(40)};
            // The native Combatant setter asks CanBeHarmful. Publish the
            // admission first, as the existing special-state fear adapter does.
            Store(actor,state with{Seed=seed,Draws=draws});
            actor.Combatant=target;actor.FocusMob=null;actor.Warmode=true;ai.ClearMoveIntent();
        }
        else
        {
            actor.Combatant=null;actor.FocusMob=null;actor.Warmode=false;ai.ClearMoveIntent();
            if(request.Action==BlindAiAction.Idle){state=state with{NextUtc=TimedEffects.Now.AddMilliseconds(request.Value*40L)};}
            else
            {
                var offset=BlindAiPolicy.Offset(request.Value,Draw,bound=>(int)(Draw()%(uint)bound));
                if(!PetGroundWorld.Point(actor,new(actor.X*2+offset.X,actor.Y*2+offset.Y),out var point))
                {Store(actor,state with{Seed=seed,Draws=draws});return Fault(actor,state with{Seed=seed,Draws=draws},"source wander destination cannot be projected");}
                state=state with{Goal=point,NextUtc=TimedEffects.Now};Store(actor,state with{Seed=seed,Draws=draws});
                actor.SetCurrentSpeedToActive();bool progress=ai.MoveToPoint(point,0);
                state=state with{NextStepUtc=TimedEffects.TimeAt(ai.NextMove)};
                if(actor.Location==point){state=state with{Goal=null,NextUtc=TimedEffects.Now.AddMilliseconds(NeutralDelay(actor)*40L),NextStepUtc=null};}
                else if(!progress){return Fault(actor,state with{Seed=seed,Draws=draws},"source walk entry failed in native path adapter");}
            }
        }
        Store(actor,state with{Seed=seed,Draws=draws});return true;
    }
}
