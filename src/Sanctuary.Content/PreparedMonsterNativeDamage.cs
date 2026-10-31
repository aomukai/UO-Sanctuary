// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Notification/bookkeeping portion of native Mobile.Damage, in native order.
// Incoming mitigation and UO BaseCreature.Damage modifiers are NOT replayed.
// The actor consumer must validate its modifier/event capabilities separately.
// Source HP and death remain owned by the ordered commitment/return phases.
internal static class PreparedMonsterNativeDamage
{
    internal static void Notify(BaseCreature attacker,BaseCreature target,int after256)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        int amount=PreparedMonsterCredit.Amount(target,after256);
        if(amount==0){return;} // SkillHealth also skips native Damage here.
        int before=SkillHealth.Balance256(target),displayed=target.Hits;
        void Unchanged()
        {
            if(target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)!=before||target.Hits!=displayed)
            {throw new NotSupportedException("Native damage callback changed life/deleted recipient before ordered HP commitment");}
        }
        if(!target.CanBeDamaged()){throw new NotSupportedException("Native recipient became invulnerable before ordered HP commitment");}
        int regional=amount;bool accepted=target.Region.OnDamage(target,ref regional);Unchanged();
        if(!accepted||regional!=amount)
        {throw new NotSupportedException("Native region veto/damage modification needs an explicit prepared damage adapter");}
        // The projected native amount is capped to displayed life, just like
        // SkillHealth.Damage. Native willKill is therefore false even for an
        // exact-zero source result; actual death is deferred to source return.
        bool willKill=displayed-amount<0;
        target.Spell?.OnCasterHurt();Unchanged();
        PreparedMonsterCredit.Register(attacker,target,after256);
        target.DisruptiveAction();Unchanged();
        target.Paralyzed=false;Unchanged();
        switch(Mobile.VisibleDamageType)
        {
            case VisibleDamageType.Related:target.SendVisibleDamageRelated(attacker,amount);break;
            case VisibleDamageType.Everyone:target.SendVisibleDamageEveryone(amount);break;
            case VisibleDamageType.Selective:target.SendVisibleDamageSelective(attacker,amount);break;
        }
        target.OnDamage(amount,attacker,willKill);Unchanged();
        target.Mount?.OnRiderDamaged(amount,attacker,willKill);Unchanged();
    }
}
