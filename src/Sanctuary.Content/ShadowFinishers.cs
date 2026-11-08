// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

// Ordinary own NPC Attack: Tiger before the full weapon fill, Cobra after it,
// then mitigation/allocation and progressive consumption before contact.
internal static class ShadowFinishers
{
    internal static bool Builder(int id)=>id is 254 or 265 or 280 or 259;
    internal static IronGolemContact.Prepared Prepare(AssassinShadowBody body,PlayerMobile owner,BaseCreature target,
        Func<int,bool> range,DateTime at,IronGolemContact.Rules rules)
    {
        var states=AssassinCharges.Finisher(body);
        if(states.Any(s=>s.Ability is not(CombatAbility.TigerStrike or CombatAbility.CobraStrike or CombatAbility.PhoenixStrike or CombatAbility.FistsOfFire)))
        {throw new NotSupportedException("Shadow ordinary finisher has an unqualified elemental release consumer");}
        int flags=NpcMeleeResult.Resolve(body,target,range,()=>ClassCombat.IsHostile(owner,target),
            n=>NpcEquipmentRandom.Limited(body,n),states.Sum(s=>s.AttackPercent));
        if(!NpcEquipmentImpact.Fills(flags)){return new(rules,NpcEquipmentImpact.Prepare(body,target,flags));}
        int tiger=AssassinCharges.TigerPercent(states.FirstOrDefault(s=>s.Ability==CombatAbility.TigerStrike),body.OwnedSkillRank(254));
        var cobra=AssassinCharges.CobraDrain(states.FirstOrDefault(s=>s.Ability==CombatAbility.CobraStrike),body.OwnedSkillRank(265));
        FistsFirePacket fire=null;var fireState=states.FirstOrDefault(s=>s.Ability==CombatAbility.FistsOfFire);
        var impact=OriginalNpcMelee.Impact(body,target,flags,new(AdditionalPercent:tiger,HitFlags:2),afterFill:fill=>
        {
            fire=FistsFireRelease.CaptureShadow(body,fireState);
            fill=FistsFireRelease.InlineShadow(fill,fire);
            return fill with{
            // Raw source fields are fixed-point flat amounts for ordinary
            // non-hireling NPCs, even though the shared helper names percent.
            LifeDrain=checked(fill.LifeDrain+cobra.LifePercent),ManaDrain=checked(fill.ManaDrain+cobra.ManaPercent)};
        });
        var prepared=new IronGolemContact.Prepared(rules,impact);
        // The source progressive-release routine checks tighter melee range
        // than GetResultFlags. Modern Attack's consume-one policy is shared
        // with players; later contact cancellation does not refund allocation.
        if(range(0))
        {
            if(fire is{Count:>=2})
            {
                var center=target.Location;var map=target.Map;
                prepared.AfterContact=()=>FistsFireRelease.AfterShadow(body,center,map,fire,at);
            }
            var phoenix=states.FirstOrDefault(s=>s.Ability==CombatAbility.PhoenixStrike);
            if(phoenix!=null){_=MartialPhoenix.AfterShadow(body,target,target.Location,phoenix,at);}
            foreach(var state in states){AssassinCharges.Consume(body,state,CommonMeleeSkills.Attack.Number("prgchargesconsumed"));}
        }
        return prepared;
    }
}
