// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Item stat128/function10. Incoming melee damage, not an attack attempt or
// physical Thorns. Its separate lightning packet suppresses damage callbacks.
internal static class NpcLightningReturn
{
    internal static int Bonus(BaseCreature source)=>NpcEquipmentCombat.EventBonus(source,"light-thorns");
    internal static void Require(BaseCreature source,BaseCreature target)
    {
        if(source is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false}}||target is not{Deleted:false})
        {throw new NotSupportedException("NPC lightning return needs its original living owned source and NPC recipient");}
        _=NativeMonsterReturn.SourceIdentity(source);_=NativeMonsterReturn.Metadata(target);
        PreparedMonsterNativeDamage.RequireBaseModifiers(source,target);
        IronGolemContact.RequireRecoveryEvents(target);
        if(MonsterUninterruptibles.Active(target)||MonsterUninterruptibles.Delayed(target))
        {throw new NotSupportedException("Lightning return needs the unfinished uninterruptible/death-delay adapter");}
        _=IronGolemContact.Events(target,requireFrozen:true);
        if(Bonus(source)>int.MaxValue/256){throw new NotSupportedException("NPC lightning return exceeds its signed packet range");}
    }
    internal static void RequireIncoming(Mobile attacker,Mobile defender,bool ranged)
    {
        if(ranged||defender is not BaseCreature actor||Bonus(actor)<=0){return;}
        if(attacker is not BaseCreature target)
        {throw new NotSupportedException("NPC lightning return to a player needs its separate player damage/return adapter");}
        Require(actor,target);
    }
    // Native AOS and scalar typed weapon paths have already qualified contact.
    // They do not supply the result packet needed by on-struck curse callbacks.
    internal static void RequireNativeIncoming(Mobile attacker,Mobile defender,bool ranged)
    {
        RequireIncoming(attacker,defender,ranged);
        if(ranged||defender is not BaseCreature actor||Bonus(actor)<=0){return;}
        if(actor is SkeletonGuardian guardian&&NpcItemProcs.Get(guardian)is{IncomingRequired:true})
        {throw new NotSupportedException("Native scalar incoming hit needs its packet-backed on-struck proc adapter");}
    }
    internal static void NativeIncoming(Mobile attacker,Mobile defender,bool ranged,DateTime at)
    {
        RequireNativeIncoming(attacker,defender,ranged);
        if(!ranged&&defender is BaseCreature actor&&Bonus(actor)>0){Apply(actor,(BaseCreature)attacker,at);}
    }
    internal static bool Apply(BaseCreature source,BaseCreature target,DateTime at)
    {
        if(source==null||target==null){return false;}
        int amount=Bonus(source);if(amount<=0){return false;}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Lightning return requires UTC source time");}
        // The preceding attacker event (e.g. Iron Maiden) can retire the
        // recipient. Positive callback no longer has a live HP receiver.
        if(target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)<=0){return true;}
        Require(source,target);
        var owner=(PlayerMobile)source.ControlMaster;var metadata=NativeMonsterReturn.Metadata(target);
        if(target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)<=0||!target.CanBeDamaged()||!metadata.Killable
            ||source.Map==null||source.Map==Map.Internal||target.Map!=source.Map||!ClassCombat.IsHostile(owner,target)
            ||SpellHelper.IsTown(target.Location,owner)&&!NativeMonsterReturn.SourceIdentity(source).Town){return true;}
        int pierce=ReviveEquipmentEvents.Handles(source)?ReviveEquipmentOffense.Capture(source,target).Pierce.Lightning:NpcEquipmentCombat.Bonus(source,"pierce-ltng");
        PreparedMonsterDamagePacket Mitigate(PreparedMonsterDamagePacket p)
        {
            var parts=SkillCombat.MitigatePrepared(source,target,new(0,0,0,p.Lightning256/256.0,0),out int absorbed,
                pierceOverride:element=>element=="ltng"?pierce:0);
            int lightning=NamedElements.To256(parts.Lightning);
            return p with{Total256=lightning,Lightning256=lightning,AbsorbedLife256=absorbed};
        }
        var packet=new PreparedMonsterDamagePacket(0,checked(amount*256),0,0,0x4021,HitClass:0x4D,Lightning256:checked(amount*256));
        var services=new InstantSpellNpcDamage(owner,target,metadata,null,at,"NPC lightning return",
            ()=>throw new InvalidOperationException("SOFTHIT lightning return must not draw defender RNG"),emitter:source,npcMitigation:Mitigate);
        using var targeting=NpcItemSpellCasts.PreserveCombatTarget(source);
        PreparedMonsterDamage.ReleaseMissile(source,target,packet,new(false,()=>false,()=>metadata.Velocity,()=>target is BaseHire),
            _=>throw new InvalidOperationException("Lightning return has no stun draw"),services,at);
        target.FixedEffect(0x374A,10,10,0x481,0); // Identified lightning contact placeholder.
        return true;
    }
}
