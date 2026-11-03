// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using Server;
namespace Sanctuary;

// One item-facing enumeration for ordinary/named properties and captured
// inherent properties. Keep player activation/selected-hand policy outside it.
internal static class EquipmentIntrinsicProperties
{
    internal static IEnumerable<(NamedProperty Property,NamedPropertyRoll Roll)> For(Item item)
    {
        if(ClassGear.State(item) is not{Identified:true} state||ClassGear.Broken(item)){yield break;}
        foreach(var property in state.NamedItemId!=null?NamedGear.IntrinsicProperties(item):OrdinaryAffixProperties.Properties(state))
        {yield return property;}
        foreach(var property in GrimoireEquipment.Properties(item)){yield return property;}
    }
}
