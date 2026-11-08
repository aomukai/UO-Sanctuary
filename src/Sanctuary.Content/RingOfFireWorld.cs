// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record RingOfFireFlight(long Id,long Cast,uint Owner,RingOfFireMotionState Motion,RingOfFireBirth Birth,
    ulong Seed,RingOfFirePacket Packet,uint LastCollision,DateTime NextUtc,string Fault=null);
internal sealed record RingOfFireWorldSave(RingOfFireFlight[] Flights,long NextId,WeaponFlightLease[] Leases);
public sealed class RingOfFireWorldProfiles:GenericPersistence
{
    private RingOfFireWorldSave _snapshot;
    public RingOfFireWorldProfiles():base("SanctuaryRingOfFireWorld",249){}
    public override void Serialize(){_snapshot=RingOfFireWorld.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Ring world save");}RingOfFireWorld.Restore(JsonSerializer.Deserialize<RingOfFireWorldSave>(reader.ReadString()));}
}

// Candidate world composition, not admitted through WarlockCombat. Contact,
// delay and lifecycle rules are an explicit pending Sanctuary proposal, not
// claimed current hit1 semantics. Reuse the verified launch/motion/age kernels.
internal static class RingOfFireWorld
{
    private static ImmutableDictionary<long,RingOfFireFlight> _flights=ImmutableDictionary<long,RingOfFireFlight>.Empty;
    private static Dictionary<(long,uint),WeaponFlightLease> _leases=new();
    private static readonly HashSet<long> Loaded=new();
    private static long _nextId;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(RingOfFireWorld));
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    public static void Configure()=>_=new RingOfFireWorldProfiles();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    private static bool Owner(PlayerMobile p)=>p is{Alive:true,Deleted:false}&&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock;
    private static bool Ready(long cast,uint target,DateTime at)=>!_leases.TryGetValue((cast,target),out var lease)||lease.UntilUtc<=at;
    private static void Lease(long cast,uint target,DateTime at)=>_leases[(cast,target)]=new(cast,target,at.AddMilliseconds(440));
    internal static long CreatePaidCandidate(PlayerMobile owner,SkillValues values,DateTime at,Func<RingOfFireLaunchRequest,bool> allocate=null)
    {
        if(!Owner(owner)||owner.Map==null||owner.Map==Map.Internal||values?.Ability!=CombatAbility.RingOfFire||at.Kind!=DateTimeKind.Utc
            ||at<=DateTime.UnixEpoch||at>DateTime.MaxValue.AddMilliseconds(-440)||MinionSafeAreas.Contains(owner)||!NecromancerEffects.CanPay(owner,values.Mana256)){return 0;}
        var birth=RingOfFireDamage.Birth(values,DruidFirestorm.Modifiers(owner));
        // Native host has no equivalent opaque unit+12c field;zero is explicit
        // in the candidate projection. Failed allocation publishes nothing.
        var plan=RingOfFireMotion.Prepare(owner,values,0,allocate);
        if(plan.Motions.Length==0){return 0;}
        long cast=checked(_nextId+1);var staged=new List<RingOfFireFlight>();
        foreach(var motion in plan.Motions)
        {
            var s=new RingOfFireFlight(checked(cast+staged.Count),cast,owner.Serial.Value,motion,birth,SourceRandom.InitLow(SeedLow()),null,owner.Serial.Value,at.AddMilliseconds(40));
            Validate(s);staged.Add(s);
        }
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return 0;}
        foreach(var s in staged){_flights=_flights.Add(s.Id,s);}_nextId=staged[^1].Id;return cast;
    }
    private static Point3D Center(RingOfFireMotionState motion)=>new((int)Math.Round(motion.X),(int)Math.Round(motion.Y),motion.Z);
    private static bool Inside(RingOfFireMotionState m)=>m.X>=0&&m.Y>=0&&m.X<Map.Maps[m.Map].Width&&m.Y<Map.Maps[m.Map].Height;
    private static bool Recipient(PlayerMobile owner,BaseCreature target,RingOfFireFlight s,Point3D center,int x,int y)
        =>SourceDamageAreas.Recipient(owner,target,s.Motion.Map,x,y,center.Z,3,true,center)&&Ready(s.Cast,target.Serial.Value,s.NextUtc);
    private static RingOfFireFlight Explosion(PlayerMobile owner,RingOfFireFlight state,RingOfFireMotionState location)
    {
        if(state.Packet!=null){return state;}
        // Same source-subtile/native-LOS projection as Tainted Fire Ball;
        // retain the subtile center for the actual circle membership check.
        int x=(int)Math.Floor(location.X*2+.5),y=(int)Math.Floor(location.Y*2+.5);
        var center=new Point3D((x+1)/2,(y+1)/2,location.Z);
        var map=Map.Maps[state.Motion.Map];var targets=new List<BaseCreature>();
        foreach(var t in map.GetMobilesInRange<BaseCreature>(center,2))
        {if(Recipient(owner,t,state,center,x,y)){TaintedFireBallMissiles.RequireOrdinaryRecipient(owner,t);targets.Add(t);}}
        ulong seed=state.Seed;var packet=RingOfFireDamage.RollBorn(owner,state.Birth,ref seed);state=state with{Packet=packet,Seed=seed};
        foreach(var t in targets)
        {if(Recipient(owner,t,state,center,x,y)){RingOfFireDamage.DeliverPrepared(owner,t,packet,state.Motion.Frame,state.NextUtc);Lease(state.Cast,t.Serial.Value,state.NextUtc);}}
        Effects.SendLocationEffect(center,map,0x36BD,8,10,0x489,0); // Identified candidate explosion placeholder.
        return state;
    }
    private static void Advance(RingOfFireFlight state,bool fighting)
    {
        var owner=World.FindMobile((Serial)state.Owner)as PlayerMobile;var m=state.Motion;var map=Map.Maps[m.Map];
        if(!Owner(owner)||owner.Map!=Map.Internal&&owner.Map!=map){Remove(state.Id);return;}
        var next=state;bool removed=false,barrier=false;
        int Move()
        {
            var moved=RingOfFireMotion.Step(m);next=next with{Motion=moved};
            barrier=moved.X<0||moved.Y<0||moved.X>=map.Width||moved.Y>=map.Height;return barrier?2:1;
        }
        Mobile Find(SourceMissileCell cell)
        {
            foreach(var t in map.GetMobilesInRange<Mobile>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,m.Z),3))
            {
                if(t is not{Alive:true,Deleted:false,Blessed:false}||Math.Abs(t.Z-m.Z)>=16||t.Serial.Value==state.LastCollision
                    ||!Ready(state.Cast,t.Serial.Value,state.NextUtc)||!ClassCombat.IsHostile(owner,t)||!owner.CanBeHarmful(t,false)||SpellHelper.IsTown(t.Location,owner)){continue;}
                int size=t is BaseCreature b?CreatureProfiles.For(b).Traits.Size??2:2;
                if(!CombatHitboxes.SourceTouches(2,size,cell.X-t.X*2,cell.Y-t.Y*2)){continue;}
                TaintedFireBallMissiles.RequireOrdinaryRecipient(owner,t);return t;
            }
            return null;
        }
        bool Wall(SourceMissileCell cell)=>DruidFirestorm.CellBlocked(map,m.Z,m.X,m.Y,cell.X,cell.Y);
        int Hit(uint serial,bool terminal)
        {
            var location=barrier?m:next.Motion;
            if(fighting){next=Explosion(owner,next,location);}
            if(terminal){removed=true;return 2;}
            if(World.FindMobile((Serial)serial)is BaseCreature target)
            {
                // One source chance draw even at100%;movement eligibility is
                // the shared native monster rule,independent of fire damage.
                ulong seed=next.Seed;_=SourceRandom.Limited(ref seed,100);next=next with{Seed=seed,LastCollision=serial};
                Lease(state.Cast,serial,state.NextUtc);if(fighting){_=SourceKnockbacks.Begin(owner,target);}
            }
            return 1; // Candidate pierces;later contacts cannot explode again.
        }
        bool active=fighting&&state.Fault==null&&owner.Map==map&&!MinionSafeAreas.Contains(owner);
        if(!active)
        {
            var moved=RingOfFireMotion.Step(m);if(moved.Frame>=11||!Inside(moved)){Remove(state.Id);return;}
            _flights=_flights.SetItem(state.Id,state with{Motion=moved,NextUtc=state.NextUtc.AddMilliseconds(40)});return;
        }
        var result=BasicMissileUpdate.Run(new(true,true,m.VelocityFp8,11-m.Frame,2,11,true),new(Move,_=>{},()=>true,()=>true,()=>2,()=>next.Motion.Steps.Cells,
            c=>Wall(c)?4:Find(c)!=null?1:0,c=>Wall(c)?0:Find(c)?.Serial.Value??0,Hit));
        if(removed||barrier||result.Result==2){Remove(state.Id);return;}
        if(!_flights.ContainsKey(state.Id)){return;}
        _flights=_flights.SetItem(state.Id,next with{NextUtc=state.NextUtc.AddMilliseconds(40)});
        if(next.Motion.Frame%2==0&&state.Id%4==0){Effects.SendLocationEffect(Center(next.Motion),map,0x36D4,2,10,0x489,0);} // Identified sparse travel placeholder.
    }
    internal static void Tick(DateTime at,bool? actions=null,long? onlyCast=null)
    {
        if(World.Loading){return;}if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Ring clock must be UTC");}
        while(true)
        {
            var s=_flights.Values.Where(f=>f.NextUtc<=at&&(!onlyCast.HasValue||f.Cast==onlyCast.Value)).OrderBy(f=>f.NextUtc).ThenBy(f=>f.Id).FirstOrDefault();if(s==null){break;}
            var owner=World.FindMobile((Serial)s.Owner)as PlayerMobile;
            if(!Owner(owner)||owner.Map!=Map.Internal&&owner.Map?.MapID!=s.Motion.Map){Remove(s.Id);continue;}
            bool loaded=Loaded.Remove(s.Id);
            if(loaded)
            {
                // Offline elapsed updates never replay hits after reload.
                int due=(int)Math.Min(11-s.Motion.Frame,(at-s.NextUtc).Ticks/TimeSpan.TicksPerMillisecond/40+1);var m=s.Motion;
                for(int i=0;i<due;i++){m=RingOfFireMotion.Step(m);}
                if(m.Frame>=11||!Inside(m)){Remove(s.Id);}else{_flights=_flights.SetItem(s.Id,s with{Motion=m,NextUtc=s.NextUtc.AddMilliseconds(40L*due)});}continue;
            }
            try{Advance(s,actions??AssassinTrapRoster.Fighting(s.Owner));}
            catch(NotSupportedException e)
            {
                Log.Error(e,"Candidate Ring suspended: {Id}",s.Id);
                var m=RingOfFireMotion.Step(s.Motion);if(m.Frame>=11||!Inside(m)){Remove(s.Id);}else{_flights=_flights.SetItem(s.Id,s with{Motion=m,NextUtc=s.NextUtc.AddMilliseconds(40),Fault=e.Message});}
            }
        }
        var casts=_flights.Values.Select(s=>s.Cast).ToHashSet();foreach(var k in _leases.Where(p=>p.Value.UntilUtc<=at||!casts.Contains(p.Key.Item1)).Select(p=>p.Key).ToArray()){_leases.Remove(k);}
    }
    private static void Remove(long id){_flights=_flights.Remove(id);Loaded.Remove(id);}
    internal static void Clear(PlayerMobile owner)
    {foreach(var s in _flights.Values.Where(s=>s.Owner==owner.Serial.Value).ToArray()){Remove(s.Id);}var casts=_flights.Values.Select(s=>s.Cast).ToHashSet();foreach(var key in _leases.Keys.Where(k=>!casts.Contains(k.Item1)).ToArray()){_leases.Remove(key);}}
    private static RingOfFireFlight Copy(RingOfFireFlight s)=>s with{Motion=s.Motion with{Steps=new(s.Motion.Steps.Cells.ToArray())}};
    internal static RingOfFireWorldSave Snapshot()=>new(_flights.Values.OrderBy(s=>s.Id).Select(Copy).ToArray(),_nextId,_leases.Values.OrderBy(s=>s.Cast).ThenBy(s=>s.Target).ToArray());
    private static void Validate(RingOfFireFlight s)
    {
        var m=s?.Motion;
        if(s==null||s.Id<=0||s.Cast<=0||s.Cast>s.Id||!((Serial)s.Owner).IsMobile||m==null||m.Request is not{Flags:7,Missile:689,Skill:394,Context12c:0}
            ||m.Request.Caster!=s.Owner||s.Birth is not{Rank:>=1 and <=255,Minimum256:>=0,Modifiers:not null}||s.Birth.Maximum256<s.Birth.Minimum256||m.Request.Rank!=s.Birth.Rank
            ||m.Map<0||m.Map>=Map.Maps.Length||Map.Maps[m.Map]==null||Map.Maps[m.Map]==Map.Internal||m.Frame is <0 or >=11||m.Z is <-128 or >127
            ||!double.IsFinite(m.X+m.Y)||m.X<0||m.Y<0||m.X>=Map.Maps[m.Map].Width||m.Y>=Map.Maps[m.Map].Height
            ||m.VelocityFp8!=4800-1000*(m.Frame/5)||m.AccelerationCounter!=m.Frame%5||m.Steps?.Cells is not{Length:>=1 and <=10}
            ||m.Steps.Cells.Any(c=>c.X<0||c.Y<0||c.X>=Map.Maps[m.Map].Width*2||c.Y>=Map.Maps[m.Map].Height*2)
            ||s.LastCollision!=0&&!((Serial)s.LastCollision).IsMobile||s.NextUtc.Kind!=DateTimeKind.Utc||s.NextUtc<=DateTime.UnixEpoch||s.NextUtc>DateTime.MaxValue.AddMilliseconds(-440))
        {throw new InvalidDataException("Invalid candidate Ring flight");}
        if(s.Packet!=null)
        {
            RingOfFireDamage.Validate(s.Packet);if(s.Packet.Owner!=s.Owner||s.Packet.Rank!=s.Birth.Rank||s.Packet.Minimum256!=s.Birth.Minimum256||s.Packet.Maximum256!=s.Birth.Maximum256||s.Packet.Modifiers!=s.Birth.Modifiers)
            {throw new InvalidDataException("Changed candidate Ring birth packet");}
        }
    }
    internal static void Restore(RingOfFireWorldSave saved)
    {
        if(saved?.Flights==null||saved.Leases==null||saved.NextId<0){throw new InvalidDataException("Missing candidate Ring save");}
        var flights=ImmutableDictionary<long,RingOfFireFlight>.Empty;var leases=new Dictionary<(long,uint),WeaponFlightLease>();
        foreach(var s in saved.Flights){Validate(s);if(s.Id>saved.NextId||flights.ContainsKey(s.Id)){throw new InvalidDataException("Duplicate candidate Ring flight");}flights=flights.Add(s.Id,Copy(s));}
        foreach(var l in saved.Leases){if(l.Cast<=0||l.Cast>saved.NextId||!((Serial)l.Target).IsMobile||l.UntilUtc.Kind!=DateTimeKind.Utc||!leases.TryAdd((l.Cast,l.Target),l)){throw new InvalidDataException("Invalid candidate Ring lease");}}
        _flights=flights;_leases=leases;_nextId=saved.NextId;Loaded.Clear();foreach(long id in flights.Keys){Loaded.Add(id);}
    }
}
