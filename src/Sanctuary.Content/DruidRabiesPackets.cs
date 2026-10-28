// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;

namespace Sanctuary;

// Prepared source-zero poison only. The ordinary bite and independent infection
// marker/emitter belong to separate consumers; this does not enable casting.
internal static class DruidRabiesPackets
{
    internal static WeaponMissileDamage Prepare(SkillValues values,int poisonMastery)
    {
        if(values.Ability!=CombatAbility.Rabies){throw new ArgumentException("Rabies values required",nameof(values));}
        return new(0,0,0,0,0,false,PoisonJavelinPackets.Prepare(new(),values,poisonMastery,true));
    }

    // Child missiles retain skill rank but recalculate hard synergy/mastery
    // from the original caster. Neither weapon nor infected-host stats enter.
    internal static WeaponMissileDamage Capture(PlayerMobile caster,int retainedRank)
        =>PoisonJavelinPackets.Cloud(caster,retainedRank,CombatAbility.Rabies);

    // srvhit53/dmg11 keep the original absolute infection deadline. A new
    // recipient gets only its remaining time; poison cure is not a marker cure.
    internal static bool TryInfectionLength(long deadlineFrame,long nowFrame,int skillFrames,out int frames)
    {
        frames=0;
        if(deadlineFrame<0||nowFrame<0||skillFrames<0||deadlineFrame<nowFrame){return false;}
        long remaining=deadlineFrame-nowFrame;
        if(remaining<10||remaining>skillFrames){return false;}
        frames=(int)remaining;return true;
    }
}
