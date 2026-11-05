// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
using Server.Spells.Necromancy;
namespace Sanctuary;

internal static class IronGolemContact
{
    internal sealed record Saved(MonsterReturnMetadata Metadata,int[] SourceEvents,int[] TargetEvents,bool NativeRecovery,NpcWeaponImpact Impact);
    internal static Saved Capture(Prepared prepared)
    {
        if(prepared==null||prepared.Consumed){throw new InvalidOperationException("Cannot save a consumed ordinary contact");}
        var saved=new Saved(prepared.Rules.Metadata,prepared.Rules.SourceEvents.Subscriptions(),prepared.Rules.TargetEvents.Subscriptions(),prepared.Rules.NativeRecovery,prepared.Impact);
        _=Restore(saved);return saved;
    }
    internal static Prepared Restore(Saved saved)
    {
        if(saved?.Metadata==null||saved.Impact is not{} impact||impact.Packet is not{} p||impact.Actor==0||impact.Target==0||impact.Actor==impact.Target
            ||saved.Metadata.Mapping==null||saved.Metadata.Class< -1||saved.Metadata.Base< -1||saved.Metadata.Modes<0||saved.Metadata.Velocity<0
            ||impact.Spectral!=null||impact.Poison.Rate256<0||impact.Poison.Frames<0||p.Physical256<0||p.Total256<0||p.StunFrames<0||p.ColdFrames<0
            ||p.Magic256<0||p.Fire256<0||p.Lightning256<0||p.Cold256<0||p.Poison256<0||p.AbsorbedLife256<0||p.LifeDrain256<0||p.ManaDrain256<0||p.StaminaDrain256<0||p.FreezeFrames<0||(p.ResultFlags&~0xffff)!=0
            ||(long)p.Physical256+p.Magic256+p.Fire256+p.Lightning256+p.Cold256+p.Poison256+p.LifeDrain256!=p.Total256)
        {throw new System.IO.InvalidDataException("Invalid saved ordinary contact");}
        return new(new(saved.Metadata,NpcCombatEvents.RestoreSubscriptions(saved.SourceEvents),NpcCombatEvents.RestoreSubscriptions(saved.TargetEvents),saved.NativeRecovery),impact);
    }
    internal static void RequireRecoveryEvents(BaseCreature target)
    {
        if(target.IsParagon||target is EncounterCreature encounter&&encounter.Has(EliteModifier.LightningEnchanted))
        {throw new NotSupportedException("Recipient unique recovery events need an explicit live return consumer");}
    }
    internal static NpcCombatEvents Events(BaseCreature target,bool requireFrozen=false)
    {
        if(ReviveEquipmentEvents.Handles(target)){return ReviveEquipmentEvents.Registry(target);}
        if(requireFrozen&&ReviveNativeActivation.State(target)is{Setup.EquipmentLifeProfile:not null})
        {throw new NotSupportedException("Frozen recipient needs explicit registered-event admission");}
        if(NpcEquipmentCombat.Context(target) is{} item){return new(item);}
        if(target.Items.Any(i=>i.Layer is not(Layer.Invalid or Layer.Backpack or Layer.Hair or Layer.FacialHair or Layer.Mount)&&ClassGear.Converted(i)))
        {throw new NotSupportedException("Recipient converted equipment needs its NPC combat-event adapter");}
        return NpcCombatEvents.Empty;
    }
    // Shared ordered ordinary-NPC consumer. Iron keeps its existing release-
    // time allocation; reviewed unarmed summons capture the same packet at start.
    internal sealed record Rules(MonsterReturnMetadata Metadata,NpcCombatEvents SourceEvents,NpcCombatEvents TargetEvents,bool NativeRecovery);
    internal sealed class Prepared(Rules rules,NpcWeaponImpact impact)
    {
        internal readonly Rules Rules=rules;internal readonly NpcWeaponImpact Impact=impact;
        internal bool Consumed;
        internal void Cancel(){Consumed=true;}
    }
    private sealed class Scope(Prepared prepared,int entryHits,BaseCreature source)
    {
        internal NpcWeaponImpact Impact=prepared.Impact;
        internal readonly NpcCombatEvents SourceEvents=prepared.Rules.SourceEvents,TargetEvents=prepared.Rules.TargetEvents;
        internal readonly bool NativeRecovery=prepared.Rules.NativeRecovery;
        // Attacker damage events can retire an original Revive before hit
        // presentation/return. Retain only this synchronous packet's identity.
        internal readonly MonsterReturnIdentity SourceIdentity=source is{Alive:true,Deleted:false}?NativeMonsterReturn.SourceIdentity(source):null;
        internal readonly int EntryHits=entryHits;internal int Loss;
    }
    private sealed class Contact(BaseCreature body,PlayerMobile owner,Func<int,bool> range,BaseCreature target,MonsterReturnMetadata metadata,Scope scope):MonsterContactServices
    {
        internal override PreparedMonsterDamagePacket CapturedPacket()=>scope.Impact?.Packet;
        internal override bool InMeleeRange(int bonus)=>range(bonus);
        internal override bool AttackerInDeathMode()=>body is not{Alive:true,Deleted:false}||SkillHealth.Balance256(body)<=0;
        internal override bool Hostile()=>ClassCombat.IsHostile(owner,target);
        internal override bool DefenderHasRoom()=>target.Map==body.Map&&target.Map is{} map&&map!=Map.Internal;
        internal override bool DefenderInTown()=>SpellHelper.IsTown(target.Location,owner);
        internal override bool AttackerAllowedInTown()=>scope.SourceIdentity.Town;
        internal override bool DefenderKillable()=>metadata.Killable&&target.CanBeDamaged();
        internal override bool DefenderDead()=>target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)<=0;
    }
    private sealed class Damage(BaseCreature source,PlayerMobile owner,BaseCreature target,MonsterReturnMetadata metadata,Scope scope,DateTime at):PreparedMonsterDamageServices
    {
        private bool Freed;
        internal override void StoreLastAttacker()=>source.DoHarmful(target);
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p)
        {if(source is BloodGolemBody blood){BloodGolemHealing.Dealt(blood,target,p);}return scope.SourceEvents.Dispatch("domeleedamage",p,source,target,at);}
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)=>scope.TargetEvents.Dispatch("damagedinmelee",p,target,source,at);
        internal override bool OrdinaryNpcFlatDrains=>true;
        internal override void BeforeHealth(PreparedMonsterDamagePacket p)
        {if(scope.Impact==null){throw new InvalidOperationException("No allocated ordinary NPC drain context");}} // Existing allocators retain their preflight;qualified filled packets use the shared drain receiver.
        internal override void BeforeHealthCommit(Mobile attacker,BaseCreature recipient,int after256)
            {PreparedMonsterNativeDamage.Notify(attacker,recipient,after256);scope.Loss=Math.Max(0,scope.EntryHits-(after256+255)/256);}
        internal override void ApplyCold(int frames)
        {
            if(frames!=scope.Impact.Packet.ColdFrames){throw new InvalidOperationException("Allocated cold duration changed");}
            if(frames>0){ColdStates.Commit(source,target,frames,true,ColdStates.Effect(target),NecromancerEffects.DifficultyDivisor(target),TimedEffects.TickAt(at));}
        }
        internal override bool SupportsFreeze=>true;
        internal override void ApplyFreeze(int frames)=>NpcFreezeStatus.Commit(source,target,frames,at);
        internal override void ApplyPoison()
        {
            var impact=scope.Impact;
            if(impact.Poison.Rate256>0&&impact.Poison.Frames>0)
            {NpcEquipmentPoison.Commit(new(impact.Actor,impact.CreditOwner,impact.Poison),target,at);}
        }
        internal override void SurvivedDamage(){if(!scope.NativeRecovery){RequireRecoveryEvents(target);}} // Unique damage effects belong to source return, not a second aggregate FHR roll.
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p)=>scope.TargetEvents.Dispatch("killed",p,target,source,at);
        internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p)=>scope.SourceEvents.Dispatch("kill",p,source,target,at);
        internal override void PresentOverlay(int overlay)
        {target.FixedEffect(0x37B9,10,12,0x481,0);} // Source overlay descriptor retained in packet; identified native placeholder.
        internal override int BaseHitClass()=>scope.SourceIdentity.HitClass;
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)=>scope.SourceEvents.Dispatch("domeleeattack",p,source,target,at);
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>scope.TargetEvents.Dispatch("attackedinmelee",p,target,source,at);
        internal override bool AttackerInActiveMode()=>source is{Alive:true,Deleted:false}&&SkillHealth.Balance256(source)>0;
        internal override void BeforeThorns(PreparedMonsterDamagePacket p)
        {if(scope.Impact==null){throw new InvalidOperationException("NPC packet freed before return");}}
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death)
            =>PreparedMonsterReturn.Dispatch(ref p,new NativeMonsterReturn(source,owner,target,metadata,death,scope.NativeRecovery?()=>EliteCombat.OnStruck(target,source,scope.Loss):null,sourceIdentity:scope.SourceIdentity),true);
        internal override void FreePacket()
        {if(Freed){throw new InvalidOperationException("NPC allocated packet freed twice");}Freed=true;scope.Impact=null;NativeMonsterReturn.Session(target).Packets++;}
    }
    internal static Rules Require(BaseCreature body,PlayerMobile owner,BaseCreature target,bool nativeRecovery=false)
    {
        // All capability checks here precede the actor's hit/block/damage RNG.
        if(body is IronGolemBody)
        {
            foreach(string code in new[]{"cold","fire","light","poison","damage","magic"})
            {if(ClassGear.Bonus(owner,"pierce-immunity-"+code)!=0){throw new NotSupportedException("Live Iron modern Sunder delivery is unfinished: "+code);}}
        }
        var metadata=NativeMonsterReturn.Metadata(target);NativeMonsterReturn.Session(target);
        var item=NpcEquipmentCombat.Context(body);
        if(item==null&&body is not ISourceUnarmedNpc&&!ReviveEquipmentEvents.Handles(body)){throw new NotSupportedException("Missing ordinary NPC offensive context");}
        if(ReviveEquipmentEvents.Handles(body)&&(body.ControlMaster!=owner||owner is not{Alive:true,Deleted:false}))
        {throw new NotSupportedException("Original Revive contact lost its living owner");}
        _=NativeMonsterReturn.SourceIdentity(body);
        if(ReviveEquipmentEvents.Handles(body)){_=ReviveEquipmentOffense.Capture(body,target);}
        var sourceEvents=NpcCombatEvents.ForSource(body);
        var targetEvents=Events(target,requireFrozen:true);
        if(sourceEvents.Subscriptions().Contains(134)){NpcFreezeStatus.Require(body,target);}
        if(nativeRecovery)
        {if(target.IsParagon){throw new NotSupportedException("Paragon damage/recovery needs its ordered actor adapter");}}
        else{RequireRecoveryEvents(target);}
        if(MonsterUninterruptibles.Active(target)||MonsterUninterruptibles.Delayed(target))
        {throw new NotSupportedException("Live recipient uninterruptible completion/death-delay adapter is unfinished");}
        // PreparedMonsterDamage already dispatches the shared fractional
        // Iron Maiden attacker event before defender Life Tap and HP clipping.
        _=NpcArmorOnHit.Require(body,target);
        PreparedMonsterNativeDamage.RequireBaseModifiers(body,target);
        return new(metadata,sourceEvents,targetEvents,nativeRecovery);
    }
    internal static Prepared Prepare(BaseCreature body,BaseCreature target,Rules rules,Func<int> result)
        =>new(rules,body is SkeletonGuardian guardian?NpcEquipmentImpact.Prepare(guardian,target,result())
            :ReviveEquipmentEvents.Handles(body)?ReviveEquipmentImpact.Prepare(body,target,result())
            :throw new NotSupportedException("No admitted ordinary NPC allocator"));
    internal static MonsterContactResult Release(BaseCreature body,PlayerMobile owner,BaseCreature target,Prepared prepared,Func<int,bool> range,DateTime at)
    {
        if(prepared==null||prepared.Consumed||prepared.Impact.Actor!=body.Serial.Value||prepared.Impact.Target!=target.Serial.Value)
        {throw new InvalidOperationException("Ordinary NPC contact is missing,changed or already consumed");}
        if(NpcOpenWounds.Chance(body)>0)
        {
            // Settle before entering the qualified hit: a callback must never
            // dispatch overdue damage while its incoming HP is being committed.
            TimedEffects.Settle(target,at);
            if(target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)<=0)
            {prepared.Cancel();return new(MonsterContactExit.SettledRecipientFreed,prepared.Impact.Packet with{ResultFlags=prepared.Impact.Packet.ResultFlags&~3},null);}
            _=NpcEquipmentRegeneration.ItemRate(target); // Existing unsupported material rates refuse before event RNG.
        }
        prepared.Consumed=true;
        var scope=new Scope(prepared,target.Hits,body);var metadata=prepared.Rules.Metadata;
        var recipient=new PreparedMonsterRecipient(target is EncounterCreature encounter&&encounter.Grade is EncounterGrade.Unique or EncounterGrade.SuperUnique,
            ()=>CreatureProfiles.For(target).Traits.Boss??false,()=>metadata.Velocity,()=>target is BaseHire);
        using var attempted=PaladinThorns.Attempt(body,target,false);
        var result=PreparedMonsterContact.Release(body,target,new Contact(body,owner,range,target,metadata,scope),recipient,
            n=>NpcEquipmentRandom.Limited(body,n),new Damage(body,owner,target,metadata,scope,at),at);
        if(result.Commit is{AfterHealth256:>0}){NpcSpectralHit.After(body,target,prepared.Impact.Spectral,at);}
        return result;
    }
    internal static MonsterContactResult Release(IronGolemBody body,PlayerMobile owner,IronGolemWorldState state,BaseCreature target,DateTime at)
    {
        var prepared=Prepare(body,target,Require(body,owner,target),()=>IronGolemWorld.Result(body,owner,state,target));
        return Release(body,owner,target,prepared,bonus=>IronGolemWorld.Melee(body,state,target,bonus),at);
    }
}
