// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Sanctuary;

// Share the named-property interpretation while retaining ordinary affix IDs,
// original rolled scalar values and the existing one-int-per-modifier save layout.
// NPC/current-catalogue rolls must provide their own versioned definitions;
// never resolve their row IDs through the legacy ordinary AffixBalance registry.
public static class OrdinaryAffixProperties
{
    private static readonly ConcurrentDictionary<AffixModifier,NamedProperty> _templates=new();
    internal static NamedProperty Template(AffixModifier mod,string slot="0")
    {
        var template=_templates.GetOrAdd(mod,CreateTemplate);
        return slot=="0"?template:template with{Slot=slot};
    }
    private static NamedProperty CreateTemplate(AffixModifier mod)
    {
        JsonElement? parameter=string.IsNullOrEmpty(mod.Parameter)?null:
            int.TryParse(mod.Parameter,NumberStyles.Integer,CultureInfo.InvariantCulture,out int numeric)
                ? JsonSerializer.SerializeToElement(numeric):JsonSerializer.SerializeToElement(mod.Parameter);
        // properties.txt func20 always adds the indestructible flag, including
        // the ordinary "of Ages" row whose original min/max are both zero.
        return new("0",mod.Code,mod.Code,true,parameter,mod.Code=="indestruct"?1:mod.Min,mod.Code=="indestruct"?1:mod.Max);
    }
    internal static bool ValidMode(AffixModifier mod)=>mod.Mode switch
    {
        ModifierMode.Numeric=>!mod.Code.EndsWith("/lvl",StringComparison.Ordinal)
            && mod.Code is not("rep-dur" or "rep-quant" or "dmg-fire" or "dmg-ltng" or "dmg-cold" or "dmg-pois") && mod.Min<=mod.Max,
        ModifierMode.DamageRange=>(mod.Code is "dmg-fire" or "dmg-ltng" or "dmg-cold" or "dmg-pois") && mod.Min<=mod.Max,
        ModifierMode.Parameter=>(mod.Code.EndsWith("/lvl",StringComparison.Ordinal) || mod.Code is "rep-dur" or "rep-quant")
            && int.TryParse(mod.Parameter,NumberStyles.Integer,CultureInfo.InvariantCulture,out int parameter) && parameter>=0,
        _=>false
    };
    internal static NamedPropertyRoll Projection(AffixModifier mod,int? stored,string slot="0")
    {
        if(!ValidMode(mod)){throw new InvalidDataException("Unsupported ordinary property representation");}
        if(mod.Mode==ModifierMode.Numeric)
        {
            if(stored is not{} value || value<mod.Min || value>mod.Max){throw new InvalidDataException("Invalid saved ordinary scalar");}
            return new(slot,mod.Code=="indestruct"?1:value,mod.Code=="indestruct"?1:value);
        }
        if(stored!=null){throw new InvalidDataException("Ordinary range/parameter property acquired a scalar roll");}
        if(mod.Mode==ModifierMode.DamageRange){return new(slot,mod.Min,mod.Max);}
        int parameter=NamedPropertyRuntime.Parameter(Template(mod,slot));return new(slot,parameter,parameter);
    }
    internal static IEnumerable<(NamedProperty Property,NamedPropertyRoll Roll)> Properties(GearState state)
    {
        if(state is not{NamedItemId:null}){yield break;}
        foreach(var affix in state.Roll.Affixes)
        {
            for(int i=0;i<affix.Modifiers.Length;i++)
            {
                var mod=affix.Modifiers[i];var definition=affix.Definition.Modifiers[i];
                yield return(Template(definition),Projection(definition,mod.Value));
            }
        }
    }
    internal static int Bonus(GearState state,string code,int level)
    {
        int total=0;foreach(var (property,roll) in Properties(state)){total+=NamedPropertyRuntime.Bonus(property,roll,code,level);}return total;
    }
    // Intrinsic maintenance/quantity/indestructibility applies independently of
    // equipped/identified bonuses, matching the existing named-item consumers.
    internal static int FixedValue(GearState state,string code)
    {
        int total=0;foreach(var (property,roll) in Properties(state)){if(property.Code==code){total+=roll.Value;}}return total;
    }
}
