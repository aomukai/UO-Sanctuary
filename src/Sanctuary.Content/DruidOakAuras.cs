// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record OakLease(uint Recipient,uint Creator,uint Supplier,int Rank,int Percent,DateTime UntilUtc,bool Control);
internal sealed record OakEmitter(uint Source,DateTime NextUtc);
internal sealed record OakAuraSave(OakLease[] Leases,OakEmitter[] Emitters);
public sealed class OakAuraProfiles:GenericPersistence
{
    private OakAuraSave _snapshot;
    public OakAuraProfiles():base("SanctuaryOakSageAuras",174){}
    public override void Serialize(){_snapshot=DruidOakAuras.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Oak aura version");}DruidOakAuras.Restore(JsonSerializer.Deserialize<OakAuraSave>(reader.ReadString()));}
    public override void PostDeserialize()=>DruidOakAuras.Expire(TimedEffects.Now);
}

// do65's self/control and recipient states are distinct stat lists. Expiry is
// committed with the existing resource callback before getters lose the stat.
// Source radius uses the existing2subtiles/native tile and16height adapter.
internal static class DruidOakAuras
{
    private static Dictionary<(uint,bool),OakLease> _leases=new();
    private static Dictionary<uint,OakEmitter> _emitters=new();
    public static OakAuraProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    internal const int PeriodFrames=5,LeaseFrames=6;
    private static bool Alive(Mobile actor)=>actor is{Alive:true,Deleted:false,IsDeadBondedPet:false};
    internal static int Percent(Mobile actor)=>!Alive(actor)?0:
        (_leases.GetValueOrDefault((actor.Serial.Value,false))?.Percent??0)+(_leases.GetValueOrDefault((actor.Serial.Value,true))?.Percent??0);
    internal static OakLease State(Mobile target,bool control=false)=>Alive(target)?_leases.GetValueOrDefault((target.Serial.Value,control)):null;
    internal static OakLease Replacement(OakLease old,uint target,uint source,int rank,int percent,DateTime at,bool control)
    {
        if(rank is <1 or >255 || percent!=DruidOakStats.Aura(rank).LifePercent || at.Kind!=DateTimeKind.Utc
            || at>DateTime.MaxValue.AddMilliseconds(-LeaseFrames*40) || !((Serial)target).IsMobile || !((Serial)source).IsMobile)
        {throw new ArgumentException("Unsupported Oak aura application");}
        if(old!=null && old.UntilUtc>at && old.Rank>rank){return null;}
        uint creator=old!=null&&old.UntilUtc>at&&old.Rank==rank?old.Creator:source;
        return new(target,creator,source,rank,percent,at.AddMilliseconds(LeaseFrames*40),control);
    }
    internal static bool Apply(DruidOakBody source,Mobile target,int rank,DateTime at,bool control=false)
    {
        if(!Alive(source) || !Alive(target)){return false;}
        var key=(target.Serial.Value,control);
        var next=Replacement(_leases.GetValueOrDefault(key),target.Serial.Value,source.Serial.Value,rank,DruidOakStats.Aura(rank).LifePercent,at,control);
        if(next==null){return false;}
        var before=BattleOrders.Capture(target);_leases[key]=next;BattleOrders.Change(target,before);return true;
    }
    internal static bool Recipient(DruidOakBody source,PlayerMobile owner,Mobile target,int radius)
    {
        if(target==source || !Alive(target) || target.Map!=source.Map || Math.Abs(target.Z-source.Z)>=16
            || !AmazonInnerSight.InRadius(target.X-source.X,target.Y-source.Y,radius)
            || !ClassCombat.Friendly(owner,target) || SpellHelper.IsTown(target.Location,target))
        {return false;}
        return target is PlayerMobile || target is BaseCreature creature && SourceAuraTargets.Eligible(creature);
    }
    internal static bool Pulse(DruidOakBody source,DateTime at)
    {
        if(!Alive(source) || source.Prepared is not{} state || source.ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner
            || state.Owner!=owner.Serial.Value || !PersistentMinions.Active(owner,source) || !source.Permitted(owner)
            || source.Map==null || source.Map==Map.Internal || source.Map!=owner.Map)
        {return false;}
        int rank=state.Birth.AuraRank;var aura=DruidOakStats.Aura(rank);
        Apply(source,source,rank,at,true);
        var targets=new List<Mobile>();
        foreach(var target in source.Map.GetMobilesInRange<Mobile>(source.Location,(aura.Radius+1)/2))
        {if(Recipient(source,owner,target,aura.Radius)){targets.Add(target);}}
        targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));
        foreach(var target in targets){Apply(source,target,rank,at);}
        // No gameplay-visible final effect is asserted. XW and oak_sage art,
        // control visuals and summon/spirit audio remain separate assets.
        return true;
    }
    internal static void Schedule(DruidOakBody source,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc || at>DateTime.MaxValue.AddMilliseconds(-PeriodFrames*40)){throw new ArgumentException("Oak aura clock must be UTC");}
        _emitters[source.Serial.Value]=new(source.Serial.Value,at.AddMilliseconds(PeriodFrames*40));
    }
    internal static void Advance(DruidOakBody source,DateTime at)
    {
        if(!_emitters.TryGetValue(source.Serial.Value,out var emitter)){Schedule(source,at);return;}
        if(emitter.NextUtc>at){return;}
        if(!Pulse(source,at)){Stop(source);return;}
        // No offline/lag pulse replay. The current-executable global ordering
        // and resume phase remain unverified; this is the retained aura adapter.
        Schedule(source,at);
    }
    internal static void Stop(DruidOakBody source){if(source!=null){_emitters.Remove(source.Serial.Value);}}
    internal static void Clear(Mobile target)
    {
        if(target==null){return;}
        var before=Alive(target)?BattleOrders.Capture(target):default;
        bool changed=_leases.Remove((target.Serial.Value,false));changed=_leases.Remove((target.Serial.Value,true))||changed;
        if(changed&&Alive(target)){BattleOrders.Change(target,before);}
    }
    internal static void Expire(DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Oak expiry clock must be UTC");}
        // Each removed stat list invokes its resource callback. This native
        // state/serial ordering does not certify modern equal-event ordering.
        foreach(var lease in _leases.Values.Where(l=>l.UntilUtc<=at || !Alive(World.FindMobile((Serial)l.Recipient))).OrderBy(l=>l.Recipient).ThenBy(l=>l.Control).ToArray())
        {
            var target=World.FindMobile((Serial)lease.Recipient);var before=Alive(target)?BattleOrders.Capture(target):default;
            _leases.Remove((lease.Recipient,lease.Control));
            if(Alive(target)){BattleOrders.Change(target,before);}
        }
        foreach(uint id in _emitters.Keys.ToArray())
        {if(World.FindMobile((Serial)id) is not DruidOakBody{Alive:true,Deleted:false}){_emitters.Remove(id);}}
    }
    internal static OakAuraSave Snapshot()=>new(_leases.Values.OrderBy(x=>x.Recipient).ThenBy(x=>x.Control).ToArray(),_emitters.Values.OrderBy(x=>x.Source).ToArray());
    internal static void Restore(OakAuraSave save)
    {
        if(save?.Leases==null || save.Emitters==null || save.Leases.Length>1000000 || save.Emitters.Length>1000000)
        {throw new InvalidDataException("Missing Oak aura state");}
        var leases=new Dictionary<(uint,bool),OakLease>();var emitters=new Dictionary<uint,OakEmitter>();
        foreach(var lease in save.Leases)
        {
            if(lease==null || !((Serial)lease.Recipient).IsMobile || !((Serial)lease.Creator).IsMobile || !((Serial)lease.Supplier).IsMobile
                || lease.Rank is <1 or >255 || lease.Percent!=DruidOakStats.Aura(lease.Rank).LifePercent
                || lease.UntilUtc.Kind!=DateTimeKind.Utc || !leases.TryAdd((lease.Recipient,lease.Control),lease))
            {throw new InvalidDataException("Invalid Oak aura lease");}
        }
        foreach(var emitter in save.Emitters)
        {
            if(emitter==null || !((Serial)emitter.Source).IsMobile || emitter.NextUtc.Kind!=DateTimeKind.Utc
                || emitter.NextUtc>DateTime.MaxValue.AddMilliseconds(-PeriodFrames*40) || !emitters.TryAdd(emitter.Source,emitter))
            {throw new InvalidDataException("Invalid Oak aura emitter");}
        }
        _leases=leases;_emitters=emitters;
    }
}
