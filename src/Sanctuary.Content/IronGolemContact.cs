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
    internal static void RequireRecoveryEvents(BaseCreature target)
    {
        if(target.IsParagon||target is EncounterCreature encounter&&encounter.Has(EliteModifier.LightningEnchanted))
        {throw new NotSupportedException("Recipient unique recovery events need an explicit live return consumer");}
    }
    internal static NpcCombatEvents Events(BaseCreature target)
    {
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
    private sealed class Scope(Prepared prepared,int entryHits)
    {
        internal NpcWeaponImpact Impact=prepared.Impact;
        internal readonly NpcCombatEvents SourceEvents=prepared.Rules.SourceEvents,TargetEvents=prepared.Rules.TargetEvents;
        internal readonly bool NativeRecovery=prepared.Rules.NativeRecovery;
        internal readonly int EntryHits=entryHits;internal int Loss;
    }
    private sealed class Contact(SkeletonGuardian body,PlayerMobile owner,Func<int,bool> range,BaseCreature target,MonsterReturnMetadata metadata,Scope scope):MonsterContactServices
    {
        internal override PreparedMonsterDamagePacket CapturedPacket()=>scope.Impact?.Packet;
        internal override bool InMeleeRange(int bonus)=>range(bonus);
        internal override bool AttackerInDeathMode()=>body is not{Alive:true,Deleted:false}||SkillHealth.Balance256(body)<=0;
        internal override bool Hostile()=>ClassCombat.IsHostile(owner,target);
        internal override bool DefenderHasRoom()=>target.Map==body.Map&&target.Map is{} map&&map!=Map.Internal;
        internal override bool DefenderInTown()=>SpellHelper.IsTown(target.Location,owner);
        internal override bool AttackerAllowedInTown()=>NativeMonsterReturn.Identity(body.SourceMonsterId).Town;
        internal override bool DefenderKillable()=>metadata.Killable&&target.CanBeDamaged();
        internal override bool DefenderDead()=>target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)<=0;
    }
    private sealed class Damage(SkeletonGuardian source,PlayerMobile owner,BaseCreature target,MonsterReturnMetadata metadata,Scope scope,DateTime at):PreparedMonsterDamageServices
    {
        private bool Freed;
        internal override void StoreLastAttacker()=>source.DoHarmful(target);
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p)=>scope.SourceEvents.Dispatch("domeleedamage",p);
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)=>scope.TargetEvents.Dispatch("damagedinmelee",p);
        internal override void BeforeHealth(PreparedMonsterDamagePacket p)
        {if(scope.Impact==null){throw new InvalidOperationException("No allocated ordinary NPC drain context");}} // Positive drains were refused before rolling.
        internal override void BeforeHealthCommit(Mobile attacker,BaseCreature recipient,int after256)
            {PreparedMonsterNativeDamage.Notify(attacker,recipient,after256);scope.Loss=Math.Max(0,scope.EntryHits-(after256+255)/256);}
        internal override void ApplyCold(int frames)
        {
            if(frames!=scope.Impact.Packet.ColdFrames){throw new InvalidOperationException("Allocated cold duration changed");}
            if(frames>0){ColdStates.Commit(source,target,frames,true,ColdStates.Effect(target),NecromancerEffects.DifficultyDivisor(target),TimedEffects.TickAt(at));}
            var impact=scope.Impact;
            if(impact.Poison.Rate256>0&&impact.Poison.Frames>0)
            {NpcEquipmentPoison.Commit(new(impact.Actor,impact.CreditOwner,impact.Poison),target,at);}
        }
        internal override void SurvivedDamage(){if(!scope.NativeRecovery){RequireRecoveryEvents(target);}} // Unique damage effects belong to source return, not a second aggregate FHR roll.
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p)=>scope.TargetEvents.Dispatch("killed",p);
        internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p)=>scope.SourceEvents.Dispatch("kill",p);
        internal override void PresentOverlay(int overlay)
        {target.FixedEffect(0x37B9,10,12,0x481,0);} // Source overlay descriptor retained in packet; identified native placeholder.
        internal override int BaseHitClass()=>NativeMonsterReturn.Identity(source.SourceMonsterId).HitClass;
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)=>scope.SourceEvents.Dispatch("domeleeattack",p);
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>scope.TargetEvents.Dispatch("attackedinmelee",p);
        internal override bool AttackerInActiveMode()=>source is{Alive:true,Deleted:false}&&SkillHealth.Balance256(source)>0;
        internal override void BeforeThorns(PreparedMonsterDamagePacket p)
        {if(scope.Impact==null){throw new InvalidOperationException("NPC packet freed before return");}}
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death)
            =>PreparedMonsterReturn.Dispatch(ref p,new NativeMonsterReturn(source,owner,target,metadata,death,scope.NativeRecovery?()=>EliteCombat.OnStruck(target,source,scope.Loss):null),true);
        internal override void FreePacket()
        {if(Freed){throw new InvalidOperationException("NPC allocated packet freed twice");}Freed=true;scope.Impact=null;NativeMonsterReturn.Session(target).Packets++;}
    }
    internal static Rules Require(SkeletonGuardian body,PlayerMobile owner,BaseCreature target,bool nativeRecovery=false)
    {
        // All capability checks here precede the actor's hit/block/damage RNG.
        if(body is IronGolemBody)
        {
            foreach(string code in new[]{"cold","fire","light","poison","damage","magic"})
            {if(ClassGear.Bonus(owner,"pierce-immunity-"+code)!=0){throw new NotSupportedException("Live Iron modern Sunder delivery is unfinished: "+code);}}
        }
        var metadata=NativeMonsterReturn.Metadata(target);NativeMonsterReturn.Session(target);
        var item=NpcEquipmentCombat.Context(body);
        if(item==null&&body is not ISourceUnarmedNpc){throw new NotSupportedException("Missing ordinary NPC offensive context");}
        var sourceEvents=item is{} equipped?new NpcCombatEvents(equipped):NpcCombatEvents.Empty;
        var targetEvents=Events(target);
        if(nativeRecovery)
        {if(target.IsParagon){throw new NotSupportedException("Paragon damage/recovery needs its ordered actor adapter");}}
        else{RequireRecoveryEvents(target);}
        if(MonsterUninterruptibles.Active(target)||MonsterUninterruptibles.Delayed(target))
        {throw new NotSupportedException("Live recipient uninterruptible completion/death-delay adapter is unfinished");}
        if(NecromancerEffects.Curse(body)?.Ability==CombatAbility.IronMaiden)
        {throw new NotSupportedException("Iron Maiden needs its source-ordered attacker event4 consumer");}
        if(target.GetSkillMod("EvilOmen")!=null||BloodOathSpell.GetBloodOath(body)==target||target.Controlled&&!target.Summoned||target.SubdueBeforeTame
            ||target.GetType().GetMethod(nameof(BaseCreature.Damage))?.DeclaringType!=typeof(BaseCreature))
        {throw new NotSupportedException("Native recipient damage modifier/override needs an explicit ordered adapter");}
        return new(metadata,sourceEvents,targetEvents,nativeRecovery);
    }
    internal static Prepared Prepare(SkeletonGuardian body,BaseCreature target,Rules rules,Func<int> result)
        =>new(rules,NpcEquipmentImpact.Prepare(body,target,result()));
    internal static MonsterContactResult Release(SkeletonGuardian body,PlayerMobile owner,BaseCreature target,Prepared prepared,Func<int,bool> range,DateTime at)
    {
        if(prepared==null||prepared.Consumed||prepared.Impact.Actor!=body.Serial.Value||prepared.Impact.Target!=target.Serial.Value)
        {throw new InvalidOperationException("Ordinary NPC contact is missing,changed or already consumed");}
        prepared.Consumed=true;
        var scope=new Scope(prepared,target.Hits);var metadata=prepared.Rules.Metadata;
        var recipient=new PreparedMonsterRecipient(target is EncounterCreature encounter&&encounter.Grade is EncounterGrade.Unique or EncounterGrade.SuperUnique,
            ()=>CreatureProfiles.For(target).Traits.Boss??false,()=>metadata.Velocity,()=>target is BaseHire);
        using var attempted=PaladinThorns.Attempt(body,target,false);
        return PreparedMonsterContact.Release(body,target,new Contact(body,owner,range,target,metadata,scope),recipient,
            n=>NpcEquipmentRandom.Limited(body,n),new Damage(body,owner,target,metadata,scope,at),at);
    }
    internal static MonsterContactResult Release(IronGolemBody body,PlayerMobile owner,IronGolemWorldState state,BaseCreature target,DateTime at)
    {
        var prepared=Prepare(body,target,Require(body,owner,target),()=>IronGolemWorld.Result(body,owner,state,target));
        return Release(body,owner,target,prepared,bonus=>IronGolemWorld.Melee(body,state,target,bonus),at);
    }
}
