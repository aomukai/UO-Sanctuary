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
using Server.Spells;

namespace Sanctuary;

internal sealed record GrimWardSpecies(string Source,bool Large,bool Small);
internal enum WardPhase{Start,Body,End}
internal sealed record GrimWardActor(uint Marker,uint Owner,uint Corpse,int Map,Point3D Origin,int StartClass,int BodyClass,
    int StartFrames,int Rank,int FindPotion,int Level,DateTime BornUtc,WardPhase Phase,int LastPulse=-1);
public sealed class GrimWardProfiles:GenericPersistence
{
    public GrimWardProfiles():base("SanctuaryGrimWards",243){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(GrimWardActors.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Grim Ward actor version");}
        GrimWardActors.Restore(JsonSerializer.Deserialize<GrimWardActor[]>(reader.ReadString()));
    }
}

// Paid learned input is a separate adapter. This controller binds the reviewed
// placement/child/body services to real corpses, items, UTC and recipients.
internal static class GrimWardActors
{
    private static ImmutableDictionary<uint,GrimWardActor> _states=ImmutableDictionary<uint,GrimWardActor>.Empty;
    private static readonly Dictionary<string,GrimWardSpecies> Species=new(StringComparer.OrdinalIgnoreCase);
    public static GrimWardProfiles Profiles{get;private set;}
    public static void Configure()
    {
        Profiles=new();
        foreach(var row in BalanceCatalog.Read<GrimWardSpecies>("grim-ward-species"))
        {if(string.IsNullOrWhiteSpace(row.Source)||!Species.TryAdd(row.Source,row)){throw new InvalidDataException("Invalid Ward species size");}}
    }
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static bool Target(PlayerMobile owner,Corpse corpse)=>CorpseSkillRules.Usable(owner,corpse,SkeletonPets.Range)
        &&CorpseSkillRules.Soft(corpse)&&CorpseSkillRules.Profiles.Find(corpse)?.Source is{} source&&Species.ContainsKey(source)
        &&SpellHelper.CheckTown(owner.Location,owner)&&SpellHelper.CheckTown(corpse.Location,owner);
    private static SkillValues Values(GrimWardActor s)=>new(SkillMechanics.For(CombatAbility.GrimWard),
        k=>k=="Grim Ward"?s.Rank:0,k=>k=="Find Potion"?s.FindPotion:0,s.Level);
    internal static GrimWardActor CreatePrepared(PlayerMobile owner,Corpse corpse,SkillValues context,DateTime at)
    {
        if(!Target(owner,corpse)||context?.Ability!=CombatAbility.GrimWard||context.Rank is <1 or >255||at.Kind!=DateTimeKind.Utc
            ||ClassCreation.Profiles.TryGet(owner.Serial,out var profile)!=true||profile.Class!=DiabloClass.Barbarian)
        {return null;}
        int hard=checked((int)context.Reference("skill","Find Potion","blvl"));
        if(hard is <0 or >255){throw new NotSupportedException("Ward hard-point context out of range");}
        var species=Species[CorpseSkillRules.Profiles.Find(corpse).Source];
        var map=corpse.Map;var points=new Dictionary<LeapCell,Point3D?>();
        Point3D? Ground(LeapCell c)
        {
            if(!points.TryGetValue(c,out var p))
            {p=LeapTerrain.Ground(owner,map,c.X/2,c.Y/2,corpse.Z,out var found)?found:null;points.Add(c,p);}
            return p;
        }
        GrimWardActor created=null;
        var services=new GrimWardLifecycleServices(_=>new(252,249,255),_=>new(1,1,0,corpse.X*2,corpse.Y*2,0,0),
            _=>Target(owner,corpse),(_,_,_)=>true,(id,x,y,size,mask,neighbor)=>
            {
                if(size!=2||mask!=0x1000||neighbor!=1){throw new NotSupportedException("Ward placement contract changed");}
                var cell=SourceFreeCoordinates.Find(new(x,y),c=>Ground(c)!=null,allowBlockedOriginFallback:true);
                return cell is{} chosen&&Ground(chosen)!=null?(true,chosen.X,chosen.Y):(false,0,0);
            },_=>(species.Large,species.Small),()=>1000,spawn=>
            {
                var origin=Ground(new(spawn.X,spawn.Y))??throw new NotSupportedException("Ward lost its accepted ground");
                var marker=new Static(0x1B7A){Name="Grim Ward startup (placeholder)",Movable=false};marker.MoveToWorld(origin,map);
                created=new(marker.Serial.Value,owner.Serial.Value,corpse.Serial.Value,map.MapID,origin,spawn.Missile,spawn.Missile+1,
                    spawn.Missile==249?5:8,context.Rank,hard,profile.Level,at,WardPhase.Start);
                _states=_states.Add(marker.Serial.Value,created);return unchecked((int)marker.Serial.Value);
            },_=>{},_=>{if(!CorpseSkillRules.Reserve(corpse)){throw new InvalidOperationException("Ward lost corpse reservation");}},_=>{},
            null,null,null,null,null);
        // The real corpse remains lootable. Source nodraw affects presentation;
        // NoSelect is the existing shared mechanical one-consumer reservation.
        return GrimWardLifecycle.Place(unchecked((int)owner.Serial.Value),150,context.Rank,services)?created:null;
    }
    private static GrimWardActor StartBody(GrimWardActor s,Item start)
    {
        GrimWardActor next=null;
        var services=new GrimWardLifecycleServices(_=>new(252,249,255),null,null,null,null,null,null,spawn=>
        {
            if(spawn.Missile!=s.BodyClass||spawn.Range!=1000){throw new NotSupportedException("Ward active child changed");}
            var body=new Static(0x1B7A){Name="Grim Ward active totem (placeholder)",Movable=false};body.MoveToWorld(s.Origin,start.Map);
            next=s with{Marker=body.Serial.Value,Phase=WardPhase.Body};return unchecked((int)body.Serial.Value);
        },null,null,null,_=>new(s.BodyClass,0,6,30),_=>unchecked((int)s.Owner),(_,_,_)=>checked((int)Values(s).Eval("calc1")),null,null);
        GrimWardLifecycle.StartHit(new(unchecked((int)s.Marker),3,s.StartClass,s.Origin.X*2,s.Origin.Y*2,150,s.Rank),services);
        if(next==null){throw new NotSupportedException("Ward failed to create its active child");}
        _states=_states.Remove(s.Marker).Add(next.Marker,next);start.Delete();return next;
    }
    private static bool Fighting(GrimWardActor s,PlayerMobile owner)=>AssassinTrapRoster.Fighting(s.Owner)&&owner.Map?.MapID==s.Map
        &&ClassCreation.Profiles.TryGet(owner.Serial,out var p)&&p.Class==DiabloClass.Barbarian;
    internal static void Tick(DateTime at)
    {
        if(World.Loading){return;}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Ward clock must be UTC");}
        foreach(var original in _states.Values)
        {
            var s=original;var marker=World.FindItem((Serial)s.Marker);var owner=World.FindMobile((Serial)s.Owner) as PlayerMobile;
            if(marker is not{Deleted:false}||marker.Map?.MapID!=s.Map||marker.Location!=s.Origin||owner is not{Deleted:false,Alive:true})
            {Remove(s);continue;}
            long frame=(at-s.BornUtc).Ticks/TimeSpan.TicksPerMillisecond/40;
            if(frame<0){continue;}
            if(frame>=s.StartFrames+1009){Remove(s);continue;}
            if(frame>=s.StartFrames+1000)
            {
                if(s.Phase!=WardPhase.End){_states=_states.SetItem(s.Marker,s with{Phase=WardPhase.End});marker.Name="Grim Ward expiry (placeholder)";}
                continue;
            }
            if(frame<s.StartFrames){continue;}
            if(s.Phase==WardPhase.Start){s=StartBody(s,marker);marker=World.FindItem((Serial)s.Marker);}
            int pulse=(int)(frame-s.StartFrames)/6*6;
            if(pulse<=s.LastPulse){continue;}
            // Advance before callbacks. Offline/other-facet intervals expire
            // normally and cannot be replayed as a burst on return or reload.
            var next=s with{LastPulse=pulse};_states=_states.SetItem(s.Marker,next);
            if(!Fighting(s,owner)){continue;}
            GrimWardWorldPulse.Step(marker,owner,s.BodyClass,s.Rank,1000,1000-pulse,()=>Values(s),()=>1,at,owner.Serial.Value);
        }
    }
    private static void Remove(GrimWardActor s)
    {_states=_states.Remove(s.Marker);World.FindItem((Serial)s.Marker)?.Delete();}
    internal static void Clear(PlayerMobile owner)
    {foreach(var s in _states.Values){if(s.Owner==owner.Serial.Value){Remove(s);}}}
    internal static GrimWardActor[] Snapshot()=>_states.Values.OrderBy(s=>s.Marker).ToArray();
    internal static void Restore(GrimWardActor[] states)
    {
        var next=ImmutableDictionary<uint,GrimWardActor>.Empty;
        foreach(var s in states??throw new InvalidDataException("Missing Ward actor states"))
        {
            if(s==null||!((Serial)s.Marker).IsItem||!((Serial)s.Corpse).IsItem||!((Serial)s.Owner).IsMobile||s.Marker==s.Corpse
                ||s.Map<0||s.Map>=Map.Maps.Length||Map.Maps[s.Map]==null||Map.Maps[s.Map]==Map.Internal
                ||s.Origin.X<0||s.Origin.Y<0||s.Origin.X>=Map.Maps[s.Map].Width||s.Origin.Y>=Map.Maps[s.Map].Height
                ||s.StartClass is not(249 or 252 or 255)||s.BodyClass!=s.StartClass+1||s.StartFrames!=(s.StartClass==249?5:8)
                ||s.Rank is <1 or >255||s.FindPotion is <0 or >255||s.Level is <1 or >99||s.BornUtc.Kind!=DateTimeKind.Utc
                ||s.Phase is <WardPhase.Start or >WardPhase.End||s.LastPulse is <-1 or >996||s.LastPulse!=-1&&s.LastPulse%6!=0
                ||s.Phase==WardPhase.Start&&s.LastPulse!=-1||next.ContainsKey(s.Marker))
            {throw new InvalidDataException("Invalid Ward actor state");}
            next=next.Add(s.Marker,s);
        }
        _states=next;
    }
}
