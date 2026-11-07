// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells.Necromancy;
namespace Sanctuary;

// Notification/bookkeeping portion of native Mobile.Damage, in native order.
// Incoming mitigation and UO BaseCreature.Damage modifiers are NOT replayed.
// The actor consumer must validate its modifier/event capabilities separately.
// Source HP and death remain owned by the ordered commitment/return phases.
internal static class PreparedMonsterNativeDamage
{
    internal static void RequireIncoming(PlayerMobile source,BaseCreature target,Mobile attacker=null)
    {
        var method=target.GetType().GetMethod(nameof(BaseCreature.Damage))?.DeclaringType;
        bool demonGuard=(target is WarlockGoatmanBody or WarlockTaintedBody or WarlockDefilerBody||NpcOriginalItemProcs.Active(target))&&method==typeof(SkeletonGuardian);
        // These demons and qualified original Valkyries inherit only the
        // guardian's friendship wrapper.
        // Preserve its refusal before the ordered path bypasses Damage; the
        // actual emitter,not its credit owner,determines that relationship.
        if(demonGuard&&ClassCombat.Friendly(attacker??source,target))
        {throw new NotSupportedException("Friendly demon recipient rejected before ordered damage");}
        RequireBaseModifiers(source,target,demonGuard);
    }
    internal static void RequireBaseModifiers(Mobile source,BaseCreature target,bool demonGuard=false)
    {
        var method=target.GetType().GetMethod(nameof(BaseCreature.Damage))?.DeclaringType;
        // Ordinary contact performs hostility qualification before HP. Its
        // explicit original Valkyrie profile supplies the same known guardian
        // wrapper; spell callers check friendship in RequireIncoming above.
        demonGuard|=NpcOriginalItemProcs.Active(target)&&method==typeof(SkeletonGuardian);
        bool incoming=ReviveEquipmentIncoming.Handles(target);
        if(incoming){ReviveEquipmentIncoming.RequireBody(target);}
        if(target.GetSkillMod("EvilOmen")!=null||source!=null&&BloodOathSpell.GetBloodOath(source)==target
            ||target.Controlled&&!target.Summoned&&!incoming
            ||target.SubdueBeforeTame||method!=typeof(BaseCreature)&&!demonGuard)
        {throw new NotSupportedException("This recipient's native incoming damage modifier needs an ordered adapter.");}
        if(target.IsParagon){throw new NotSupportedException("Paragon damage/recovery needs its ordered actor adapter.");}
    }
    internal static void Notify(Mobile attacker,BaseCreature target,int after256)
    {ArgumentNullException.ThrowIfNull(attacker);NotifyCore(attacker,target,after256);}
    internal static void NotifyAnonymous(BaseCreature target,int after256)=>NotifyCore(null,target,after256);
    private static void NotifyCore(Mobile attacker,BaseCreature target,int after256)
    {
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
        if(attacker!=null){PreparedMonsterCredit.Register(attacker,target,after256);}
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
