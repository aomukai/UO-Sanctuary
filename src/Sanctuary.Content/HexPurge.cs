// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record HexPurgeValues(int AttackPercent,int AttackRate,int BuffFrames,int Mana256);
// Raw unmastered source suppliers. Capture timing, geometry, chance resolution
// and charge consumption belong to the unsupported modern callbacks36/39.
internal sealed record HexPurgePlan(int Rank,int Minimum256,int Maximum256,int DebuffFrames,int ChanceRaw,int InitialCharges,int RadiusRaw);
internal sealed record HexPurgeBuff(uint Owner,int Rank,DateTime UntilUtc,HexPurgeValues Values);
internal sealed record HexPurgeTarget(uint Target,uint Creator,DateTime UntilUtc,HexPurgePlan Plan);
internal sealed record HexPurgeSnapshot(HexPurgeBuff[] Buffs,HexPurgeTarget[] Targets);
public sealed class HexPurgeProfiles:GenericPersistence
{
    public HexPurgeProfiles():base("SanctuaryHexPurge",185){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(HexPurge.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Hex Purge version");}HexPurge.Restore(JsonSerializer.Deserialize<HexPurgeSnapshot>(reader.ReadString()));}
    public override void PostDeserialize()=>HexPurge.Prune();
}
public static class HexPurge
{
    private static ImmutableDictionary<uint,HexPurgeBuff> _buffs=ImmutableDictionary<uint,HexPurgeBuff>.Empty;
    private static ImmutableDictionary<uint,HexPurgeTarget> _targets=ImmutableDictionary<uint,HexPurgeTarget>.Empty;
    public static HexPurgeProfiles Profiles{get;private set;}
    public static void Configure(){Profiles=new();EventSink.WorldSave+=Prune;}
    private static bool Alive(Mobile p)=>p is{Alive:true,Deleted:false,IsDeadBondedPet:false};
    internal static HexPurgeBuff Buff(Mobile p)=>Alive(p)&&p is PlayerMobile&&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock
        &&_buffs.TryGetValue(p.Serial.Value,out var b)&&b.UntilUtc>TimedEffects.Now?b:null;
    internal static HexPurgeTarget Debuff(Mobile p)=>Alive(p)&&_targets.TryGetValue(p.Serial.Value,out var t)&&t.UntilUtc>TimedEffects.Now?t:null;
    public static void Clear(Mobile p){if(p!=null){_buffs=_buffs.Remove(p.Serial.Value);_targets=_targets.Remove(p.Serial.Value);}}
    internal static void Prune()
    {
        foreach(var b in _buffs.Values){if(Buff(World.FindMobile((Serial)b.Owner))==null){_buffs=_buffs.Remove(b.Owner);}}
        foreach(var t in _targets.Values){if(Debuff(World.FindMobile((Serial)t.Target))==null){_targets=_targets.Remove(t.Target);}}
    }
    private static void Route(SkillValues v)
    {
        var s=SkillMechanics.For(CombatAbility.HexPurge);
        if(v.Ability!=CombatAbility.HexPurge||v.Rank is <1 or >255||s.SourceId!=389||s.Number("srvdofunc")!=23||s.Text("aurastate")!="hexpurge"||s.Text("auratargetstate")!="hexpurgedebuff"
            ||s.Text("passiveitype")!="weap"||s.Number("passivereqweaponcount")!=1||s.Text("passivestat1")!="attackrate"||s.Text("passivestat2")!="item_tohit_percent"
            ||s.Number("requiresweapon")!=1||s.Text("itypea1")!="weap"||s.Text("etypea1")!="h2h"||s.Text("auraevent1")!="domeleedamage"
            ||s.Text("auraevent2")!="domissiledamage"||s.Text("auraevent3")!="hextrigger"||Enumerable.Range(1,3).Any(i=>s.Number("auraeventfunc"+i)!=36)
            ||s.Parameter(1)!=39||s.Text("EType")!="mag"||s.Number("SrcDam")!=0||s.Number("ResultFlags")!=32)
        {throw new NotSupportedException("Unsupported Hex Purge route");}
    }
    internal static HexPurgeValues Capture(SkillValues v)
    {Route(v);return new(checked((int)v.Eval("passivecalc2")),checked((int)v.Eval("passivecalc1")),checked((int)v.Eval("auralencalc")),v.Mana256);}
    internal static HexPurgePlan Plan(SkillValues v)
    {Route(v);return new(v.Rank,v.Damage256(false),v.Damage256(true),checked((int)v.Eval("calc1")),checked((int)v.Eval("calc2")),checked((int)v.Eval("calc3")),checked((int)v.Eval("aurarangecalc")));}
    internal static HexPurgePlan Plan(PlayerMobile p)
    {
        if(!Alive(p)||!ClassCreation.Profiles.TryGet(p.Serial,out var c)||c.Class!=DiabloClass.Warlock||NamedElements.MasteryPercent(p,"mag")!=0)
        {throw new NotSupportedException("Unsupported Purge explosion context; modern magic mastery placement remains unverified");}
        return Plan(SkillMechanics.Values(p,CombatAbility.HexPurge));
    }
    internal static bool CastPrepared(PlayerMobile p)
    {
        if(!WeaponHexes.CanCast(p)){return false;}WeaponHexes.GuardBuff(p,CombatAbility.HexPurge);
        var v=SkillMechanics.Values(p,CombatAbility.HexPurge);var values=Capture(v);var old=Buff(p);
        if(old!=null&&old.Rank>v.Rank||!NecromancerEffects.CanPay(p,values.Mana256)){return false;}
        if(!NecromancerEffects.Pay(p,values.Mana256)){return false;}
        var next=new HexPurgeBuff(p.Serial.Value,v.Rank,TimedEffects.Now.AddMilliseconds(values.BuffFrames*40L),values);Validate(next);
        _buffs=_buffs.SetItem(next.Owner,next);p.FixedEffect(0x375A,10,15,0x48E,0);p.PlaySound(0x1E5); // Identified hex_enchant/cast placeholders.
        return true;
    }
    private static HexPurgeValues Active(Mobile p)
    {if(p is not PlayerMobile owner||!WeaponHexes.Equipped(owner)||Buff(p) is not{} b){return null;}WeaponHexes.GuardBuff(p,CombatAbility.HexPurge);return b.Values;}
    internal static int AttackPercent(Mobile p)=>Active(p)?.AttackPercent??0;
    internal static int AttackRate(Mobile p)=>Active(p)?.AttackRate??0;
    // Store a caller-supplied plan for an explicitly eligible event. This does
    // not select a target, roll a chance, detonate, spend charges or reapply.
    internal static bool ApplyPrepared(PlayerMobile p,BaseCreature target,HexPurgeBuff registered,HexPurgePlan supplied,bool eligible,DateTime at)
    {
        if(!eligible||!Alive(p)||registered==null||registered.Owner!=p.Serial.Value||registered.UntilUtc<=at||supplied?.Rank!=registered.Rank||!ClassCreation.Profiles.TryGet(p.Serial,out var c)||c.Class!=DiabloClass.Warlock||!ClassCombat.IsHostile(p,target)||!SourceAuraTargets.Eligible(target)){return false;}
        WeaponHexes.GuardTarget(target,CombatAbility.HexPurge);
        if(Debuff(target)!=null){throw new NotSupportedException("Modern Purge reapplication and charge reset remain unverified");}
        if(NamedElements.MasteryPercent(p,"mag")!=0){throw new NotSupportedException("Modern Purge magic mastery placement remains unverified");}
        Validate(registered);Validate(supplied);var next=new HexPurgeTarget(target.Serial.Value,p.Serial.Value,at.AddMilliseconds(supplied.DebuffFrames*40L),supplied);Validate(next);
        _targets=_targets.SetItem(next.Target,next);target.FixedEffect(0x375A,10,15,0x48E,0);return true; // Identified hexpurgedebuff placeholder.
    }
    internal static HexPurgeSnapshot Snapshot()=>new(_buffs.Values.OrderBy(x=>x.Owner).ToArray(),_targets.Values.OrderBy(x=>x.Target).ToArray());
    private static bool IsMobile(uint n)=>((Serial)n).IsMobile;
    private static bool Time(DateTime t)=>t.Kind==DateTimeKind.Utc&&t>DateTime.UnixEpoch;
    private static void Validate(HexPurgeBuff b)
    {if(b==null||!IsMobile(b.Owner)||b.Rank is <1 or >255||!Time(b.UntilUtc)||b.Values!=Capture(new(SkillMechanics.For(CombatAbility.HexPurge),k=>k=="Hex Purge"?b.Rank:0,_=>0,99))){throw new InvalidDataException("Invalid Hex Purge buff");}}
    private static void Validate(HexPurgePlan p)
    {
        if(p==null||p.Rank is <1 or >255||p.DebuffFrames is <150 or >250||p.DebuffFrames%5!=0||p.InitialCharges!=Math.Min(1+p.Rank/10,3)||p.RadiusRaw!=4||p.ChanceRaw<69+(p.Rank-1)/2||p.ChanceRaw>89+(p.Rank-1)/2)
        {throw new InvalidDataException("Invalid Hex Purge plan");}
        int blast=(p.DebuffFrames-150)/5;
        if(!Enumerable.Range(0,21).Any(bane=>{var v=new SkillValues(SkillMechanics.For(CombatAbility.HexPurge),k=>k=="Hex Purge"?p.Rank:0,k=>k=="Hex Bane"?bane:k=="Eldritch Blast"?blast:0,99);return v.Damage256(false)==p.Minimum256&&v.Damage256(true)==p.Maximum256;}))
        {throw new InvalidDataException("Inconsistent Hex Purge damage plan");}
    }
    private static void Validate(HexPurgeTarget t)
    {if(t==null||!IsMobile(t.Target)||!IsMobile(t.Creator)||!Time(t.UntilUtc)){throw new InvalidDataException("Invalid Hex Purge target");}Validate(t.Plan);}
    internal static void Restore(HexPurgeSnapshot snapshot)
    {
        if(snapshot?.Buffs==null||snapshot.Targets==null||snapshot.Buffs.Length>1000000||snapshot.Targets.Length>1000000){throw new InvalidDataException("Missing Hex Purge snapshot");}
        var buffs=ImmutableDictionary<uint,HexPurgeBuff>.Empty;var targets=ImmutableDictionary<uint,HexPurgeTarget>.Empty;
        foreach(var b in snapshot.Buffs){Validate(b);if(buffs.ContainsKey(b.Owner)){throw new InvalidDataException("Duplicate Hex Purge owner");}buffs=buffs.Add(b.Owner,b);}
        foreach(var t in snapshot.Targets){Validate(t);if(targets.ContainsKey(t.Target)){throw new InvalidDataException("Duplicate Hex Purge target");}targets=targets.Add(t.Target,t);}
        _buffs=buffs;_targets=targets;
    }
}
