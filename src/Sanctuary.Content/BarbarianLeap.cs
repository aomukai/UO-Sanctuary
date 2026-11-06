// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public enum LeapPhase{Windup,Airborne,Recovery}
public sealed record LeapFlight(uint Owner,uint Reservation,int Map,Point3D Origin,Point3D Destination,Point3D Position,
    int Rank,LeapPhase Phase,int Remaining,int AirFrames,int MinimumAirFrames,int Velocity256,long Length16,long Travel16,DateTime NextUtc,bool DelayedDeath,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] LeapAttackFlight Attack=null);
public sealed class LeapProfiles:GenericPersistence
{
    public LeapProfiles():base("SanctuaryLeaps",145){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(BarbarianLeap.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Leap version");}
        BarbarianLeap.Restore(JsonSerializer.Deserialize<LeapFlight[]>(reader.ReadString()));
    }
}

public static class BarbarianLeap
{
    private static ImmutableDictionary<uint,LeapFlight> _flights=ImmutableDictionary<uint,LeapFlight>.Empty;
    private static void Store(LeapFlight state)=>_flights=_flights.SetItem(state.Owner,state);
    private static CombatAbility Ability(LeapFlight state)=>state.Attack==null?CombatAbility.Leap:CombatAbility.LeapAttack;
    public static LeapProfiles Profiles{get;private set;}
    public static void Configure()
    {
        Profiles=new();var previous=ClassCombatHooks.DeathAllowed;
        ClassCombatHooks.DeathAllowed=owner=>BeforeDeath(owner) && previous?.Invoke(owner)!=false;
    }
    public static void Initialize()
    {
        void Disconnected(Mobile mobile){if(mobile is PlayerMobile p&&State(p) is{} state){Offline(p,state);}}
        EventSink.Disconnected+=Disconnected;EventSink.Logout+=Disconnected;
        Timer.StartTimer(TimeSpan.Zero,TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    }
    internal static LeapFlight State(Mobile owner)=>owner!=null?_flights.GetValueOrDefault(owner.Serial.Value):null;
    public static bool Locked(Mobile owner)=>State(owner) is{} state && owner is{Alive:true,Deleted:false}
        && owner.Map?.MapID==state.Map && owner.Location==state.Position;
    internal static bool Airborne(Mobile owner)=>Locked(owner) && State(owner).Phase==LeapPhase.Airborne;
    internal static bool Uninterruptible(Mobile owner)=>Locked(owner) && State(owner).Phase!=LeapPhase.Recovery;
    private static bool BeforeDeath(PlayerMobile owner)
    {
        if(!Uninterruptible(owner)){return true;}
        Store(State(owner) with{DelayedDeath=true});return false;
    }
    internal static int Velocity(int run,int percent)=>(int)Math.Clamp((long)run*256*percent/100,0,int.MaxValue);
    internal static int Frames(LeapFlight state)=>state.Remaining+Math.Max(state.MinimumAirFrames,
        state.Length16==0?0:(int)((state.Length16+(long)state.Velocity256*8-1)/((long)state.Velocity256*8)))+(state.Attack?.EndFrame??1);
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
        =>BeginCore(owner,selected,point,now,false);
    internal static bool BeginAttack(PlayerMobile owner,Serial selected,Point3D point,long now)
        =>BeginCore(owner,selected,point,now,true);
    private static bool BeginCore(PlayerMobile owner,Serial selected,Point3D point,long now,bool attack)
    {
        var action=ClassCombat.For(owner);var ability=attack?CombatAbility.LeapAttack:CombatAbility.Leap;
        if(!BarbarianCombat.Available(owner,ability) || owner is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
            || owner.Mounted || !AlliedOverlap.Allowed(owner) || HitRecovery.Active(owner,now) || State(owner)!=null
            || now-action.NextAction<0 || action.PendingAbility!=CombatAbility.None){return false;}
        if(selected!=Serial.Zero)
        {if(World.FindMobile(selected) is not{Deleted:false} target || target.Map!=owner.Map){return false;}point=target.Location;}
        var values=SkillMechanics.Values(owner,ability);LeapAttackFlight captured=null;int windup=5;Point3D destination;
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        if(attack)
        {
            if(!BarbarianLeapAttack.WeaponAllowed(owner)||!BarbarianLeapAttack.Destination(owner,selected,point,out destination)){return false;}
            try
            {
                captured=BarbarianLeapAttack.Capture(owner,selected.Value,out windup);
                if(World.FindMobile(selected) is BaseCreature target){PlayerWeaponContact.RequireOrdinary(owner,target,true);}
            }
            catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
        }
        else if(!LeapTerrain.Destination(owner,point,(int)values.Eval("aurarangecalc"),out destination)){return false;}
        var marker=LandingReservations.Create(owner,owner.Map,destination);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){marker.Delete();return false;}
        long dx=destination.X-owner.X,dy=destination.Y-owner.Y;
        var flight=new LeapFlight(owner.Serial.Value,marker.Serial.Value,owner.Map.MapID,owner.Location,destination,owner.Location,
            values.Rank,LeapPhase.Windup,windup,0,values.Param(5),Velocity(9,(int)values.Eval("calc2")),
            (long)Math.Ceiling(Math.Sqrt(dx*dx+dy*dy)*65536),0,TimedEffects.Now.AddMilliseconds(40),false,captured);
        Store(flight);
        action.PendingAbility=ability;action.PendingTarget=selected;action.PendingPoint=point;
        action.Timing=new(Frames(flight),windup);action.NextAction=now+action.Timing.DurationMs;action.Animation=16;
        owner.Direction=owner.GetDirectionTo(destination);owner.RevealingAction();owner.PlaySound(0x238);
        // Existing native cast pose/sound are identified placeholders for the jump arc.
        ClassCombat.SendAnimation(owner,16,action.Timing.DurationMs);owner.Animate(16,5,1,true,false,0);
        ClassCombat.SendState(owner,action);return true;
    }
    private static void Position(PlayerMobile owner,Point3D point)
    {
        owner.NetState?.ResetMovementState();owner.Location=point;owner.ProcessDelta();
        owner.NetState?.ResetMovementState();owner.NetState?.SendMobileUpdate(owner);
    }
    private static void Clear(PlayerMobile owner,LeapFlight state)
    {
        _flights=_flights.Remove(state.Owner);
        if(World.FindMobile((Serial)state.Reservation) is SkillLandingReservation marker && marker.Owner==state.Owner){marker.Delete();}
        if(owner==null || owner.Deleted){return;}
        var action=ClassCombat.For(owner);if(action.PendingAbility==Ability(state)){action.PendingAbility=CombatAbility.None;}
        action.NextAction=Core.TickCount;owner.NetState?.ResetMovementState();
    }
    private static Point3D SafeLanding(PlayerMobile owner,LeapFlight state,Map map)
    {
        if(LeapTerrain.Clear(owner,map,state.Destination)){return state.Destination;}
        if(LeapTerrain.Clear(owner,map,state.Origin)){return state.Origin;}
        if(LeapTerrain.Ground(owner,map,state.Origin.X,state.Origin.Y,state.Origin.Z,out var ground)){return ground;}
        for(int radius=1;radius<=18;radius++)for(int dx=-radius;dx<=radius;dx++)for(int dy=-radius;dy<=radius;dy++)
        {if(Math.Max(Math.Abs(dx),Math.Abs(dy))==radius && LeapTerrain.Ground(owner,map,state.Origin.X+dx,state.Origin.Y+dy,state.Origin.Z,out ground)){return ground;}}
        return state.Origin;
    }
    private static void Abort(PlayerMobile owner,LeapFlight state)
    {
        if(owner is{Alive:true,Deleted:false} && owner.Map?.MapID==state.Map){Position(owner,SafeLanding(owner,state,owner.Map));}
        Clear(owner,state);
        if(state.DelayedDeath && owner is{Alive:true,Deleted:false}){owner.Kill();}
    }
    private static void Offline(PlayerMobile owner,LeapFlight state)
    {
        if(owner.Map!=Map.Internal&&owner.Map?.MapID!=state.Map){Clear(owner,state);return;}
        var map=Map.Maps[state.Map];var point=SafeLanding(owner,state,map);
        // Native player load/logout internalizes the body. Complete storage at a
        // legal landing without replaying combat or a second resource payment.
        if(owner.Map!=Map.Internal){Position(owner,point);}
        owner.LogoutMap=map;owner.LogoutLocation=point;Clear(owner,state);
        if(state.DelayedDeath && owner.Alive)
        {owner.MoveToWorld(point,map);owner.Kill();owner.LogoutMap=map;owner.LogoutLocation=point;owner.Internalize();}
    }
    private static void Knockback(PlayerMobile owner)
    {
        int range=(int)SkillMechanics.Values(owner,CombatAbility.Leap).Eval("calc1");
        foreach(var target in owner.Map.GetMobilesInRange<BaseCreature>(owner.Location,(int)Math.Ceiling(SkillGeometry.Tiles(range))))
        {
            long dx=(target.X-owner.X)*2,dy=(target.Y-owner.Y)*2;
            // Source filter8583 has hostility/target/town checks, no LOS200 bit.
            // It supplies result9 with no damage and does not check shield block.
            if(dx*dx+dy*dy<=(long)range*range && Math.Abs(target.Z-owner.Z)<16 && ClassCombat.IsHostile(owner,target)
                && SourceKnockbacks.Rule(target) is{Knockback:true,GetHit:true})
            {owner.DoHarmful(target,true);SourceKnockbacks.Begin(owner,target);}
        }
    }
    internal static void Tick(DateTime now)
    {
        if(World.Loading){return;}
        foreach(uint serial in _flights.Keys.ToArray())
        {
            if(!_flights.TryGetValue(serial,out var state)){continue;}
            if(World.FindMobile((Serial)serial) is not PlayerMobile{Deleted:false} owner){Clear(null,state);continue;}
            if(owner.Map==Map.Internal && owner.LogoutMap?.MapID==state.Map){Offline(owner,state);continue;}
            if(!owner.Alive){Clear(owner,state);continue;}
            if(owner.Map?.MapID!=state.Map || owner.Location!=state.Position || !ClassCombat.Enabled(owner)
                || ClassCreation.Profiles.TryGet(owner.Serial,out var profile)!=true || profile.Class!=DiabloClass.Barbarian)
            {Abort(owner,state);continue;}
            if(now<state.NextUtc){continue;}
            state=state with{NextUtc=now.AddMilliseconds(40)};var action=ClassCombat.For(owner);
            action.PendingAbility=Ability(state);action.NextAction=Core.TickCount+40;
            if(state.Phase==LeapPhase.Recovery)
            {
                if(state.Attack is not{} attack){Clear(owner,state);continue;}
                attack=attack with{Frame=attack.Frame+1};
                bool strike=!attack.Released&&attack.Frame>=attack.ContactFrame;
                if(strike){attack=attack with{Released=true};}
                state=state with{Attack=attack};Store(state);
                if(strike){BarbarianLeapAttack.Strike(owner,attack);}
                if(State(owner)!=state){continue;}
                if(attack.Frame>=attack.EndFrame){Clear(owner,state);}continue;
            }
            if(World.FindMobile((Serial)state.Reservation) is not SkillLandingReservation{Deleted:false} marker
                || marker.Owner!=state.Owner || marker.Map!=owner.Map || marker.Location!=state.Destination)
            {Abort(owner,state);continue;}
            if(state.Phase==LeapPhase.Windup)
            {
                state=state with{Remaining=state.Remaining-1};
                if(state.Remaining==0){state=state with{Phase=LeapPhase.Airborne};}
                Store(state);continue;
            }
            long travel=Math.Min(state.Length16,state.Travel16+(long)state.Velocity256*8);
            var end=state.Destination;var start=state.Origin;
            Point3D position=state.Length16==0?end:new(start.X+(int)((end.X-start.X)*travel/state.Length16),
                start.Y+(int)((end.Y-start.Y)*travel/state.Length16),start.Z+(int)((end.Z-start.Z)*travel/state.Length16));
            bool landingCheck=true;
            if(state.Attack is{} airborne)
            {
                int remaining=airborne.CheckRemaining-1;landingCheck=remaining<=0;
                state=state with{Attack=airborne with{CheckRemaining=landingCheck?airborne.CheckPeriod:remaining}};
            }
            state=state with{AirFrames=state.AirFrames+1,Travel16=travel,Position=position};Store(state);
            if(owner.Location!=position){Position(owner,position);}
            // Location/region callbacks may deal lethal damage synchronously.
            // Preserve the death flag written by the native OnBeforeDeath hook.
            if(_flights.TryGetValue(serial,out var current)){state=current;}
            if(owner.Deleted || owner.Map?.MapID!=state.Map || owner.Location!=state.Position){Abort(owner,state);continue;}
            if(landingCheck && travel==state.Length16 && state.AirFrames>=state.MinimumAirFrames)
            {
                if(!LeapTerrain.Clear(owner,owner.Map,end)){Abort(owner,state);continue;}
                state=state with{Phase=LeapPhase.Recovery,Remaining=1};Store(state);marker.Delete();
                if(state.DelayedDeath){owner.Kill();}
                if(owner is{Deleted:false,Alive:true})
                {
                    if(state.Attack==null){Knockback(owner);}
                    else if(SkillMechanics.Rank(owner,CombatAbility.LeapAttack)>0)
                    {
                        var landed=BarbarianLeapAttack.Land(owner,state);
                        if(State(owner)==state){state=state with{Attack=landed};Store(state);}
                    }
                    Teleportation.ArrivalFields(owner);
                }
            }
            ClassCombat.SendState(owner,action);
        }
        LandingReservations.Prune(marker=>_flights.TryGetValue(marker.Owner,out var state) && state.Reservation==marker.Serial.Value || BarbarianWhirlwind.Owns(marker));
    }
    public static LeapFlight[] Snapshot()=>_flights.Values.OrderBy(s=>s.Owner).ToArray();
    public static void Restore(LeapFlight[] states)
    {
        var next=ImmutableDictionary<uint,LeapFlight>.Empty;
        foreach(var state in states??throw new InvalidDataException("Missing Leap state"))
        {
            if(!((Serial)state.Owner).IsMobile || !((Serial)state.Reservation).IsMobile || state.Owner==state.Reservation || state.Map is <0 or >255
                || Map.Maps[state.Map]==null || state.Phase is <LeapPhase.Windup or >LeapPhase.Recovery || state.Rank<1
                || state.Remaining<0 || state.Remaining>(state.Attack==null?5:10000) || state.AirFrames<0 || state.MinimumAirFrames<1 || state.Velocity256<=0
                || state.Length16<0 || state.Travel16<0 || state.Travel16>state.Length16 || state.NextUtc.Kind!=DateTimeKind.Utc
                || state.Attack!=null&&!BarbarianLeapAttack.ValidSaved(state.Attack)
                || next.ContainsKey(state.Owner)){throw new InvalidDataException("Invalid Leap state");}
            next=next.Add(state.Owner,state);
        }
        _flights=next;
    }
}
