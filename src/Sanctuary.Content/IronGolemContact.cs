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
    private static NpcCombatEvents Events(BaseCreature target)
    {
        if(NpcEquipmentCombat.Context(target) is{} item){return new(item);}
        if(target.Items.Any(i=>i.Layer is not(Layer.Invalid or Layer.Backpack or Layer.Hair or Layer.FacialHair or Layer.Mount)&&ClassGear.Converted(i)))
        {throw new NotSupportedException("Recipient converted equipment needs its NPC combat-event adapter");}
        return NpcCombatEvents.Empty;
    }
    private sealed class Scope
    {
        internal NpcWeaponImpact Impact;internal readonly NpcCombatEvents SourceEvents,TargetEvents;
        internal Scope(NpcCombatEvents sourceEvents,NpcCombatEvents targetEvents){SourceEvents=sourceEvents;TargetEvents=targetEvents;}
    }
    private sealed class Contact(IronGolemBody body,PlayerMobile owner,IronGolemWorldState state,BaseCreature target,MonsterReturnMetadata metadata,Scope scope):MonsterContactServices
    {
        internal override PreparedMonsterDamagePacket CapturedPacket()=>scope.Impact?.Packet;
        internal override bool InMeleeRange(int bonus)=>IronGolemWorld.Melee(body,state,target,bonus);
        internal override bool AttackerInDeathMode()=>body is not{Alive:true,Deleted:false}||SkillHealth.Balance256(body)<=0;
        internal override bool Hostile()=>ClassCombat.IsHostile(owner,target);
        internal override bool DefenderHasRoom()=>target.Map==body.Map&&target.Map is{} map&&map!=Map.Internal;
        internal override bool DefenderInTown()=>SpellHelper.IsTown(target.Location,owner);
        internal override bool AttackerAllowedInTown()=>NativeMonsterReturn.Identity("irongolem").Town;
        internal override bool DefenderKillable()=>metadata.Killable&&target.CanBeDamaged();
        internal override bool DefenderDead()=>target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)<=0;
    }
    private sealed class Damage(IronGolemBody source,PlayerMobile owner,BaseCreature target,MonsterReturnMetadata metadata,Scope scope,DateTime at):PreparedMonsterDamageServices
    {
        private bool Freed;
        internal override void StoreLastAttacker()=>source.DoHarmful(target);
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p)=>scope.SourceEvents.Dispatch("domeleedamage",p);
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)=>scope.TargetEvents.Dispatch("damagedinmelee",p);
        internal override void BeforeHealth(PreparedMonsterDamagePacket p)
        {if(scope.Impact==null){throw new InvalidOperationException("No allocated ordinary NPC drain context");}} // Positive drains were refused before rolling.
        internal override void BeforeHealthCommit(Mobile attacker,BaseCreature recipient,int after256)
            =>PreparedMonsterNativeDamage.Notify(attacker,recipient,after256);
        internal override void ApplyCold(int frames)
        {
            if(frames!=scope.Impact.Packet.ColdFrames){throw new InvalidOperationException("Allocated cold duration changed");}
            if(frames>0){ColdStates.Commit(source,target,frames,true,ColdStates.Effect(target),NecromancerEffects.DifficultyDivisor(target),TimedEffects.TickAt(at));}
            var impact=scope.Impact;
            if(impact.Poison.Rate256>0&&impact.Poison.Frames>0)
            {NpcEquipmentPoison.Commit(new(impact.Actor,impact.CreditOwner,impact.Poison),target,at);}
        }
        internal override void SurvivedDamage()=>RequireRecoveryEvents(target); // Unique damage effects belong to source return, not a second aggregate FHR roll.
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p)=>scope.TargetEvents.Dispatch("killed",p);
        internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p)=>scope.SourceEvents.Dispatch("kill",p);
        internal override void PresentOverlay(int overlay)
        {target.FixedEffect(0x37B9,10,12,0x481,0);} // Source overlay descriptor retained in packet; identified native placeholder.
        internal override int BaseHitClass()=>NativeMonsterReturn.Identity("irongolem").HitClass;
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)=>scope.SourceEvents.Dispatch("domeleeattack",p);
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>scope.TargetEvents.Dispatch("attackedinmelee",p);
        internal override bool AttackerInActiveMode()=>source is{Alive:true,Deleted:false}&&SkillHealth.Balance256(source)>0;
        internal override void BeforeThorns(PreparedMonsterDamagePacket p)
        {if(scope.Impact==null){throw new InvalidOperationException("NPC packet freed before return");}}
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death)
            =>PreparedMonsterReturn.Dispatch(ref p,new NativeMonsterReturn(source,owner,target,metadata,death),true);
        internal override void FreePacket()
        {if(Freed){throw new InvalidOperationException("NPC allocated packet freed twice");}Freed=true;scope.Impact=null;NativeMonsterReturn.Session(target).Packets++;}
    }
    internal static MonsterContactResult Release(IronGolemBody body,PlayerMobile owner,IronGolemWorldState state,BaseCreature target,DateTime at)
    {
        // Capability refusal precedes hit/block/damage RNG and all callbacks.
        foreach(string code in new[]{"cold","fire","light","poison","damage","magic"})
        {if(ClassGear.Bonus(owner,"pierce-immunity-"+code)!=0){throw new NotSupportedException("Live Iron modern Sunder delivery is unfinished: "+code);}}
        var metadata=NativeMonsterReturn.Metadata(target);NativeMonsterReturn.Session(target);
        var item=NpcEquipmentCombat.Context(body)??throw new NotSupportedException("Missing live Iron original");
        var scope=new Scope(new(item),Events(target));
        RequireRecoveryEvents(target);
        if(MonsterUninterruptibles.Active(target)||MonsterUninterruptibles.Delayed(target))
        {throw new NotSupportedException("Live recipient uninterruptible completion/death-delay adapter is unfinished");}
        if(NecromancerEffects.Curse(body)?.Ability==CombatAbility.IronMaiden)
        {throw new NotSupportedException("Iron Maiden needs its source-ordered attacker event4 consumer");}
        if(target.GetSkillMod("EvilOmen")!=null||BloodOathSpell.GetBloodOath(body)==target||target.Controlled&&!target.Summoned||target.SubdueBeforeTame
            ||target.GetType().GetMethod(nameof(BaseCreature.Damage))?.DeclaringType!=typeof(BaseCreature))
        {throw new NotSupportedException("Native recipient damage modifier/override needs an explicit ordered adapter");}
        int flags=IronGolemWorld.Result(body,owner,state,target);
        scope.Impact=NpcEquipmentImpact.Prepare(body,target,flags);
        var recipient=new PreparedMonsterRecipient(target is EncounterCreature encounter&&encounter.Grade is EncounterGrade.Unique or EncounterGrade.SuperUnique,
            ()=>CreatureProfiles.For(target).Traits.Boss??false,()=>metadata.Velocity,()=>target is BaseHire);
        using var attempted=PaladinThorns.Attempt(body,target,false);
        return PreparedMonsterContact.Release(body,target,new Contact(body,owner,state,target,metadata,scope),recipient,
            n=>NpcEquipmentRandom.Limited(body,n),new Damage(body,owner,target,metadata,scope,at),at);
    }
}
