// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
namespace Sanctuary;

// Resource channel0=life,1=mana,2=stamina. PercentageA/B are item/skill
// pools for life/mana,and active/passive skill pools for stamina. Coefficients
// retain their source stat's raw units rather than assuming a common shift.
internal readonly record struct NpcResourceInput(int Kind,int Channel,int Level,int Base256,int Flat256,
    int LevelCoefficientRaw,int PercentageA,int PercentageB,int Attribute=0);

internal static class NpcResourceValues
{
    internal static int Calculate(NpcResourceInput input)
    {
        // Player progression stops at99; source summon levels need not. Raven
        // birth,for example,retains owner level plus effective skill ranks.
        if(input.Kind is not(0 or 1)||input.Channel is <0 or >2||input.Level<1||input.Kind==0&&input.Level>99)
        {throw new ArgumentOutOfRangeException(nameof(input));}
        int resource=new[]{7,9,11}[input.Channel],perLevel=new[]{216,217,242}[input.Channel];
        int attribute=input.Channel==1?1:3,first=input.Channel==0?76:input.Channel==1?77:162,second=input.Channel==2?163:first;
        EquipmentStatValue[] Fields(params(int Stat,int Value)[] values)=>values.Where(x=>x.Value!=0)
            .Select(x=>new EquipmentStatValue(EquipmentStatKey.Pack(x.Stat),x.Value)).OrderBy(x=>x.Packed).ToArray();
        var actor=new EquipmentStatUnit(new object(),input.Kind);var item=new EquipmentStatUnit(new object(),4);
        var basis=Fields((resource,input.Base256),(12,input.Level),(attribute,30),(second,input.PercentageB));
        if(input.Channel==2){basis=basis.Concat(Fields((first,input.PercentageA))).OrderBy(x=>x.Packed).ToArray();}
        actor.Stats=new(){Flags=EquipmentStatNode.Extended,Owner=actor,OwnerKind=input.Kind,BaseValues=basis,FullValues=(EquipmentStatValue[])basis.Clone()};
        item.Stats=new(){Flags=EquipmentStatNode.Extended,Owner=item,OwnerKind=4};
        var bonuses=Fields((resource,input.Flat256),(perLevel,input.LevelCoefficientRaw),(attribute,input.Attribute));
        if(input.Channel!=2){bonuses=bonuses.Concat(Fields((first,input.PercentageA))).OrderBy(x=>x.Packed).ToArray();}
        var node=new EquipmentStatNode{Flags=0x40,BaseValues=bonuses};
        // These are transient numeric nodes,not fake player/world actors.
        // The player branch is only an explicit oracle control. Live NPCs
        // never acquire player class coefficients or owner attributes.
        var values=new EquipmentStatValues(_=>4,_=>0,u=>u.Kind==0?new EquipmentClassCoefficients(4,4,16):null,(_,_,_,_,_)=>{});
        values.Recalculate(actor.Stats,EquipmentStatKey.Pack(resource));
        EquipmentStatGraph.Post(item,node,true,values);EquipmentStatGraph.Merge(actor,item,true,values);
        return EquipmentStatValues.Total(actor.Stats,EquipmentStatKey.Pack(resource));
    }
}
