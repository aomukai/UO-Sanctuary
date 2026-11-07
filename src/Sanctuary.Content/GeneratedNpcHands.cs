// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal static class GeneratedNpcHands
{
    internal static (SourceInventoryWeapon Primary,SourceInventoryWeapon Secondary) Read(GeneratedNpcItem[] originals)
    {
        ArgumentNullException.ThrowIfNull(originals);
        var slots=new SourceInventoryWeapon[13];var inventory=new SourceInventoryWeapons(true,true,true,SourceInventoryWeapons.Invalid,slots);
        foreach(var item in originals)
        {
            var s=item.Snapshot;string code=s.Metadata.Code;
            var row=new SourceInventoryWeapon(item.Serial.Value,4,NpcItemAffixes.Pool.IsType(code,"weap"),NpcItemAffixes.Pool.IsType(code,"mpot"),
                s.Equipment.Body,true,checked((uint)s.Metadata.Flags)|s.Equipment.Flags);
            slots[row.Body]=row;inventory.Insert(row);
        }
        return(inventory.Primary(),inventory.Secondary());
    }
}
