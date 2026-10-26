// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;
public sealed record CycloneArmorState(uint Owner,int Rank,int Remaining256,int Maximum256,int DisplayPercent=0);
public sealed class CycloneArmorProfiles:GenericPersistence
{
    public CycloneArmorProfiles():base("SanctuaryCycloneArmor",164){}
    public override void Serialize(IGenericWriter writer)
    {writer.Write(0);writer.Write(JsonSerializer.Serialize(DruidCycloneArmor.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Cyclone Armor version");}DruidCycloneArmor.Restore(JsonSerializer.Deserialize<CycloneArmorState[]>(reader.ReadString()));}
    public override void PostDeserialize()=>DruidCycloneArmor.Prune();
}
public static class DruidCycloneArmor
{
    // Immutable snapshots avoid mutating live state from serialization workers.
    private static ImmutableDictionary<uint,CycloneArmorState> _states=ImmutableDictionary<uint,CycloneArmorState>.Empty;
    public static CycloneArmorProfiles Profiles{get;private set;}
    public static void Configure(){Profiles=new CycloneArmorProfiles();EventSink.WorldSave+=Prune;}
    internal static CycloneArmorState State(Mobile owner)=>owner is PlayerMobile{Alive:true,Deleted:false}
        && ClassCreation.Profiles.TryGet(owner.Serial,out var p) && p.Class==DiabloClass.Druid?_states.GetValueOrDefault(owner.Serial.Value):null;
    public static void Clear(Mobile owner){if(owner!=null){_states=_states.Remove(owner.Serial.Value);}}
    internal static void Prune()
    {foreach(var row in _states.Values){if(World.FindMobile((Serial)row.Owner) is not PlayerMobile p || State(p)==null){_states=_states.Remove(row.Owner);}}}
    internal static CycloneArmorState[] Snapshot()=>_states.Values.OrderBy(x=>x.Owner).ToArray();
    internal static void Validate(CycloneArmorState row)
    {
        if(row==null || !((Serial)row.Owner).IsMobile || row.Rank is <1 or >255 || row.Maximum256<=0
            || row.Remaining256<=0 || row.Remaining256>row.Maximum256 || row.DisplayPercent is <0 or >100)
        {throw new InvalidDataException("Invalid saved Cyclone Armor pool");}
    }
    internal static void Restore(CycloneArmorState[] rows)
    {
        if(rows==null || rows.Length>1000000){throw new InvalidDataException("Missing Cyclone Armor state");}
        var next=ImmutableDictionary<uint,CycloneArmorState>.Empty;
        foreach(var row in rows){Validate(row);if(next.ContainsKey(row.Owner)){throw new InvalidDataException("Duplicate Cyclone Armor owner");}next=next.Add(row.Owner,row);}
        _states=next;
    }
    internal static bool Cast(PlayerMobile owner,SkillValues values)
    {
        var definition=SkillMechanics.For(CombatAbility.CycloneArmor);
        if(values.Ability!=CombatAbility.CycloneArmor || definition.Number("srvdofunc")!=18
            || definition.Text("aurastate")!="cyclonearmor" || values.Eval("auralencalc")!=0)
        {throw new NotSupportedException("Unsupported Cyclone Armor cast route");}
        var previous=State(owner);
        // Group0 leaves the old state for the shared factory's rank check.
        // Current-build lower-rank recast observation is still pending.
        if(previous!=null && previous.Rank>values.Rank){return false;}
        int maximum=checked((int)values.Eval("aurastatcalc1"));
        if(maximum<=0 || maximum!=values.Eval("aurastatcalc2")){throw new NotSupportedException("Unsupported Cyclone Armor capacity");}
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        var state=new CycloneArmorState(owner.Serial.Value,values.Rank,maximum,maximum,previous?.Rank==values.Rank?previous.DisplayPercent:0);Validate(state);
        _states=_states.SetItem(owner.Serial.Value,state);
        owner.FixedEffect(0x375A,10,15,0x481,0);owner.PlaySound(0x1E5); // Identified cast/audio placeholders.
        return true;
    }
    public static GearDamage Absorb(Mobile owner,GearDamage damage)
    {
        if(State(owner) is not{} state || damage.Fire<=0 && damage.Cold<=0 && damage.Lightning<=0){return damage;}
        var hit=CycloneArmorPackets.Absorb(new(state.Remaining256,state.Maximum256,state.DisplayPercent),
            NamedElements.To256(damage.Fire),NamedElements.To256(damage.Cold),NamedElements.To256(damage.Lightning));
        if(hit.Removed){Clear(owner);}
        else{_states=_states.SetItem(state.Owner,state with{Remaining256=hit.Pool.Remaining256,DisplayPercent=hit.Pool.DisplayPercent});}
        return damage with{Fire=hit.Fire256/256.0,Cold=hit.Cold256/256.0,Lightning=hit.Lightning256/256.0};
    }
}
