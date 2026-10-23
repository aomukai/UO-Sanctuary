// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed record WhirlwindFlight(uint Owner,uint Reservation,int Map,Point3D Origin,Point3D Position,Point3D[] Path,int Index,
    uint Primary,uint Secondary,int Frame,int NextHit,bool SecondaryHand,int Velocity256,long Distance16,bool Attacked,bool DelayedDeath,DateTime NextUtc);
public sealed class WhirlwindProfiles:GenericPersistence
{
    private WhirlwindFlight[] _snapshot=[];
    internal static int LoadedFlights{get;private set;}
    public WhirlwindProfiles():base("SanctuaryWhirlwinds",146){}
    public override void Serialize(){_snapshot=BarbarianWhirlwind.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Whirlwind version");}
        var states=JsonSerializer.Deserialize<WhirlwindFlight[]>(reader.ReadString());
        LoadedFlights=states?.Length??0;BarbarianWhirlwind.Restore(states);
    }
}

public static class BarbarianWhirlwind
{
    private static readonly Dictionary<uint,WhirlwindFlight> _flights=new();
    private static PlayerMobile _planning;
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    public static WhirlwindProfiles Profiles{get;private set;}
    public static void Configure()
    {
        Profiles=new();var previous=ClassCombatHooks.DeathAllowed;
        ClassCombatHooks.DeathAllowed=owner=>BeforeDeath(owner) && previous?.Invoke(owner)!=false;
        EventSink.Disconnected+=actor=>{if(actor is PlayerMobile owner && State(owner) is{} state){End(owner,state);}};
    }
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static WhirlwindFlight State(Mobile owner)=>owner!=null?_flights.GetValueOrDefault(owner.Serial.Value):null;
    internal static bool Locked(Mobile owner)=>State(owner) is{} state && owner is{Alive:true,Deleted:false} && owner.Map?.MapID==state.Map
        && (owner.Location==state.Position || ChargeMotion.Active(owner));
    internal static bool Passing(Mobile owner)=>ReferenceEquals(owner,_planning) || Locked(owner);
    internal static bool Owns(SkillLandingReservation marker)=>_flights.TryGetValue(marker.Owner,out var state) && state.Reservation==marker.Serial.Value;
    private static bool BeforeDeath(PlayerMobile owner)
    {
        if(!Locked(owner)){return true;}_flights[owner.Serial.Value]=State(owner) with{DelayedDeath=true};return false;
    }
    internal static int Interval(PlayerMobile owner)
    {
        // Released2.4.3 uses the basic attack effect frame with all equipment IAS,
        // averaging the two weapon frames upward when dual wielding.
        if(WeaponHands.Pair(owner) is not{} pair){return Math.Max(1,CombatTiming.For(owner,CombatAbility.BasicAttack).EffectFrame);}
        int first,second;
        using(WeaponHands.Use(owner,pair.Primary)){first=CombatTiming.For(owner,CombatAbility.BasicAttack).EffectFrame;}
        using(WeaponHands.Use(owner,pair.Secondary)){second=CombatTiming.For(owner,CombatAbility.BasicAttack).EffectFrame;}
        return Math.Max(1,(first+second+1)/2);
    }
    private static bool Weapon(PlayerMobile owner)=>CombatTiming.Equipped(owner)!=null && !ClassGear.Broken(CombatTiming.Equipped(owner)) && PaladinVengeance.WeaponAllowed(owner);
    private static (uint Primary,uint Secondary) Weapons(PlayerMobile owner)=>WeaponHands.Pair(owner) is{} pair
        ?(pair.Primary.Serial.Value,pair.Secondary.Serial.Value):(CombatTiming.Equipped(owner)?.Serial.Value??0,0);
    private static Point3D[] Path(PlayerMobile owner,Point3D aim)
    {
        var origin=owner.Location;int dx=aim.X-origin.X,dy=aim.Y-origin.Y,steps=Math.Max(Math.Abs(dx),Math.Abs(dy));
        var points=new List<Point3D>();var previous=_planning;_planning=owner;
        try
        {
            var last=origin;
            for(int i=1;i<=steps;i++)
            {
                var next=new Point3D(origin.X+(int)Math.Round((double)dx*i/steps),origin.Y+(int)Math.Round((double)dy*i/steps),last.Z);
                var direction=Utility.GetDirection(last,next);
                if(!Server.Movement.Movement.CheckMovement(owner,owner.Map,last,direction,out int z)){break;}
                next=new(next.X,next.Y,z);if(!LeapTerrain.Allowed(owner,owner.Map,next)){break;}
                points.Add(next);last=next;
            }
        }
        finally{_planning=previous;}
        return points.ToArray();
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D aim,long now)
    {
        var action=ClassCombat.For(owner);
        if(!BarbarianCombat.Available(owner,CombatAbility.Whirlwind) || owner is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false,Spell:null}
            || owner.Mounted || !Weapon(owner) || !AlliedOverlap.Allowed(owner) || HitRecovery.Active(owner,now) || State(owner)!=null
            || action.PendingAbility!=CombatAbility.None || now-action.NextAction<0){return false;}
        if(selected!=Serial.Zero)
        {if(World.FindMobile(selected) is not{} target || !ClassCombat.IsHostile(owner,target)){return false;}aim=target.Location;}
        if(!LeapTerrain.Allowed(owner,owner.Map,owner.Location) || !LeapTerrain.Allowed(owner,owner.Map,aim)
            || !owner.InRange(aim,CombatProtocol.SightRange)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Whirlwind);if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        var path=Path(owner,aim);if(path.Length==0){return false;}
        var end=path.Length==0?owner.Location:path[^1];var marker=LandingReservations.Create(owner,owner.Map,end);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){marker.Delete();return false;}
        var weapons=Weapons(owner);
        // Reviewed player launch uses character WalkVelocity6<<8, not RunVelocity
        // or the projectile75% scale. Modern FRW/slow identity remains acceptance work.
        var state=new WhirlwindFlight(owner.Serial.Value,marker.Serial.Value,owner.Map.MapID,owner.Location,owner.Location,path,0,
            weapons.Primary,weapons.Secondary,0,3,false,6*256,0,false,false,TimedEffects.Now.AddMilliseconds(40));
        _flights.Add(owner.Serial.Value,state);action.PendingAbility=CombatAbility.Whirlwind;action.PendingPoint=aim;action.PendingTarget=Serial.Zero;
        action.Animation=11;action.Timing=new(Math.Max(1,path.Length*6),3);action.NextAction=now+40;
        owner.RevealingAction();owner.Direction=owner.GetDirectionTo(aim);owner.PlaySound(0x238); // Identified native spin/sound placeholders.
        ClassCombat.SendAnimation(owner,11,action.Timing.DurationMs);owner.Animate(11,5,1,true,true,0);ClassCombat.SendState(owner,action);return true;
    }
    private static void Clear(PlayerMobile owner,WhirlwindFlight state)
    {
        _flights.Remove(state.Owner);
        if(World.FindMobile((Serial)state.Reservation) is SkillLandingReservation marker && marker.Owner==state.Owner){marker.Delete();}
        if(owner is not{Deleted:false}){return;}var action=ClassCombat.For(owner);
        if(action.PendingAbility==CombatAbility.Whirlwind){action.PendingAbility=CombatAbility.None;action.NextAction=Core.TickCount;}
        owner.NetState?.ResetMovementState();ClassCombat.SendState(owner,action);
    }
    private static void End(PlayerMobile owner,WhirlwindFlight state)
    {
        if(_flights.TryGetValue(state.Owner,out var current) && current.DelayedDeath){state=state with{DelayedDeath=true};}
        Clear(owner,state);if(state.DelayedDeath && owner is{Alive:true,Deleted:false}){owner.Kill();}
    }
    internal static void ExternalMove(PlayerMobile owner)
    {if(!ChargeMotion.Active(owner) && State(owner) is{} state){End(owner,state);}}
    private static Point3D SavedPoint(PlayerMobile owner,WhirlwindFlight state,Map map)
    {
        if(LeapTerrain.Ground(owner,map,state.Position.X,state.Position.Y,state.Position.Z,out var point)){return point;}
        for(int i=Math.Min(state.Index,state.Path.Length)-1;i>=0;i--)
        {var old=state.Path[i];if(LeapTerrain.Ground(owner,map,old.X,old.Y,old.Z,out point)){return point;}}
        if(LeapTerrain.Ground(owner,map,state.Origin.X,state.Origin.Y,state.Origin.Z,out point)){return point;}
        return state.Origin;
    }
    private static void Offline(PlayerMobile owner,WhirlwindFlight state)
    {
        var map=Map.Maps[state.Map];var point=SavedPoint(owner,state,map);owner.LogoutMap=map;owner.LogoutLocation=point;Clear(owner,state);
        if(state.DelayedDeath && owner.Alive)
        {owner.MoveToWorld(point,map);owner.Kill();owner.LogoutMap=map;owner.LogoutLocation=point;owner.Internalize();}
    }
    private static uint Next(PlayerMobile owner,uint previous)
    {
        var candidates=SkillCombat.Targets(owner,owner.Location,SkillGeometry.Tiles(5));
        return PaladinZeal.NextSerial(candidates.Select(x=>x.Serial.Value),previous);
    }
    private static WhirlwindFlight Pulse(PlayerMobile owner,WhirlwindFlight state)
    {
        int hands=WeaponHands.Pair(owner)!=null?2:1;uint previous=ClassCombat.For(owner).PendingTarget.Value;
        for(int i=0;i<hands;i++)
        {
            var weapons=Weapons(owner);
            if(!Weapon(owner) || weapons.Primary!=state.Primary || weapons.Secondary!=state.Secondary){End(owner,state);return state;}
            uint id=Next(owner,previous);previous=id;
            IDisposable scope=null;
            if(WeaponHands.Pair(owner) is{} pair){scope=WeaponHands.Use(owner,state.SecondaryHand?pair.Secondary:pair.Primary);}
            try
            {
                if(id!=0 && World.FindMobile((Serial)id) is{} target)
                {
                    owner.DoHarmful(target,true);var values=SkillMechanics.Values(owner,CombatAbility.Whirlwind);
                    using var attempted=PaladinThorns.Attempt(owner,target,false);
                    if(ClassStats.CheckHit(owner,target,(int)values.Formula("toht"),HitRoll)==true)
                    {GearEffects.Strike(owner,target,ElementRoll(),Effects,skillPhysicalPercent:(int)values.Eval("calc1"),physicalRoll:PhysicalRoll);}
                }
            }
            finally{scope?.Dispose();}
            // No-target do76 still toggles once, then ends the pulse's hand loop.
            state=state with{SecondaryHand=!state.SecondaryHand};if(id==0 || owner.Deleted || !owner.Alive){break;}
        }
        ClassCombat.For(owner).PendingTarget=(Serial)previous;
        if(_flights.TryGetValue(state.Owner,out var current)){state=state with{DelayedDeath=current.DelayedDeath};}
        return state with{Attacked=true,NextHit=state.Frame+Interval(owner)};
    }
    internal static void Tick(DateTime now)
    {
        if(World.Loading){return;}
        foreach(uint id in _flights.Keys.ToArray())
        {
            if(!_flights.TryGetValue(id,out var state)){continue;}
            if(World.FindMobile((Serial)id) is not PlayerMobile{Deleted:false} owner){Clear(null,state);continue;}
            if(owner.Map==Map.Internal && owner.LogoutMap?.MapID==state.Map){Offline(owner,state);continue;}
            if(!owner.Alive){Clear(owner,state);continue;}
            var weapons=Weapons(owner);
            if(owner.Map?.MapID!=state.Map || owner.Location!=state.Position || owner.Frozen || owner.Paralyzed || owner.Spell!=null || !BarbarianCombat.Available(owner,CombatAbility.Whirlwind)
                || !Weapon(owner) || weapons.Primary!=state.Primary || weapons.Secondary!=state.Secondary
                || World.FindMobile((Serial)state.Reservation) is not SkillLandingReservation{Deleted:false} marker || marker.Owner!=state.Owner || marker.Map!=owner.Map
                || marker.Location!=(state.Path.Length==0?state.Origin:state.Path[^1]))
            {End(owner,state);continue;}
            if(now<state.NextUtc){continue;}
            state=state with{Frame=state.Frame+1,NextUtc=now.AddMilliseconds(40),Distance16=state.Distance16+(long)state.Velocity256*8};_flights[id]=state;
            var action=ClassCombat.For(owner);action.PendingAbility=CombatAbility.Whirlwind;action.NextAction=Core.TickCount+40;
            if(state.Frame>=state.NextHit)
            {state=Pulse(owner,state);if(!_flights.ContainsKey(id)){continue;}_flights[id]=state;}
            bool blocked=false;
            while(state.Index<state.Path.Length)
            {
                var next=state.Path[state.Index];int cost=next.X!=owner.X && next.Y!=owner.Y?92682:65536;
                if(state.Distance16<cost){break;}
                if(!ChargeMotion.TryStep(owner,owner.GetDirectionTo(next),p=>p==next && LeapTerrain.Allowed(owner,owner.Map,p),true)){blocked=true;break;}
                if(_flights.TryGetValue(id,out var current)){state=state with{DelayedDeath=current.DelayedDeath};}
                state=state with{Position=owner.Location,Index=state.Index+1,Distance16=state.Distance16-cost};_flights[id]=state;
            }
            if(blocked || state.Index==state.Path.Length)
            {
                // Released short-distance fix guarantees an attempt before ending.
                if(!state.Attacked){state=Pulse(owner,state);if(!_flights.ContainsKey(id)){continue;}_flights[id]=state;}
                End(owner,state);
            }
            else{ClassCombat.SendState(owner,action);}
        }
    }
    public static WhirlwindFlight[] Snapshot()=>_flights.Values.OrderBy(x=>x.Owner).ToArray();
    public static void Restore(WhirlwindFlight[] states)
    {
        _flights.Clear();foreach(var state in states??throw new InvalidDataException("Missing Whirlwind state"))
        {
            if(!((Serial)state.Owner).IsMobile || !((Serial)state.Reservation).IsMobile || state.Owner==state.Reservation || state.Map is <0 or >255
                || Map.Maps[state.Map]==null || state.Path==null || state.Path.Length>CombatProtocol.SightRange || state.Index<0 || state.Index>state.Path.Length
                || state.Frame<0 || state.NextHit<0 || state.Velocity256<=0 || state.Distance16<0 || state.NextUtc.Kind!=DateTimeKind.Utc
                || !_flights.TryAdd(state.Owner,state)){throw new InvalidDataException("Invalid Whirlwind state");}
        }
    }
}
