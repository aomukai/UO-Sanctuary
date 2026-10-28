// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// Shared monster recipient phase from ExecuteEvents. The caller must commit
// health first and supply actual recipient metadata. This is neither contact
// qualification nor a substitute for the outstanding lethal/death bridge.
internal static class PreparedMonsterStun
{
    internal static int Resolve(int frames,bool unique,Func<int,int> limited,
        Func<bool> boss,Func<int> velocity,Func<bool> hireling)
    {
        ArgumentNullException.ThrowIfNull(limited);ArgumentNullException.ThrowIfNull(boss);
        ArgumentNullException.ThrowIfNull(velocity);ArgumentNullException.ThrowIfNull(hireling);
        if(frames<=0){return 0;}
        if(unique)
        {
            int roll=limited(100);
            if(roll is <0 or >=100){throw new ArgumentOutOfRangeException(nameof(limited));}
            if(roll<90){return 0;}
        }
        if(boss()||velocity()==0){return 0;}
        return hireling()&&frames>=13?13:Math.Min(frames,250);
    }
    internal static bool CommitSurvivor(BaseCreature target,int duration,DateTime at)
    {
        if(duration is <0 or >250 || at.Kind!=DateTimeKind.Utc){throw new ArgumentOutOfRangeException();}
        if(duration==0){return false;}
        // Old ExecuteEvents may install a transient stun at zero source HP
        // before later death-mode consumption. Native Damage dispatches death
        // immediately; refuse that unsupported bridge rather than equate it.
        if(target is not{Alive:true,Deleted:false} || SkillHealth.Balance256(target)<256)
        {throw new NotSupportedException("NPC lethal/death stun recipient bridge is not verified");}
        return CombatStuns.ApplyPreparedDuration(target,duration,at);
    }
}
