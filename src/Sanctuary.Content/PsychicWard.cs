// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record PsychicWardState(uint Owner,int Rank,int Remaining256,int Maximum256,int StunFrames);
public sealed class PsychicWardProfiles:GenericPersistence
{
    public PsychicWardProfiles():base("SanctuaryPsychicWard",182){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(PsychicWard.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Psychic Ward version");}PsychicWard.Restore(JsonSerializer.Deserialize<PsychicWardState[]>(reader.ReadString()));}
    public override void PostDeserialize()=>PsychicWard.Prune();
}
// Prepared buff and physical-pool consumer. Learned casting and automatic
// reactive event37 delivery stay gated; no guessed on-hit/HP-loss hook is added.
public static class PsychicWard
{
    private static ImmutableDictionary<uint,PsychicWardState> _states=ImmutableDictionary<uint,PsychicWardState>.Empty;
    public static PsychicWardProfiles Profiles{get;private set;}
    internal static Func<bool> Allocate=()=>true;
    public static void Configure(){Profiles=new();EventSink.WorldSave+=Prune;}
    internal static PsychicWardState State(Mobile owner)=>owner is PlayerMobile{Alive:true,Deleted:false}
        &&ClassCreation.Profiles.TryGet(owner.Serial,out var p)&&p.Class==DiabloClass.Warlock?_states.GetValueOrDefault(owner.Serial.Value):null;
    public static void Clear(Mobile owner){if(owner!=null){_states=_states.Remove(owner.Serial.Value);}}
    internal static void Prune()
    {foreach(var row in _states.Values){if(World.FindMobile((Serial)row.Owner) is not PlayerMobile p||State(p)==null){_states=_states.Remove(row.Owner);}}}
    internal static PsychicWardState[] Snapshot()=>_states.Values.OrderBy(x=>x.Owner).ToArray();
    private static void Validate(PsychicWardState row)
    {
        if(row==null||!((Serial)row.Owner).IsMobile||row.Rank is <1 or >255||row.Maximum256<=0||row.Remaining256<=0||row.Remaining256>row.Maximum256||row.StunFrames is <37 or >85)
        {throw new InvalidDataException("Invalid Psychic Ward pool");}
        var s=SkillMechanics.For(CombatAbility.PsychicWard);
        var v=new SkillValues(s,key=>key==s.SourceKey?row.Rank:0,_=>0,1);
        int extra=row.Maximum256/256-checked((int)v.Eval("aurastatcalc1")/256);
        if(row.Maximum256%256!=0||extra is <0 or >600||extra%15!=0||row.StunFrames!=v.Eval("calc1"))
        {throw new InvalidDataException("Inconsistent Psychic Ward capture");}
    }
    internal static void Restore(PsychicWardState[] rows)
    {
        if(rows==null||rows.Length>1000000){throw new InvalidDataException("Missing Psychic Ward snapshot");}
        var next=ImmutableDictionary<uint,PsychicWardState>.Empty;
        foreach(var row in rows){Validate(row);if(next.ContainsKey(row.Owner)){throw new InvalidDataException("Duplicate Psychic Ward owner");}next=next.Add(row.Owner,row);}
        _states=next;
    }
    internal static (int Capacity256,int StunFrames,int Mana256) Values(SkillValues values)
    {
        var s=SkillMechanics.For(CombatAbility.PsychicWard);
        if(values.Ability!=CombatAbility.PsychicWard||values.Rank is <1 or >255||s.SourceId!=387||s.Number("srvdofunc")!=18||s.Text("aurastate")!="psychicward"
            ||s.Text("aurastat1")!="psychicward"||s.Text("aurastat2")!="psychicwardmax"||s.Text("auraevent1")!="absorbdamage"||s.Number("auraeventfunc1")!=40
            ||s.Text("auraevent2")!="damagedinmelee"||s.Number("auraeventfunc2")!=37||values.Eval("auralencalc")!=0)
        {throw new NotSupportedException("Unsupported Psychic Ward route");}
        int capacity=checked((int)values.Eval("aurastatcalc1"));
        if(capacity<=0||capacity!=values.Eval("aurastatcalc2")){throw new NotSupportedException("Unsupported Psychic Ward capacity");}
        return(capacity,checked((int)values.Eval("calc1")),values.Mana256);
    }
    internal static bool CastPrepared(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map==Map.Internal||!ClassCreation.Profiles.TryGet(owner.Serial,out var p)||p.Class!=DiabloClass.Warlock){return false;}
        if(SorceressEffects.Shield(owner)!=null){throw new NotSupportedException("Psychic Ward/Energy Shield priority remains unverified");}
        var v=SkillMechanics.Values(owner,CombatAbility.PsychicWard);var values=Values(v);
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        // do18 clears its group before the state factory. Preserve that order,
        // including loss of the previous armor if allocation fails.
        DefensiveArmors.ClearGroup(owner);
        if(!Allocate()||!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        var state=new PsychicWardState(owner.Serial.Value,v.Rank,values.Capacity256,values.Capacity256,values.StunFrames);Validate(state);
        _states=_states.SetItem(state.Owner,state);
        owner.FixedEffect(0x375A,10,15,0x48D,0);owner.PlaySound(0x1E5); // Identified shield/cast placeholders.
        return true;
    }
    internal static double AbsorbPhysical(Mobile owner,double damage)
    {
        if(damage<=0||State(owner) is not{} state){return damage;}
        if(SorceressEffects.Shield(owner)!=null){throw new NotSupportedException("Psychic Ward/Energy Shield priority remains unverified");}
        var step=SourceAbsorptionPool.Take(state.Remaining256,NamedElements.To256(damage));
        if(step.Remaining256==0){Clear(owner);}else{_states=_states.SetItem(state.Owner,state with{Remaining256=step.Remaining256});}
        return step.Damage256/256.0;
    }
    // The caller supplies an already-established eligible event and its
    // captured registration state. This does not choose pre/post-absorb timing
    // or whether a fully absorbed/breaking hit delivers modern event37.
    internal static bool ReactPrepared(PlayerMobile owner,PsychicWardState registered,BaseCreature attacker,bool eligible,DateTime at)
    {
        if(!eligible||registered==null||registered.Owner!=owner?.Serial.Value||owner is not{Alive:true,Deleted:false}
            ||!ClassCreation.Profiles.TryGet(owner.Serial,out var p)||p.Class!=DiabloClass.Warlock||!ClassCombat.IsHostile(owner,attacker)){return false;}
        Validate(registered);return CombatStuns.Apply(attacker,registered.StunFrames,at);
    }
}
