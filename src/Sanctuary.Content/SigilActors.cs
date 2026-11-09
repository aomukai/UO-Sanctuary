// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record SigilActor(uint Marker,uint Owner,int Map,Point3D Origin,int Rank,int Level,int Missile,string MissileKey,
    int Frames,DateTime BornUtc,int LastPulse=-1,bool Retiring=false,CombatAbility Ability=CombatAbility.SigilLethargy,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] int? RancorPulse=null);
internal sealed record SigilStat(int Stat,int Layer,int Value);
internal sealed record SigilOwnerState(uint Owner,DateTime EpochUtc,SigilStat[] Stats,float Expiry,uint Flags,DateTime EventUtc);
internal sealed record SigilActorSave(SigilActor[] Actors,SigilOwnerState[] Owners);
internal sealed record SigilWorldData(int MissileCount,SummonSource[] Missiles);
public sealed class SigilActorProfiles:GenericPersistence
{
    private SigilActorSave _snapshot;
    public SigilActorProfiles():base("SanctuarySigils",244){}
    public override void Serialize(){_snapshot=SigilActors.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Sigil actor version");}
        SigilActors.Restore(JsonSerializer.Deserialize<SigilActorSave>(reader.ReadString()));
    }
}

// Shared original world host for the three Sigils. Prepared markers alone
// do not admit learned skills;each recipient handler must opt into its ability.
internal static class SigilActors
{
    private static ImmutableDictionary<uint,SigilActor> _actors=ImmutableDictionary<uint,SigilActor>.Empty;
    private static ImmutableDictionary<uint,SigilOwnerState> _owners=ImmutableDictionary<uint,SigilOwnerState>.Empty;
    private static readonly Lazy<SigilWorldData> Data=new(()=>BalanceCatalog.Read<SigilWorldData>("sigil-world").Single());
    internal static event Action<SigilActor,PlayerMobile,DateTime> Pulse;
    public static void Configure()=>_=new SigilActorProfiles();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    private static bool Supported(CombatAbility ability)=>ability is CombatAbility.SigilLethargy or CombatAbility.SigilRancor or CombatAbility.SigilDeath;
    private static SkillValues Values(CombatAbility ability,int rank,int level)
    {
        if(!Supported(ability)){throw new InvalidDataException("Unsupported saved Sigil ability");}
        var row=SkillMechanics.For(ability);return new(row,k=>k==row.SourceKey?rank:0,_=>0,level);
    }
    internal static SkillValues Values(SigilActor s)=>Values(s.Ability,s.Rank,s.Level);
    internal static int RecipientFunction(CombatAbility ability)=>ability switch
    {CombatAbility.SigilLethargy or CombatAbility.SigilDeath=>30,CombatAbility.SigilRancor=>61,_=>throw new NotSupportedException("Unknown Sigil recipient")};
    private static (int Id,string Key,int Frames) Body(SkillValues values)
    {
        var row=SkillMechanics.For(values.Ability);int tier=Math.Clamp(checked((int)values.Eval("prgcalc1")),1,3);
        string key=row.Text("srvmissile"+"abc"[tier-1]);var source=Data.Value.Missiles.Single(m=>m.Key==key);var missile=new MissileValues(source.Fields,values);int id=missile.Number("*ID");
        int frames=checked((int)missile.Eval("Range"));
        var ids=values.Ability switch
        {CombatAbility.SigilLethargy=>new[]{393,699,686,702},CombatAbility.SigilRancor=>new[]{396,700,687,703},
            CombatAbility.SigilDeath=>new[]{400,701,688,704},_=>throw new NotSupportedException("Unknown Sigil body")};
        if(row.SourceId!=ids[0]||row.Number("srvdofunc")!=157||row.Text("aurastate")!="sigil"||values.Eval("calc3")!=1
            ||id!=ids[tier]||missile.Number("pSrvDoFunc")!=14||missile.Number("Param1")!=6||missile.Number("Param2")!=RecipientFunction(values.Ability)
            ||missile.Number("Size")!=1||frames!=100+25*values.Rank||frames is <1 or >32767)
        {throw new NotSupportedException("Unqualified current Sigil body profile");}
        return(id,key,frames);
    }
    private static SigilTrackingState Tracking(SigilOwnerState saved)
    {
        var state=new SigilTrackingState{Expiry=saved.Expiry,Flags=saved.Flags};
        foreach(var stat in saved.Stats){state.Stats.Add((stat.Stat,stat.Layer),stat.Value);}return state;
    }
    private static SigilStat[] Stats(SigilTrackingState s)=>s.Stats.Select(v=>new SigilStat(v.Key.Stat,v.Key.Layer,v.Value)).ToArray();
    private static int Frame(SigilOwnerState state,DateTime at)
    {
        long frame=(at-state.EpochUtc).Ticks/TimeSpan.TicksPerMillisecond/40;
        if(frame is <1 or >int.MaxValue-32767){throw new NotSupportedException("Sigil owner clock is outside the qualified frame domain");}
        return(int)frame;
    }
    private static bool Cell(PlayerMobile owner,Map map,Point3D point)=>map!=null&&map!=Map.Internal&&point.X>=0&&point.Y>=0
        &&point.X<map.Width&&point.Y<map.Height&&point.Z is >=-128 and <=127&&!SpellHelper.IsTown(point,owner)
        &&map.CanFit(point,1,true,false,true)&&owner.InLOS(point);
    internal static bool Ground(PlayerMobile owner,Map map,Point3D requested,out Point3D ground)
    {
        // Current size1/mask5 placement checks terrain,not occupying actors.
        // Reuse native floor/static projection without leap's travel/multi or
        // mobile-occupancy restrictions,which do not belong to this spell.
        ground=requested;if(Cell(owner,map,ground)){return true;}
        if(map==null||map==Map.Internal||requested.X<0||requested.Y<0||requested.X>=map.Width||requested.Y>=map.Height){return false;}
        ground=new(requested.X,requested.Y,map.GetAverageZ(requested.X,requested.Y));if(Cell(owner,map,ground)){return true;}
        foreach(var tile in map.Tiles.GetStaticAndMultiTiles(requested.X,requested.Y))
        {
            var data=TileData.ItemTable[tile.ID&TileData.MaxItemValue];
            if(data.Surface&&!data.Impassable){ground=new(requested.X,requested.Y,tile.Z+data.CalcHeight);if(Cell(owner,map,ground)){return true;}}
        }
        return false;
    }
    internal static bool TargetPoint(PlayerMobile owner,Point3D requested,out Point3D point)
    {
        point=requested;return owner is{Alive:true,Deleted:false,Map:not null}&&owner.Map!=Map.Internal&&!MinionSafeAreas.Contains(owner)
            &&owner.InRange(requested,50)&&Ground(owner,owner.Map,requested,out point)
            &&SkillGeometry.SourceDistance((point.X-owner.X)*2,(point.Y-owner.Y)*2)<=100;
    }
    internal static SigilActor CreatePrepared(PlayerMobile owner,SkillValues values,Point3D point,DateTime at)=>Create(owner,values,point,at,null);
    internal static SigilActor CreatePaid(PlayerMobile owner,SkillValues values,Point3D point,DateTime at,int cost)
        =>cost==values.Mana256?Create(owner,values,point,at,()=>owner is{Alive:true,Deleted:false}&&NecromancerEffects.Pay(owner,cost))
            :throw new NotSupportedException("Sigil payment does not match its captured skill values");
    private static SigilActor Create(PlayerMobile owner,SkillValues values,Point3D point,DateTime at,Func<bool> payment)
    {
        if(owner is not{Alive:true,Deleted:false,Map:not null}||owner.Map==Map.Internal||at.Kind!=DateTimeKind.Utc
            ||values==null||!Supported(values.Ability)||values.Rank is <1 or >255||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)
            ||profile.Class!=DiabloClass.Warlock||MinionSafeAreas.Contains(owner)){return null;}
        var shape=Body(values);var map=owner.Map;
        if(!Ground(owner,map,point,out var ground)||!owner.InRange(ground,50)){return null;}
        var saved=_owners.GetValueOrDefault(owner.Serial.Value);
        if(saved!=null&&saved.EventUtc<=at){_owners=_owners.Remove(owner.Serial.Value);saved=null;}
        bool fresh=saved==null;saved??=new(owner.Serial.Value,at.AddMilliseconds(-40),[],0,0,at);
        int frame=Frame(saved,at);var born=saved.EpochUtc.AddMilliseconds(frame*40L);
        var tracking=Tracking(saved);SigilActor created=null;var resolved=new Dictionary<uint,SigilTrackedMissile>();
        bool Track(SigilTrackingRequest request)
        {
            if(request.State!=205||request.Limit!=1||request.Start!=frame||created?.Marker!=request.Missile||request.RemovalCallbackRva!=0x3a7f30)
            {throw new NotSupportedException("World Sigil lost its reviewed tracking context");}
            bool result=SigilTracking.Run(unchecked((int)request.Missile),request.Limit,request.Start,request.End,new(()=>new(tracking,fresh),guid=>
            {
                uint id=unchecked((uint)guid);
                if(!resolved.TryGetValue(id,out var actor))
                {
                    if(!_actors.TryGetValue(id,out var prior)||prior.Owner!=owner.Serial.Value||World.FindItem((Serial)id) is not{Deleted:false}){return null;}
                    actor=new(3,true,prior.Retiring?0:Math.Max(0,(float)(prior.BornUtc.AddMilliseconds(prior.Frames*40L)-at).TotalMilliseconds));resolved.Add(id,actor);
                }
                return actor;
            }));
            foreach(var pair in resolved)
            {if(pair.Value.Lifetime==0&&_actors.TryGetValue(pair.Key,out var prior)){_actors=_actors.SetItem(pair.Key,prior with{Retiring=true});}}
            _owners=_owners.SetItem(owner.Serial.Value,saved with{Stats=Stats(tracking),Expiry=tracking.Expiry,Flags=tracking.Flags});return result;
        }
        var context=new SigilLaunchContext(owner.Serial.Value,SkillMechanics.For(values.Ability).SourceId,values.Rank,1,Data.Value.MissileCount,0,205,frame);
        int result=SigilLaunch.Run(context,new(()=>true,()=>shape.Id,()=>{},id=>id==shape.Id&&Cell(owner,map,ground),
            ()=>new(ground.X*2,ground.Y*2),p=>SkillGeometry.SourceDistance(Math.Abs(p.X-owner.X*2),Math.Abs(p.Y-owner.Y*2)),request=>
            {
                if(request.Body.Flags!=1||request.Body.Missile!=shape.Id||request.Body.Point!=new Point2D(ground.X*2,ground.Y*2))
                {throw new NotSupportedException("World Sigil factory request changed");}
                var marker=new Static(0x1B7A){Name=SkillMechanics.For(values.Ability).Name+" (placeholder)",Movable=false};marker.MoveToWorld(ground,map);
                if(payment!=null&&!payment()){marker.Delete();return null;}
                created=new(marker.Serial.Value,owner.Serial.Value,map.MapID,ground,values.Rank,profile.Level,shape.Id,shape.Key,shape.Frames,born,Ability:values.Ability);
                _actors=_actors.Add(created.Marker,created);
                // Current flags1 stationary factory initializes float14 to
                // int Range*40 (then converts to float),captured at400cc8.
                return new(marker.Serial.Value,3,true,shape.Frames*40);
            },field=>field==0x198?checked((int)values.Eval("calc3")):throw new NotSupportedException("Unknown Sigil formula field"),Track,end=>
            {
                var current=_owners[owner.Serial.Value];_owners=_owners.SetItem(owner.Serial.Value,current with{EventUtc=current.EpochUtc.AddMilliseconds(end*40L)});
            }));
        return result==1?created:null;
    }
    internal static void Tick(DateTime at)
    {
        if(World.Loading){return;}if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Sigil clock must be UTC");}
        foreach(var s in _actors.Values)
        {
            var item=World.FindItem((Serial)s.Marker);var owner=World.FindMobile((Serial)s.Owner)as PlayerMobile;
            if(item is not{Deleted:false}||item.Map?.MapID!=s.Map||item.Location!=s.Origin||owner is not{Alive:true,Deleted:false}){Remove(s);continue;}
            long age=(at-s.BornUtc).Ticks/TimeSpan.TicksPerMillisecond/40;if(age<0){continue;}
            if(!s.Retiring&&age>s.Frames){Remove(s);continue;}
            int elapsed=s.Retiring?s.Frames:Math.Min((int)age,s.Frames),pulse=elapsed/6*6;
            bool fighting=AssassinTrapRoster.Fighting(s.Owner)&&owner.Map==item.Map&&!MinionSafeAreas.Contains(owner);
            bool terminal=s.Retiring||elapsed>=s.Frames;
            if(pulse>s.LastPulse)
            {
                var next=s with{LastPulse=pulse};_actors=_actors.SetItem(s.Marker,next);
                int driverFrame=terminal?elapsed:pulse,skillId=SkillMechanics.For(s.Ability).SourceId,recipient=RecipientFunction(s.Ability);
                var services=new GrimWardLifecycleServices(null,null,null,null,null,null,null,null,null,null,null,
                    _=>new(-1,0,6,recipient),null,null,(id,fn,skill,rank)=>
                    {
                        if(id!=unchecked((int)s.Marker)||fn!=recipient||skill!=skillId||rank!=s.Rank){throw new NotSupportedException("Sigil pulse source identity changed");}
                        if(fighting){Pulse?.Invoke(next,owner,at);}
                    },_=>terminal?2:1);
                _=GrimWardLifecycle.Tick(new(unchecked((int)s.Marker),3,s.Missile,s.Origin.X*2,s.Origin.Y*2,skillId,s.Rank),driverFrame,services);
            }
            if(terminal){Remove(s);}
        }
        foreach(var state in _owners.Values)
        {
            if(state.EventUtc<=at||World.FindMobile((Serial)state.Owner)is not PlayerMobile{Alive:true,Deleted:false})
            {_owners=_owners.Remove(state.Owner);} // Owner-state205 carries no recipient stats or player resource recovery.
        }
    }
    private static void Remove(SigilActor s){_actors=_actors.Remove(s.Marker);World.FindItem((Serial)s.Marker)?.Delete();}
    internal static void Clear(PlayerMobile owner)
    {foreach(var s in _actors.Values){if(s.Owner==owner.Serial.Value){Remove(s);}}_owners=_owners.Remove(owner.Serial.Value);}
    internal static SigilActorSave Snapshot()=>new(_actors.Values.OrderBy(s=>s.Marker).ToArray(),_owners.Values.OrderBy(s=>s.Owner).ToArray());
    internal static bool ClaimRancorPulse(SigilActor pulse)
    {
        if(pulse.Ability!=CombatAbility.SigilRancor||pulse.LastPulse<0||!_actors.TryGetValue(pulse.Marker,out var live)
            ||live!=pulse||(live.RancorPulse??-1)>=pulse.LastPulse){return false;}
        _actors=_actors.SetItem(live.Marker,live with{RancorPulse=pulse.LastPulse});return true;
    }
    internal static void Restore(SigilActorSave saved)
    {
        if(saved?.Actors==null||saved.Owners==null){throw new InvalidDataException("Missing Sigil state");}
        var actors=ImmutableDictionary<uint,SigilActor>.Empty;var owners=ImmutableDictionary<uint,SigilOwnerState>.Empty;
        foreach(var s in saved.Actors)
        {
            if(s==null||!((Serial)s.Marker).IsItem||!((Serial)s.Owner).IsMobile||s.Map<0||s.Map>=Map.Maps.Length||Map.Maps[s.Map]==null||Map.Maps[s.Map]==Map.Internal
                ||s.Origin.X<0||s.Origin.Y<0||s.Origin.X>=Map.Maps[s.Map].Width||s.Origin.Y>=Map.Maps[s.Map].Height
                ||s.Rank is <1 or >255||s.Level is <1 or >99||s.BornUtc.Kind!=DateTimeKind.Utc||s.BornUtc<=DateTime.UnixEpoch
                ||s.LastPulse is < -1||s.LastPulse>s.Frames||s.LastPulse!=-1&&s.LastPulse%6!=0||actors.ContainsKey(s.Marker))
            {throw new InvalidDataException("Invalid Sigil actor state");}
            var body=Body(Values(s));if(s.Missile!=body.Id||s.MissileKey!=body.Key||s.Frames!=body.Frames){throw new InvalidDataException("Saved Sigil body disagrees with pinned profile");}
            if(s.RancorPulse is{} pulse&&(s.Ability!=CombatAbility.SigilRancor||pulse<0||pulse%6!=0||pulse>s.LastPulse))
            {throw new InvalidDataException("Invalid prepared Rancor pulse cursor");}
            actors=actors.Add(s.Marker,s);
        }
        foreach(var s in saved.Owners)
        {
            if(s==null||!((Serial)s.Owner).IsMobile||s.EpochUtc.Kind!=DateTimeKind.Utc||s.EpochUtc<=DateTime.UnixEpoch||s.EventUtc.Kind!=DateTimeKind.Utc||s.EventUtc<s.EpochUtc
                ||!float.IsFinite(s.Expiry)||s.Expiry<0||(s.Flags&~2U)!=0||s.Stats==null||s.Stats.Length>17||owners.ContainsKey(s.Owner))
            {throw new InvalidDataException("Invalid Sigil owner state");}
            var keys=new HashSet<(int,int)>();
            foreach(var stat in s.Stats)
            {if(stat==null||stat.Stat is not(354 or 355)||stat.Layer is <0 or >8||stat.Stat==354&&stat.Layer==0||!keys.Add((stat.Stat,stat.Layer))){throw new InvalidDataException("Invalid Sigil tracking stat");}}
            int maximum=s.Stats.FirstOrDefault(v=>v.Stat==355&&v.Layer==0)?.Value??0;
            if(maximum is <0 or >8){throw new InvalidDataException("Invalid Sigil slot maximum");}
            owners=owners.Add(s.Owner,s);
        }
        _actors=actors;_owners=owners;
    }
}
