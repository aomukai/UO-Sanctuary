// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;

namespace Sanctuary;

internal sealed record ItemPropertyCreationState(ulong Seed,bool Ethereal,bool HasProperties,EquipmentStatValue[] ItemBase,EquipmentStatValue[] Properties);

// Fresh, unattached item-owned numeric graph. Base initialization and the
// recipient's body/time/class/change services are explicit caller inputs.
// This is not a complete item factory or an NPC equipment-admission profile.
internal sealed class ItemPropertyCreationContext:EquipmentPropertyServices
{
    internal readonly EquipmentStatUnit Unit;
    private readonly EquipmentStatValues Values;
    private readonly EquipmentPropertyBase Base;
    private readonly bool IsWeapon,IsArmor,Throws;
    private EquipmentStatNode Properties;
    private ulong RandomSeed;
    internal bool Ethereal{get;private set;}
    internal int Draws{get;private set;}
    internal ulong Seed=>RandomSeed;
    internal uint Random(){Draws++;return SourceRandom.Roll(ref RandomSeed);}
    // Quality fallback restores the item-data low seed,not the unit/base RNG.
    internal void InitLow(uint seed)=>RandomSeed=SourceRandom.InitLow(unchecked((int)seed));
    internal ItemPropertyCreationContext(Item item,EquipmentStatValues values,EquipmentPropertyBase basis,bool weapon,bool armor,bool throwable,
        ulong seed,EquipmentStatValue[] initialBase)
    {
        if(item is not{Deleted:false}||values==null||initialBase==null||weapon&&armor
            ||initialBase.Select(v=>v.Packed).Distinct().Count()!=initialBase.Length
            ||initialBase.Any(v=>EquipmentStatValues.Definition(v.Stat)==null))
        {throw new ArgumentException("A live item,explicit services and unique known base stats are required");}
        Values=values;Base=basis;IsWeapon=weapon;IsArmor=armor;Throws=throwable;RandomSeed=seed;
        Unit=new(item,4);var original=initialBase.OrderBy(v=>v.Packed).ToArray();
        Unit.Stats=new(){Flags=EquipmentStatNode.Extended,OwnerKind=4,Owner=Unit,BaseValues=original,FullValues=(EquipmentStatValue[])original.Clone()};
    }
    internal static bool Admitted(EquipmentPropertyPlan plan)=>EquipmentPropertyWriter.CanCreate(plan)&&!plan.Steps.Any(s=>s.Function==10);
    internal void Apply(EquipmentPropertyPlan plan,EquipmentPropertyInput input)
    {
        if(!Admitted(plan)){throw new NotSupportedException("Fresh property needs verified current semantics: "+plan?.Code);}
        EquipmentPropertyWriter.Create(plan,input,0,this);
    }
    internal void ApplySkill(EquipmentPropertyPlan plan,EquipmentPropertyInput input,ItemSkillPropertyServices metadata)
        =>ItemSkillPropertyWriter.Apply(plan,input,metadata,this);
    internal override EquipmentPropertyBase Basis=>Base;
    internal override bool Weapon=>IsWeapon;
    internal override bool Armor=>IsArmor;
    internal override bool Throwable=>Throws;
    internal override int BaseValue(int stat)=>EquipmentStatValues.Base(Unit.Stats,EquipmentStatKey.Pack(stat),false);
    internal override void SetBase(int stat,int value)=>Values.SetBase(Unit.Stats,EquipmentStatKey.Pack(stat),value);
    internal override bool GetOrCreateList(int state,int flags)
    {
        if(state!=0||flags!=0x40){throw new NotSupportedException("Fresh affix context requires state0/magic list");}
        if(Properties==null){Properties=new(){Flags=0x40,OwnerKind=4};EquipmentStatGraph.Post(Unit,Properties,true,Values);}
        return true;
    }
    internal override int ListValue(int stat,int layer)=>EquipmentStatValues.Raw(Properties.BaseValues,EquipmentStatKey.Pack(stat,layer));
    internal override void SetList(int stat,int value,int layer)=>Values.SetBase(Properties,EquipmentStatKey.Pack(stat,layer),value);
    internal override void AddList(int stat,int value,int layer)=>SetList(stat,checked(ListValue(stat,layer)+value),layer);
    internal override int Roll(int bound)
    {
        if(bound<=0){throw new ArgumentOutOfRangeException(nameof(bound));}
        Draws++;return(int)SourceRandom.Limited(ref RandomSeed,bound);
    }
    internal override void SetEthereal()=>Ethereal=true;
    internal ItemPropertyCreationState Capture()=>new(RandomSeed,Ethereal,Properties!=null,(EquipmentStatValue[])Unit.Stats.BaseValues.Clone(),Properties==null?[]:(EquipmentStatValue[])Properties.BaseValues.Clone());
    internal static ItemPropertyCreationContext Restore(Item item,EquipmentStatValues values,EquipmentPropertyBase basis,bool weapon,bool armor,bool throwable,ItemPropertyCreationState state)
    {
        if(state is not{ItemBase:not null,Properties:not null}||!state.HasProperties&&state.Properties.Length!=0
            ||state.Properties.Select(v=>v.Packed).Distinct().Count()!=state.Properties.Length||state.Properties.Any(v=>EquipmentStatValues.Definition(v.Stat)==null))
        {throw new ArgumentException("Invalid fresh property graph snapshot");}
        var result=new ItemPropertyCreationContext(item,values,basis,weapon,armor,throwable,state.Seed,state.ItemBase){Ethereal=state.Ethereal};
        if(state.HasProperties)
        {
            result.Properties=new(){Flags=0x40,OwnerKind=4,BaseValues=state.Properties.OrderBy(v=>v.Packed).ToArray()};
            EquipmentStatGraph.Post(result.Unit,result.Properties,true,values);
        }
        return result;
    }
}
