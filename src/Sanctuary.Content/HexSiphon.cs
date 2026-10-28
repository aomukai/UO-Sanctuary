// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record HexSiphonValues(int AttackPercent,int LifeAfterKill,int ManaAfterKill,int BuffFrames,int DebuffFrames,int DamagePercent,int Mana256);
internal sealed record HexSiphonBuff(uint Owner,int Rank,DateTime UntilUtc,HexSiphonValues Values);
internal sealed record HexSiphonDebuff(uint Target,uint Creator,int Rank,DateTime UntilUtc,int DamagePercent);
internal sealed record HexSiphonSnapshot(HexSiphonBuff[] Buffs,HexSiphonDebuff[] Debuffs);
public sealed class HexSiphonProfiles:GenericPersistence
{
    public HexSiphonProfiles():base("SanctuaryHexSiphon",184){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(HexSiphon.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Hex Siphon version");}HexSiphon.Restore(JsonSerializer.Deserialize<HexSiphonSnapshot>(reader.ReadString()));}
    public override void PostDeserialize()=>HexSiphon.Prune();
}
// Prepared do23 self-state and supplied event36receiver. Kill returns use the
// existing generic item-kill route; no new hex-specific kill callback is invented.
public static class HexSiphon
{
    private static ImmutableDictionary<uint,HexSiphonBuff> _buffs=ImmutableDictionary<uint,HexSiphonBuff>.Empty;
    private static ImmutableDictionary<uint,HexSiphonDebuff> _debuffs=ImmutableDictionary<uint,HexSiphonDebuff>.Empty;
    public static HexSiphonProfiles Profiles{get;private set;}
    public static void Configure(){Profiles=new();EventSink.WorldSave+=Prune;}
    private static bool Alive(Mobile p)=>p is{Alive:true,Deleted:false,IsDeadBondedPet:false};
    internal static HexSiphonBuff Buff(Mobile p)=>Alive(p)&&p is PlayerMobile&&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock
        &&_buffs.TryGetValue(p.Serial.Value,out var b)&&b.UntilUtc>TimedEffects.Now?b:null;
    internal static HexSiphonDebuff Debuff(Mobile p)=>Alive(p)&&_debuffs.TryGetValue(p.Serial.Value,out var d)&&d.UntilUtc>TimedEffects.Now?d:null;
    public static void Clear(Mobile p){if(p!=null){_buffs=_buffs.Remove(p.Serial.Value);_debuffs=_debuffs.Remove(p.Serial.Value);}}
    internal static void Prune()
    {
        foreach(var b in _buffs.Values){if(Buff(World.FindMobile((Serial)b.Owner))==null){_buffs=_buffs.Remove(b.Owner);}}
        foreach(var d in _debuffs.Values){if(Debuff(World.FindMobile((Serial)d.Target))==null){_debuffs=_debuffs.Remove(d.Target);}}
    }
    internal static HexSiphonValues Capture(SkillValues v)
    {
        var s=SkillMechanics.For(CombatAbility.HexSiphon);
        if(v.Ability!=CombatAbility.HexSiphon||v.Rank is <1 or >255||s.SourceId!=386||s.Number("srvdofunc")!=23||s.Text("aurastate")!="hexsiphon"||s.Text("auratargetstate")!="hexsiphondebuff"
            ||s.Text("passiveitype")!="weap"||s.Number("passivereqweaponcount")!=1||s.Text("passivestat1")!="item_tohit_percent"||s.Text("passivestat3")!="item_healafterkill"||s.Text("passivestat5")!="item_manaafterkill"
            ||s.Text("aurastat1")!="damagepercent"||s.Number("requiresweapon")!=1||s.Text("itypea1")!="weap"||s.Text("etypea1")!="h2h"
            ||s.Text("auraevent1")!="domeleeattack"||s.Text("auraevent2")!="domissiledamage"||s.Text("auraevent3")!="hextrigger"||Enumerable.Range(1,3).Any(i=>s.Number("auraeventfunc"+i)!=36))
        {throw new NotSupportedException("Unsupported Hex Siphon route");}
        return new(checked((int)v.Eval("passivecalc1")),checked((int)v.Eval("passivecalc3")),checked((int)v.Eval("passivecalc5")),
            checked((int)v.Eval("auralencalc")),checked((int)v.Eval("calc1")),checked((int)v.Eval("aurastatcalc1")),v.Mana256);
    }
    internal static bool CastPrepared(PlayerMobile p)
    {
        if(!WeaponHexes.CanCast(p)){return false;}WeaponHexes.GuardBuff(p,CombatAbility.HexSiphon);
        var v=SkillMechanics.Values(p,CombatAbility.HexSiphon);var values=Capture(v);var old=Buff(p);
        if(old!=null&&old.Rank>v.Rank||!NecromancerEffects.CanPay(p,values.Mana256)){return false;}
        if(!NecromancerEffects.Pay(p,values.Mana256)){return false;}
        var next=new HexSiphonBuff(p.Serial.Value,v.Rank,TimedEffects.Now.AddMilliseconds(values.BuffFrames*40L),values);Validate(next);
        _buffs=_buffs.SetItem(next.Owner,next);p.FixedEffect(0x375A,10,15,0x48C,0);p.PlaySound(0x1E5); // Identified hex_enchant/cast placeholders.
        return true;
    }
    private static HexSiphonValues Active(Mobile p)
    {
        if(p is not PlayerMobile owner||!WeaponHexes.Equipped(owner)||Buff(p) is not{} b){return null;}
        WeaponHexes.GuardBuff(p,CombatAbility.HexSiphon);return b.Values;
    }
    internal static int AttackPercent(Mobile p)=>Active(p)?.AttackPercent??0;
    internal static (int Life,int Mana) AfterKill(PlayerMobile p)=>Active(p) is{} v?(v.LifeAfterKill,v.ManaAfterKill):(0,0);
    internal static int DamagePercent(Mobile p)
    {if(Debuff(p) is not{} d){return 0;}WeaponHexes.GuardTarget(p,CombatAbility.HexSiphon);return d.DamagePercent;}
    internal static bool ApplyPrepared(PlayerMobile p,BaseCreature target,HexSiphonBuff registered,bool eligible,DateTime at)
    {
        if(!eligible||!Alive(p)||registered==null||registered.Owner!=p.Serial.Value||registered.UntilUtc<=at||!ClassCreation.Profiles.TryGet(p.Serial,out var c)||c.Class!=DiabloClass.Warlock||!ClassCombat.IsHostile(p,target)||!SourceAuraTargets.Eligible(target)){return false;}
        WeaponHexes.GuardTarget(target,CombatAbility.HexSiphon);Validate(registered);var old=Debuff(target);
        if(old!=null&&old.Rank>registered.Rank){return false;}
        var until=at.AddMilliseconds(registered.Values.DebuffFrames*40L);
        var next=old!=null&&old.Rank==registered.Rank?old with{UntilUtc=until}:new(target.Serial.Value,p.Serial.Value,registered.Rank,until,registered.Values.DamagePercent);
        Validate(next);_debuffs=_debuffs.SetItem(next.Target,next);target.FixedEffect(0x375A,10,15,0x48C,0); // Identified hexsyphondebuff placeholder.
        return true;
    }
    internal static HexSiphonSnapshot Snapshot()=>new(_buffs.Values.OrderBy(x=>x.Owner).ToArray(),_debuffs.Values.OrderBy(x=>x.Target).ToArray());
    private static bool IsMobile(uint n)=>((Serial)n).IsMobile;
    private static bool Time(DateTime t)=>t.Kind==DateTimeKind.Utc&&t>DateTime.UnixEpoch;
    private static void Validate(HexSiphonBuff b)
    {
        if(b==null||!IsMobile(b.Owner)||b.Rank is <1 or >255||!Time(b.UntilUtc)||b.Values is not{} v||v.AttackPercent!=20+9*(b.Rank-1)||v.LifeAfterKill<b.Rank||v.LifeAfterKill>b.Rank+20||v.ManaAfterKill!=v.LifeAfterKill
            ||v.BuffFrames!=3600+300*(b.Rank-1)||v.DebuffFrames is <150 or >250||v.DebuffFrames%5!=0||v.DamagePercent!=-33||v.Mana256!=(25+b.Rank-1)*128)
        {throw new InvalidDataException("Invalid Hex Siphon buff");}
    }
    private static void Validate(HexSiphonDebuff d)
    {if(d==null||!IsMobile(d.Target)||!IsMobile(d.Creator)||d.Rank is <1 or >255||!Time(d.UntilUtc)||d.DamagePercent!=-33){throw new InvalidDataException("Invalid Hex Siphon debuff");}}
    internal static void Restore(HexSiphonSnapshot snapshot)
    {
        if(snapshot?.Buffs==null||snapshot.Debuffs==null||snapshot.Buffs.Length>1000000||snapshot.Debuffs.Length>1000000){throw new InvalidDataException("Missing Hex Siphon snapshot");}
        var buffs=ImmutableDictionary<uint,HexSiphonBuff>.Empty;var debuffs=ImmutableDictionary<uint,HexSiphonDebuff>.Empty;
        foreach(var b in snapshot.Buffs){Validate(b);if(buffs.ContainsKey(b.Owner)){throw new InvalidDataException("Duplicate Hex Siphon owner");}buffs=buffs.Add(b.Owner,b);}
        foreach(var d in snapshot.Debuffs){Validate(d);if(debuffs.ContainsKey(d.Target)){throw new InvalidDataException("Duplicate Hex Siphon target");}debuffs=debuffs.Add(d.Target,d);}
        _buffs=buffs;_debuffs=debuffs;
    }
}
