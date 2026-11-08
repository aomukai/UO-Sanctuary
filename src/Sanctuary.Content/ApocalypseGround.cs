// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ApocalypseGroundField(uint Marker,uint Owner,int Map,Point3D Origin,int Rank,DateTime BornUtc,int Remaining,uint TargetMemory);
internal sealed record ApocalypseCasterSeed(uint Owner,ulong Seed);
internal sealed record ApocalypseGroundSave(ApocalypseGroundField[] Fields,ApocalypseCasterSeed[] Casters);
public sealed class ApocalypseGroundProfiles:GenericPersistence
{
    private ApocalypseGroundSave _snapshot;
    public ApocalypseGroundProfiles():base("SanctuaryApocalypseGround",248){}
    public override void Serialize(){_snapshot=ApocalypseGround.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Apocalypse ground version");}ApocalypseGround.Restore(JsonSerializer.Deserialize<ApocalypseGroundSave>(reader.ReadString()));}
}

// Prepared original marker host;learned input/payment and full current world
// lifecycle parity remain separate. The caller supplies the initial caster
// stream;never reseed a live owner or use a missile-private explosion roll.
internal static class ApocalypseGround
{
    private static ImmutableDictionary<uint,ApocalypseGroundField> _fields=ImmutableDictionary<uint,ApocalypseGroundField>.Empty;
    private static ImmutableDictionary<uint,ulong> _seeds=ImmutableDictionary<uint,ulong>.Empty;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ApocalypseGround));
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    public static void Configure()=>_=new ApocalypseGroundProfiles();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static void BindSeedPrepared(PlayerMobile owner,ulong seed)
    {
        if(owner is not{Alive:true,Deleted:false}||_seeds.ContainsKey(owner.Serial.Value)){throw new NotSupportedException("Apocalypse owner stream missing or already bound");}
        _seeds=_seeds.Add(owner.Serial.Value,seed);
    }
    internal static ulong Seed(PlayerMobile owner)=>_seeds.TryGetValue(owner.Serial.Value,out var seed)?seed:throw new NotSupportedException("Apocalypse caster RNG provider is not bound");
    private static uint Random(PlayerMobile owner)
    {ulong seed=Seed(owner);uint sample=SourceRandom.Roll(ref seed);_seeds=_seeds.SetItem(owner.Serial.Value,seed);return sample;}
    private sealed class Launch(PlayerMobile owner,Point3D point,int rank,DateTime at,bool allocation,Func<bool> payment):GroundLaunchServices
    {
        internal ApocalypseGroundField Created;
        private Point3D Ground=point;
        internal override bool HasSkill(int skill)=>skill==401;
        internal override int SkillMissile()=>throw new NotSupportedException("Do123 does not query an ordinary skill missile");
        internal override int MissileCount=>throw new NotSupportedException("Do123 does not query missile table count");
        internal override int ProgressiveMissile(int skill)=>skill==401?722:0;
        internal override bool HasRoom()=>owner.Map!=null&&owner.Map!=Map.Internal;
        internal override Point2D Coordinates()=>new(Ground.X*2,Ground.Y*2);
        internal override bool DestinationRoom(Point2D p)=>HasRoom()&&Ground.X>=0&&Ground.Y>=0&&Ground.X<owner.Map.Width&&Ground.Y<owner.Map.Height;
        internal override bool Town()=>Server.Spells.SpellHelper.IsTown(Ground,owner);
        internal override int MissileSize(int missile)=>WarlockGroundRoutes.For(CombatAbility.Apocalypse).Size;
        internal override bool Blocked(Point2D p,int size,int mask)
        {if(size!=1||mask!=5){throw new NotSupportedException("Apocalypse placement binding changed");}return !SigilActors.Ground(owner,owner.Map,Ground,out Ground);}
        internal override int Distance(Point2D p)=>SkillGeometry.SourceDistance(Math.Abs(p.X-owner.X*2),Math.Abs(p.Y-owner.Y*2));
        internal override void SetDoFlag(){} // Source action flag is caller-owned until learned startup is integrated.
        internal override uint Random()=>ApocalypseGround.Random(owner);
        internal override bool Create(GroundMissileRequest request)
        {
            if(request.Skill!=401||request.Rank!=rank||request.Missile!=722||request.Flags!=1||request.Point!=Coordinates())
            {throw new NotSupportedException("Apocalypse ground factory identity changed");}
            if(Distance(request.Point)>100||!allocation){return false;}
            var marker=new Static(0x1B7A){Name="Apocalypse ground (placeholder)",Movable=false};marker.MoveToWorld(Ground,owner.Map);
            if(payment!=null&&!payment()){marker.Delete();return false;}
            Created=new(marker.Serial.Value,owner.Serial.Value,owner.Map.MapID,Ground,rank,at,50,0);_fields=_fields.Add(Created.Marker,Created);return true;
        }
        internal override void SetTargetMemory(uint sample)
        {Created=Created with{TargetMemory=sample};_fields=_fields.SetItem(Created.Marker,Created);}
        internal override void Notify(GroundMissileRequest request,uint sample){} // Identified original marker stands in for client notification.
    }
    internal static void EnsureSeed(PlayerMobile owner)
    {
        // Established native seed adapter;retain the saved owner stream after
        // initial binding. Cross-ability D2 interleaving remains unverified.
        if(!_seeds.ContainsKey(owner.Serial.Value)){BindSeedPrepared(owner,SourceRandom.InitLow(SeedLow()));}
    }
    internal static ApocalypseGroundField CreatePaid(PlayerMobile owner,SkillValues values,Point3D point,DateTime at)
    {
        if(values?.Ability!=CombatAbility.Apocalypse){throw new NotSupportedException("Apocalypse needs its own captured payment values");}
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return null;}
        EnsureSeed(owner);
        return Create(owner,point,values.Rank,at,true,()=>NecromancerEffects.Pay(owner,values.Mana256));
    }
    internal static ApocalypseGroundField CreatePrepared(PlayerMobile owner,Point3D point,int rank,DateTime at,bool allocation=true)
        =>Create(owner,point,rank,at,allocation,null);
    private static ApocalypseGroundField Create(PlayerMobile owner,Point3D point,int rank,DateTime at,bool allocation,Func<bool> payment)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Warlock
            ||rank is <1 or >255||at.Kind!=DateTimeKind.Utc||at<=DateTime.UnixEpoch){return null;}
        _=Seed(owner);var route=WarlockGroundRoutes.For(CombatAbility.Apocalypse);
        if(route.StartFunction!=0||route.Lifetime!=50||route.MissileDo!=1||route.MissileHit!=23||route.HitDispatch!=163){throw new NotSupportedException("Apocalypse delayed ground route changed");}
        var launch=new Launch(owner,point,rank,at,allocation,payment);
        return GroundMissileLaunch.Do123(401,rank,launch)?launch.Created:null;
    }
    private sealed class Hit(ApocalypseGroundField s,PlayerMobile owner,bool fighting):GroundHitServices
    {
        internal override bool Present()=>true;
        internal override bool HasRow()=>true;
        internal override int Skill()=>401;
        internal override int Rank()=>s.Rank;
        internal override bool HasOwner()=>owner!=null;
        internal override bool HasVictim()=>false;
        internal override int HitParameter()=>163;
        internal override void SetTarget()=>throw new InvalidOperationException("Stationary Apocalypse expiry has no victim target");
        internal override void Dispatch(int function,int skill,int rank)
        {
            if(function!=163||skill!=401||rank!=s.Rank){throw new NotSupportedException("Apocalypse hit23 dispatch changed");}
            if(!fighting){return;}
            ulong seed=Seed(owner);var packet=ApocalypsePackets.Capture(owner,ref seed,s.Rank);_seeds=_seeds.SetItem(s.Owner,seed);
            _=ApocalypseArea.ReleasePrepared(owner,Map.Maps[s.Map],s.Origin,packet);
        }
    }
    internal static void Tick(DateTime at,bool? allowActions=null,uint? onlyOwner=null)
    {
        if(World.Loading){return;}if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Apocalypse clock must be UTC");}
        foreach(var initial in _fields.Values.Where(s=>!onlyOwner.HasValue||s.Owner==onlyOwner.Value).OrderBy(s=>s.BornUtc).ThenBy(s=>s.Marker).ToArray())
        {
            var s=initial;var marker=World.FindItem((Serial)s.Marker);var owner=World.FindMobile((Serial)s.Owner)as PlayerMobile;
            if(marker is not{Deleted:false}||marker.Map?.MapID!=s.Map||marker.Location!=s.Origin||owner is not{Alive:true,Deleted:false}){Remove(s);continue;}
            long elapsed=(at-s.BornUtc).Ticks/TimeSpan.TicksPerMillisecond/40;int advance=(int)Math.Clamp(elapsed-(50-s.Remaining),0,s.Remaining);
            bool fighting=(allowActions??AssassinTrapRoster.Fighting(s.Owner))&&owner.Map==marker.Map&&!MinionSafeAreas.Contains(owner);
            for(int i=0;i<advance;i++)
            {
                bool terminal=false;int remaining=s.Remaining;
                try
                {
                    var result=BasicMissileUpdate.Run(new(true,true,0,remaining,0,0,false),new(null,value=>remaining=value,null,null,null,null,null,null,(target,end)=>
                    {
                        if(target!=0||!end){throw new InvalidOperationException("Apocalypse unexpectedly collided before expiry");}
                        terminal=true;_fields=_fields.Remove(s.Marker);int flags=GroundMissileLaunch.Hit23(new Hit(s,owner,fighting));
                        return(flags&4)!=0?1:2; // Generic terminal no-victim wrapper deletes after raw hit23 returns1.
                    }));
                    s=s with{Remaining=result.Remaining};
                    if(terminal){World.FindItem((Serial)s.Marker)?.Delete();break;}
                    _fields=_fields.SetItem(s.Marker,s);
                }
                catch(Exception e){Remove(s);Log.Error(e,"Apocalypse prepared ground failed: {Marker}",s.Marker);break;}
            }
        }
    }
    private static void Remove(ApocalypseGroundField s){_fields=_fields.Remove(s.Marker);World.FindItem((Serial)s.Marker)?.Delete();}
    internal static void Clear(PlayerMobile owner){foreach(var s in _fields.Values){if(s.Owner==owner.Serial.Value){Remove(s);}}}
    internal static ApocalypseGroundSave Snapshot()=>new(_fields.Values.OrderBy(s=>s.Marker).ToArray(),_seeds.OrderBy(s=>s.Key).Select(s=>new ApocalypseCasterSeed(s.Key,s.Value)).ToArray());
    internal static void Restore(ApocalypseGroundSave saved)
    {
        if(saved?.Fields==null||saved.Casters==null){throw new InvalidDataException("Missing Apocalypse ground state");}
        var fields=ImmutableDictionary<uint,ApocalypseGroundField>.Empty;var seeds=ImmutableDictionary<uint,ulong>.Empty;
        foreach(var seed in saved.Casters)
        {if(seed==null||!((Serial)seed.Owner).IsMobile||seeds.ContainsKey(seed.Owner)){throw new InvalidDataException("Invalid Apocalypse caster stream");}seeds=seeds.Add(seed.Owner,seed.Seed);}
        foreach(var s in saved.Fields)
        {
            if(s==null||!((Serial)s.Marker).IsItem||!seeds.ContainsKey(s.Owner)||s.Map<0||s.Map>=Map.Maps.Length||Map.Maps[s.Map]==null||Map.Maps[s.Map]==Map.Internal
                ||s.Origin.X<0||s.Origin.Y<0||s.Origin.X>=Map.Maps[s.Map].Width||s.Origin.Y>=Map.Maps[s.Map].Height||s.Origin.Z is < -128 or >127
                ||s.Rank is <1 or >255||s.Remaining is <1 or >50||s.BornUtc.Kind!=DateTimeKind.Utc||s.BornUtc<=DateTime.UnixEpoch||fields.ContainsKey(s.Marker))
            {throw new InvalidDataException("Invalid Apocalypse ground field");}fields=fields.Add(s.Marker,s);
        }
        _fields=fields;_seeds=seeds;
    }
}
