// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;

namespace Sanctuary;

public readonly record struct WeaponLeechResult(int Life256,int Mana256,int VictimMana256);

public static class WeaponLeech
{
    // Player weapon leech. Keep each source integer division and the 6-bit percent precision.
    internal static WeaponLeechResult Calculate(int physical256,int victimLife256,int victimMana256,
        int lifePercent,int manaPercent,int drain,int lifeDivisor,int manaDivisor)
    {
        int physical=Math.Min(Math.Max(0,physical256),Math.Max(0,victimLife256));
        long life=Math.Max(0,lifePercent),mana=Math.Max(0,manaPercent);int healedLife=0,healedMana=0;
        if((life!=0 || mana!=0) && drain>0)
        {
            life*=64;mana*=64;if(lifeDivisor>0){life/=lifeDivisor;}if(manaDivisor>0){mana/=manaDivisor;}
            int Recovered(long percent)
            {
                Int128 amount=(Int128)physical*percent/100;
                if(drain!=100){amount=amount*drain/100;}
                return (int)Int128.Min(int.MaxValue,amount/64);
            }
            if(physical>0){healedLife=Recovered(life);healedMana=Recovered(mana);}
        }
        // Literal older ExecuteEvents tail: the mutated mana-leech field is also
        // deducted from victim mana. Modern executable parity remains explicit.
        long remaining=Math.Max(0,victimMana256);
        if(mana>0){remaining-=mana;if(remaining<256){remaining=0;}}
        return new(healedLife,healedMana,(int)remaining);
    }
    internal static void Apply(PlayerMobile owner,BaseCreature target,int physical256,MissileSourceStats saved)
    {
        if(saved.LifeDrain==0 && saved.ManaDrain==0 || owner is not{Alive:true,Deleted:false} || target is not{Alive:true,Deleted:false}){return;}
        // Sanctuary's existing per-creature difficulty projection is shared with GemCombat.
        int divisor=MonsterVariants.Difficulty(target) switch{MonsterDifficulty.Nightmare=>2,MonsterDifficulty.Hell=>3,_=>1};
        int manaBefore=NecromancerEffects.ManaBalance256(target);
        var result=Calculate(physical256,SkillHealth.Balance256(target),manaBefore,saved.LifeDrain,saved.ManaDrain,GemCombat.Drain(target),divisor,divisor);
        SkillHealth.Heal(owner,result.Life256);NecromancerEffects.AddMana(owner,result.Mana256);
        // Commit before native Damage dispatches death/corpse callbacks. The D2
        // event writes HP/mana before its later death dispatch; nonlethal values match.
        if(result.VictimMana256!=manaBefore){NecromancerEffects.SetMana256(target,result.VictimMana256);}
        if(result.Life256>0 || result.Mana256>0)
        {owner.FixedEffect(0x376A,10,10,result.Life256>0?0x26:0x489,0);} // Identified leech placeholder; overlays151/152 pending.
    }
}
