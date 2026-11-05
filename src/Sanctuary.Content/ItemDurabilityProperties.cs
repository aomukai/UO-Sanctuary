// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Complete older property function13 for an explicit item-owned graph. Its
// operative maximum-durability reader is a required source-stat service.
// Original player projection/fresh generic admission remain unchanged.
internal static class ItemDurabilityProperties
{
    internal static bool Admitted(EquipmentPropertyPlan plan)=>plan is{Steps.Length:1}&&plan.Steps[0]is{Function:13,Stat:75};
    internal static int Apply(EquipmentPropertyPlan plan,EquipmentPropertyInput input,int mode,EquipmentPropertyServices s,Func<int> maximum)
    {
        if(!Admitted(plan)||s==null||maximum==null||mode is<0 or>7){throw new NotSupportedException("Durability percent needs its exact item writer/stat context");}
        var step=plan.Steps[0];if(mode==1){EquipmentPropertyWriter.ResetBase(s,step.Stat);}
        int min=Math.Min(input.Minimum,input.Maximum),max=Math.Max(input.Minimum,input.Maximum);
        int value=min==max?min:checked(min+s.Roll(checked(max-min+1)));
        int result=EquipmentPropertyWriter.Write(s,input,step.Set,step.Stat,0,value,0,0x40);
        if(result==0){return 0;}int durability=maximum();if(durability>0){s.SetBase(72,durability);}return result;
    }
}
