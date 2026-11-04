// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ReviveSwingState(uint Actor,uint Owner,string Binding,ReviveSwingPlan Plan,int Map,Point3D Origin,uint Target,
    DateTime StartedUtc,DateTime ActionUtc,DateTime EndUtc,IronGolemContact.Saved Pending,string Fault=null);
public sealed class ReviveSwingProfiles:GenericPersistence
{
    public ReviveSwingProfiles():base("SanctuaryReviveSwings",231){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(ReviveSwings.SaveSnapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown original swing version");}ReviveSwings.Restore(JsonSerializer.Deserialize<ReviveSwingState[]>(reader.ReadString()));}
    public override void PostDeserialize()=>ReviveSwings.Advance(TimedEffects.Now);
}

internal static class ReviveSwings
{
    internal const string Profile="frozen-equipment-native-a1-v1";
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveSwings));
    private sealed class Live(ReviveSwingState state,IronGolemContact.Prepared packet)
    {internal ReviveSwingState State=state;internal IronGolemContact.Prepared Packet=packet;}
    private static Dictionary<uint,Live> States=new();
    internal static int LoadedPending{get;private set;}
    internal static ReviveSwingProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()
    {
        var previous=ClassCombatHooks.CreatureSwing;var overlap=ClassCombatHooks.OverlapMovement;
        ClassCombatHooks.CreatureSwing=(actor,target)=>actor is BaseCreature source&&Handles(source)?Begin(source,target,TimedEffects.Now):previous?.Invoke(actor,target);
        ClassCombatHooks.OverlapMovement=actor=>Busy(actor,TimedEffects.Now)||overlap?.Invoke(actor)==true;
        Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Advance(TimedEffects.Now));
    }
    internal static bool Handles(BaseCreature actor)=>ReviveNativeActivation.State(actor)is{Setup.EquipmentSwingProfile:Profile};
    internal static bool Busy(BaseCreature actor,DateTime at)=>actor!=null&&States.TryGetValue(actor.Serial.Value,out var live)&&live.State.EndUtc>at;
    internal static ReviveSwingState State(BaseCreature actor)=>actor!=null&&States.TryGetValue(actor.Serial.Value,out var live)?live.State:null;
    internal static ReviveSwingState[] Snapshot()=>States.Values.Select(v=>v.State).OrderBy(r=>r.Actor).ToArray();
    internal static ReviveSwingState[] SaveSnapshot()=>Snapshot().Where(r=>World.FindMobile((Serial)r.Actor)is BaseCreature{Alive:true,Deleted:false} actor&&Handles(actor)&&actor.ControlMaster?.Serial.Value==r.Owner).ToArray();
    internal static ReviveNativeSetup Prepare(ReviveNativeSetup setup,ReviveOriginalState original)
    {
        var graph=ReviveEquipmentGraph.Prepare(setup,original);var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        _=ReviveSwingClock.Calculate(graph.LifePlan.Source,ReviveBeforeDeath.Equipment(before).Select(FrozenEquipmentEffects.Bind).ToArray(),graph.CombatPlan.Level,100);
        return graph with{EquipmentSwingProfile=Profile};
    }
    internal static void Validate(ReviveNativeState state)
    {
        if(state.Setup.EquipmentSwingProfile==null){return;}
        try{if(state.Setup.EquipmentSwingProfile!=Profile||Prepare(state.Setup,state.Original)!=state.Setup){throw new InvalidDataException("Invalid original native swing profile");}}
        catch(Exception e)when(e is NotSupportedException or OverflowException){throw new InvalidDataException("Unavailable original native swing",e);}
    }
    private static void Validate(ReviveSwingState row)
    {
        if(row==null||row.Actor==0||row.Owner==0||row.Target==0||!((Serial)row.Actor).IsMobile||!((Serial)row.Owner).IsMobile||!((Serial)row.Target).IsMobile||row.Actor==row.Target||row.Actor==row.Owner
            ||row.Binding is not{Length:64}||row.Binding.Any(c=>c is not(>= '0' and <= '9' or >= 'a' and <= 'f'))||row.Plan==null
            ||row.Map<0||row.Map>=Map.Maps.Length||Map.Maps[row.Map]==null||Map.Maps[row.Map]==Map.Internal
            ||row.StartedUtc.Kind!=DateTimeKind.Utc||row.ActionUtc.Kind!=DateTimeKind.Utc||row.EndUtc.Kind!=DateTimeKind.Utc
            ||row.Plan.Action<=0||row.Plan.End<row.Plan.Action||row.Plan!=ReviveSwingClock.Calculate(row.Plan.Source,row.Plan.FasterAttackRate,row.Plan.ActorRate,row.Plan.WeaponSpeed)
            ||row.ActionUtc-row.StartedUtc!=TimeSpan.FromMilliseconds(row.Plan.Action*40L)||row.EndUtc-row.StartedUtc!=TimeSpan.FromMilliseconds(row.Plan.End*40L))
        {throw new InvalidDataException("Invalid persisted original swing clock/identity");}
        if(!World.Loading&&World.FindMobile((Serial)row.Actor)is BaseCreature actor&&ReviveNativeActivation.State(actor)is{} native
            &&(!Handles(actor)||row.Owner!=native.Owner||row.Plan.Source!=native.Setup.LifePlan.Source||row.Binding!=ReviveEquipmentLife.Identity(native)))
        {throw new InvalidDataException("Persisted swing differs from its retained original");}
        if(row.Pending!=null)
        {
            var packet=IronGolemContact.Restore(row.Pending);
            if(packet.Impact.Actor!=row.Actor||packet.Impact.Target!=row.Target||packet.Impact.CreditOwner!=0&&packet.Impact.CreditOwner!=row.Owner)
            {throw new InvalidDataException("Persisted original swing packet changed its actors");}
        }
    }
    internal static void Restore(ReviveSwingState[] rows)
    {
        if(rows==null){throw new InvalidDataException("Missing original swing states");}var next=new Dictionary<uint,Live>();
        foreach(var row in rows)
        {Validate(row);if(!next.TryAdd(row.Actor,new(row,row.Pending==null?null:IronGolemContact.Restore(row.Pending)))){throw new InvalidDataException("Duplicate original swing actor");}}
        States=next;LoadedPending=rows.Count(r=>r.Pending!=null);
    }
    internal static void Cancel(BaseCreature actor,bool keepBusy)
    {
        if(actor==null||!States.TryGetValue(actor.Serial.Value,out var live)){return;}
        live.Packet?.Cancel();live.Packet=null;live.State=live.State with{Pending=null};
        if(!keepBusy){States.Remove(actor.Serial.Value);}
    }
    private static bool Ready(BaseCreature actor)=>ReviveNativeActivation.CanAct(actor)&&!actor.Frozen&&!actor.Paralyzed&&!FreezeStates.Waiting(actor)
        &&CreatureHitRecovery.Remaining(actor)==0&&ClassCombatHooks.ActionAllowed?.Invoke(actor)!=false
        &&!(actor.Spell is Server.Spells.Spell{IsCasting:true,BlocksMovement:true});
    internal static TimeSpan Begin(BaseCreature actor,Mobile target,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Original swing needs UTC");}
        if(States.TryGetValue(actor.Serial.Value,out var active)&&active.State.EndUtc>at){return active.State.EndUtc-at;}
        if(!Ready(actor)||target is not BaseCreature{Alive:true,Deleted:false,Blessed:false} victim||!victim.CanBeDamaged()
            ||!ClassCombat.IsHostile(actor.ControlMaster as PlayerMobile,victim)||!actor.CanBeHarmful(victim,false))
        {return TimeSpan.FromMilliseconds(40);}
        try
        {
            var owner=(PlayerMobile)actor.ControlMaster;var plan=ReviveSwingClock.Capture(actor);
            if(!ReviveMeleeGeometry.Within(actor,victim,3)){return TimeSpan.FromMilliseconds(40);}
            var rules=IronGolemContact.Require(actor,owner,victim);
            // Refuse subscriptions which cannot survive a save before hit/damage RNG.
            _=NpcCombatEvents.RestoreSubscriptions(rules.SourceEvents.Subscriptions());_=NpcCombatEvents.RestoreSubscriptions(rules.TargetEvents.Subscriptions());
            var prepared=IronGolemContact.Prepare(actor,victim,rules,()=>ReviveMeleeGeometry.Result(actor,owner,victim));
            var row=new ReviveSwingState(actor.Serial.Value,owner.Serial.Value,ReviveEquipmentLife.Identity(ReviveNativeActivation.State(actor)),plan,actor.Map.MapID,actor.Location,victim.Serial.Value,
                at,at.AddMilliseconds(plan.Action*40L),at.AddMilliseconds(plan.End*40L),IronGolemContact.Capture(prepared));
            States[actor.Serial.Value]=new(row,prepared);actor.Direction=actor.GetDirectionTo(victim);actor.AIObject?.ResetApproachState();
            actor.Animate(4,5,1,true,false,0); // Identified native A1 placeholder; the mechanical clock is source-owned.
            return row.EndUtc-at;
        }
        catch(Exception e)when(e is NotSupportedException or InvalidDataException or OverflowException)
        {Log.Warning("Original swing refused for {Actor}: {Reason}",actor.Serial.Value,e.Message);return TimeSpan.FromMilliseconds(100);}
    }
    internal static void Advance(DateTime at)
    {
        foreach(var pair in States.ToArray())
        {
            var live=pair.Value;var row=live.State;var source=World.FindMobile((Serial)row.Actor)as BaseCreature;
            if(source is not{Alive:true,Deleted:false}||!Handles(source)||source.ControlMaster?.Serial.Value!=row.Owner)
            {States.Remove(pair.Key);continue;}
            if(!ReviveNativeActivation.CanAct(source))
            {Cancel(source,true);if(row.EndUtc<=at){States.Remove(pair.Key);}continue;}
            if(live.Packet!=null)
            {
                if(!Ready(source)||source.Map.MapID!=row.Map||source.Location!=row.Origin||ReviveEquipmentLife.Identity(ReviveNativeActivation.State(source))!=row.Binding)
                {Cancel(source,false);continue;}
                if(at>=row.ActionUtc)
                {
                    live.State=row with{Pending=null};live.Packet=null;
                    try
                    {
                        var target=World.FindMobile((Serial)row.Target)as BaseCreature;
                        if(target is{Alive:true,Deleted:false})
                        {_=ReviveMeleeGeometry.Release(source,(PlayerMobile)source.ControlMaster,target,IronGolemContact.Restore(row.Pending),at);}
                    }
                    catch(Exception e)when(e is NotSupportedException or InvalidDataException or InvalidOperationException or OverflowException)
                    {live.State=live.State with{Fault=e.Message};Log.Warning("Original swing canceled for {Actor}: {Reason}",row.Actor,e.Message);}
                }
            }
            if(row.EndUtc<=at){States.Remove(pair.Key);}
        }
    }
}
