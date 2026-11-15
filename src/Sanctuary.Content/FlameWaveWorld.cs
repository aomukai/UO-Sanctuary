// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record FlameWaveLane(int Offset,double X,double Y,double Vx,double Vy,DateTime UntilUtc);
// 695/716 makers and their 240 children share position, packet and expiry.
// They are retained as paired graph stages, not independent damage multipliers.
internal sealed record FlameWaveFire(long Id,int Maker,double X,double Y,DateTime CreatedUtc,DateTime UntilUtc);
internal sealed record FlameWaveCast(long Id,uint Owner,int Map,int Z,double X,double Y,int AimX,int AimY,
    FlameWaveBirth Birth,ulong Seed,int Frame,DateTime CreatedUtc,DateTime NextUtc,FlameWaveLane[] Lanes,FlameWaveFire[] Fires);
internal sealed record FlameWaveSave(FlameWaveCast[] Casts,WeaponFlightLease[] Leases,long NextId);
internal readonly record struct FlameWaveAdvance(int Updates,int HeadContacts,int Burns,int Fields,int Damage256,string Fault=null);
public sealed class FlameWaveProfiles:GenericPersistence
{
    public FlameWaveProfiles():base("SanctuaryFlameWave",255){}
    public override void Serialize(IGenericWriter w){w.Write(0);w.Write(JsonSerializer.Serialize(FlameWaveWorld.Snapshot()));}
    public override void Deserialize(IGenericReader r)
    {if(r.ReadInt()!=0){throw new InvalidDataException("Unknown Flame Wave version");}FlameWaveWorld.Restore(JsonSerializer.Deserialize<FlameWaveSave>(r.ReadString()),true);}
}

// Candidate mapping of unavailable modern156/42/38. Never called from learned
// input until the complete world proposal is approved. Uses the existing clock,
// source geometry, fire mitigation, cooldown and fractional resource services.
internal static class FlameWaveWorld
{
    internal static bool Approved=>false;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(FlameWaveWorld));
    private static Dictionary<long,FlameWaveCast> Casts=new();
    private static Dictionary<(long,uint),WeaponFlightLease> Leases=new();
    private static HashSet<long> Loaded=new();
    private static long _next;
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    public static void Configure()=>_=new FlameWaveProfiles();
    internal static FlameWaveSave Snapshot()=>new(Casts.Values.OrderBy(n=>n.Id).ToArray(),Leases.Values.OrderBy(n=>n.Cast).ThenBy(n=>n.Target).ToArray(),_next);
    internal static FlameWaveCast[] For(Mobile p)=>Snapshot().Casts.Where(n=>n.Owner==p.Serial.Value).ToArray();
    internal static void Clear(Mobile p)
    {if(p==null){return;}foreach(var n in For(p)){Casts.Remove(n.Id);}Prune();}
    private static void Prune(){foreach(var k in Leases.Keys.Where(k=>!Casts.ContainsKey(k.Item1)).ToArray()){Leases.Remove(k);}}
    private static PlayerMobile Owner(uint id)=>World.FindMobile((Serial)id)is PlayerMobile{Alive:true,Deleted:false} p
        &&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock?p:null;
    private static Point3D Point(double x,double y,int z)=>new((int)Math.Round(x),(int)Math.Round(y),z);
    internal static bool Aim(PlayerMobile p,Serial selected,ref Point3D point)=>p is{Map:not null}&&Owner(p.Serial.Value)==p
        &&MiasmaWorld.Fighting(p,p.Map.MapID)&&ShapeRestrictions.Allowed(p,CombatAbility.FlameWave)&&LobProjectiles.Aim(p,selected,ref point)
        &&(point.X!=p.X||point.Y!=p.Y);
    internal static long ReleasePrepared(PlayerMobile p,Point3D point,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||at<=DateTime.UnixEpoch){throw new ArgumentException("Flame Wave clock must be UTC");}
        if(!Aim(p,Serial.Zero,ref point)||!FireGround.Ready(p,CombatAbility.FlameWave,at)){return 0;}
        var b=FlameWaveBirths.Capture(p);if(!NecromancerEffects.CanPay(p,b.Numbers.Mana256)){return 0;}
        long id=checked(_next+1);var n=new FlameWaveCast(id,p.Serial.Value,p.Map.MapID,p.Z,p.X,p.Y,point.X-p.X,point.Y-p.Y,b,
            SourceRandom.InitLow(SeedLow()),0,at,at.AddMilliseconds(40),[],[]);Validate(n);
        if(!NecromancerEffects.Pay(p,b.Numbers.Mana256)){return 0;}
        _next=id;Casts.Add(id,n);FireGround.Delay(p,CombatAbility.FlameWave,at);p.PlaySound(0x1E5);return id; // Cast placeholder.
    }
    private static bool ClearSegment(Map map,double x,double y,double endX,double endY,int z)
    {
        var previous=Point(x,y,z);int count=Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(endX-x),Math.Abs(endY-y))*2));
        for(int i=0;i<=count;i++)
        {
            var p=Point(x+(endX-x)*i/count,y+(endY-y)*i/count,z);
            if(p.X<0||p.Y<0||p.X>=map.Width||p.Y>=map.Height||LobProjectiles.Barrier(map,p)
                ||!map.LineOfSight(new Point3D(previous.X,previous.Y,z+12),new Point3D(p.X,p.Y,z+12))){return false;}previous=p;
        }
        return true;
    }
    private static FlameWaveLane[] Launch(FlameWaveCast n,PlayerMobile p,DateTime at)
    {
        int speed=SlowMissilePayload.VelocityFp8(24,0,n.Birth.Numbers.Rank,33,0,true,n.Birth.Slow.VelocityPercent);
        var v=MissileVectors.VelocityFp8(n.AimX,n.AimY,speed);var d=MissileVectors.Direction(n.AimX,n.AimY);var lanes=new List<FlameWaveLane>();
        for(int offset=-n.Birth.Numbers.HalfWidth;offset<=n.Birth.Numbers.HalfWidth;offset++)
        {
            double x=n.X-offset*d.Y/8192.0,y=n.Y+offset*d.X/8192.0;
            if(ClearSegment(p.Map,n.X,n.Y,x,y,n.Z)){lanes.Add(new(offset,x,y,v.X,v.Y,at.AddMilliseconds(n.Birth.Numbers.WaveFrames*40L)));}
        }
        return lanes.ToArray();
    }
    private static bool Touches(BaseCreature t,double x,double y,int size)=>CombatHitboxes.SourceTouches(size,CreatureProfiles.For(t).Traits.Size??2,
        (int)Math.Round(x*2)-t.X*2,(int)Math.Round(y*2)-t.Y*2);
    private static IEnumerable<BaseCreature> Nearby(PlayerMobile p,double x,double y,int z)=>SkillCombat.NearbySnapshot<BaseCreature>(p.Map,Point(x,y,z),3)
        .Where(t=>MiasmaWorld.Enemy(p,t,z)).OrderBy(t=>t.Serial.Value);
    internal static void Tick(DateTime at)
    {
        if(World.Loading||!Approved){return;}
        foreach(uint owner in Casts.Values.Select(n=>n.Owner).Distinct().ToArray())
        {var r=TickPrepared(at,onlyOwner:owner);if(r.Fault!=null){Log.Warning("Flame Wave stopped unsupported graph: {Reason}",r.Fault);}}
    }
    internal static FlameWaveAdvance TickPrepared(DateTime at,bool? actions=null,uint? onlyOwner=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Flame Wave clock must be UTC");}
        int updates=0,heads=0,burns=0,fields=0,damage=0;
        while(true)
        {
            var n=Casts.Values.Where(n=>(!onlyOwner.HasValue||n.Owner==onlyOwner)&&n.NextUtc<=at).OrderBy(n=>n.NextUtc).ThenBy(n=>n.Id).FirstOrDefault();if(n==null){break;}
            var p=Owner(n.Owner);if(p==null){Casts.Remove(n.Id);continue;}
            bool overdueLoad=Loaded.Remove(n.Id)&&n.NextUtc<at;
            if(overdueLoad||!MiasmaWorld.Fighting(p,n.Map,actions)||Math.Abs(p.Z-n.Z)>=16)
            {
                long frames=(at-n.NextUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;
                var parked=n with{Frame=checked(n.Frame+(int)frames),NextUtc=n.NextUtc.AddMilliseconds(frames*40),Lanes=n.Lanes.Where(l=>l.UntilUtc>at).ToArray(),Fires=n.Fires.Where(f=>f.UntilUtc>at).ToArray()};
                Store(parked);continue; // Missed emissions and burns never replay; UTC expiry is unchanged.
            }
            try
            {
                var r=Step(n,p);updates++;heads+=r.HeadContacts;burns+=r.Burns;fields+=r.Fields;damage=(int)Math.Min(int.MaxValue,(long)damage+r.Damage256);
            }
            catch(NotSupportedException e){Casts.Remove(n.Id);Prune();return new(updates,heads,burns,fields,damage,e.Message);}
        }
        Prune();return new(updates,heads,burns,fields,damage);
    }
    private static void Store(FlameWaveCast n)
    {if(n.Frame>=n.Birth.Numbers.DelayFrames&&n.Lanes.Length==0&&n.Fires.Length==0){Casts.Remove(n.Id);}else{Casts[n.Id]=n;}}
    private static FlameWaveAdvance Step(FlameWaveCast n,PlayerMobile p)
    {
        DateTime at=n.NextUtc;int frame=n.Frame+1,heads=0,burns=0,created=0,damage=0;ulong seed=n.Seed;
        if(frame<=((n.Birth.Numbers.HalfWidth+1)/2))
        {
            var d=MissileVectors.Direction(n.AimX,n.AimY);int width=Math.Min(n.Birth.Numbers.HalfWidth,frame*2);
            for(int i=-width;i<=width;i++){Effects.SendLocationEffect(Point(n.X-i*d.Y/8192.0,n.Y+i*d.X/8192.0,n.Z),p.Map,0x36BD,2,10,0x489,0);}
        } // 708 expanding-wall placeholder; visual controller causes no extra hit.
        var fires=n.Fires.Where(f=>f.UntilUtc>at).ToList();var lanes=new List<FlameWaveLane>();
        var source=frame==n.Birth.Numbers.DelayFrames?Launch(n,p,at):n.Lanes;
        var ready=new HashSet<uint>();
        foreach(var lane in source.OrderBy(l=>l.Offset))
        {
            if(at>=lane.UntilUtc){continue;}double x=lane.X+lane.Vx,y=lane.Y+lane.Vy;
            var trace=SourceMissileSteps.Crossings((long)Math.Round(lane.X*131072+32768),(long)Math.Round(lane.Y*131072+32768),
                (int)Math.Round(lane.Vx*131072),(int)Math.Round(lane.Vy*131072));bool wall=false;double lastX=lane.X,lastY=lane.Y;
            foreach(var cell in trace.Cells)
            {
                double cx=cell.X/2.0,cy=cell.Y/2.0;
                if(!ClearSegment(p.Map,lastX,lastY,cx,cy,n.Z)){wall=true;break;}lastX=cx;lastY=cy;
                foreach(var t in Nearby(p,cx,cy,n.Z).Where(t=>Touches(t,cx,cy,1)))
                {if(!Leases.TryGetValue((n.Id,t.Serial.Value),out var lease)||lease.UntilUtc<=at){ready.Add(t.Serial.Value);}}
                // Reuse the do6 changed-path creation condition. Emit at unique
                // crossed source cells; offset makers coalesce at the same cell.
                var plan=MoltenBoulderPackets.Trail(true,true,true,true,240,8,cell.X,cell.Y,n.Birth.Numbers.Rank,1);
                if(plan.Requests.Length>0)
                {
                    foreach(int maker in new[]{695,716})
                    {
                        if(fires.Any(f=>f.Maker==maker&&f.X==cx&&f.Y==cy)){continue;}
                        int lifetime=maker==695?n.Birth.Numbers.TailFrames:n.Birth.Numbers.LingerFrames;
                        fires.Add(new(checked(++_next),maker,cx,cy,at,at.AddMilliseconds(lifetime*40L)));created++;
                    }
                }
            }
            if(!wall&&ClearSegment(p.Map,lastX,lastY,x,y,n.Z)){lanes.Add(lane with{X=x,Y=y});}
            else{Effects.SendLocationEffect(Point(lastX,lastY,n.Z),p.Map,0x36BD,3,10,0x489,0);} // fireexplode placeholder, no bonus explosion damage.
        }
        // Commit movement, RNG-independent topology and leases before callbacks.
        foreach(uint id in ready){Leases[(n.Id,id)]=new(n.Id,id,at.AddMilliseconds(600));}
        var current=n with{Frame=frame,NextUtc=at.AddMilliseconds(40),Lanes=lanes.ToArray(),Fires=fires.ToArray()};Casts[n.Id]=current;
        var head=FlameWaveBirths.Endpoints(n.Birth,false);var ground=FlameWaveBirths.Endpoints(n.Birth,true);
        foreach(uint id in ready.OrderBy(i=>i))
        {
            if(World.FindMobile((Serial)id)is not BaseCreature t||!MiasmaWorld.Enemy(p,t,n.Z)){continue;}
            TaintedFireBallMissiles.RequireOrdinaryRecipient(p,t);int raw=SorceressSpellMissiles.Roll(head.Min,head.Max,seed,out seed);
            damage=(int)Math.Min(int.MaxValue,(long)damage+SorceressSpellMissiles.ImpactPrepared(p,t,"flamewave","fire",raw,n.Birth.Modifiers,at,missileReaction:true));heads++;
        }
        // One 240 packet per recipient per cast/update, even where tail, static
        // and adjacent lanes overlap. Separate paid casts remain independent.
        var touched=new HashSet<uint>();
        foreach(var f in fires.Where(f=>f.CreatedUtc<at).OrderBy(f=>f.Id))
        {
            foreach(var t in Nearby(p,f.X,f.Y,n.Z).Where(t=>Touches(t,f.X,f.Y,2)))
            {
                if(!touched.Add(t.Serial.Value)){continue;}if(!SourceFireFields.Supported(p,t,out string why)){SourceFireFields.Gap(why);continue;}
                var packet=SourceFireFields.Roll(ground.Min,ground.Max,ref seed);int hp=SkillHealth.Balance256(t);
                SourceFireFields.Deliver(p,t,packet,n.Birth.Modifiers,at,41);damage=(int)Math.Min(int.MaxValue,(long)damage+Math.Max(0,hp-SkillHealth.Balance256(t)));burns++;
            }
            if(frame%5==0){Effects.SendLocationEffect(Point(f.X,f.Y,n.Z),p.Map,0x398C,3,10,0x489,0);} // Ground flame placeholder.
        }
        if(Casts.ContainsKey(n.Id)){Store(current with{Seed=seed});}return new(1,heads,burns,created,damage);
    }
    private static bool Utc(DateTime at)=>at.Kind==DateTimeKind.Utc&&at>DateTime.UnixEpoch;
    private static bool Position(int map,int z,double x,double y)=>map>=0&&map<Map.Maps.Length&&Map.Maps[map]!=null&&Map.Maps[map]!=Map.Internal
        &&z is >=-128 and <=127&&double.IsFinite(x+y)&&x>=0&&y>=0&&x<Map.Maps[map].Width&&y<Map.Maps[map].Height;
    private static void Validate(FlameWaveCast n)
    {
        if(n==null||n.Id<=0||n.Owner==0||!((Serial)n.Owner).IsMobile||!Position(n.Map,n.Z,n.X,n.Y)||n.AimX==0&&n.AimY==0
            ||Math.Abs((long)n.AimX)>80||Math.Abs((long)n.AimY)>80||n.Seed==0||n.Frame<0||!Utc(n.CreatedUtc)||!Utc(n.NextUtc)
            ||n.NextUtc!=n.CreatedUtc.AddMilliseconds((n.Frame+1L)*40)||n.Lanes==null||n.Fires==null)
        {throw new InvalidDataException("Invalid Flame Wave cast");}FlameWaveBirths.Validate(n.Birth);
        if(n.Lanes.Length>n.Birth.Numbers.HalfWidth*2+1||n.Lanes.Select(l=>l.Offset).Distinct().Count()!=n.Lanes.Length){throw new InvalidDataException("Invalid Flame Wave lane count");}
        foreach(var l in n.Lanes)
        {
            int speed=SlowMissilePayload.VelocityFp8(24,0,n.Birth.Numbers.Rank,33,0,true,n.Birth.Slow.VelocityPercent);var v=MissileVectors.VelocityFp8(n.AimX,n.AimY,speed);
            if(l.Offset < -n.Birth.Numbers.HalfWidth||l.Offset>n.Birth.Numbers.HalfWidth||!Position(n.Map,n.Z,l.X,l.Y)||(l.Vx,l.Vy)!=v
                ||l.UntilUtc!=n.CreatedUtc.AddMilliseconds((n.Birth.Numbers.DelayFrames+n.Birth.Numbers.WaveFrames)*40L))
            {throw new InvalidDataException("Invalid Flame Wave lane");}
        }
        foreach(var f in n.Fires)
        {
            if(f.Id<=0||f.Maker is not(695 or 716)||!Position(n.Map,n.Z,f.X,f.Y)||f.X*2!=Math.Floor(f.X*2)||f.Y*2!=Math.Floor(f.Y*2)
                ||!Utc(f.CreatedUtc)||f.CreatedUtc<n.CreatedUtc||f.CreatedUtc>=n.NextUtc||!Utc(f.UntilUtc)
                ||f.UntilUtc!=f.CreatedUtc.AddMilliseconds((f.Maker==695?n.Birth.Numbers.TailFrames:n.Birth.Numbers.LingerFrames)*40L))
            {throw new InvalidDataException("Invalid Flame Wave maker/child");}
        }
    }
    internal static void Restore(FlameWaveSave saved,bool loaded=false)
    {
        if(saved?.Casts==null||saved.Leases==null||saved.NextId<0){throw new InvalidDataException("Missing Flame Wave graph");}
        var casts=new Dictionary<long,FlameWaveCast>();var leases=new Dictionary<(long,uint),WeaponFlightLease>();var ids=new HashSet<long>();
        foreach(var n in saved.Casts)
        {Validate(n);if(!ids.Add(n.Id)||!casts.TryAdd(n.Id,n)){throw new InvalidDataException("Duplicate Flame Wave cast");}foreach(var f in n.Fires){if(!ids.Add(f.Id)){throw new InvalidDataException("Duplicate Flame Wave child");}}}
        foreach(var l in saved.Leases)
        {if(l==null||!casts.ContainsKey(l.Cast)||l.Target==0||!((Serial)l.Target).IsMobile||!Utc(l.UntilUtc)||!leases.TryAdd((l.Cast,l.Target),l)){throw new InvalidDataException("Invalid Flame Wave lease");}}
        if(ids.Any(id=>id>saved.NextId)){throw new InvalidDataException("Flame Wave identity rollback");}
        Casts=casts;Leases=leases;Loaded=loaded?casts.Keys.ToHashSet():new();_next=saved.NextId; // Atomic full-graph admission.
    }
}
