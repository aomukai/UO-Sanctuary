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

internal sealed record TaintedAuraEmitter(uint Actor,DateTime NextUtc);
public sealed class TaintedAuraProfiles:GenericPersistence
{
    private TaintedAuraEmitter[] _snapshot;
    public TaintedAuraProfiles():base("SanctuaryTaintedAuras",230){}
    public override void Serialize(){_snapshot=TaintedResistFire.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Tainted aura version");}TaintedResistFire.Restore(JsonSerializer.Deserialize<TaintedAuraEmitter[]>(reader.ReadString()));}
}

// Inherent right-skill selection from the positive summon grant. This is not
// an S1 cast or a substitute GenericPet movement/combat controller. Shared
// state3 lives in PaladinAuras, retaining its source skill identity.
internal static class TaintedResistFire
{
    private static readonly Dictionary<uint,TaintedAuraEmitter> Emitters=new();
    internal const int PeriodFrames=50,LeaseFrames=51;
    public static void Configure()=>_ = new TaintedAuraProfiles();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Tick(TimedEffects.Now));
    internal static TaintedAuraEmitter State(WarlockTaintedBody body)=>body==null?null:Emitters.GetValueOrDefault(body.Serial.Value);
    private static void Clock(DateTime at)
    {if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-LeaseFrames*40)){throw new ArgumentException("Tainted aura requires UTC");}}
    private static bool Attached(WarlockTaintedBody b,PlayerMobile owner)=>b is{Alive:true,Deleted:false}&&b.Prepared?.Owner==owner?.Serial.Value
        &&owner is{Alive:true,Deleted:false}&&b.ControlMaster==owner&&b.Map!=null&&b.Map!=Map.Internal&&owner.Map==b.Map
        &&PersistentMinions.Active(owner,b)&&b.Permitted(owner)&&!MinionSafeAreas.ShouldPark(owner,b);
    private static TaintedFireResistance Values(WarlockTaintedBody body)
    {
        var value=TaintedNpcSkills.ResistFire(body.Prepared.Snapshot.Birth.Suppliers with{ResistFireRank=TaintedNpcRanks.Level(body,420)});
        if(value.PeriodFrames!=PeriodFrames){throw new NotSupportedException("Unsupported Tainted aura period");}return value;
    }
    internal static void EnsureSelected(WarlockTaintedBody body,DateTime at)
    {
        Clock(at);if(body is{Alive:true,Deleted:false}&&body.Prepared!=null)
        {Emitters.TryAdd(body.Serial.Value,new(body.Serial.Value,at.AddMilliseconds(PeriodFrames*40)));}
    }
    internal static void Select(WarlockTaintedBody body,DateTime at)
    {
        Clock(at);bool previous=State(body)!=null;
        if(body is not{Alive:true,Deleted:false}||body.Prepared==null){throw new NotSupportedException("Tainted aura selection requires its captured NPC");}
        if(previous){PaladinAuras.ClearResistanceState(body);}
        Emitters[body.Serial.Value]=new(body.Serial.Value,at.AddMilliseconds(PeriodFrames*40));
        // Explicit creation of an offline prepared actor registers selection,
        // but does not buff other actors. Normal connected creation is immediate.
        if(body.ControlMaster is PlayerMobile{NetState:not null}){PulsePrepared(body,at);}
    }
    internal static bool Recipient(WarlockTaintedBody source,PlayerMobile owner,Mobile target,int radius)
        =>target!=source&&target is{Alive:true,Deleted:false}&&target.Map==source.Map&&Math.Abs(target.Z-source.Z)<16
            &&AmazonInnerSight.InRadius(target.X-source.X,target.Y-source.Y,radius)&&ClassCombat.Friendly(owner,target)
            &&!SpellHelper.IsTown(target.Location,target)&&CorpseSkillRules.AllowsAura(target)
            &&(target is PlayerMobile||target is BaseCreature creature&&SourceAuraTargets.Eligible(creature));
    internal static bool PulsePrepared(WarlockTaintedBody body,DateTime at)
    {
        Clock(at);var owner=body?.ControlMaster as PlayerMobile;if(!Attached(body,owner)||State(body)==null){return false;}
        var aura=Values(body);var until=at.AddMilliseconds(LeaseFrames*40);
        PaladinAuras.ApplyNpcResistance(body,body,420,aura.Rank,aura.Percent,at,until);
        foreach(var target in body.Map.GetMobilesInRange<Mobile>(body.Location,(aura.Radius+1)/2))
        {if(Recipient(body,owner,target,aura.Radius)){PaladinAuras.ApplyNpcResistance(body,target,420,aura.Rank,aura.Percent,at,until);}}
        body.FixedEffect(0x375A,10,10,0x489,0); // Identified resistance-aura placeholder.
        return true;
    }
    internal static GenericPetActionServices Bind(WarlockTaintedBody body,GenericPetActionServices outer,Func<DateTime> now)
    {
        ArgumentNullException.ThrowIfNull(outer);ArgumentNullException.ThrowIfNull(now);
        return outer with{SetSkill=(side,skill,owner)=>
        {
            if(skill!=420){outer.SetSkill(side,skill,owner);return;}
            if(side!=0||owner!=-1){throw new NotSupportedException("Unsupported Tainted aura skill ownership/selection");}
            Select(body,now());
        }};
    }
    internal static void Tick(DateTime at,bool? allowActions=null)
    {
        Clock(at);
        foreach(var emitter in Emitters.Values.Where(s=>s.NextUtc<=at).OrderBy(s=>s.NextUtc).ThenBy(s=>s.Actor).ToArray())
        {
            if(World.FindMobile((Serial)emitter.Actor) is not WarlockTaintedBody{Alive:true,Deleted:false} body){Emitters.Remove(emitter.Actor);continue;}
            if(Attached(body,body.ControlMaster as PlayerMobile)&&(allowActions??body.ControlMaster.NetState!=null)){PulsePrepared(body,at);}
            // Selected auras survive parking/offline periods, without pulses
            // or catch-up bursts. Existing recipient leases expire normally.
            Emitters[emitter.Actor]=emitter with{NextUtc=at.AddMilliseconds(PeriodFrames*40)};
        }
    }
    internal static void Stop(WarlockTaintedBody body)
    {if(body!=null){Emitters.Remove(body.Serial.Value);PaladinAuras.ClearResistanceState(body);}}
    internal static void Forget(WarlockTaintedBody body){if(body!=null){Emitters.Remove(body.Serial.Value);}}
    internal static TaintedAuraEmitter[] Snapshot()=>Emitters.Values.Where(s=>World.FindMobile((Serial)s.Actor) is WarlockTaintedBody{Alive:true,Deleted:false})
        .OrderBy(s=>s.Actor).ToArray();
    internal static void Restore(TaintedAuraEmitter[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing selected Tainted aura state");}var rows=new Dictionary<uint,TaintedAuraEmitter>();
        foreach(var s in saved)
        {
            if(s==null||!((Serial)s.Actor).IsMobile||s.NextUtc.Kind!=DateTimeKind.Utc||s.NextUtc>DateTime.MaxValue.AddMilliseconds(-LeaseFrames*40)||!rows.TryAdd(s.Actor,s))
            {throw new InvalidDataException("Invalid selected Tainted aura state");}
        }
        Emitters.Clear();foreach(var pair in rows){Emitters.Add(pair.Key,pair.Value);}
    }
}
