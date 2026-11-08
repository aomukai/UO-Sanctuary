// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

// Shared original NPC progressive damage stages. Emission and consumption
// belong to the later contact callback, not a cached kick's allocation.
internal static class ShadowFinisherDamage
{
    internal static MartialCharge[] States(AssassinShadowBody body,BaseCreature target,DateTime? at=null)
    {
        var states=AssassinCharges.Finisher(body).Where(s=>s.UntilUtc>(at??TimedEffects.Now)).ToArray();
        if(states.Any(s=>s.Ability is not(CombatAbility.TigerStrike or CombatAbility.CobraStrike or CombatAbility.PhoenixStrike or CombatAbility.FistsOfFire or CombatAbility.ClawsOfThunder or CombatAbility.BladesOfIce)))
        {throw new NotSupportedException("Shadow finisher has an unqualified elemental release consumer");}
        if(new[]{280,259,274,269}.Where((id,i)=>GenericPetMetadata.Table.Skill(id).AuraState!=124+i).Any())
        {throw new NotSupportedException("Unmapped current Shadow progressive state order");}
        if(states.FirstOrDefault(s=>s.Ability==CombatAbility.BladesOfIce)?.Count==3){NpcFreezeStatus.Require(body,target);}
        return states;
    }
    internal static (FistsFirePacket Fire,IcePacket Ice,ThunderPacket Thunder) Effects(AssassinShadowBody body,MartialCharge[] states)
    {
        var graph=OriginalNpcEquipment.Graph(body);
        SkillValues Values(MartialCharge s,int id)=>ShadowSkillCasting.OwnValues(body,Math.Max(s.InitialRank,body.OwnedSkillRank(id)),id);
        var f=states.FirstOrDefault(s=>s.Ability==CombatAbility.FistsOfFire);
        var c=states.FirstOrDefault(s=>s.Ability==CombatAbility.BladesOfIce);
        var t=states.FirstOrDefault(s=>s.Ability==CombatAbility.ClawsOfThunder);
        // Live callback endpoints without an unused inline RNG draw. Actual
        // area/projectile callbacks retain their existing separate rolls.
        return(f==null?null:FistsFireRelease.Capture(Values(f,259),f.Count,graph.Read(329),_=>0),
            c==null?null:MartialCold.Capture(Values(c,274),c.Count,graph.Read(331),_=>0,c.InitialRank),
            t==null?null:MartialLightning.Capture(Values(t,269),t.Count,graph.Read(330),graph.Read(334),_=>0));
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
