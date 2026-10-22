// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record HolyShieldState(uint Owner,int Rank,int BlockBonus,DateTime UntilUtc);
public sealed class HolyShieldProfiles:GenericPersistence
{
    public HolyShieldProfiles():base("SanctuaryHolyShields",127){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(HolyShields.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Holy Shield version");}
        HolyShields.Restore(JsonSerializer.Deserialize<HolyShieldState[]>(reader.ReadString()));
    }
}
public static class HolyShields
{
    private static readonly Dictionary<uint,HolyShieldState> _states=new();
    private static readonly List<uint> _expired=new();
    public static HolyShieldProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static HolyShieldState State(Mobile owner)=>owner is{Alive:true,Deleted:false} && _states.TryGetValue(owner.Serial.Value,out var state)
        && state.UntilUtc>TimedEffects.Now?state:null;
    public static Item Shield(PlayerMobile owner)
    {
        foreach(var item in owner.Items)
        {if(ClassGear.Slot(item)==GearSlot.Shield && ClassGear.Active(owner,item)){return item;}}
        return null;
    }
    public static int BlockBonus(PlayerMobile owner)=>Shield(owner)!=null?State(owner)?.BlockBonus ?? 0:0;
    public static int DefensePercent(PlayerMobile owner)=>Shield(owner)!=null && State(owner) is{} state
        ?(int)SkillMechanics.Values(owner,CombatAbility.HolyShield,state.Rank).Eval("calc1"):0;
    public static (int Min256,int Max256) SmiteBonus(PlayerMobile owner)
    {
        if(Shield(owner)==null || State(owner) is not{} state){return(0,0);}
        var definition=SkillMechanics.For(CombatAbility.HolyShield);
        int Damage(bool maximum)=>SkillValues.Tier(definition.Number(maximum?"MaxDam":"MinDam"),state.Rank,
            tier=>definition.Number((maximum?"MaxLevDam":"MinLevDam")+tier))<<definition.Number("HitShift");
        return(Damage(false),Damage(true));
    }
    public static void Cast(PlayerMobile owner,SkillValues values)
    {
        _states[owner.Serial.Value]=new(owner.Serial.Value,values.Rank,(int)values.Eval("aurastatcalc1"),TimedEffects.Now.AddMilliseconds(values.Eval("auralencalc")*40));
        owner.FixedEffect(0x373A,10,15,0x481,0); // Placeholder enchanted shield overlay.
    }
    public static void Clear(Mobile owner)=>_states.Remove(owner.Serial.Value);
    public static HolyShieldState[] Snapshot()
    {
        _expired.Clear();foreach(var row in _states)
        {if(row.Value.UntilUtc<=TimedEffects.Now || World.FindMobile((Serial)row.Key) is not{Alive:true,Deleted:false}){_expired.Add(row.Key);}}
        foreach(uint owner in _expired){_states.Remove(owner);}
        return new List<HolyShieldState>(_states.Values).ToArray();
    }
    public static void Restore(HolyShieldState[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing Holy Shields");}
        _states.Clear();
        foreach(var state in saved)
        {if(state.Owner==0 || state.Rank<1 || state.BlockBonus is <0 or >40 || !_states.TryAdd(state.Owner,state)){throw new InvalidDataException("Invalid Holy Shield");}}
    }
}
