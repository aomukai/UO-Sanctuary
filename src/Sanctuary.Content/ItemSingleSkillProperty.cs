// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Text.Json;
namespace Sanctuary;

// Complete ordinary property22 over the existing range/stat/list services.
// Storage is separate from the recipient's skill ownership and cast routes.
internal static class ItemSingleSkillProperty
{
    internal static bool Supports(EquipmentPropertyPlan plan)=>plan is{Code:"skill",Steps.Length:1}&&plan.Steps[0] is{Function:22,Stat:107};
    internal static int Apply(EquipmentPropertyPlan plan,EquipmentPropertyInput input,EquipmentPropertyServices target,int state=0,int flags=0x40)
    {
        if(plan==null){return 0;}
        if(!Supports(plan)||target==null){throw new NotSupportedException("Individual skill property needs its ordinary raw writer");}
        int value=EquipmentPropertyWriter.Rolled(input,target);
        // The original writer deliberately narrows the property layer to16bits.
        // It does not look up a skill or reuse an incoming first-step result.
        return EquipmentPropertyWriter.Write(target,input,plan.Steps[0].Set,107,unchecked((ushort)input.Parameter),value,state,flags);
    }
    internal static int Parameter(NamedProperty property)
    {
        if(property.Parameter is not{} parameter){return 0;}
        if(parameter.ValueKind==JsonValueKind.Number&&parameter.TryGetInt32(out int id)){return id;}
        if(parameter.ValueKind==JsonValueKind.String)
        {
            string name=parameter.GetString();if(int.TryParse(name,out id)){return id;}
            var matches=ItemSkillExecution.All.Where(s=>s.Name.Length>0&&string.Equals(s.Name,name,StringComparison.OrdinalIgnoreCase)).ToArray();
            if(matches.Length==1){return matches[0].Id;}
        }
        throw new NotSupportedException("Individual skill property has an unresolved source parameter: "+parameter);
    }
}
