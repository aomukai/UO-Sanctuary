// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
using Server.Spells.Necromancy;
namespace Sanctuary;

// A player actor consumer of the existing ordered source-to-NPC pipeline.
// Item processors and native encounter reactions remain explicit adapters.
internal static class PoisonDaggerContact
{
    internal static void Require(PlayerMobile source,BaseCreature target)
    {
        WeaponStartEffects.Require(source);
        var metadata=NativeMonsterReturn.Metadata(target);IronGolemContact.Events(target);
        if(metadata.Base==78&&!target.Frozen&&NamedControl.FrozenRemaining(target)==0)
        {throw new NotSupportedException("Sand-leaper return movement needs its Poison Dagger actor adapter.");}
        if(MonsterUninterruptibles.Active(target)||MonsterUninterruptibles.Delayed(target))
        {throw new NotSupportedException("Recipient uninterruptible/death-delay completion is not adapted for Poison Dagger.");}
        if(target.GetSkillMod("EvilOmen")!=null||BloodOathSpell.GetBloodOath(source)==target||target.Controlled&&!target.Summoned
            ||target.SubdueBeforeTame||target.GetType().GetMethod(nameof(BaseCreature.Damage))?.DeclaringType!=typeof(BaseCreature))
        {throw new NotSupportedException("This recipient's native incoming damage modifier needs an ordered adapter.");}
        if(target.IsParagon){throw new NotSupportedException("Paragon damage/recovery needs its ordered actor adapter.");}
    }
    private sealed class Scope(PoisonDaggerCombat.Prepared prepared,PreparedMonsterDamagePacket packet)
    {
        internal readonly PoisonDaggerCombat.Prepared Prepared=prepared;
        internal PreparedMonsterDamagePacket Packet=packet;
        internal int Before,After,Loss,EntryHits;
        internal Point3D Point;internal Map Map;
        internal IDisposable KillRecovery;
    }
    private sealed class Contact(PlayerMobile source,BaseCreature target,MonsterReturnMetadata metadata,Scope scope):MonsterContactServices
    {
        internal override PreparedMonsterDamagePacket CapturedPacket()=>scope.Packet;
        internal override bool InMeleeRange(int bonus)
        {
            if(bonus!=1){throw new NotSupportedException("Unmapped Poison Dagger contact range bonus");}
            return source.Map==target.Map&&source.InRange(target,CombatTiming.Range(source,CombatAbility.BasicAttack))&&source.InLOS(target);
        }
        internal override bool AttackerInDeathMode()=>source is not{Alive:true,Deleted:false}||SkillHealth.Balance256(source)<=0;
        internal override bool Hostile()=>ClassCombat.IsHostile(source,target);
        internal override bool DefenderHasRoom()=>target.Map is{} map&&map!=Map.Internal;
        internal override bool DefenderInTown()=>SpellHelper.IsTown(target.Location,source);
        internal override bool AttackerAllowedInTown()=>false;
        internal override bool DefenderKillable()=>metadata.Killable&&target.CanBeDamaged();
        internal override bool DefenderDead()=>target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)<=0;
    }
    private sealed class Damage(PlayerMobile source,BaseCreature target,MonsterReturnMetadata metadata,NpcCombatEvents events,Scope scope,DateTime at):PreparedMonsterDamageServices
    {
        private bool Freed;
        internal override void StoreLastAttacker()=>source.DoHarmful(target,true);
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p)
        {NamedCombat.Crushing(source,target,false,PoisonDaggerCombat.Effects);return p;}
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)=>events.Dispatch("damagedinmelee",p);
        internal override void BeforeHealth(PreparedMonsterDamagePacket p)
        {scope.Before=SkillHealth.Balance256(target);WeaponLeech.Apply(source,target,p.Physical256,scope.Prepared.Drain);}
        internal override void BeforeHealthCommit(Mobile attacker,BaseCreature recipient,int after256)
        {PreparedMonsterNativeDamage.Notify(attacker,recipient,after256);scope.After=after256;scope.Loss=Math.Max(0,scope.EntryHits-(after256+255)/256);}
        internal override void ApplyCold(int frames)
        {
            if(frames>0){ColdStates.Commit(source,target,frames,true,ColdStates.Effect(target),NecromancerEffects.DifficultyDivisor(target),TimedEffects.TickAt(at));}
            scope.Prepared.Impact.AllocatedPoison?.Commit(source,target,at);
        }
        internal override void SurvivedDamage()
        {
            // Preserve existing supported conditional item consumers. Their
            // internal registration/RNG order still needs individual acceptance.
            NamedCombat.AfterStrike(source,target,false,PoisonDaggerCombat.Effects,setupApplied:true);
        }
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p)=>events.Dispatch("killed",p);
        internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p)
        {
            if(CombatItemEvents.Allowed(source,target)&&RewardLife.KillRecoveryEligible(target,source))
            {
                target.LastKiller=source;NamedCombat.Killed(source,target);
                scope.KillRecovery=CombatItemEvents.SuppressKillRecovery(source,target);
            }
            return p;
        }
        internal override void PresentOverlay(int overlay)=>target.FixedEffect(0x37B9,10,12,0x481,0); // Identified placeholder.
        internal override int BaseHitClass()=>6; // Pinned knife rows use 1ht/HITCLASS_OneHandThrust.
        internal override void AfterHitPresentation(PreparedMonsterDamagePacket packet)
        {
            if(CombatTiming.Equipped(source) is{} weapon&&NamedAmmunition.Maximum(weapon)==0){weapon.WearOnHit(source,target);}
            if(scope.Loss>0&&scope.Map!=null&&scope.Map!=Map.Internal&&ClassGear.Bonus(source,"bloody")>0)
            {new Blood().MoveToWorld(scope.Point,scope.Map);}
        }
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)
        {
            // Armor reactions retain their compatibility attempt hook. Life
            // Tap/Iron Maiden already ran in the fractional damage-event phase.
            NecromancerEffects.WeaponHit(source,target,Math.Min(scope.Loss,p.Physical256>>8),false,lifeTap:false,ironMaiden:false);
            return p;
        }
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>events.Dispatch("attackedinmelee",p);
        internal override bool AttackerInActiveMode()=>source is{Alive:true,Deleted:false}&&SkillHealth.Balance256(source)>0;
        internal override void BeforeThorns(PreparedMonsterDamagePacket p){}
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death)
            =>PreparedMonsterReturn.Dispatch(ref p,new NativeMonsterReturn(source,source,target,metadata,death,
                ()=>EliteCombat.OnStruck(target,source,scope.Loss)),true);
        internal override void FreePacket()
        {if(Freed){throw new InvalidOperationException("Player contact packet freed twice");}Freed=true;scope.Packet=null;NativeMonsterReturn.Session(target).Packets++;}
    }
    internal static MonsterContactResult Release(PlayerMobile source,BaseCreature target,PoisonDaggerCombat.Prepared prepared,DateTime at)
    {
        Require(source,target);var metadata=NativeMonsterReturn.Metadata(target);NativeMonsterReturn.Session(target);
        var impact=prepared.Impact;var parts=impact.Parts;
        var packet=new PreparedMonsterDamagePacket(NamedElements.To256(parts.Physical),impact.LifeDamage256??0,impact.SourceStunFrames,
            prepared.ColdFrames,prepared.Flags,HitClass:(prepared.Flags&1)!=0?0x50:0,
            Magic256:NamedElements.To256(parts.Magic),Fire256:NamedElements.To256(parts.Fire),Lightning256:NamedElements.To256(parts.Lightning),
            Cold256:NamedElements.To256(parts.Cold),Poison256:impact.AllocatedPoison?.Rate256??0,AbsorbedLife256:impact.Absorbed256);
        var scope=new Scope(prepared,packet){Point=target.Location,Map=target.Map,EntryHits=target.Hits};var recipient=new PreparedMonsterRecipient(target is EncounterCreature e&&e.Grade is EncounterGrade.Unique or EncounterGrade.SuperUnique,
            ()=>CreatureProfiles.For(target).Traits.Boss??false,()=>metadata.Velocity,()=>target is BaseHire);
        using var attempted=PaladinThorns.Attempt(source,target,false);
        try
        {
            return PreparedMonsterContact.Release(source,target,new Contact(source,target,metadata,scope),recipient,Utility.Random,
                new Damage(source,target,metadata,IronGolemContact.Events(target),scope,at),at);
        }
        finally{scope.KillRecovery?.Dispose();}
    }
}
