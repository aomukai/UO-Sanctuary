// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record WakeFireFlight(long Id,long Execution,uint Actor,uint Owner,int Map,int Z,int Rank,bool Wave,
    double X,double Y,double Vx,double Vy,int PerpendicularX,int PerpendicularY,bool PathChanged,int Frame,int Frames,
    int VelocityFp8,int Minimum256,int Maximum256,int Pierce,SlowMissilePayload Slow,ulong Seed,uint LastCollision,
    SourceStepTrace Steps,DateTime NextUtc,string Fault=null);
internal sealed record WakeFireLease(long Execution,uint Target,DateTime UntilUtc);
internal sealed record WakeFireSave(WakeFireFlight[] Flights,WakeFireLease[] Leases,long NextId);
public sealed class WakeFireMissileProfiles:GenericPersistence
{
    private WakeFireSave _snapshot;
    public WakeFireMissileProfiles():base("SanctuaryWakeFireMissiles",228){}
    public override void Serialize(){_snapshot=AssassinWakeFireMissiles.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Wake fire missile version");}AssassinWakeFireMissiles.Restore(JsonSerializer.Deserialize<WakeFireSave>(reader.ReadString()));}
}

// Real maker/wave backend. Approved Sanctuary rule: one NPC do125 firing
// and its descendants share a four-frame lease; other firings/traps are
// independent. The exact D2R NPC shot-versus-placement identity remains
// unresolved reference data, superseded only by that approved adaptation.
// Shared native Q12/cell/terrain adapters retain their existing geometry limits.
public static class AssassinWakeFireMissiles
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AssassinWakeFireMissiles));
    private static readonly Dictionary<long,WakeFireFlight> Flights=new();
    private static readonly Dictionary<(long,uint),DateTime> Leases=new();
    private static long _nextId;
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    public static void Configure()=>_=new WakeFireMissileProfiles();
    internal static DateTime NextFrame=>Flights.Values.Select(x=>x.NextUtc).DefaultIfEmpty(DateTime.MaxValue).Min();
    internal static WakeFireFlight State(long id)=>Flights.GetValueOrDefault(id);
    internal static void Dispatch(AssassinSentryBody body,uint targetSerial)
    {
        Point2D? point=null;
        if(World.FindMobile((Serial)targetSerial) is{Deleted:false} target&&target!=body)
        {
            if(target.Map!=body.Map){throw new NotSupportedException("Cross-map pending Wake target is not mapped");}
            point=new(target.X*2,target.Y*2);
        }
        var aim=AssassinSentryAim.Resolve(body.Prepared.CachedAim,point);
        // Complete target-coordinate getter refuses either zero coordinate.
        if(aim.X==0||aim.Y==0){return;}
        Create(body,aim);
    }
    internal static long Create(AssassinSentryBody body,Point2D aim)=>Create(body,body.X,body.Y,aim.X-body.X*2,aim.Y-body.Y*2,false,0,body.Prepared.NextFrameUtc);
    private static long Create(AssassinSentryBody body,double x,double y,int dx,int dy,bool wave,long execution,DateTime at)
    {
        if(body is not{Deleted:false}||!(body.Alive||wave&&body.ReleasedProjectileOwner)||body.Prepared is not{ControllerFault:null,Creation.Ability:CombatAbility.WakeOfFire} c)
        {throw new InvalidOperationException("Invalid live Wake source");}
        string missile=wave?"wake of destruction":"wake of destruction maker";
        int F(string field)=>NecromancerSummons.Source(missile,field);
        if(F("Size")!=(wave?1:2)||F("CollideType")!=(wave?3:8)||F("pSrvDoFunc")!=(wave?1:31)
            ||F("NextHit")!=1||F("NextDelay")!=4||F("LastCollide")!=1||F("DamageRate")!=205||F("CollideKill")!=0
            ||F("ToHit")!=0||F("pSrvHitFunc")!=0||F("pSrvDmgFunc")!=0||F("SrcDamage")!=0||F("Accel")!=0||F("ResultFlags")!=4)
        {throw new NotSupportedException("Unsupported Wake missile source route");}
        var slow=AmazonSlowMissiles.Capture(body,F("CanSlow")!=0);
        var plan=AssassinWakeFire.Plan(AssassinSentryCreation.Values(c.Creation),wave,new((int)Math.Floor(x*2+.5),(int)Math.Floor(y*2+.5)),new(dx,dy),c.Creation.Passive("passive_fire_mastery"),slow.VelocityPercent);
        if(plan==null){return 0;}
        if(plan.VelocityFp8==0){throw new NotSupportedException("Zero-velocity Wake path flags are not mapped");}
        // Store the launcher's original perpendicular before factory zero-vector
        // fallback. Children use opposite stored vectors, not a rotated velocity.
        var perpendicular=AssassinWakeFire.Perpendicular(new(dx,dy));
        if(dx==0&&dy==0){dx=dy=1;}
        var v=MissileVectors.VelocityFp8(dx,dy,plan.VelocityFp8);long id=checked(_nextId+1);
        var s=new WakeFireFlight(id,wave?execution:id,body.Serial.Value,c.Owner,c.Map,c.Position.Z,c.Creation.Rank,wave,
            x,y,v.X,v.Y,perpendicular.X,perpendicular.Y,false,0,plan.Frames,plan.VelocityFp8,
            slow.Damage256(plan.Minimum256),slow.Damage256(plan.Maximum256),c.Creation.Passive("passive_fire_pierce"),slow,
            SourceRandom.InitLow(SeedLow()),0,new([new((int)Math.Floor(x*2+.5),(int)Math.Floor(y*2+.5))]),at.AddMilliseconds(40));
        Validate(s);Flights.Add(id,s);_nextId=id;return id;
    }
    private static void Validate(WakeFireFlight s)
    {
        if(s==null||s.Id<=0||s.Execution<=0||s.Execution>s.Id||!s.Wave&&s.Execution!=s.Id
            ||!((Serial)s.Actor).IsMobile||!((Serial)s.Owner).IsMobile||s.Rank is <1 or >255
            ||s.Map<0||s.Map>=Map.Maps.Length||Map.Maps[s.Map]==null||Map.Maps[s.Map]==Map.Internal||s.Z is <-128 or >127
            ||s.Frames!=(s.Wave?20:25+2*s.Rank)||s.Frame<0||s.Frame>=s.Frames||s.VelocityFp8<1||s.VelocityFp8>(s.Wave?1536:1920)
            ||!double.IsFinite(s.X)||!double.IsFinite(s.Y)||!double.IsFinite(s.Vx)||!double.IsFinite(s.Vy)
            ||s.X<0||s.Y<0||s.X>=Map.Maps[s.Map].Width||s.Y>=Map.Maps[s.Map].Height||Math.Abs(s.Vx)>1||Math.Abs(s.Vy)>1
            ||Math.Abs((long)s.PerpendicularX)>=100||Math.Abs((long)s.PerpendicularY)>=100||s.Minimum256<0||s.Maximum256<s.Minimum256
            ||s.Slow.VelocityPercent is <0 or >100||s.Slow.DamagePercent is <0 or >100||s.NextUtc.Kind!=DateTimeKind.Utc
            ||s.LastCollision!=0&&!((Serial)s.LastCollision).IsMobile||s.Steps?.Cells is not{Length:>=1 and <=10}
            ||s.Steps.Cells.Any(p=>p.X is <0 or >65535||p.Y is <0 or >65535))
        {throw new InvalidDataException("Invalid saved Wake flight");}
    }
    internal static bool Delayed(long execution,uint target,DateTime at)=>Leases.TryGetValue((execution,target),out var until)&&until>at;
    internal static void TickFrame(DateTime at,bool? actions=null)
    {
        foreach(var s in Flights.Values.Where(x=>x.NextUtc==at).OrderByDescending(x=>x.Id).ToArray())
        {
            try{Advance(s,actions);}
            catch(Exception e)
            {
                if(s.Frame+1>=s.Frames){Flights.Remove(s.Id);}else{Flights[s.Id]=s with{Fault=e.Message,Frame=s.Frame+1,NextUtc=s.NextUtc.AddMilliseconds(40)};}
                Log.Error(e,"Wake flight suspended: {Id}",s.Id);
            }
        }
        var active=Flights.Values.Select(x=>x.Execution).ToHashSet();
        foreach(var key in Leases.Keys.Where(k=>Leases[k]<=at||!active.Contains(k.Item1)).ToArray()){Leases.Remove(key);}
    }
    private static void Advance(WakeFireFlight s,bool? actions)
    {
        int frame=s.Frame+1;var next=s with{Frame=frame,NextUtc=s.NextUtc.AddMilliseconds(40)};
        var body=World.FindMobile((Serial)s.Actor) as AssassinSentryBody;
        bool fighting=body is{Deleted:false}&&(body.Alive||body.ReleasedProjectileOwner)&&body.Prepared.Owner==s.Owner&&body.Map?.MapID==s.Map
            &&body.Prepared.Creation.Ability==CombatAbility.WakeOfFire&&body.Prepared.ControllerFault==null&&(actions??AssassinTrapRoster.Fighting(s.Owner));
        if(!fighting||s.Fault!=null)
        {if(frame>=s.Frames){Flights.Remove(s.Id);}else{Flights[s.Id]=next;}return;}
        // Complete do31 reads the PREVIOUS movement flag and attempts both
        // children before the maker's ordinary collision/movement/expiry pass.
        if(!s.Wave&&s.PathChanged)
        {
            Create(body,s.X,s.Y,s.PerpendicularX,s.PerpendicularY,true,s.Execution,s.NextUtc);
            Create(body,s.X,s.Y,-s.PerpendicularX,-s.PerpendicularY,true,s.Execution,s.NextUtc);
        }
        next=next with{X=s.X+s.Vx,Y=s.Y+s.Vy,PathChanged=(int)Math.Floor(s.X*2+.5)!=(int)Math.Floor((s.X+s.Vx)*2+.5)||(int)Math.Floor(s.Y*2+.5)!=(int)Math.Floor((s.Y+s.Vy)*2+.5)};
        if(frame>=s.Frames||next.X<0||next.Y<0||next.X>=body.Map.Width||next.Y>=body.Map.Height){Flights.Remove(s.Id);return;}
        next=next with{Steps=SourceMissileSteps.Advance(s.X,s.Y,s.Vx,s.Vy,s.Steps)};
        if(World.FindMobile((Serial)s.Owner) is not PlayerMobile owner){throw new NotSupportedException("Missing Wake allegiance owner");}
        foreach(var cell in next.Steps.Cells)
        {
            if(cell.X<=0||cell.Y<=0||cell.X>=body.Map.Width*2||cell.Y>=body.Map.Height*2){Flights.Remove(s.Id);return;}
            BaseCreature victim=null;
            // Maker mode8 checks environment only; wave mode3 has a finder.
            if(s.Wave)
            foreach(var candidate in body.Map.GetMobilesInRange<BaseCreature>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,s.Z),3))
            {
                if(candidate.Serial.Value==s.LastCollision||Delayed(s.Execution,candidate.Serial.Value,s.NextUtc)||Math.Abs(candidate.Z-s.Z)>=16
                    ||!(body.ReleasedProjectileOwner?body.CorpseProjectileTarget(owner,candidate):body.TrapTarget(owner,candidate))||SpellHelper.IsTown(candidate.Location,candidate)){continue;}
                int? size=CreatureProfiles.For(candidate).Traits.Size;
                if(size is not(>=1 and <=3)){throw new NotSupportedException("Wake collider has no mapped source size");}
                if(CombatHitboxes.SourceTouches(1,size.Value,cell.X-candidate.X*2,cell.Y-candidate.Y*2)){victim=candidate;break;}
            }
            if(victim!=null)
            {
                // Both history and lease are committed before receiver effects;
                // blocked/immune contacts still consume this collision window.
                Leases[(s.Execution,victim.Serial.Value)]=s.NextUtc.AddMilliseconds(160);
                Flights[s.Id]=next with{LastCollision=victim.Serial.Value};
                var hit=AssassinSentryBolts.Impact(body,victim,"wake of destruction",s.Minimum256,s.Maximum256,s.Pierce,s.Seed,"fire",205);
                Flights[s.Id]=next with{LastCollision=victim.Serial.Value,Seed=hit.Seed};return;
            }
            if(!body.Map.LineOfSight(new Point3D((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z+12),new((cell.X+1)/2,(cell.Y+1)/2,s.Z+12)))
            {Flights.Remove(s.Id);return;}
        }
        Flights[s.Id]=next;
        if(frame%2==0){Effects.SendLocationEffect(new((int)Math.Round(next.X),(int)Math.Round(next.Y),s.Z),body.Map,0x36D4,3,10,0x489,0);} // Identified fire placeholder.
    }
    internal static WakeFireSave Snapshot()=>new(Flights.Values.OrderBy(x=>x.Id).ToArray(),Leases.OrderBy(x=>x.Key.Item1).ThenBy(x=>x.Key.Item2).Select(x=>new WakeFireLease(x.Key.Item1,x.Key.Item2,x.Value)).ToArray(),_nextId);
    internal static void Restore(WakeFireSave save)
    {
        if(save?.Flights==null||save.Leases==null||save.NextId<0){throw new InvalidDataException("Missing Wake missile state");}
        var flights=new Dictionary<long,WakeFireFlight>();var leases=new Dictionary<(long,uint),DateTime>();
        foreach(var s in save.Flights){Validate(s);if(s.Id>save.NextId||!flights.TryAdd(s.Id,s)){throw new InvalidDataException("Duplicate Wake flight");}}
        foreach(var group in flights.Values.GroupBy(x=>x.Execution))
        {
            var first=group.First();
            if(group.Any(x=>x.Actor!=first.Actor||x.Owner!=first.Owner||x.Map!=first.Map||x.Rank!=first.Rank))
            {throw new InvalidDataException("Conflicting Wake execution ownership");}
        }
        var active=flights.Values.Select(x=>x.Execution).ToHashSet();
        foreach(var l in save.Leases)
        {if(l==null||!active.Contains(l.Execution)||!((Serial)l.Target).IsMobile||l.UntilUtc.Kind!=DateTimeKind.Utc||!leases.TryAdd((l.Execution,l.Target),l.UntilUtc)){throw new InvalidDataException("Invalid Wake hit window");}}
        Flights.Clear();foreach(var pair in flights){Flights.Add(pair.Key,pair.Value);}
        Leases.Clear();foreach(var pair in leases){Leases.Add(pair.Key,pair.Value);}_nextId=save.NextId;
    }
}
