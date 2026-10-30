// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary;

public sealed record FrozenEquipmentSnapshot(int Version,uint OriginalItem,string RuntimeType,int Layer,int ItemId,int Hue,string BaseCode,
    string GearJson,string PropertiesJson,string FixedJson,string LevelsJson,string UnavailableReason=null);
internal sealed record FrozenProperty(NamedProperty Property,NamedPropertyRoll Roll);
internal sealed record FrozenBonus(string Code,int Value);
internal sealed record FrozenEquipmentLevel(int Level,int Defense,int Minimum,int Maximum,int Life256,int Mana256);

// Value payload only: no item reference, item factory, loot move or reroll.
// Property parameters/full gear rolls remain available separately from numeric
// suppliers. This does not yet attach a stat graph or dispatch NPC item procs.
internal static class FrozenEquipmentEffects
{
    internal static FrozenEquipmentSnapshot Capture(Item item)
    {
        if(item is not{Deleted:false}){throw new NotSupportedException("No equipped original item");}
        var state=ClassGear.State(item);
        if(!ClassGear.Converted(item))
        {return new(0,item.Serial.Value,item.GetType().FullName,(int)item.Layer,item.ItemID,item.Hue,null,null,"[]","[]","[]","Native item combat effects have no converted equipment supplier");}
        bool active=ClassGear.Slot(item)!=GearSlot.Cosmetic&&!ClassGear.Broken(item)&&state is{Identified:true};
        var properties=active?(state.NamedItemId!=null?NamedGear.IntrinsicProperties(item):OrdinaryAffixProperties.Properties(state)).Select(p=>new FrozenProperty(p.Property,p.Roll)).ToArray():[];
        var fixedBonuses=new List<FrozenBonus>();
        if(active)
        {
            foreach(var gem in state.Gems)foreach(var effect in GemSockets.Effects(item,gem)){fixedBonuses.Add(new(effect.Code,effect.Minimum));}
            foreach(var rune in state.Runes){fixedBonuses.Add(new(rune.Pattern.EffectCode,rune.Percentage));}
            foreach(var material in state.MaterialRolls)
            {
                string code=material.Code switch{"resistance-fire"=>"res-fire","resistance-cold"=>"res-cold","resistance-lightning"=>"res-ltng","resistance-poison"=>"res-pois","resistance-physical"=>"res-physical",_=>null};
                if(code!=null){fixedBonuses.Add(new(code,material.Value));}
            }
        }
        var levels=Enumerable.Range(1,99).Select(level=>
        {
            var context=new ItemStatContext(item,level);var damage=context.WeaponDamage;
            return new FrozenEquipmentLevel(level,context.Defense,damage.Min,damage.Max,context.LifeBonus256,context.ManaBonus256);
        }).ToArray();
        var snapshot=new FrozenEquipmentSnapshot(0,item.Serial.Value,item.GetType().FullName,(int)item.Layer,item.ItemID,item.Hue,
            state?.BaseCode??ClassGear.Definition(item)?.BaseCode,state==null?null:JsonSerializer.Serialize(state),JsonSerializer.Serialize(properties),JsonSerializer.Serialize(fixedBonuses),JsonSerializer.Serialize(levels));
        Validate(snapshot);return snapshot;
    }
    internal static T Decode<T>(string json)where T:class
    {
        try{return JsonSerializer.Deserialize<T>(json)??throw new InvalidDataException("Missing frozen equipment payload");}
        catch(Exception e)when(e is JsonException or ArgumentException){throw new InvalidDataException("Invalid frozen equipment payload",e);}
    }
    internal static GearState Gear(FrozenEquipmentSnapshot snapshot)=>snapshot.GearJson==null?null:Decode<GearState>(snapshot.GearJson);
    internal static FrozenProperty[] Properties(FrozenEquipmentSnapshot snapshot)=>Decode<FrozenProperty[]>(snapshot.PropertiesJson);
    internal static void Validate(FrozenEquipmentSnapshot snapshot)
    {
        if(snapshot is null||snapshot.Version!=0||!((Serial)snapshot.OriginalItem).IsItem||string.IsNullOrWhiteSpace(snapshot.RuntimeType)
            ||snapshot.Layer<0||snapshot.ItemId<0||snapshot.Hue<0){throw new InvalidDataException("Invalid frozen equipment identity");}
        var properties=Properties(snapshot);var fixed_=Decode<FrozenBonus[]>(snapshot.FixedJson);var levels=Decode<FrozenEquipmentLevel[]>(snapshot.LevelsJson);
        if(properties.Any(p=>p?.Property==null||string.IsNullOrWhiteSpace(p.Property.Code))||fixed_.Any(p=>p==null||string.IsNullOrWhiteSpace(p.Code)))
        {throw new InvalidDataException("Invalid frozen equipment property");}
        if(snapshot.UnavailableReason!=null)
        {
            if(string.IsNullOrWhiteSpace(snapshot.UnavailableReason)||properties.Length!=0||fixed_.Length!=0||levels.Length!=0){throw new InvalidDataException("Unsupported equipment contains invented numeric suppliers");}
        }
        else if(string.IsNullOrWhiteSpace(snapshot.BaseCode)||levels.Length!=99||levels.Where((p,i)=>p==null||p.Level!=i+1||p.Defense<0||p.Minimum<0||p.Maximum<p.Minimum).Any())
        {throw new InvalidDataException("Incomplete frozen equipment numeric levels");}
        _=Gear(snapshot);
    }
    internal static FrozenEquipmentContext Bind(FrozenEquipmentSnapshot snapshot)=>new(snapshot);
}
internal sealed class FrozenEquipmentContext
{
    private readonly FrozenProperty[] _properties;
    private readonly FrozenBonus[] _fixed;
    private readonly FrozenEquipmentLevel[] _levels;
    private readonly string _unavailable;
    internal FrozenEquipmentContext(FrozenEquipmentSnapshot snapshot)
    {
        FrozenEquipmentEffects.Validate(snapshot);_unavailable=snapshot.UnavailableReason;
        _properties=FrozenEquipmentEffects.Properties(snapshot);
        _fixed=FrozenEquipmentEffects.Decode<FrozenBonus[]>(snapshot.FixedJson);
        _levels=FrozenEquipmentEffects.Decode<FrozenEquipmentLevel[]>(snapshot.LevelsJson);
    }
    private void Require(int level)
    {
        if(level is <1 or >99){throw new ArgumentOutOfRangeException(nameof(level));}
        if(_unavailable!=null){throw new NotSupportedException(_unavailable);}
    }
    internal int Bonus(string code,int level)
    {
        Require(level);int total=0;
        foreach(var p in _properties){total=checked(total+NamedPropertyRuntime.Bonus(p.Property,p.Roll,code,level));}
        foreach(var p in _fixed){if(p.Code==code){total=checked(total+p.Value);}}
        return total;
    }
    internal FrozenEquipmentLevel At(int level){Require(level);return _levels[level-1];}
}
