// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record WolverineLease(uint Recipient,uint Creator,uint Supplier,int Rank,int AttackPercent,int DamagePercent,DateTime UntilUtc,bool Control,int FlatDamage=0,CombatAbility Ability=CombatAbility.HeartOfWolverine);
internal sealed record WolverineSave(WolverineLease[] Leases,OakEmitter[] Emitters);
public sealed class WolverineAuraProfiles:GenericPersistence
{
    private WolverineSave _snapshot;
    public WolverineAuraProfiles():base("SanctuaryWolverineAuras",175){}
    public override void Serialize(){_snapshot=DruidWolverineAuras.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Wolverine aura version");}DruidWolverineAuras.Restore(JsonSerializer.Deserialize<WolverineSave>(reader.ReadString()));}
    public override void PostDeserialize()=>DruidWolverineAuras.Expire(TimedEffects.Now);
}

// Shared combat-spirit do65 lifetime/filter/rank contract. Keep the legacy
// Wolverine persistence name and default lease fields for old saves. Heart adds
// AR/physical ED; Barbs adds flat ATD in its own states. No resource callback.
internal static class DruidWolverineAuras
{
    private static Dictionary<(uint,bool,CombatAbility),WolverineLease> _leases=new();
    private static Dictionary<uint,OakEmitter> _emitters=new();
    public static WolverineAuraProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    private static bool Alive(Mobile m)=>m is{Alive:true,Deleted:false,IsDeadBondedPet:false};
    internal static WolverineLease State(Mobile m,bool control=false,CombatAbility ability=CombatAbility.HeartOfWolverine)=>Alive(m)?_leases.GetValueOrDefault((m.Serial.Value,control,ability)):null;
    internal static bool Managed(Mobile m)=>State(m)!=null || State(m,true)!=null || State(m,false,CombatAbility.SpiritOfBarbs)!=null || State(m,true,CombatAbility.SpiritOfBarbs)!=null;
    internal static int FlatDamage(Mobile m)=>(State(m,false,CombatAbility.SpiritOfBarbs)?.FlatDamage??0)+(State(m,true,CombatAbility.SpiritOfBarbs)?.FlatDamage??0);
    private readonly record struct AuraValues(int Radius,int AttackPercent,int DamagePercent,int FlatDamage);
    private static AuraValues Values(CombatAbility ability,int rank)
    {
        if(ability==CombatAbility.HeartOfWolverine){var a=DruidWolverineStats.Aura(rank);return new(a.Radius,a.AttackPercent,a.DamagePercent,0);}
        if(ability==CombatAbility.SpiritOfBarbs){var a=DruidBarbsStats.Aura(rank);return new(a.Radius,0,0,a.FlatDamage);}
        throw new NotSupportedException("Unsupported combat spirit aura");
    }
    internal static int AttackPercent(Mobile m)=>(State(m)?.AttackPercent??0)+(State(m,true)?.AttackPercent??0);
    internal static int DamagePercent(Mobile m)=>(State(m)?.DamagePercent??0)+(State(m,true)?.DamagePercent??0);
    internal static WolverineLease Replacement(WolverineLease old,uint target,uint source,int rank,DateTime at,bool control,CombatAbility ability=CombatAbility.HeartOfWolverine)
    {
        var aura=Values(ability,rank);
        if(old!=null&&old.Ability!=ability || at.Kind!=DateTimeKind.Utc || at>DateTime.MaxValue.AddMilliseconds(-DruidOakAuras.LeaseFrames*40)
            || !((Serial)target).IsMobile || !((Serial)source).IsMobile)
        {throw new ArgumentException("Unsupported Wolverine aura application");}
        if(old!=null && old.UntilUtc>at && old.Rank>rank){return null;}
        uint creator=old!=null&&old.UntilUtc>at&&old.Rank==rank?old.Creator:source;
        return new(target,creator,source,rank,aura.AttackPercent,aura.DamagePercent,at.AddMilliseconds(DruidOakAuras.LeaseFrames*40),control,aura.FlatDamage,ability);
    }
    internal static bool Apply(DruidOakBody source,Mobile target,int rank,DateTime at,bool control=false)
    {
        if(!Alive(source) || !Alive(target)){return false;}
        var key=(target.Serial.Value,control,source.SpiritAbility);var next=Replacement(_leases.GetValueOrDefault(key),target.Serial.Value,source.Serial.Value,rank,at,control,source.SpiritAbility);
        if(next==null){return false;}_leases[key]=next;return true;
    }
    internal static bool Pulse(DruidOakBody source,DateTime at)
    {
        if(!Alive(source) || source.Prepared is not{} state || source.ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner
            || state.Owner!=owner.Serial.Value || !PersistentMinions.Active(owner,source) || !source.Permitted(owner)
            || source.Map==null || source.Map==Map.Internal || source.Map!=owner.Map){return false;}
        int rank=DruidSpiritStats.EffectiveAuraRank(state.Birth.AuraRank,BattleCommand.Bonus(source));var aura=Values(source.SpiritAbility,rank);
        Apply(source,source,rank,at,true);
        var targets=new List<Mobile>();
        foreach(var target in source.Map.GetMobilesInRange<Mobile>(source.Location,(aura.Radius+1)/2))
        {if(DruidOakAuras.Recipient(source,owner,target,aura.Radius)){targets.Add(target);}}
        targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));foreach(var target in targets){Apply(source,target,rank,at);}
        return true;
    }
    internal static void Schedule(DruidOakBody source,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc || at>DateTime.MaxValue.AddMilliseconds(-DruidOakAuras.PeriodFrames*40)){throw new ArgumentException("Wolverine clock must be UTC");}
        _emitters[source.Serial.Value]=new(source.Serial.Value,at.AddMilliseconds(DruidOakAuras.PeriodFrames*40));
    }
    internal static void Advance(DruidOakBody source,DateTime at)
    {
        if(!_emitters.TryGetValue(source.Serial.Value,out var emitter)){Schedule(source,at);return;}
        if(emitter.NextUtc>at){return;}if(!Pulse(source,at)){Stop(source);return;}
        Schedule(source,at); // Retained no-lag/offline-pulse-replay adapter.
    }
    internal static void Stop(DruidOakBody source){if(source!=null){_emitters.Remove(source.Serial.Value);}}
    internal static void Clear(Mobile target)
    {if(target!=null){foreach(var key in _leases.Keys.Where(k=>k.Item1==target.Serial.Value).ToArray()){_leases.Remove(key);}}}
    internal static void Expire(DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Wolverine expiry clock must be UTC");}
        foreach(var lease in _leases.Values.Where(l=>l.UntilUtc<=at||!Alive(World.FindMobile((Serial)l.Recipient))).ToArray())
        {_leases.Remove((lease.Recipient,lease.Control,lease.Ability));}
        foreach(uint id in _emitters.Keys.ToArray())
        {if(World.FindMobile((Serial)id) is not DruidOakBody{Alive:true,Deleted:false,SpiritAbility:CombatAbility.HeartOfWolverine or CombatAbility.SpiritOfBarbs}){_emitters.Remove(id);}}
    }
    internal static WolverineSave Snapshot()=>new(_leases.Values.OrderBy(x=>x.Recipient).ThenBy(x=>x.Control).ThenBy(x=>x.Ability).ToArray(),_emitters.Values.OrderBy(x=>x.Source).ToArray());
    internal static void Restore(WolverineSave save)
    {
        if(save?.Leases==null || save.Emitters==null || save.Leases.Length>1000000 || save.Emitters.Length>1000000)
        {throw new InvalidDataException("Missing Wolverine aura state");}
        var leases=new Dictionary<(uint,bool,CombatAbility),WolverineLease>();var emitters=new Dictionary<uint,OakEmitter>();
        foreach(var lease in save.Leases)
        {
            if(lease==null || !((Serial)lease.Recipient).IsMobile || !((Serial)lease.Creator).IsMobile || !((Serial)lease.Supplier).IsMobile
                || lease.Ability is not(CombatAbility.HeartOfWolverine or CombatAbility.SpiritOfBarbs) || lease.Rank is <1 or >255 || lease.UntilUtc.Kind!=DateTimeKind.Utc)
            {throw new InvalidDataException("Invalid Wolverine aura lease");}
            var aura=Values(lease.Ability,lease.Rank);
            if(lease.AttackPercent!=aura.AttackPercent || lease.DamagePercent!=aura.DamagePercent || lease.FlatDamage!=aura.FlatDamage || !leases.TryAdd((lease.Recipient,lease.Control,lease.Ability),lease))
            {throw new InvalidDataException("Wolverine aura values or duplicate state");}
        }
        foreach(var emitter in save.Emitters)
        {
            if(emitter==null || !((Serial)emitter.Source).IsMobile || emitter.NextUtc.Kind!=DateTimeKind.Utc
                || emitter.NextUtc>DateTime.MaxValue.AddMilliseconds(-DruidOakAuras.PeriodFrames*40) || !emitters.TryAdd(emitter.Source,emitter))
            {throw new InvalidDataException("Invalid Wolverine emitter");}
        }
        _leases=leases;_emitters=emitters;
    }
}
