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

internal sealed record RabiesEmitter(long Id,uint Caster,uint Host,int Rank,int Map,int TotalFrames,int Age,
    DateTime StartedUtc,DateTime UntilUtc,DateTime NextUtc,bool Emitting=true,string Fault=null);
internal sealed record RabiesContagion(long Id,uint Caster,int Rank,int Map,int Z,double X,double Y,double Vx,double Vy,
    int Age,DateTime InfectionUntilUtc,DateTime NextUtc,WeaponMissileDamage Damage,uint LastCollision,ulong Seed,SourceStepTrace Steps)
{public string Fault{get;init;}}
internal sealed record RabiesMarker(long Id,uint Caster,uint Host,int Rank,int Map,int TotalFrames,DateTime StartedUtc,DateTime UntilUtc);
internal sealed record RabiesCasterSeed(uint Caster,ulong Seed);
internal sealed record RabiesInfectionSave(RabiesEmitter[] Infections,RabiesContagion[] Contagions,RabiesCasterSeed[] Seeds,long NextId)
{public RabiesMarker[] Markers{get;init;}}

public sealed class RabiesInfectionProfiles:GenericPersistence
{
    private RabiesInfectionSave _snapshot;
    public RabiesInfectionProfiles():base("SanctuaryRabiesInfections",178){}
    public override void Serialize(){_snapshot=DruidRabiesInfections.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(1);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {
        int version=reader.ReadInt();if(version is <0 or >1){throw new InvalidDataException("Unknown Rabies infection version");}
        var saved=JsonSerializer.Deserialize<RabiesInfectionSave>(reader.ReadString());
        if(version==1&&saved?.Markers==null){throw new InvalidDataException("Missing version1 Rabies markers");}
        DruidRabiesInfections.Restore(saved);
    }
}

// Independent state112 and missile515/516 preparation. Casting is not enabled
// here. Existing poison cure/replacement never edits this infection ledger.
public static class DruidRabiesInfections
{
    private sealed record Pool(ImmutableDictionary<long,RabiesEmitter> Emitters,ImmutableDictionary<uint,RabiesMarker> Markers,ImmutableDictionary<long,RabiesContagion> Contagions,
        ImmutableDictionary<uint,ulong> Seeds,long NextId);
    private static Pool _pool=new(ImmutableDictionary<long,RabiesEmitter>.Empty,ImmutableDictionary<uint,RabiesMarker>.Empty,ImmutableDictionary<long,RabiesContagion>.Empty,ImmutableDictionary<uint,ulong>.Empty,0);
    private static HashSet<long> _loaded=[];
    private const long FrameTicks=TimeSpan.TicksPerMillisecond*40;
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    public static RabiesInfectionProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static RabiesInfectionSave Snapshot()=>new(_pool.Emitters.Values.OrderBy(x=>x.Id).ToArray(),
        _pool.Contagions.Values.OrderBy(x=>x.Id).Select(x=>x with{Steps=new([..x.Steps.Cells])}).ToArray(),
        _pool.Seeds.OrderBy(x=>x.Key).Select(x=>new RabiesCasterSeed(x.Key,x.Value)).ToArray(),_pool.NextId){Markers=_pool.Markers.Values.OrderBy(x=>x.Id).ToArray()};
    internal static RabiesMarker Infection(Mobile host)=>host!=null&&_pool.Markers.TryGetValue(host.Serial.Value,out var state)?state:null;
    internal static RabiesEmitter[] Emitters(uint caster)=>_pool.Emitters.Values.Where(x=>x.Caster==caster).OrderBy(x=>x.Id).ToArray();
    private static void PruneMarkers(DateTime at,uint? casterScope=null)=>_pool=_pool with{Markers=_pool.Markers.RemoveRange(_pool.Markers.Values.Where(x=>x.UntilUtc<=at&&(!casterScope.HasValue||x.Caster==casterScope.Value)).Select(x=>x.Host))};
    internal static RabiesContagion[] Children(uint caster)=>_pool.Contagions.Values.Where(x=>x.Caster==caster).OrderBy(x=>x.Id).ToArray();
    private static PlayerMobile Caster(uint serial)=>World.FindMobile((Serial)serial) as PlayerMobile;
    private static bool Utc(DateTime value)=>value.Kind==DateTimeKind.Utc;
    private static int Length(PlayerMobile owner,int rank)=>SkillMechanics.Values(owner,CombatAbility.Rabies,rank).DurationFrames;
    private static void Routes()
    {
        int F(string row,string field)=>NecromancerSummons.Source(row,field);
        if(F("rabiesplague","pSrvDoFunc")!=30||F("rabiesplague","Param1")!=4||F("rabiesplague","Param2")!=7
            ||F("rabiescontagion","pSrvDoFunc")!=1||F("rabiescontagion","pSrvHitFunc")!=53||F("rabiescontagion","pSrvDmgFunc")!=11
            ||F("rabiescontagion","Range")!=20||F("rabiescontagion","Activate")!=2||F("rabiescontagion","Vel")!=5
            ||F("rabiescontagion","Size")!=1||F("rabiescontagion","LastCollide")!=1||F("rabiescontagion","ToHit")!=0
            ||F("rabiescontagion","NextHit")!=0||F("rabiescontagion","CollideKill")!=0)
        {throw new NotSupportedException("Unsupported Rabies infection route");}
    }
    internal static RabiesMarker Mark(PlayerMobile owner,BaseCreature host,int rank,int frames,DateTime at)
    {
        Routes();
        if(owner is not{Deleted:false,Alive:true}||host is not{Deleted:false,Alive:true}||rank is <1 or >255||frames<1||frames>Length(owner,rank)
            ||!Utc(at)||host.Map==null||host.Map==Map.Internal||!ClassCombat.IsHostile(owner,host))
        {throw new NotSupportedException("Unsupported Rabies infection supplier");}
        // One marker per recipient, including infections from another caster.
        // A new poison packet may still be delivered by the caller.
        if(_pool.Markers.TryGetValue(host.Serial.Value,out var existing)&&existing.UntilUtc>at){return existing;}
        long id=checked(_pool.NextId+1);
        var state=new RabiesEmitter(id,owner.Serial.Value,host.Serial.Value,rank,host.Map.MapID,frames,0,at,at.AddTicks(frames*FrameTicks),at.AddTicks(FrameTicks));
        Validate(state,id);
        var seeds=_pool.Seeds.ContainsKey(state.Caster)?_pool.Seeds:_pool.Seeds.Add(state.Caster,SourceRandom.InitLow(SeedLow()));
        var marker=new RabiesMarker(id,state.Caster,state.Host,state.Rank,state.Map,state.TotalFrames,state.StartedUtc,state.UntilUtc);
        Validate(marker,id);_pool=_pool with{Emitters=_pool.Emitters.Add(id,state),Markers=_pool.Markers.SetItem(marker.Host,marker),Seeds=seeds,NextId=id};return marker;
    }
    // Exact offline/facet/dead-caster behavior is not established. Unsupported
    // contexts are explicitly recorded and age without replaying attacks.
    private static string Unsupported(PlayerMobile owner,int map,bool? actions)
    {
        if(owner is not{Deleted:false}){return "Original Rabies caster is missing";}
        if(!owner.Alive){return "Dead original Rabies caster behavior is unverified";}
        if(!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Druid){return "Rabies caster class/history changed";}
        if(owner.Map==null||owner.Map==Map.Internal||owner.Map.MapID!=map){return "Rabies owner facet/logout behavior is unverified";}
        if(!(actions??owner.NetState is{Running:true})){return "Rabies offline behavior is unverified";}
        return null;
    }
    private static void Emit(PlayerMobile owner,BaseCreature host,RabiesEmitter state,DateTime at)
    {
        ulong seed=_pool.Seeds[state.Caster];int dx=(int)SourceRandom.Limited(ref seed,15)-7,dy=(int)SourceRandom.Limited(ref seed,15)-7;
        _pool=_pool with{Seeds=_pool.Seeds.SetItem(state.Caster,seed)};
        if(dx==0&&dy==0){dx=dy=1;} // Complete factory accepts zero offset and nudges aim.
        var slow=AmazonSlowMissiles.Capture(owner,true);
        int velocity=SlowMissilePayload.VelocityFp8(5,0,state.Rank,2,0,true,slow.VelocityPercent);
        var vector=MissileVectors.VelocityFp8(dx,dy,velocity);
        long id=checked(_pool.NextId+1);
        var child=new RabiesContagion(id,state.Caster,state.Rank,host.Map.MapID,host.Z,host.X,host.Y,vector.X,vector.Y,0,
            _pool.Markers.TryGetValue(state.Host,out var marker)&&marker.UntilUtc>at?marker.UntilUtc:DateTime.UnixEpoch,at.AddTicks(FrameTicks),DruidRabiesPackets.Capture(owner,state.Rank),0,SourceRandom.InitLow(SeedLow()),new([new(host.X*2,host.Y*2)]));
        Validate(child,id);_pool=_pool with{Contagions=_pool.Contagions.Add(id,child),NextId=id};
    }
    private static void RemoveEmitter(long id){_pool=_pool with{Emitters=_pool.Emitters.Remove(id)};_loaded.Remove(id);}
    private static void Advance(RabiesEmitter state,DateTime at,bool? actions)
    {
        var host=World.FindMobile((Serial)state.Host) as BaseCreature;var owner=Caster(state.Caster);
        bool loaded=_loaded.Remove(state.Id);string fault=Unsupported(owner,state.Map,actions);
        if(host is not{Alive:true,Deleted:false}||owner is not{Deleted:false}){RemoveEmitter(state.Id);return;}
        if(host.Map==null||host.Map.MapID!=state.Map){fault="Rabies host changed facet";}
        if(loaded||fault!=null)
        {
            long skipped=(at-state.NextUtc).Ticks/FrameTicks+1;
            if(skipped>state.TotalFrames+1L-state.Age){RemoveEmitter(state.Id);return;}
            _pool=_pool with{Emitters=_pool.Emitters.SetItem(state.Id,state with{Age=state.Age+(int)skipped,NextUtc=state.NextUtc.AddTicks(skipped*FrameTicks),Fault=fault})};return;
        }
        // Do30 owns a distinct missile lifetime. Generic collision's return is
        // ignored; the next do30 only retires once current is negative.
        var clock=DruidRabiesEmitterClock.Step(state.TotalFrames,state.Age);
        if(clock.Retire){RemoveEmitter(state.Id);return;}
        if(clock.Emit){Emit(owner,host,state,state.NextUtc);}
        _pool=_pool with{Emitters=_pool.Emitters.SetItem(state.Id,state with{Age=clock.Age,NextUtc=state.NextUtc.AddTicks(FrameTicks),Fault=null})};
    }
    internal static BaseCreature Recipient(PlayerMobile owner,RabiesContagion child)
    {
        var map=Map.Maps[child.Map];
        foreach(var cell in child.Steps.Cells)
        {
            foreach(var target in map.GetMobilesInRange<BaseCreature>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,child.Z),3))
            {
                if(target.Serial.Value==child.LastCollision){continue;}
                int size=CreatureProfiles.For(target).Traits.Size??2;
                if(Math.Abs(target.Z-child.Z)<16&&CombatHitboxes.SourceTouches(1,size,cell.X-target.X*2,cell.Y-target.Y*2)
                    &&ClassCombat.IsHostile(owner,target)&&!SpellHelper.IsTown(target.Location,owner)){return target;}
            }
        }
        return null;
    }
    private static void RemoveChild(long id){_pool=_pool with{Contagions=_pool.Contagions.Remove(id)};_loaded.Remove(id);}
    internal static bool Contact(RabiesContagion child,PlayerMobile owner,BaseCreature target,DateTime at,out RabiesContagion next)
    {
        next=child;
        if(target==null||target.Serial.Value==child.LastCollision){return true;}
        next=child with{LastCollision=target.Serial.Value}; // Most recent identity, never a permanent visited set.
        if(!ClassCombat.IsHostile(owner,target)){return true;}
        long remaining=(child.InfectionUntilUtc-at).Ticks/FrameTicks;
        if(!DruidRabiesPackets.TryInfectionLength(remaining,0,Length(owner,child.Rank),out int frames)){return false;}
        Mark(owner,target,child.Rank,frames,at); // Marker precedes poison; existing marker is not refreshed.
        ulong seed=child.Seed;var p=child.Damage.Elements;
        int rate=WeaponMissileDamage.RollValue(p.PoisonMin,p.PoisonMax,0,n=>(int)SourceRandom.Limited(ref seed,n));next=next with{Seed=seed};
        owner.DoHarmful(target,true);PoisonJavelins.Poison(owner,target,rate,frames,at);return true;
    }
    private static void Advance(RabiesContagion child,DateTime at,bool? actions)
    {
        var owner=Caster(child.Caster);string fault=Unsupported(owner,child.Map,actions);
        if(_loaded.Remove(child.Id)||fault!=null)
        {
            long skipped=(at-child.NextUtc).Ticks/FrameTicks+1;
            if(skipped>=20-child.Age){RemoveChild(child.Id);return;}
            _pool=_pool with{Contagions=_pool.Contagions.SetItem(child.Id,child with{Age=child.Age+(int)skipped,NextUtc=child.NextUtc.AddTicks(skipped*FrameTicks),Fault=fault})};return;
        }
        int age=child.Age+1;double x=child.X+child.Vx,y=child.Y+child.Vy;var map=Map.Maps[child.Map];
        if(age>=20||x<0||y<0||x>=map.Width||y>=map.Height
            ||!map.LineOfSight(new Point3D((int)Math.Round(child.X),(int)Math.Round(child.Y),child.Z+12),new Point3D((int)Math.Round(x),(int)Math.Round(y),child.Z+12)))
        {RemoveChild(child.Id);return;}
        var next=child with{Age=age,X=x,Y=y,NextUtc=child.NextUtc.AddTicks(FrameTicks),Steps=SourceMissileSteps.Advance(child.X,child.Y,child.Vx,child.Vy,child.Steps),Fault=null};
        if(age>=2&&Recipient(owner,next) is{} target&&!Contact(next,owner,target,child.NextUtc,out next)){RemoveChild(child.Id);return;}
        _pool=_pool with{Contagions=_pool.Contagions.SetItem(next.Id,next)};
    }
    internal static void Tick(DateTime at,bool? actions=null,uint? casterScope=null)
    {
        if(!Utc(at)){throw new ArgumentException("Rabies clock must be UTC",nameof(at));}
        while(true)
        {
            var infection=_pool.Emitters.Values.Where(x=>x.NextUtc<=at&&(!casterScope.HasValue||x.Caster==casterScope.Value)).OrderBy(x=>x.NextUtc).ThenBy(x=>x.Id).FirstOrDefault();
            var child=_pool.Contagions.Values.Where(x=>x.NextUtc<=at&&(!casterScope.HasValue||x.Caster==casterScope.Value)).OrderBy(x=>x.NextUtc).ThenBy(x=>x.Id).FirstOrDefault();
            if(infection==null&&child==null){PruneMarkers(at,casterScope);return;}
            PruneMarkers(infection!=null&&(child==null||infection.NextUtc<=child.NextUtc)?infection.NextUtc:child.NextUtc,casterScope);
            // Stable local queue; exact modern global unit/event tie order is unverified.
            if(infection!=null&&(child==null||infection.NextUtc<=child.NextUtc)){Advance(infection,at,actions);}else{Advance(child,at,actions);}
        }
    }
    private static bool Identity(uint serial)=>((Serial)serial).IsMobile;
    private static bool MapId(int map)=>map>=0&&map<Map.Maps.Length&&Map.Maps[map]!=null&&Map.Maps[map]!=Map.Internal;
    private static void Validate(RabiesEmitter s,long nextId)
    {
        if(s==null||s.Id<=0||s.Id>nextId||!Identity(s.Caster)||!Identity(s.Host)||s.Rank is <1 or >255||!MapId(s.Map)
            ||s.TotalFrames<1||s.TotalFrames>100+10*(s.Rank-1)||s.Age<0||s.Age>s.TotalFrames+1
            ||!Utc(s.StartedUtc)||!Utc(s.UntilUtc)||!Utc(s.NextUtc)||s.UntilUtc!=s.StartedUtc.AddTicks(s.TotalFrames*FrameTicks)
            ||!s.Emitting||s.NextUtc!=s.StartedUtc.AddTicks((s.Age+1L)*FrameTicks))
        {throw new InvalidDataException("Invalid saved Rabies infection");}
    }
    private static void Validate(RabiesMarker s,long nextId)
    {
        if(s==null||s.Id<=0||s.Id>nextId||!Identity(s.Caster)||!Identity(s.Host)||s.Rank is <1 or >255||!MapId(s.Map)
            ||s.TotalFrames<1||s.TotalFrames>100+10*(s.Rank-1)||!Utc(s.StartedUtc)||!Utc(s.UntilUtc)
            ||s.UntilUtc!=s.StartedUtc.AddTicks(s.TotalFrames*FrameTicks)){throw new InvalidDataException("Invalid saved Rabies marker");}
    }
    private static void Validate(RabiesContagion s,long nextId)
    {
        var e=s?.Damage?.Elements;
        if(s==null||s.Id<=0||s.Id>nextId||!Identity(s.Caster)||s.Rank is <1 or >255||!MapId(s.Map)||s.Age is <0 or >=20
            ||!Utc(s.InfectionUntilUtc)||!Utc(s.NextUtc)||!double.IsFinite(s.X+s.Y+s.Vx+s.Vy)||s.X<0||s.Y<0||s.X>=Map.Maps[s.Map].Width||s.Y>=Map.Maps[s.Map].Height
            ||Math.Abs(s.Vx)>.12||Math.Abs(s.Vy)>.12||s.LastCollision!=0&&!Identity(s.LastCollision)||s.Steps?.Cells is not{Length:>=1 and <=10}
            ||s.Steps.Cells.Any(c=>c.X<0||c.Y<0||c.X>=Map.Maps[s.Map].Width*2||c.Y>=Map.Maps[s.Map].Height*2)
            ||e==null||e.PoisonMin<0||e.PoisonMax<e.PoisonMin||e.PoisonFrames!=100+10*(s.Rank-1)
            ||e!=new MissileSourceStats(PoisonMin:e.PoisonMin,PoisonMax:e.PoisonMax,PoisonFrames:e.PoisonFrames)||s.Damage!=new WeaponMissileDamage(0,0,0,0,0,false,e))
        {throw new InvalidDataException("Invalid saved Rabies contagion");}
    }
    internal static void Restore(RabiesInfectionSave saved)
    {
        if(saved?.Infections==null||saved.Contagions==null||saved.Seeds==null||saved.NextId<0||saved.Infections.Length>100000||saved.Contagions.Length>100000||saved.Markers?.Length>100000)
        {throw new InvalidDataException("Missing Rabies infection state");}
        // Version0 combined marker/emitter snapshots migrate without resetting
        // rank, deadline, elapsed age, children, poison or RNG. Main's older
        // worlds without this optional profile still start empty.
        var markers=saved.Markers??saved.Infections.Select(s=>new RabiesMarker(s.Id,s.Caster,s.Host,s.Rank,s.Map,s.TotalFrames,s.StartedUtc,s.UntilUtc)).ToArray();
        var emitters=ImmutableDictionary<long,RabiesEmitter>.Empty;var marked=ImmutableDictionary<uint,RabiesMarker>.Empty;
        var children=ImmutableDictionary<long,RabiesContagion>.Empty;var seeds=ImmutableDictionary<uint,ulong>.Empty;var ids=new HashSet<long>();var markerIds=new HashSet<long>();
        foreach(var s in markers)
        {Validate(s,saved.NextId);if(!markerIds.Add(s.Id)||marked.ContainsKey(s.Host)){throw new InvalidDataException("Duplicate Rabies marker");}marked=marked.Add(s.Host,s);}
        foreach(var s in saved.Infections)
        {
            if(saved.Markers==null&&!s.Emitting){continue;}
            Validate(s,saved.NextId);if(!ids.Add(s.Id)){throw new InvalidDataException("Duplicate Rabies emitter");}emitters=emitters.Add(s.Id,s);
        }
        foreach(var s in saved.Contagions){Validate(s,saved.NextId);if(!ids.Add(s.Id)){throw new InvalidDataException("Duplicate Rabies missile");}children=children.Add(s.Id,s with{Steps=new([..s.Steps.Cells])});}
        foreach(var s in saved.Seeds){if(s==null||!Identity(s.Caster)||seeds.ContainsKey(s.Caster)){throw new InvalidDataException("Invalid Rabies caster seed");}seeds=seeds.Add(s.Caster,s.Seed);}
        foreach(var marker in marked.Values)
        {
            if(children.ContainsKey(marker.Id)||emitters.TryGetValue(marker.Id,out var emitter)
                &&marker!=new RabiesMarker(emitter.Id,emitter.Caster,emitter.Host,emitter.Rank,emitter.Map,emitter.TotalFrames,emitter.StartedUtc,emitter.UntilUtc))
            {throw new InvalidDataException("Rabies marker identity disagrees with its emitter");}
        }
        if(marked.Values.Any(x=>!seeds.ContainsKey(x.Caster))||emitters.Values.Any(x=>!seeds.ContainsKey(x.Caster))||children.Values.Any(x=>!seeds.ContainsKey(x.Caster)))
        {throw new InvalidDataException("Missing Rabies caster seed");}
        _pool=new(emitters,marked,children,seeds,saved.NextId);_loaded=ids;
    }
}
