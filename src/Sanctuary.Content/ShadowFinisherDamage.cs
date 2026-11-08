// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

// Shared original NPC progressive damage stages. Emission and consumption
// belong to the later contact callback, not a cached kick's allocation.
internal static class ShadowFinisherDamage
{
    internal static MartialCharge[] States(AssassinShadowBody body,BaseCreature target)
    {
        var states=AssassinCharges.Finisher(body);
        if(states.Any(s=>s.Ability is not(CombatAbility.TigerStrike or CombatAbility.CobraStrike or CombatAbility.PhoenixStrike or CombatAbility.FistsOfFire or CombatAbility.ClawsOfThunder or CombatAbility.BladesOfIce)))
        {throw new NotSupportedException("Shadow finisher has an unqualified elemental release consumer");}
        if(new[]{280,259,274,269}.Where((id,i)=>GenericPetMetadata.Table.Skill(id).AuraState!=124+i).Any())
        {throw new NotSupportedException("Unmapped current Shadow progressive state order");}
        if(states.FirstOrDefault(s=>s.Ability==CombatAbility.BladesOfIce)?.Count==3){NpcFreezeStatus.Require(body,target);}
        return states;
    }
    internal static NpcFillPacket Inline(AssassinShadowBody body,MartialCharge[] states,NpcFillPacket fill,
        out FistsFirePacket fire,out IcePacket ice,out ThunderPacket thunder)
    {
        var cobra=AssassinCharges.CobraDrain(states.FirstOrDefault(s=>s.Ability==CombatAbility.CobraStrike),body.OwnedSkillRank(265));
        fire=FistsFireRelease.CaptureShadow(body,states.FirstOrDefault(s=>s.Ability==CombatAbility.FistsOfFire));
        fill=FistsFireRelease.InlineShadow(fill,fire);
        ice=MartialCold.CaptureShadow(body,states.FirstOrDefault(s=>s.Ability==CombatAbility.BladesOfIce));
        fill=MartialCold.InlineShadow(fill,ice);
        thunder=MartialLightning.CaptureShadow(body,states.FirstOrDefault(s=>s.Ability==CombatAbility.ClawsOfThunder));
        if(thunder!=null){fill=fill with{Lightning=checked(fill.Lightning+thunder.Inline256),ResultFlags=fill.ResultFlags|16384};}
        // These are fixed-point flat fields for a non-hireling NPC.
        return fill with{LifeDrain=checked(fill.LifeDrain+cobra.LifePercent),ManaDrain=checked(fill.ManaDrain+cobra.ManaPercent)};
    }
}
