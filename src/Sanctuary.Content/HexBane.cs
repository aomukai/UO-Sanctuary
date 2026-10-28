// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record HexBaneValues(int Minimum,int Maximum,int AttackPercent,int BuffFrames,int DebuffFrames,int DefensePercent,int TargetAttackPercent,int Mana256);
internal sealed record HexBaneBuff(uint Owner,int Rank,DateTime UntilUtc,HexBaneValues Values);
internal sealed record HexBaneDebuff(uint Target,uint Creator,int Rank,DateTime UntilUtc,int DefensePercent,int AttackPercent);
internal sealed record HexBaneSnapshot(HexBaneBuff[] Buffs,HexBaneDebuff[] Debuffs);
public sealed class HexBaneProfiles:GenericPersistence
{
    public HexBaneProfiles():base("SanctuaryHexBane",183){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(HexBane.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Hex Bane version");}HexBane.Restore(JsonSerializer.Deserialize<HexBaneSnapshot>(reader.ReadString()));}
    public override void PostDeserialize()=>HexBane.Prune();
}
// Prepared self-enchantment and supplied debuff. Modern event36 delivery,
// group6/7 replacement and learned casting are deliberately not inferred.
public static class HexBane
{
    private static ImmutableDictionary<uint,HexBaneBuff> _buffs=ImmutableDictionary<uint,HexBaneBuff>.Empty;
    private static ImmutableDictionary<uint,HexBaneDebuff> _debuffs=ImmutableDictionary<uint,HexBaneDebuff>.Empty;
    public static HexBaneProfiles Profiles{get;private set;}
    public static void Configure(){Profiles=new();EventSink.WorldSave+=Prune;}
    private static bool Alive(Mobile p)=>p is{Alive:true,Deleted:false,IsDeadBondedPet:false};
    internal static HexBaneBuff Buff(Mobile p)=>Alive(p)&&p is PlayerMobile&&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock
        &&_buffs.TryGetValue(p.Serial.Value,out var b)&&b.UntilUtc>TimedEffects.Now?b:null;
    internal static HexBaneDebuff Debuff(Mobile p)=>Alive(p)&&_debuffs.TryGetValue(p.Serial.Value,out var d)&&d.UntilUtc>TimedEffects.Now?d:null;
    public static void Clear(Mobile p){if(p!=null){_buffs=_buffs.Remove(p.Serial.Value);_debuffs=_debuffs.Remove(p.Serial.Value);}}
    internal static void Prune()
    {
        foreach(var b in _buffs.Values){if(Buff(World.FindMobile((Serial)b.Owner))==null){_buffs=_buffs.Remove(b.Owner);}}
        foreach(var d in _debuffs.Values){if(Debuff(World.FindMobile((Serial)d.Target))==null){_debuffs=_debuffs.Remove(d.Target);}}
    }
    internal static HexBaneValues Capture(SkillValues v)
    {
        var s=SkillMechanics.For(CombatAbility.HexBane);
        if(v.Ability!=CombatAbility.HexBane||v.Rank is <1 or >255||s.SourceId!=385||s.Number("srvdofunc")!=23||s.Text("aurastate")!="hexbane"||s.Text("auratargetstate")!="hexbanedebuff"
            ||s.Text("passiveitype")!="weap"||s.Number("passivereqweaponcount")!=1||s.Text("passivecalc1")!="enma"||s.Text("passivecalc2")!="exma"||s.Text("passivecalc3")!="toht"
            ||s.Number("requiresweapon")!=1||s.Text("itypea1")!="weap"||s.Text("etypea1")!="h2h"||s.Text("EType")!="mag"||s.Text("auraevent1")!="domeleeattack"||s.Text("auraevent2")!="domissiledamage"||s.Text("auraevent3")!="hextrigger"
            ||Enumerable.Range(1,3).Any(i=>s.Number("auraeventfunc"+i)!=36))
        {throw new NotSupportedException("Unsupported Hex Bane route");}
        // enma/exma are whole-point mastered endpoints. Zero magic mastery is
        // the admitted prepared context; new magic-mastery placement is open.
        return new(v.Damage256(false)/256,v.Damage256(true)/256,checked((int)v.Eval("passivecalc3")),
            checked((int)v.Eval("auralencalc")),checked((int)v.Eval("calc1")),checked((int)v.Eval("aurastatcalc1")),checked((int)v.Eval("aurastatcalc2")),v.Mana256);
    }
    internal static bool CastPrepared(PlayerMobile p)
    {
        if(!WeaponHexes.CanCast(p)){return false;}WeaponHexes.GuardBuff(p,CombatAbility.HexBane);
        if(NamedElements.MasteryPercent(p,"mag")!=0){throw new NotSupportedException("Modern Hex Bane magic mastery timing remains unverified");}
        var v=SkillMechanics.Values(p,CombatAbility.HexBane);var values=Capture(v);var old=Buff(p);
        // Complete shared do23/factory preserves a stronger same-skill state.
        // Modern cross-hex group replacement is a separate unsupported route.
        if((old!=null&&old.Rank>v.Rank)||!NecromancerEffects.CanPay(p,values.Mana256)){return false;}
        if(!NecromancerEffects.Pay(p,values.Mana256)){return false;}
        var next=new HexBaneBuff(p.Serial.Value,v.Rank,TimedEffects.Now.AddMilliseconds(values.BuffFrames*40L),values);Validate(next);
        _buffs=_buffs.SetItem(next.Owner,next);p.FixedEffect(0x375A,10,15,0x48D,0);p.PlaySound(0x1E5); // Identified hex-enchant/cast placeholders.
        return true;
    }
    internal static int AttackPercent(Mobile p)
    {if(p is not PlayerMobile owner||!WeaponHexes.Equipped(owner)||Buff(p) is not{} b){return 0;}WeaponHexes.GuardBuff(p,CombatAbility.HexBane);return b.Values.AttackPercent;}
    internal static double WeaponMagic(Mobile p)
    {if(p is not PlayerMobile owner||!WeaponHexes.Equipped(owner)||Buff(p)==null){return 0;}WeaponHexes.GuardBuff(p,CombatAbility.HexBane);return WeaponMagic(p,Utility.RandomDouble());}
    internal static double WeaponMagic(Mobile p,double roll)
    {
        if(p is not PlayerMobile owner||!WeaponHexes.Equipped(owner)||Buff(p) is not{} b){return 0;}
        WeaponHexes.GuardBuff(p,CombatAbility.HexBane);
        if(NamedElements.MasteryPercent(owner,"mag")!=0){throw new NotSupportedException("Modern Hex Bane magic mastery timing remains unverified");}
        return b.Values.Minimum+(b.Values.Maximum-b.Values.Minimum)*Math.Clamp(roll,0,1);
    }
    internal static int DefensePercent(Mobile p)
    {if(Debuff(p) is not{} d){return 0;}WeaponHexes.GuardTarget(p,CombatAbility.HexBane);return d.DefensePercent;}
    internal static int TargetAttackPercent(Mobile p)
    {if(Debuff(p) is not{} d){return 0;}WeaponHexes.GuardTarget(p,CombatAbility.HexBane);return d.AttackPercent;}
    // The caller must establish event36 eligibility and target selection. No
    // generic hit/miss/hextrigger hook or speculative event ordering is added.
    internal static bool ApplyPrepared(PlayerMobile p,BaseCreature target,HexBaneBuff registered,bool eligible,DateTime at)
    {
        if(!eligible||!Alive(p)||registered==null||registered.Owner!=p.Serial.Value||registered.UntilUtc<=at||!ClassCreation.Profiles.TryGet(p.Serial,out var c)||c.Class!=DiabloClass.Warlock||!ClassCombat.IsHostile(p,target)||!SourceAuraTargets.Eligible(target)){return false;}
        WeaponHexes.GuardTarget(target,CombatAbility.HexBane);Validate(registered);var old=Debuff(target);
        if(old!=null&&old.Rank>registered.Rank){return false;}
        var until=at.AddMilliseconds(registered.Values.DebuffFrames*40L);
        var next=old!=null&&old.Rank==registered.Rank?old with{UntilUtc=until}:new(target.Serial.Value,p.Serial.Value,registered.Rank,until,registered.Values.DefensePercent,registered.Values.TargetAttackPercent);
        Validate(next);_debuffs=_debuffs.SetItem(next.Target,next);target.FixedEffect(0x375A,10,15,0x48D,0); // Identified hexbanedebuff placeholder.
        return true;
    }
    internal static HexBaneSnapshot Snapshot()=>new(_buffs.Values.OrderBy(x=>x.Owner).ToArray(),_debuffs.Values.OrderBy(x=>x.Target).ToArray());
    private static bool IsMobile(uint n)=>((Server.Serial)n).IsMobile;
    private static bool Time(DateTime at)=>at.Kind==DateTimeKind.Utc&&at> DateTime.UnixEpoch;
    private static void Validate(HexBaneBuff b)
    {
        if(b==null||!IsMobile(b.Owner)||b.Rank is <1 or >255||!Time(b.UntilUtc)||b.Values is not{} v||v.Minimum<0||v.Maximum<v.Minimum
            ||v.AttackPercent!=20+9*(b.Rank-1)||v.BuffFrames!=3600+300*(b.Rank-1)||v.DebuffFrames is <150 or >250||v.DebuffFrames%5!=0
            ||v.DefensePercent!=-Math.Min(30+b.Rank-1,50)||v.TargetAttackPercent!=-25||v.Mana256!=(12+b.Rank-1)*128
            ||!Enumerable.Range(0,61).Any(total=>{var row=SkillMechanics.For(CombatAbility.HexBane);var values=new SkillValues(row,k=>k==row.SourceKey?b.Rank:0,k=>k=="Consume"?total:0,99);return v.Minimum==values.Damage256(false)/256&&v.Maximum==values.Damage256(true)/256;}))
        {throw new InvalidDataException("Invalid Hex Bane buff");}
    }
    private static void Validate(HexBaneDebuff d)
    {
        if(d==null||!IsMobile(d.Target)||!IsMobile(d.Creator)||d.Rank is <1 or >255||!Time(d.UntilUtc)||d.DefensePercent!=-Math.Min(30+d.Rank-1,50)||d.AttackPercent!=-25)
        {throw new InvalidDataException("Invalid Hex Bane debuff");}
    }
    internal static void Restore(HexBaneSnapshot snapshot)
    {
        if(snapshot?.Buffs==null||snapshot.Debuffs==null||snapshot.Buffs.Length>1000000||snapshot.Debuffs.Length>1000000){throw new InvalidDataException("Missing Hex Bane snapshot");}
        var buffs=ImmutableDictionary<uint,HexBaneBuff>.Empty;var debuffs=ImmutableDictionary<uint,HexBaneDebuff>.Empty;
        foreach(var b in snapshot.Buffs){Validate(b);if(buffs.ContainsKey(b.Owner)){throw new InvalidDataException("Duplicate Hex Bane owner");}buffs=buffs.Add(b.Owner,b);}
        foreach(var d in snapshot.Debuffs){Validate(d);if(debuffs.ContainsKey(d.Target)){throw new InvalidDataException("Duplicate Hex Bane target");}debuffs=debuffs.Add(d.Target,d);}
        _buffs=buffs;_debuffs=debuffs;
    }
}
