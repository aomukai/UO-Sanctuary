// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
namespace Sanctuary;

internal sealed record OriginalItemProcFields(uint Item,int ItemLevel,NpcItemStatCapture Full,ItemPropertyCreationState Events);

// Preserve the complete numeric original while exposing only qualified event
// fields to the existing proc subgraph. Numeric properties are never removed
// from the birth recipe or forwarded a second time through event attachment.
internal static class OriginalItemProcProjection
{
    internal static bool IsProc(EquipmentStatValue value)=>ItemEventDefinitions.Bindings(value.Stat)?.Any(e=>e.Function is 20 or 21 or 30)==true;
    internal static bool Qualified(EquipmentStatValue value)=>value.Stat is 195 or 196 or 198 or 201
        &&value.Value>0&&((ushort)value.Packed>>6)==47&&(value.Packed&63)>0;
    internal static OriginalItemProcFields Capture(Item original)=>FromCaptured(original,NpcItemStatCapture.Capture(original));
    // A saved summon recipe may supply the original birth capture rather than
    // rebuilding its numeric fields from later activation or recipient levels.
    internal static OriginalItemProcFields FromCaptured(Item original,NpcItemStatCapture captured)
    {
        if(original is not{Deleted:false}||ClassGear.State(original)is not{Identified:true} state||ClassGear.Broken(original)
            ||captured is not{ItemBase:not null,Properties:not null}||captured.Body!=IronGolemItems.Metadata(state.BaseCode).Body1)
        {throw new NotSupportedException("Original proc projection needs its intact identified original and explicit full capture");}
        foreach(var list in new[]{captured.ItemBase,captured.Properties})
        {
            if(list.Select(v=>v.Packed).Distinct().Count()!=list.Length||list.Any(v=>EquipmentStatValues.Definition(v.Stat)==null))
            {throw new InvalidDataException("Original property capture has invalid or duplicate stats");}
        }
        if(captured.ItemBase.Any(IsProc)){throw new NotSupportedException("Original base proc stats need their distinct source list context");}
        var events=captured.Properties.Where(IsProc).OrderBy(v=>v.Packed).ToArray();
        if(events.Any(v=>!Qualified(v))){throw new NotSupportedException("Original proc projection needs its qualified Fire Ball callback/body");}
        var full=captured with{ItemBase=(EquipmentStatValue[])captured.ItemBase.Clone(),Properties=(EquipmentStatValue[])captured.Properties.Clone()};
        // Property11 is reconstructed from retained arguments without draws.
        // This unused item-list seed is not a chance or world allocation seed.
        return new(original.Serial.Value,state.Roll.ItemLevel,full,new(SourceRandom.InitLow(0),false,events.Length>0,[],events));
    }
    internal static bool HasDeclaredProc(Item original)=>ClassGear.State(original)is{Identified:true}&&!ClassGear.Broken(original)
        &&EquipmentIntrinsicProperties.For(original).Any(p=>NamedPropertyRuntime.ProcArguments(p.Property));
    internal static EquipmentStatValue[] Legacy(EquipmentStatValue[] stats)
    {
        if(stats.Any(v=>IsProc(v)&&!Qualified(v))){throw new NotSupportedException("Ordinary original item needs its unsupported proc consumer");}
        return stats.Where(v=>!IsProc(v)).ToArray();
    }
}
