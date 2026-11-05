// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Already mitigated source freeze length. Keep resistance/CBF/half exactly
// once in the allocation phase,then source freeze-or-cold mode/divisor here.
internal static class NpcFreezeStatus
{
    internal static void Require(BaseCreature source,BaseCreature target)
    {
        if(source is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile}||target is not{Deleted:false}||target is BaseHire)
        {throw new NotSupportedException("NPC freeze needs its original owned source and non-hireling recipient");}
        _=NativeMonsterReturn.SourceIdentity(source);_=NativeMonsterReturn.Metadata(target);
        _=IronGolemContact.Events(target,requireFrozen:true);
        _=ItemDurationDefenses.CannotFreeze(target);_=ItemDurationDefenses.HalfFreeze(target);
        _=ColdStates.Effect(target);_=NecromancerEffects.DifficultyDivisor(target);
    }
    internal static bool Commit(BaseCreature source,BaseCreature target,int frames,DateTime at)
    {
        if(frames<=0){return false;}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Freeze delivery requires UTC source time");}
        int? effect=PaladinHolyFreeze.SourceRule(target)==null?null:ColdStates.Effect(target);
        int mode=FreezeStates.Mode(1,MonsterUninterruptibles.Active(target),CreatureProfiles.For(target).Traits.Boss==true,NamedCombat.Elite(target),target is BaseHire,effect);
        if(mode==0){return false;}
        bool applied=mode==1
            ?ColdStates.Commit(source,target,frames,true,ColdStates.Effect(target),NecromancerEffects.DifficultyDivisor(target),TimedEffects.TickAt(at))
            :FreezeStates.Commit(source,target,frames,true,NecromancerEffects.DifficultyDivisor(target),TimedEffects.TickAt(at));
        if(applied){target.FixedEffect(0x374A,10,15,0x480,0);} // Identified shared cold/freeze placeholder.
        return applied;
    }
    private sealed class Status(BaseCreature source,BaseCreature target,DateTime at,int pierce):PreparedMonsterDamageServices
    {
        // The owning hit already dispatched harmful/credit notifications.
        // The shared receiver records the actual last-attacker identity once;
        // do not mutate ordinary Combatant or replay a native zero-Damage hit.
        internal override void StoreLastAttacker(){}
        internal override PreparedMonsterDamagePacket MitigateMissile(PreparedMonsterDamagePacket p)
        {
            // CalculateTotalDamage has an absorb-event phase even when all
            // direct channels are zero. Reuse the existing defensive service;
            // do not confuse that phase with a second damage event.
            _=SkillCombat.MitigatePrepared(source,target,default,out int absorbed);
            if(absorbed!=0){throw new NotSupportedException("Zero-damage freeze absorb callback changed the packet");}
            int frames=ColdStates.LengthFrames(p.FreezeFrames,NamedElements.Resistance(source,target,"cold",target.ColdResistance,pierce),ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target));
            return p with{FreezeFrames=frames};
        }
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Zero-result status packet has no damage event");
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Zero-result status packet has no incoming damage event");
        internal override void BeforeHealth(PreparedMonsterDamagePacket p){}
        internal override bool SupportsFreeze=>true;
        internal override void ApplyCold(int frames){if(frames!=0){throw new InvalidOperationException("Item freeze packet has no ordinary cold length");}}
        internal override void ApplyFreeze(int frames)=>Commit(source,target,frames,at);
        internal override void SurvivedDamage()=>throw new InvalidOperationException("Zero-damage status has no recovery notification");
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Zero-damage status has no death event");
        internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Zero-damage status has no kill event");
        internal override void PresentOverlay(int value)=>throw new InvalidOperationException("Direct status has no hit-presentation phase");
        internal override int BaseHitClass()=>throw new InvalidOperationException("Direct status has no hit class");
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Direct status has no attempt event");
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Direct status has no attacked event");
        internal override bool AttackerInActiveMode()=>false;
        internal override void BeforeThorns(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Direct status has no thorns phase");
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death)=>throw new InvalidOperationException("Direct status has no return phase");
        internal override void FreePacket()=>throw new InvalidOperationException("Direct status owns a local stack packet");
    }
    internal static bool Execute(BaseCreature source,BaseCreature target,int frames,DateTime at)
    {
        Require(source,target);if(frames<=0){return false;}
        var owner=(PlayerMobile)source.ControlMaster;var metadata=NativeMonsterReturn.Metadata(target);
        if(target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)<=0||!target.CanBeDamaged()||!metadata.Killable
            ||source.Map==Map.Internal||source.Map==null||target.Map!=source.Map||!ClassCombat.IsHostile(owner,target)
            ||Server.Spells.SpellHelper.IsTown(target.Location,owner)&&!NativeMonsterReturn.SourceIdentity(source).Town){return false;}
        int pierce=ReviveEquipmentEvents.Handles(source)?ReviveEquipmentOffense.Capture(source,target).Pierce.Cold:NpcEquipmentCombat.Bonus(source,"pierce-cold");
        var packet=new PreparedMonsterDamagePacket(0,0,0,0,0,FreezeFrames:frames);
        PreparedMonsterDamage.CommitStatus(source,target,packet,new(false,()=>false,()=>metadata.Velocity,()=>target is BaseHire),
            _=>throw new InvalidOperationException("Item freeze status has no stun draw"),new Status(source,target,at,pierce),at);
        return true;
    }
}
