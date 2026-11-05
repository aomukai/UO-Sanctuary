// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;

namespace Sanctuary;

internal sealed record ItemPropertySkill(int Id,string Name,int RequiredLevel,int MaximumLevel);
internal sealed record ItemSkillPropertyCatalog(string ReferenceVersion,int ChargedLevelBits,ItemPropertySkill[] Skills);
internal sealed record ItemSkillPropertyServices(int ItemLevel,int ChargedLevelBits,Func<int,ItemPropertySkill> Lookup);

internal static class ItemSkillProperties
{
    private static readonly Lazy<ItemSkillPropertyCatalog> Catalog=new(()=>
    {
        var c=BalanceCatalog.ReadObject<ItemSkillPropertyCatalog>("item-skill-properties");
        if(c is not{ReferenceVersion:"3.3.93847",ChargedLevelBits:6,Skills:{Length:429}}||c.Skills.Where((s,i)=>s.Id!=i||s.Name==null||(s.Name.Length==0)!=(i==362)
            ||s.RequiredLevel is <0 or >65535||s.MaximumLevel is <0 or >65535).Any())
        {throw new System.IO.InvalidDataException("Invalid current item skill-property metadata");}
        return c;
    });
    internal static ItemPropertySkill Skill(int id)=>id>=0&&id<Catalog.Value.Skills.Length?Catalog.Value.Skills[id]:null;
    internal static ItemSkillPropertyServices ForLevel(int level)=>new(level,Catalog.Value.ChargedLevelBits,Skill);
}

// Complete older property11/19 encoding for a valid item. This produces raw
// event/charge stats; actor registration,triggering and casting are separate.
internal static class ItemSkillPropertyWriter
{
    internal static bool Supports(EquipmentPropertyPlan p)=>p is{Steps.Length:>0}&&Array.TrueForAll(p.Steps,s=>s.Function is 11 or 19);
    private static int Level(EquipmentPropertyInput p,ItemPropertySkill skill,int itemLevel)
    {
        int level=p.Maximum;
        if(level<0)
        {
            int divisor=Math.Max(1,-(Math.Max(1,99-skill.RequiredLevel)/level));
            return Math.Max(1,(itemLevel-skill.RequiredLevel)/divisor);
        }
        if(level==0){return Math.Clamp((itemLevel-skill.RequiredLevel)/4+1,1,skill.MaximumLevel<=0?20:skill.MaximumLevel);}
        return level;
    }
    internal static void Apply(EquipmentPropertyPlan plan,EquipmentPropertyInput input,ItemSkillPropertyServices metadata,EquipmentPropertyServices target)
    {
        if(!Supports(plan)){throw new NotSupportedException("No encoded skill-property writer for "+plan?.Code);}
        if(metadata is not{Lookup:not null,ItemLevel:>=1 and <=787,ChargedLevelBits:>=1 and <=8}||target==null)
        {throw new ArgumentException("Explicit item level,skill lookup and charge-bit context required");}
        var skill=metadata.Lookup(input.Parameter);if(skill==null){return;}
        int level=Level(input,skill,metadata.ItemLevel);
        foreach(var step in plan.Steps)
        {
            if(step.Function==11)
            {
                int layer=unchecked((ushort)((level&63)+(input.Parameter<<6)));
                EquipmentPropertyWriter.Write(target,input,step.Set,step.Stat,layer,input.Minimum<=0?5:input.Minimum,0,0x40);
            }
            else
            {
                int capacity=input.Minimum;
                if(capacity==0){capacity=5;}
                else
                {
                    if(capacity<0){capacity=checked(level*-capacity/8-capacity);}
                    capacity=Math.Clamp(capacity,1,255);
                }
                int draw=target.Roll(capacity-capacity/8);
                int value=(capacity<<8)+((draw+capacity/8+1)&255);
                int layer=unchecked((ushort)((level&((1<<metadata.ChargedLevelBits)-1))+(input.Parameter<<metadata.ChargedLevelBits)));
                // Source19 consumes the draw and returns capacity even when
                // stat-list allocation fails; its null-safe setter then skips.
                if(target.GetOrCreateList(0,0x40)){target.SetList(step.Stat,value,layer);}
            }
        }
    }
}
