// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal readonly record struct NpcCriticalChances(int Mastery,int Generic,int Deadly,int Intrinsic);
internal sealed record NpcCriticalPacket(int Physical256,GearDamage Elements,WeaponPoisonRoll Poison,int HitClass,int Overlay,bool Intrinsic);

internal static class NpcEquipmentCritical
{
    internal static NpcCriticalChances Capture(SkeletonGuardian actor)
    {
        if(actor.GetType()!=typeof(SkeletonGuardian)&&actor is not NecromancerMinion{IsMage:false}&&actor is not IronGolemBody)
        {throw new NotSupportedException("NPC critical source body is not mapped");}
        if(NpcEquipmentCombat.Context(actor) is not{} item){throw new NotSupportedException("No original NPC critical equipment");}
        var fields=NecromancerSummons.Fields(actor.SourceMonsterId);
        if(!fields.TryGetValue("Crit",out var value)||!value.TryGetInt32(out int chance)||chance is <0 or >255)
        {throw new NotSupportedException("NPC intrinsic critical source value is not mapped");}
        // Current mapped skeleton/golem skills supply no weapon-mastery or
        // generic-critical stats. Item-granted skill integration remains gated;
        // the controlling player's skills/passives are not inherited.
        return new(0,0,item.Bonus("deadly"),chance);
    }
    internal static bool Physical(NpcCriticalChances chances,Func<uint> raw,bool ranged=false,bool skipPhysical=false)
        =>!skipPhysical&&NamedCombat.MeleeCritical(ranged?0:chances.Mastery,chances.Generic,chances.Deadly,raw);
    internal static bool Intrinsic(NpcCriticalChances chances,Func<uint> raw)=>chances.Intrinsic!=0&&raw()%100<chances.Intrinsic;
    internal static NpcCriticalPacket ApplyIntrinsic(int physical256,GearDamage elements,WeaponPoisonRoll poison,bool critical,int hitClass=0,bool defender=true)
    {
        int overlay=0;
        if(critical)
        {
            physical256=checked(physical256*2);
            elements=elements with{Fire=elements.Fire*2,Lightning=elements.Lightning*2,Cold=elements.Cold*2,Magic=elements.Magic*2};
            poison=poison with{Rate256=checked(poison.Rate256*2)};
            if(defender){if((hitClass&0xf0)==0){hitClass|=0x10;}else{overlay=54;}}
        }
        return new(physical256,elements,poison,hitClass,overlay,critical);
    }
}
