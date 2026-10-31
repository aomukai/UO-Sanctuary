// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary;

internal static class IronMaiden
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IronMaiden));
    internal static int Returned256(int physical256,int percent)
    {
        if(physical256<0||percent is <0 or >6550){throw new NotSupportedException("Iron Maiden return is outside the pinned nonnegative rank1..255 domain");}
        // Preserve the shared source helper's early division. Refuse signed
        // intermediate overflow rather than silently wrapping a damage packet.
        long product=physical256>1048576?(long)(physical256/100)*percent:(long)physical256*percent;
        if(product>int.MaxValue){throw new NotSupportedException("Iron Maiden source percentage overflows its signed intermediate");}
        return SourcePercentage.Calculate(physical256,percent,100);
    }
    internal static int Percent(int rank,bool quarter)
    {
        if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}
        var values=new SkillValues(SkillMechanics.For(CombatAbility.IronMaiden),_=>rank,_=>0,1);
        return checked((int)values.Eval(quarter?"calc2":"calc1"));
    }
    internal static void MeleeDamage(Mobile attacker,Mobile defender,int physical256,bool ranged)
    {
        if(ranged||physical256<=0||attacker is not(PlayerMobile or BaseCreature)||attacker is not{Alive:true,Deleted:false}
            ||defender is not{Deleted:false}||NecromancerEffects.Curse(attacker) is not{Ability:CombatAbility.IronMaiden} curse){return;}
        int amount;
        try{amount=Returned256(physical256,Percent(curse.Rank,attacker is PlayerMobile or BaseHire));}
        catch(NotSupportedException e){Log.Warning("Iron Maiden return unsupported for {Actor}: {Reason}",attacker.Serial.Value,e.Message);return;}
        // The ordinary player-owned Blood Golem has a non-evil alignment.
        // An unowned enemy equivalent needs a separately corroborated adapter
        // for the legacy callback's unusual post-return healing tail.
        if(defender is NecromancerMinion{Kind:CombatAbility.BloodGolem,ControlMaster:null})
        {Log.Warning("Iron Maiden enemy Blood Golem healing tail remains uncorroborated: {Target}",defender.Serial.Value);}
        PaladinThorns.Retaliate(defender,attacker,amount);
    }
}
