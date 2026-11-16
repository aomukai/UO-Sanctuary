// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace Sanctuary;

// Complete-original return ledger. A qualified spell body does not admit
// every other item using that body or an unresolved trigger stage.
internal static class NamedItemProcs
{
    private static readonly Lazy<string[]> Qualified=new(()=>
    {
        var items=BalanceCatalog.Read<string>("named-proc-items");
        if(items is not["unique/254"]){throw new InvalidDataException("Unqualified complete-original proc return ledger");}
        return items;
    });
    internal static bool Supported(NamedItemDefinition item,NamedProperty property)=>Array.IndexOf(Qualified.Value,item.Id)>=0&&item is{Id:"unique/254",SourceKey:"Hellslayer",BaseCode:"7bt"}
        &&property is{SourceEnabled:true,Code:"att-skill",SourceMin:10,SourceMax:0}
        &&ItemSkillProperties.Parameter(property)==47;
}
