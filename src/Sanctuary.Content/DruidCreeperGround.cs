// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record CreeperGround(long Id,uint Actor,uint Owner,int Map,int Rank,CreeperFields Fields,DateTime NextUtc,string Fault=null)
{public CreeperPoisonContext PoisonContext{get;init;}}
internal sealed record CreeperGroundSave(CreeperGround[] Flights,long NextId);

public sealed class CreeperGroundProfiles:GenericPersistence
{
    private CreeperGroundSave _snapshot;
    public CreeperGroundProfiles():base("SanctuaryCreeperGround",173){}
    public override void Serialize(){_snapshot=DruidCreeperGround.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Creeper ground version");}DruidCreeperGround.Restore(JsonSerializer.Deserialize<CreeperGroundSave>(reader.ReadString()));}
    public override void PostDeserialize()
    {foreach(var body in World.Mobiles.Values.OfType<DruidCreeperBody>().ToArray()){DruidCreeperGround.Migrate(body);}}
}

// Independent fields outlive their NPC. Original-player allegiance is retained
// as a declared adapter. New groups carry provisional birth-Rabies contexts and
// actual poison packets; legacy capture-only groups remain nondamaging.
public static class DruidCreeperGround
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidCreeperGround));
    private static ImmutableDictionary<long,CreeperGround> _flights=ImmutableDictionary<long,CreeperGround>.Empty;
    private static HashSet<long> _loaded=new();private static long _nextId;
    public static CreeperGroundProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    private static CreeperGround Copy(CreeperGround s)=>s with{Fields=DruidCreeperFields.Copy(s.Fields)};
    internal static CreeperGroundSave Snapshot()=>new(_flights.Values.OrderBy(x=>x.Id).Select(Copy).ToArray(),_nextId);
    internal static CreeperGround Get(long id)=>_flights.TryGetValue(id,out var s)?Copy(s):null;
    internal static void Cancel(Mobile owner)
    {if(owner!=null){foreach(var id in _flights.Values.Where(x=>x.Owner==owner.Serial.Value).Select(x=>x.Id).ToArray()){Remove(id);}}}
    private static void Remove(long id){_flights=_flights.Remove(id);_loaded.Remove(id);}
    internal static long Create(DruidCreeperBody body,int rank,CreeperHeadRequest[] heads,DateTime at)
    {
        if(body is not{Deleted:false,Alive:true} || body.ControlMaster is not PlayerMobile owner || body.Map==null || body.Map==Map.Internal
            || body.Prepared?.Owner!=owner.Serial.Value || at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("Invalid Creeper field supplier");}
        if(heads?.Length==0){return 0;}
        var fields=DruidCreeperFields.Launch(DruidCreeperFields.Empty(),rank,heads,AmazonSlowMissiles.Capture(body,true).VelocityPercent,body.Z);
        return Add(body.Serial.Value,owner.Serial.Value,body.Map.MapID,rank,fields,at.AddMilliseconds(40),DruidCreeperPoison.Capture(body.Prepared.Snapshot));
    }
    private static long Add(uint actor,uint owner,int map,int rank,CreeperFields fields,DateTime next,CreeperPoisonContext poison=null)
    {
        long id=checked(_nextId+1);var flight=new CreeperGround(id,actor,owner,map,rank,fields,next){PoisonContext=poison};Validate(flight,id);
        _flights=_flights.Add(id,Copy(flight));_nextId=id;return id;
    }
    internal static void Migrate(DruidCreeperBody body)
    {
        if(body.Deleted || body.Prepared is not{Controller:{} s} state || s.Fields.Heads.Length==0&&s.Fields.Mats.Length==0){return;}
        if(s.LastFieldId!=0){throw new InvalidDataException("Embedded Creeper fields already have an independent identity");}
        // Legacy fields move once; their launches, age and collider history are
        // not replayed. Newly loaded legacy groups discard elapsed actions too.
        long id=Add(body.Serial.Value,state.Owner,s.Map,state.Snapshot.Birth.VineAttackRank,s.Fields,s.NextUtc);
        body.RestorePrepared(state with{Controller=s with{Fields=DruidCreeperFields.Empty(),LastFieldId=id}});
        if(World.Loading || body.ControllerLoaded){_loaded.Add(id);}
    }
    private static bool Eligible(PlayerMobile owner,Map map,CreeperMat mat,BaseCreature target)
        =>target is{Alive:true,Deleted:false,Blessed:false} && target.Map==map && target.CanBeDamaged() && target is not BaseVendor
            && Math.Abs(target.Z-mat.Z)<16 && CreatureProfiles.For(target).Traits.Size is >=1 and <=3
            && (target is EncounterCreature || SourceAuraTargets.Rule(CreatureProfiles.Definition(target).Reference) is{Attackable:true})
            && !target.IsDeadBondedPet && !target.Hidden && !PaladinConversion.Active(target)
            && !target.Controlled && !target.Summoned && target.GetMaster()==null && !target.InitialInnocent
            && target.FightMode is not(FightMode.None or FightMode.Aggressor or FightMode.Evil)
            && target.IsEnemy(owner) && !SpellHelper.IsTown(target.Location,owner) && owner.CanBeHarmful(target,false);
    private static CreeperFields Step(CreeperGround s,PlayerMobile owner)
    {
        var map=Map.Maps[s.Map];var actor=World.FindMobile((Serial)s.Actor) as DruidCreeperBody;
        bool hasOwner=actor is{Deleted:false} && actor.Map==map && actor.Prepared?.Owner==s.Owner;
        uint Recipient(CreeperMat mat)
        {
            foreach(var target in map.GetMobilesInRange<BaseCreature>(new Point3D((mat.X+1)/2,(mat.Y+1)/2,mat.Z),3))
            {
                int size=CreatureProfiles.For(target).Traits.Size??2;
                if(target.Serial.Value!=mat.LastCollision && Eligible(owner,map,mat,target)
                    && CombatHitboxes.SourceTouches(3,size,mat.X-target.X*2,mat.Y-target.Y*2)){return target.Serial.Value;}
            }
            return 0;
        }
        // Complete legacy child wrapper refuses a missing owner and distances
        // above100. Source/native coordinate and room projection is explicit.
        bool Child(CreeperHead head)
        {
            if(!hasOwner){return false;}
            var cell=actor.Prepared.Controller is{} controller?DruidCreeperWorld.Cell(controller):new Point2D(actor.X*2,actor.Y*2);
            return SkillGeometry.SourceDistance(cell.X-(int)Math.Floor(head.X*2+.5),cell.Y-(int)Math.Floor(head.Y*2+.5))<=100;
        }
        return DruidCreeperFields.Step(s.Fields,true,(x,y,z)=>x<=0||y<=0||x>=map.Width||y>=map.Height
            || !map.CanFit(new Point3D((int)Math.Round(x),(int)Math.Round(y),z),1,false,false,false),Recipient,_=>true,Child,hasOwner,
            mat=>s.PoisonContext==null?mat:DruidCreeperPoison.Initialize(mat,s.PoisonContext,AmazonSlowMissiles.Capture(actor,true).DamagePercent),
            (mat,id)=>DruidCreeperPoison.Deliver(owner,World.FindMobile((Serial)id) as BaseCreature,mat,s.NextUtc));
    }
    // Optional owner scope isolates injected fixture clocks. The ordinary
    // always-running conductor advances every owner without supplying it.
    internal static void Tick(DateTime at,bool? actions=null,uint? ownerScope=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Creeper field clock must be UTC");}
        foreach(var original in _flights.Values.Where(x=>x.NextUtc<=at&&(!ownerScope.HasValue||x.Owner==ownerScope.Value)).OrderBy(x=>x.NextUtc).ThenByDescending(x=>x.Id).ToArray())
        {
            var owner=World.FindMobile((Serial)original.Owner) as PlayerMobile;
            if(owner is not{Deleted:false,Alive:true}){Remove(original.Id);continue;}
            var s=original;
            bool active=s.Fault==null && (actions??owner.NetState is{Running:true}) && owner.Map!=null && owner.Map!=Map.Internal
                && ClassCreation.Profiles.TryGet(owner.Serial,out var profile) && profile.Class==DiabloClass.Druid;
            if(_loaded.Remove(s.Id) || !active)
            {
                long frames=(at-s.NextUtc).Ticks/TimeSpan.FromMilliseconds(40).Ticks+1;
                s=s with{Fields=DruidCreeperFields.Inactive(s.Fields,frames),NextUtc=s.NextUtc.AddTicks(checked(frames*TimeSpan.FromMilliseconds(40).Ticks))};
            }
            else
            {
                try
                {
                    while(s.NextUtc<=at && (s.Fields.Heads.Length!=0 || s.Fields.Mats.Length!=0))
                    {s=s with{Fields=Step(s,owner),NextUtc=s.NextUtc.AddMilliseconds(40)};}
                }
                catch(Exception e){s=s with{Fault=e.Message};Log.Error(e,"Creeper field flight refused unsupported context: {Id}",s.Id);}
            }
            if(s.Fields.Heads.Length==0 && s.Fields.Mats.Length==0){Remove(s.Id);}else{_flights=_flights.SetItem(s.Id,s);}
        }
    }
    private static void Validate(CreeperGround s,long nextId)
    {
        if(s==null || s.Id<=0 || s.Id>nextId || !((Serial)s.Actor).IsMobile || !((Serial)s.Owner).IsMobile || s.Rank is <1 or >255
            || s.Map<0 || s.Map>=Map.Maps.Length || Map.Maps[s.Map]==null || Map.Maps[s.Map]==Map.Internal || s.NextUtc.Kind!=DateTimeKind.Utc)
        {throw new InvalidDataException("Invalid Creeper ground identity");}
        DruidCreeperFields.Validate(s.Fields);
        if(s.PoisonContext!=null)
        {
            DruidCreeperPoison.Validate(s.PoisonContext);
            if(s.Fields.Mats.Any(m=>m.Poison==null)){throw new InvalidDataException("New Creeper poison context has a missing mat packet");}
        }
        foreach(var mat in s.Fields.Mats.Where(m=>m.Poison!=null))
        {
            if(s.PoisonContext==null || mat.Poison.Damage!=DruidCreeperPoison.Damage(mat.Rank,s.PoisonContext,mat.Poison.SlowDamagePercent))
            {throw new InvalidDataException("Creeper mat poison disagrees with its captured context");}
        }
        var map=Map.Maps[s.Map];
        if(s.Fields.Heads.Any(h=>h.Rank!=s.Rank || h.X<0 || h.Y<0 || h.X>=map.Width || h.Y>=map.Height)
            || s.Fields.Mats.Any(m=>m.Rank!=s.Rank || m.X>=map.Width*2 || m.Y>=map.Height*2))
        {throw new InvalidDataException("Invalid Creeper field facet or rank");}
    }
    internal static void Restore(CreeperGroundSave saved)
    {
        if(saved?.Flights==null || saved.NextId<0 || saved.Flights.Length>100000){throw new InvalidDataException("Missing Creeper ground state");}
        var next=ImmutableDictionary<long,CreeperGround>.Empty;
        foreach(var s in saved.Flights)
        {Validate(s,saved.NextId);if(next.ContainsKey(s.Id)){throw new InvalidDataException("Duplicate Creeper ground flight");}next=next.Add(s.Id,Copy(s));}
        _flights=next;_nextId=saved.NextId;_loaded=new(next.Keys);
    }
}
