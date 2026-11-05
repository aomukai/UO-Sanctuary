// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Approved ordinary skill-tree adaptation. The literal older function10 and
// generic fresh numeric admission remain separate;war-tab-rand is not covered.
internal static class CurrentTreePropertyWriter
{
    internal static bool Supports(EquipmentPropertyPlan p)=>p is{Code:"skilltab",Steps.Length:1}&&p.Steps[0]is{Function:10,Stat:188};
    internal static void Apply(EquipmentPropertyPlan plan,EquipmentPropertyInput input,EquipmentPropertyServices target)
    {
        if(!Supports(plan)||input.Parameter is<0 or>=24||target==null)
        {throw new NotSupportedException("Ordinary skill-tree property needs its approved class/tree context");}
        int layer=8*(input.Parameter/3)+input.Parameter%3;
        // Existing fixed-layer numeric writer supplies precisely the same
        // range roll,raw stat/list operations and item RNG.
        var mapped=plan with{Steps=[plan.Steps[0]with{Function=21,Value=layer}]};
        EquipmentPropertyWriter.Create(mapped,input,0,target);
    }
}
