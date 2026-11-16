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
internal sealed record PlayerWeaponContactPrepared(int Flags,PreparedWeaponImpact Impact,PlayerWeaponDrain Drain,int ColdFrames,int BaseHitClass,Random Effects,int HitClass=0,
    bool Smite=false,Func<int,int> StunRoll=null,int? MeleeRange=null,bool SkillKnockback=false,Func<int,bool> RangeCheck=null,int FreezeFrames=0,uint HitFlags=0,int? CrushingDivisor=null,bool SuppressWear=false,bool AttemptScoped=false,
    Action<PlayerMobile,BaseCreature,DateTime> CapturedPoison=null,bool ControlStun=false,Func<bool> UninterruptibleItemCasts=null,Func<bool> ActiveSourceMode=null,Action WeaponWear=null);

internal static class PlayerWeaponContact
{
    internal static MonsterReturnMetadata Metadata(BaseCreature target,bool allowNative)
        =>allowNative&&target is not EncounterCreature&&CreatureProfiles.Definition(target).Reference==null?null:NativeMonsterReturn.Metadata(target);
    internal static void Require(PlayerMobile source,BaseCreature target,bool allowNative=false,bool weaponSetup=true)
    {
        if(weaponSetup){WeaponStartEffects.Require(source);}
        var metadata=Metadata(target,allowNative);IronGolemContact.Events(target);
        if(metadata?.Base==78&&!target.Frozen&&NamedControl.FrozenRemaining(target)==0)
        {throw new NotSupportedException("Sand-leaper return movement needs its player weapon actor adapter.");}
        if(MonsterUninterruptibles.Active(target)||MonsterUninterruptibles.Delayed(target))
        {throw new NotSupportedException("Recipient uninterruptible/death-delay completion is not adapted for player weapon contact.");}
        PreparedMonsterNativeDamage.RequireIncoming(source,target);
    }
    internal static void RequireOrdinary(PlayerMobile source,BaseCreature target,bool allowNative=false)
    {
        Require(source,target,allowNative);CreatureHitRecovery.PlayerHitClass(source);BowLaunch.RawPoison(source,CombatTiming.Equipped(source));
        if(Metadata(target,allowNative)!=null&&!SourceBlocking.TryRate(target,out _,out _))
        {throw new NotSupportedException("Recipient shield rule is unavailable for player weapon contact.");}
    }
    private sealed class OrdinaryResult(PlayerMobile source,BaseCreature target,Func<bool> inRange,Func<double> hitRoll,bool native):SourceMeleeResultServices
    {
        internal override int? AttackerKind=>0;
        internal override int? DefenderKind=>1;
        internal override bool Hostile()=>ClassCombat.IsHostile(source,target);
        internal override bool DefenderRunning()=>throw new InvalidOperationException("NPC is not a running player");
        internal override int MeleeReach()=>throw new NotSupportedException("Ordinary player contact uses zero range offset");
        internal override bool InRange(int bonus)=>bonus==1&&inRange();
        internal override bool HitSuccessful(int percent,bool missile)
        {
            if(missile){throw new InvalidOperationException("Ordinary player contact is melee");}
            bool hit=ClassStats.CheckHit(source,target,percent,hitRoll,notifyMelee:false,npcDefense:false)==true;
            if(hit){WeaponStartEffects.SuccessfulHitRoll(source,target);}return hit;
        }
        internal override int BlockOrDodge(bool avoid,bool block)
        {
            if(native){return 0;} // No invented D2 shield for an unclassified UO actor.
            if(!NpcBlockOrDodge.Try(target,avoid,block,out int flags,out _))
            {throw new NotSupportedException("Recipient shield rule is unavailable for player weapon contact.");}
            return flags;
        }
        internal override bool Uninterruptible()=>MonsterUninterruptibles.Active(target);
    }
    // For skills which let AllocCombat fill ordinary weapon lanes. A consumer
    // chooses its allocation phase; Zeal allocates anew at every do13 event.
    internal static PlayerWeaponContactPrepared PrepareOrdinary(PlayerMobile source,BaseCreature target,int attackPercent,int skillPhysicalPercent,
        Func<bool> inRange,Func<double> hitRoll,Func<double> elementRoll,Func<int,int> physicalRoll,Func<int,int> poisonRoll,Random effects,bool allowNative=false,int physicalToMagicPercent=0,Func<PreparedWeaponImpact> captured=null,int capturedColdFrames=0,PlayerWeaponDrain? capturedDrain=null,int? crushingDivisor=null,bool suppressWear=false)
    {
        RequireOrdinary(source,target,allowNative);
        int flags=SourceMeleeResult.Resolve(new OrdinaryResult(source,target,inRange,hitRoll,Metadata(target,allowNative)==null),attackPercent);
        HexBaneEvents.MeleeResult(source,target);
        PreparedWeaponImpact impact=default;int frames=0;
        if((flags&SourceMeleeResult.Successful)!=0)
        {
            WeaponStartEffects.PrepareDamage(source,target);
            if(captured!=null){impact=captured();frames=capturedColdFrames;}
            else
            {
            double roll=elementRoll();
            var damage=GearEffects.Damage(source,roll,target,skillPhysicalPercent,physicalRoll,poisonRoll,
                ()=>WeaponPoisonPackets.Melee(WeaponPoisonPackets.AddVenom(BowLaunch.RawPoison(source,CombatTiming.Equipped(source)),Venoms.State(source)),
                    NamedElements.MasteryPercent(source,"pois"),128,poisonRoll,overrideFrames:Venoms.State(source)?.Frames??0));
            impact=GearEffects.PrepareWeaponImpact(source,target,roll,effects,damage,physicalToMagicPercent:physicalToMagicPercent);
            var poison=PoisonJavelins.PreparePoison(source,target,damage.SourcePoison.Rate256,damage.SourcePoison.Frames);
            impact=impact with{LifeDamage256=checked(NamedElements.To256(impact.Parts.Total)+poison.Rate256),AllocatedPoison=poison};
            if(damage.Cold>0)
            {
                if(damage.ColdMs%40!=0){throw new NotSupportedException("Weapon cold duration has no exact source-frame projection");}
                frames=ColdStates.LengthFrames(damage.ColdMs/40,NamedElements.Resistance(source,target,"cold",target.ColdResistance),
                    ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target));
            }
            }
        }
        return new(flags,impact,capturedDrain??WeaponLeech.Capture(source),frames,CreatureHitRecovery.PlayerHitClass(source),effects,CrushingDivisor:crushingDivisor,SuppressWear:suppressWear);
    }
    private sealed class Scope(PlayerWeaponContactPrepared prepared,PreparedMonsterDamagePacket packet)
    {
        internal readonly PlayerWeaponContactPrepared Prepared=prepared;
        internal PreparedMonsterDamagePacket Packet=packet;
        internal int Before,After,Loss,EntryHits;
        internal Point3D Point;internal Map Map;
        internal IDisposable KillRecovery;
        internal PlayerItemProcWorld.Ticket ItemProcs;
    }
    private sealed class Contact(PlayerMobile source,BaseCreature target,MonsterReturnMetadata metadata,Scope scope):MonsterContactServices
    {
        internal override PreparedMonsterDamagePacket CapturedPacket()=>scope.Packet;
        internal override bool InMeleeRange(int bonus)
        {
            if(bonus!=1){throw new NotSupportedException("Unmapped player weapon contact range bonus");}
            if(scope.Prepared.RangeCheck!=null){return scope.Prepared.RangeCheck(bonus);}
            return source.Map==target.Map&&source.InRange(target,scope.Prepared.MeleeRange??CombatTiming.Range(source,CombatAbility.BasicAttack))&&source.InLOS(target);
        }
        internal override bool AttackerInDeathMode()=>source is not{Alive:true,Deleted:false}||(scope.Prepared.ActiveSourceMode is{} active?!active():SkillHealth.Balance256(source)<=0);
        internal override bool Hostile()=>ClassCombat.IsHostile(source,target);
        internal override bool DefenderHasRoom()=>target.Map is{} map&&map!=Map.Internal;
        internal override bool DefenderInTown()=>SpellHelper.IsTown(target.Location,source);
        internal override bool AttackerAllowedInTown()=>false;
        internal override bool DefenderKillable()=>(metadata?.Killable??true)&&target.CanBeDamaged();
        internal override bool DefenderDead()=>target is not{Alive:true,Deleted:false}||SkillHealth.Balance256(target)<=0;
    }
    private sealed class Damage(PlayerMobile source,BaseCreature target,MonsterReturnMetadata metadata,NpcCombatEvents events,Scope scope,DateTime at,bool missile=false):PreparedMonsterDamageServices
    {
        private bool Freed;
        internal override void StoreLastAttacker()=>source.DoHarmful(target,true);
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p)
        {
            NamedCombat.Crushing(source,target,missile,scope.Prepared.Effects,scope.Prepared.CrushingDivisor);scope.ItemProcs?.Dispatch(missile?"domissiledamage":"domeleedamage",p);
            // Missile item status callbacks belong to this successful event,
            // including zero-life-damage/fully absorbed and lethal hits.
            if(missile){NamedCombat.AfterStrike(source,target,true,scope.Prepared.Effects,setupApplied:true);}
            return p;
        }
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)=>events.Dispatch(missile?"damagedbymissile":"damagedinmelee",p);
        internal override PreparedMonsterDamagePacket MitigateMissile(PreparedMonsterDamagePacket p)=>missile?p:throw new InvalidOperationException("Melee has no missile mitigation stage"); // Caller has captured typed mitigation.
        internal override void BeforeHealth(PreparedMonsterDamagePacket p)
        {
            if((p.ResultFlags&32)==0&&(!missile||(p.HitFlags&160)==32)){WeaponLandedEvents.Notify(source,target,missile);}
            scope.Before=SkillHealth.Balance256(target);WeaponLeech.Apply(source,target,p.Physical256,scope.Prepared.Drain);
        }
        internal override void BeforeHealthCommit(Mobile attacker,BaseCreature recipient,int after256)
        {PreparedMonsterNativeDamage.Notify(attacker,recipient,after256);scope.After=after256;scope.Loss=Math.Max(0,scope.EntryHits-(after256+255)/256);}
        internal override void DamageReaction(int nativeLifeDelta)=>scope.EntryHits+=nativeLifeDelta;
        internal override void ApplyCold(int frames)
        {
            if(frames>0){ColdStates.Commit(source,target,frames,true,ColdStates.Effect(target),NecromancerEffects.DifficultyDivisor(target),TimedEffects.TickAt(at));}
        }
        internal override bool SupportsFreeze=>true;
        internal override void ApplyFreeze(int frames)
        {if(frames>0){FreezeStates.Commit(source,target,frames,true,NecromancerEffects.DifficultyDivisor(target),TimedEffects.TickAt(at));}}
        internal override void ApplyPoison()
        {
            scope.Prepared.Impact.AllocatedPoison?.Commit(source,target,at);
            scope.Prepared.CapturedPoison?.Invoke(source,target,at);
        }
        internal override void SurvivedDamage()
        {
            // Preserve existing supported conditional item consumers. Their
            // internal registration/RNG order still needs individual acceptance.
            if(!missile){NamedCombat.AfterStrike(source,target,missile,scope.Prepared.Effects,smite:scope.Prepared.Smite,skipKnockback:scope.Prepared.SkillKnockback,setupApplied:true);}
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
        internal override int BaseHitClass()=>scope.Prepared.BaseHitClass;
        internal override void AfterHitPresentation(PreparedMonsterDamagePacket packet)
        {
            scope.Prepared.WeaponWear?.Invoke();
            if(!scope.Prepared.SuppressWear&&CombatTiming.Equipped(source) is{} weapon&&NamedAmmunition.Maximum(weapon)==0){weapon.WearOnHit(source,target);}
            if(scope.Loss>0&&scope.Map!=null&&scope.Map!=Map.Internal&&ClassGear.Bonus(source,"bloody")>0)
            {new Blood().MoveToWorld(scope.Point,scope.Map);}
        }
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)
        {
            // Armor reactions retain their compatibility attempt hook. Life
            // Tap/Iron Maiden already ran in the fractional damage-event phase.
            NecromancerEffects.WeaponHit(source,target,Math.Min(scope.Loss,p.Physical256>>8),false,lifeTap:false,ironMaiden:false);
            scope.ItemProcs?.Dispatch("domeleeattack",p);
            return p;
        }
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>events.Dispatch("attackedinmelee",p);
        internal override bool AttackerInActiveMode()=>source is{Alive:true,Deleted:false}&&(scope.Prepared.ActiveSourceMode?.Invoke()??SkillHealth.Balance256(source)>0);
        internal override void BeforeThorns(PreparedMonsterDamagePacket p){}
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death)
        {
            if(metadata!=null)
            {PreparedMonsterReturn.Dispatch(ref p,new NativeMonsterReturn(source,source,target,metadata,death,()=>EliteCombat.OnStruck(target,source,scope.Loss),
                knockback:scope.Prepared.Smite||scope.Prepared.SkillKnockback?SourceKnockbacks.BeginPrepared:null),true);return;}
            // Unclassified UO actors retain their existing native recovery.
            // This adapter supplies no invented D2 class/base/mode identity.
            // Death still follows the common HP,kill,attempt and return phases.
            if((p.ResultFlags&2)!=0){death();}
            else if((p.ResultFlags&1)!=0&&p.Total256>0)
            {if((p.ResultFlags&16384)!=0){EliteCombat.OnStruck(target,source,scope.Loss);}else{CreatureHitRecovery.Apply(target,source,scope.Loss,true);}}
        }
        internal override void FreePacket()
        {if(Freed){throw new InvalidOperationException("Player contact packet freed twice");}Freed=true;scope.Packet=null;NativeMonsterReturn.Session(target).Packets++;}
    }
    private static PreparedMonsterDamagePacket Packet(PlayerWeaponContactPrepared prepared)
    {
        var impact=prepared.Impact;var parts=impact.Parts;
        return new PreparedMonsterDamagePacket(NamedElements.To256(parts.Physical),impact.LifeDamage256??NamedElements.To256(parts.Total),impact.SourceStunFrames,
            prepared.ColdFrames,prepared.Flags,HitClass:(prepared.Flags&1)!=0?prepared.HitClass:0,
            Magic256:NamedElements.To256(parts.Magic),Fire256:NamedElements.To256(parts.Fire),Lightning256:NamedElements.To256(parts.Lightning),
            Cold256:NamedElements.To256(parts.Cold),Poison256:impact.AllocatedPoison?.Rate256??0,AbsorbedLife256:impact.Absorbed256,HitFlags:prepared.HitFlags,FreezeFrames:prepared.FreezeFrames);
    }
    internal static PreparedMonsterDamageResult ReleaseMissile(PlayerMobile source,BaseCreature target,PlayerWeaponContactPrepared prepared,DateTime at,bool allowNative=false)
    {
        Require(source,target,allowNative,weaponSetup:false);var metadata=Metadata(target,allowNative);NativeMonsterReturn.Session(target);
        if(source.Map!=target.Map||!ClassCombat.IsHostile(source,target)||SpellHelper.IsTown(target.Location,source)||metadata is{Killable:false})
        {throw new NotSupportedException("Player missile lost its qualified hostile world recipient");}
        var packet=Packet(prepared);
        var scope=new Scope(prepared,packet){Point=target.Location,Map=target.Map,EntryHits=target.Hits,
            ItemProcs=(packet.HitFlags&160)==32?PlayerItemProcWorld.CaptureMissile(source,target,at,prepared.UninterruptibleItemCasts):null};
        var recipient=new PreparedMonsterRecipient(PreparedMonsterStun.IsUnique(target),()=>CreatureProfiles.For(target).Traits.Boss??false,()=>metadata?.Velocity??0,()=>target is BaseHire);
        try{return PreparedMonsterDamage.ReleaseMissile(source,target,packet,recipient,prepared.StunRoll??Utility.Random,new Damage(source,target,metadata,IronGolemContact.Events(target),scope,at,true),at);}
        finally{scope.KillRecovery?.Dispose();}
    }
    internal static MonsterContactResult Release(PlayerMobile source,BaseCreature target,PlayerWeaponContactPrepared prepared,DateTime at,bool allowNative=false)
    {
        Require(source,target,allowNative,!prepared.Smite);var metadata=Metadata(target,allowNative);NativeMonsterReturn.Session(target);
        var packet=Packet(prepared);
        var scope=new Scope(prepared,packet){Point=target.Location,Map=target.Map,EntryHits=target.Hits,ItemProcs=PlayerItemProcWorld.Capture(source,target,at,prepared.UninterruptibleItemCasts)};
        bool stunRules=CombatStuns.Rules(target,out bool stunBoss,out int stunVelocity);
        bool controlStun=prepared.Smite||prepared.ControlStun;
        var recipient=new PreparedMonsterRecipient(PreparedMonsterStun.IsUnique(target)||prepared.Smite&&target is EncounterCreature{Grade:EncounterGrade.Champion},
            ()=>controlStun?stunBoss:CreatureProfiles.For(target).Traits.Boss??false,()=>controlStun?(stunRules?stunVelocity:0):metadata?.Velocity??0,()=>target is BaseHire);
        using var attempted=prepared.AttemptScoped?null:PaladinThorns.Attempt(source,target,false);
        try
        {
            return PreparedMonsterContact.Release(source,target,new Contact(source,target,metadata,scope),recipient,prepared.StunRoll??Utility.Random,
                new Damage(source,target,metadata,IronGolemContact.Events(target),scope,at),at);
        }
        finally{scope.KillRecovery?.Dispose();}
    }
}
