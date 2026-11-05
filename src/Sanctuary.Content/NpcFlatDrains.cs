// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Already allocated/mitigated ordinary non-hireling NPC drain fields. These
// are fixed-point resource amounts,not player/hireling leech percentages.
internal static class NpcFlatDrains
{
    internal static bool Any(PreparedMonsterDamagePacket p)=>p.LifeDrain256!=0||p.ManaDrain256!=0||p.StaminaDrain256!=0;
    internal static void Require(Mobile attacker,PreparedMonsterDamagePacket p,bool admitted)
    {
        if(!Any(p)){return;}
        if(!admitted||attacker is not BaseCreature||attacker is BaseHire)
        {throw new NotSupportedException("Positive drain fields need an explicit ordinary non-hireling NPC receiver");}
    }
    internal static int Healing(int physical256,int mana256,int stamina256,int missing256,int lifeDrain256,int manaDrain256,int staminaDrain256,int effectiveness)
    {
        if(physical256<0||mana256<0||stamina256<0||missing256<0||lifeDrain256<0||manaDrain256<0||staminaDrain256<0||effectiveness<0)
        {throw new ArgumentOutOfRangeException();}
        // The outer ExecuteEvents gate deliberately excludes stamina alone.
        if((lifeDrain256==0&&manaDrain256==0)||effectiveness==0){return 0;}
        int total=checked(Math.Min(lifeDrain256,physical256)+Math.Min(manaDrain256,mana256)+Math.Min(staminaDrain256,stamina256));
        return Math.Min(missing256,SourcePercentage.Calculate(total,effectiveness,100));
    }
    internal static int Remaining(int current256,int drain256)
    {
        if(current256<0||drain256<0){throw new ArgumentOutOfRangeException();}
        if(drain256==0){return current256;}
        int remaining=checked(current256-drain256);return remaining<256?0:remaining;
    }
    internal static void BeforeHealth(BaseCreature source,BaseCreature target,PreparedMonsterDamagePacket p)
    {
        if(!Any(p)){return;}
        if(source is not{Alive:true,Deleted:false})
        {throw new NotSupportedException("A retired source with positive drains needs its retained-resource receiver");}
        int heal=Healing(p.Physical256,NecromancerEffects.ManaBalance256(target),BattleOrders.Stamina256(target),
            Math.Max(0,SkillHealth.Maximum256(source)-SkillHealth.Balance256(source)),p.LifeDrain256,p.ManaDrain256,p.StaminaDrain256,GemCombat.Drain(target));
        if(heal>0)
        {
            SkillHealth.Heal(source,heal);
            source.FixedEffect(0x376A,10,10,0x26,0); // Identified overlay151 placeholder.
        }
    }
    internal static void AfterHealth(BaseCreature target,PreparedMonsterDamagePacket p)
    {
        if(p.ManaDrain256>0){NecromancerEffects.SetMana256(target,Remaining(NecromancerEffects.ManaBalance256(target),p.ManaDrain256));}
        if(p.StaminaDrain256>0){BattleOrders.SetStamina256(target,Remaining(BattleOrders.Stamina256(target),p.StaminaDrain256));}
    }
}
