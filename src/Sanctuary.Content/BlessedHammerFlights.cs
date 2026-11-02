// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

public sealed record HammerMotion(ulong Seed,ActorMotionState Path,string Fault=null)
{
    public bool Equals(HammerMotion other)=>other!=null&&Seed==other.Seed&&Fault==other.Fault
        &&(Path==null||other.Path==null?Path==other.Path:
            Path with{Points=null,SavedSteps=null}==other.Path with{Points=null,SavedSteps=null}
            &&Path.Points.SequenceEqual(other.Path.Points)&&Path.SavedSteps.SequenceEqual(other.Path.SavedSteps));
    public override int GetHashCode()
    {if(Path==null){return HashCode.Combine(Seed,Fault);}var h=new HashCode();h.Add(Seed);h.Add(Fault);h.Add(Path with{Points=null,SavedSteps=null});foreach(var p in Path.Points){h.Add(p);}foreach(var p in Path.SavedSteps){h.Add(p);}return h.ToHashCode();}
}

// Hammer shares the complete path step/update kernel,not straight-bolt motion.
// Its older1.10outer branch moves missiles;the reconstructed1.13guard conflicts
// with that use. Modern outer dispatch/Fog/room/candidate services stay explicit.
internal static class BlessedHammerFlights
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BlessedHammerFlights));
    private static readonly HashSet<long> Restored=new();
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    public static void Configure()=>EventSink.WorldLoad+=RestoreReferences;
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static void Loaded(IEnumerable<long> ids){Restored.Clear();foreach(long id in ids){Restored.Add(id);}}
    internal static void Forget(long id)=>Restored.Remove(id);
    internal static void Cancel(Mobile actor)
    {if(actor!=null){foreach(var f in BlessedHammers.Snapshot().Where(f=>f.Owner==actor.Serial.Value&&f.Motion!=null)){BlessedHammers.Remove(f.Id);}}}
    internal static void RestoreReferences()
    {
        foreach(var f in BlessedHammers.Snapshot().Where(f=>f.Motion!=null&&f.LastCollision!=0&&World.FindMobile((Serial)f.LastCollision)==null))
        {BlessedHammers.Update(f with{LastCollision=0});Log.Information("Hammer cleared absent saved collider:{Flight}/{Collider}",f.Id,f.LastCollision);}
    }
    internal static HammerMotion Prepare(PlayerMobile p)
    {
        if(p is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(p.Serial,out var profile)||profile.Class!=DiabloClass.Paladin
            ||p.Map==null||p.Map==Map.Internal||p.Z is <-128 or >127||SpellHelper.IsTown(p.Location,p))
        {throw new NotSupportedException("Unsupported Hammer source owner");}
        var points=BlessedHammers.SourcePoints(p.X,p.Y);
        if(points.Any(q=>q.X<=0||q.Y<=0||q.X>=p.Map.Width*2||q.Y>=p.Map.Height*2||q.X>65535||q.Y>65535))
        {throw new NotSupportedException("Hammer source curve crosses an unsupported map boundary");}
        var path=new ActorMotionState((uint)(p.X*131072+32768),(uint)(p.Y*131072+32768),points,points[^1],0,3456,7680,250,0,new(0,0),new(0,0),0,0x60020,0,[],0,14);
        return new(SourceRandom.InitLow(SeedLow()),SourceActorMotion.DirectionToNext(path));
    }
    internal static void Validate(HammerFlight f)
    {
        if(f?.Motion is not{Path:not null} m||f.Captured is not{Modifiers:not null} c||f.Id<=0||f.Frame is <0 or >=120||!((Serial)f.Owner).IsMobile||f.Rank is <1 or >255
            ||f.Map<0||f.Map>=Map.Maps.Length||Map.Maps[f.Map]==null||Map.Maps[f.Map]==Map.Internal||f.Z is <-128 or >127
            ||f.OriginX<=0||f.OriginY<=0||f.OriginX>=Map.Maps[f.Map].Width||f.OriginY>=Map.Maps[f.Map].Height
            ||f.LastCollision!=0&&!((Serial)f.LastCollision).IsMobile||f.NextUtc.Kind!=DateTimeKind.Utc||f.UntilUtc.Kind!=DateTimeKind.Utc
            ||f.NextUtc>DateTime.MaxValue.AddMilliseconds(-4800)||m.Fault?.Length>4096||c.Modifiers.Potion!=0||c.Slow.VelocityPercent!=100||c.Slow.DamagePercent is <0 or >100
            ||m.Path.PathType!=14||m.Path.Points is not{Length:77}||m.Path.SavedSteps==null||m.Path.Index!=f.Point||m.Path.Index is <0 or >=77
            ||m.Path.MaximumVelocity!=7680||m.Path.Velocity!=f.VelocityQ8||m.Path.Acceleration!=f.AccelerationQ8||m.Path.AccelerationCounter!=f.AccelerationCounter
            ||m.Path.Velocity is <3456 or >7680||m.Path.Acceleration is not(0 or 250)||m.Path.AccelerationCounter is <0 or >4
            ||m.Path.Flags is not(0x60020 or 0x60028)||m.Path.Distance!=0||m.Path.CollidedMask!=0||m.Path.StepsCount!=m.Path.SavedSteps.Length||m.Path.StepsCount is <0 or >10
            ||m.Path.X!=(long)f.OriginX*131072+32768+f.XFp16||m.Path.Y!=(long)f.OriginY*131072+32768+f.YFp16
            ||m.Path.X>=Map.Maps[f.Map].Width*131072L||m.Path.Y>=Map.Maps[f.Map].Height*131072L
            ||m.Path.SavedSteps.Any(q=>q.X<0||q.Y<0||q.X>=Map.Maps[f.Map].Width*2||q.Y>=Map.Maps[f.Map].Height*2))
        {throw new InvalidDataException("Invalid Hammer source motion");}
        bool curve=true;for(int i=0;i<77;i++){var q=BlessedHammers.SourcePoint(i);curve&=m.Path.Points[i]==new Point2D(f.OriginX*2+q.X,f.OriginY*2+q.Y);}
        if(!curve||m.Path.FinalTarget!=m.Path.Points[^1]){throw new InvalidDataException("Invalid Hammer saved curve");}
        var direction=SourceActorMotion.DirectionToNext(m.Path);
        if(direction.Index!=m.Path.Index||direction.Direction!=m.Path.Direction||direction.Vector!=m.Path.Vector){throw new InvalidDataException("Invalid Hammer source direction");}
    }
    internal static ulong Impact(PlayerMobile p,Mobile target,HammerFlight f,DateTime at)
    {
        if(!BlessedHammerDamage.Supported(p,target,out string reason)){throw new NotSupportedException(reason);}
        SorceressEffects.MissileHit(p,target,"blessedhammer");if(ClassAvoidance.Missile(target,"blessedhammer")){return f.Motion.Seed;}
        ulong seed=f.Motion.Seed;int raw=BlessedHammerDamage.Roll(f.Min256,f.Max256,GemCombat.Undead(target),NamedCombat.Demon(target),n=>(int)SourceRandom.Limited(ref seed,n));
        BlessedHammerDamage.Deliver(p,(BaseCreature)target,raw,f.Captured,at);return seed;
    }
    private static void Advance(HammerFlight f,DateTime at,bool? actions)
    {
        if(World.FindMobile((Serial)f.Owner) is not PlayerMobile{Alive:true,Deleted:false} p
            ||!ClassCreation.Profiles.TryGet(p.Serial,out var profile)||profile.Class!=DiabloClass.Paladin
            ||p.Map!=Map.Internal&&(p.Map?.MapID!=f.Map||SpellHelper.IsTown(p.Location,p)))
        {BlessedHammers.Remove(f.Id);return;}
        bool loaded=Restored.Remove(f.Id);int elapsed=loaded?(int)Math.Min(120-f.Frame,(at-f.NextUtc).Ticks/TimeSpan.TicksPerMillisecond/40+1):1;
        var next=f with{Frame=f.Frame+elapsed,NextUtc=f.NextUtc.AddMilliseconds(elapsed*40L)};
        if(loaded||f.Motion.Fault!=null||p.Map==Map.Internal||!(actions??p.NetState is{Running:true}))
        {if(next.Frame>=120){BlessedHammers.Remove(f.Id);}else{BlessedHammers.Update(next);}return;}
        var map=Map.Maps[f.Map];double x=f.OriginX+f.XFp16/131072.0,y=f.OriginY+f.YFp16/131072.0;bool removed=false;
        if(SpellHelper.IsTown(new Point3D((int)Math.Round(x),(int)Math.Round(y),f.Z),p)){BlessedHammers.Remove(f.Id);return;}
        int Move()
        {
            var moved=SourceActorMotion.Advance(f.Motion.Path,(cx,cy)=>DruidFirestorm.CellBlocked(map,f.Z,x,y,cx,cy)?4:0);
            if(!moved.Moving){return 2;}
            var path=moved.State;next=next with{Point=path.Index,XFp16=(int)(path.X-(long)f.OriginX*131072-32768),YFp16=(int)(path.Y-(long)f.OriginY*131072-32768),
                VelocityQ8=path.Velocity,AccelerationQ8=path.Acceleration,AccelerationCounter=path.AccelerationCounter,Motion=f.Motion with{Path=path}};return 1;
        }
        Mobile Find(SourceMissileCell cell)
        {
            foreach(var t in map.GetMobilesInRange<Mobile>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,f.Z),3))
            {
                if(t.Serial.Value==f.LastCollision||!ClassCombat.IsHostile(p,t)||Math.Abs(t.Z-f.Z)>=16||SpellHelper.IsTown(t.Location,t)){continue;}
                int size=t is BaseCreature creature?CreatureProfiles.For(creature).Traits.Size??2:2;
                if(CombatHitboxes.SourceTouches(1,size,cell.X-t.X*2,cell.Y-t.Y*2)){return t;}
            }
            return null;
        }
        int Hit(uint serial,bool terminal)
        {
            if(terminal){removed=true;return 2;}
            if(World.FindMobile((Serial)serial) is{} target)
            {ulong seed=Impact(p,target,f,f.NextUtc);next=next with{LastCollision=serial,Motion=next.Motion with{Seed=seed}};}return 1;
        }
        var result=BasicMissileUpdate.Run(new(true,true,f.VelocityQ8,120-f.Frame,2,120,true),
            new(Move,_=>{},()=>true,()=>true,()=>2,()=>next.Motion.Path.SavedSteps.Select(q=>new SourceMissileCell(q.X,q.Y)).ToArray(),
                cell=>Find(cell)!=null?1:0,cell=>Find(cell)?.Serial.Value??0,Hit));
        if(removed||result.Result==2){BlessedHammers.Remove(f.Id);return;}
        BlessedHammers.Update(next);
        if(next.Frame%2==0){Effects.SendLocationEffect(new(f.OriginX+(int)Math.Round(next.XFp16/131072.0),f.OriginY+(int)Math.Round(next.YFp16/131072.0),f.Z),map,0x36E4,3,10,0x481,0);} // Identified hammer/trail placeholder.
    }
    internal static void TickFlight(long id,DateTime at,bool? actions=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Hammer clock must be UTC");}
        var f=BlessedHammers.State(id);if(f?.Motion==null||f.NextUtc>at){return;}
        try{Advance(f,at,actions);}
        catch(NotSupportedException e)
        {
            Log.Warning("Hammer flight suspended:{Id};{Reason}",id,e.Message);
            if(BlessedHammers.State(id)==null){return;}
            if(f.Frame+1>=120){BlessedHammers.Remove(id);}else{BlessedHammers.Update(f with{Frame=f.Frame+1,NextUtc=f.NextUtc.AddMilliseconds(40),Motion=f.Motion with{Fault=e.Message}});}
        }
    }
    internal static void Tick(DateTime at,bool? actions=null)
    {if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Hammer clock must be UTC");}while(BlessedHammers.Due(at) is{} f){TickFlight(f.Id,at,actions);}}
}
