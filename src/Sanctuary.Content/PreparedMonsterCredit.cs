// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// UO bookkeeping adapter for a source HP write. This registers the same whole
// display-HP loss as SkillHealth.Damage, including its lethal cap, without
// replaying Damage or triggering incoming native death before source return.
internal static class PreparedMonsterCredit
{
    internal static void Register(BaseCreature attacker,BaseCreature target,int after256)
    {
        if(attacker==null||target is not{Alive:true,Deleted:false}||after256<0)
        {throw new ArgumentException("Prepared damage credit requires an existing source and living recipient");}
        int before256=SkillHealth.Balance256(target);
        if(after256>before256){throw new ArgumentOutOfRangeException(nameof(after256));}
        int whole=Math.Max(0,target.Hits-(int)(((long)after256+255)/256));
        if(whole>0){target.RegisterDamage(whole,attacker);}
        if(target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)!=before256)
        {throw new NotSupportedException("Reentrant damage-credit mutation/death before prepared HP commitment is unsupported");}
    }
}
