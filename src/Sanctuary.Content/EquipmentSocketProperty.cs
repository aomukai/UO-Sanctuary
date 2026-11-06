// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
namespace Sanctuary;

internal sealed record EquipmentSocketGeometry(int Width,int Height,int Maximum);

// Property14 writes the item's base socket stat, not the magic property list.
// Its original-item adapter preserves the saved roll; it never inserts gems,
// creates sockets on the live UO item or consumes its RNG a second time.
internal static class EquipmentSocketProperty
{
    internal static EquipmentSocketGeometry Geometry(string code,int level)
    {
        var basis=NpcItemAffixes.Pool.Basis(code);var raw=basis.SourceFields;
        int I(string field)=>raw.TryGetProperty(field,out var value)?value.GetInt32():0;
        var type=CurrentItemFinishing.Catalogue.Types.Single(t=>t.Code==basis.Type);
        level=Math.Max(1,level);
        int cap=level>40?type.MaxSockets40:level>25?type.MaxSockets25:type.MaxSockets1;
        return new(I("invwidth"),I("invheight"),Math.Min(cap,I("gemsockets")));
    }
    internal static int Apply(EquipmentSocketGeometry geometry,EquipmentPropertyInput input,int first,
        EquipmentPropertyServices target,Action socketed)
    {
        int maximum=checked(geometry.Width*geometry.Height);
        if(maximum<=0){return 0;}
        maximum=Math.Min(maximum,geometry.Maximum);
        int minimum=first;
        if(minimum<=0)
        {
            minimum=EquipmentPropertyWriter.Rolled(input,target);
            if(minimum<=0){minimum=input.Parameter;}
        }
        int sockets=Math.Min(Math.Max(minimum,1),maximum);
        if(sockets<=0){return 0;}
        socketed();target.SetBase(194,sockets);return sockets;
    }
}
