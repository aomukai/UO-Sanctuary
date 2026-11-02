// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Shared ordered native notification/typed HP/return services for direct spells.
// Callers pin flags/element/eligibility and own any suppressed event scope.
internal sealed class InstantSpellNpcDamage(PlayerMobile owner,BaseCreature target,MonsterReturnMetadata metadata,FirestormModifiers modifiers,DateTime at,
string kind,Func<uint> defenderRoll,Func<Mobile,BaseCreature,bool> knockback=null,bool cold=false,int damageRate=0,
BaseCreature emitter=null,Func<PreparedMonsterDamagePacket,PreparedMonsterDamagePacket> npcMitigation=null):PreparedMonsterDamageServices
{
    private int Loss,EntryHits;private bool Freed;
    private Mobile Attacker=>emitter??(Mobile)owner;
    internal override void StoreLastAttacker(){Attacker.DoHarmful(target,true);EntryHits=target.Hits;}
    internal override PreparedMonsterDamagePacket MitigateMissile(PreparedMonsterDamagePacket p)
    {
        if(emitter!=null){return (npcMitigation??throw new NotSupportedException("NPC spell mitigation requires its captured actor context"))(p);}
        var current=modifiers with{Potion=CombatPotions.BonusAt(owner,"magical-penetration",at)};
        var parts=SkillCombat.MitigatePrepared(owner,target,DruidMoltenBoulder.Potion(current,new(0,p.Fire256/256.0,p.Cold256/256.0,p.Lightning256/256.0,p.Poison256/256.0,Magic:p.Magic256/256.0)),out int absorb,damageRate);
        int frames=p.ColdFrames;
        if(cold&&frames>0){frames=ColdStates.LengthFrames(frames,NamedElements.Resistance(owner,target,"cold",target.ColdResistance),ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target));}
        return p with{ColdFrames=frames,Total256=NamedElements.To256(parts.Total),Fire256=NamedElements.To256(parts.Fire),Cold256=NamedElements.To256(parts.Cold),
            Lightning256=NamedElements.To256(parts.Lightning),Poison256=NamedElements.To256(parts.Poison),Magic256=NamedElements.To256(parts.Magic),AbsorbedLife256=absorb};
    }
    internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException(kind+" hit2 has no missile damage event");
    internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException(kind+" hit2 has no incoming missile damage event");
    internal override void BeforeHealth(PreparedMonsterDamagePacket p){}
    internal override void BeforeHealthCommit(Mobile attacker,BaseCreature recipient,int after256)
    {PreparedMonsterNativeDamage.Notify(attacker,recipient,after256);Loss=Math.Max(0,EntryHits-(after256+255)/256);}
    internal override void ApplyCold(int frames)
    {
        if(!cold){if(frames!=0){throw new InvalidOperationException(kind+" has no cold duration");}return;}
        if(ColdStates.Commit(owner,target,frames,true,ColdStates.Effect(target),NecromancerEffects.DifficultyDivisor(target),TimedEffects.TickAt(at)))
        {target.FixedEffect(0x374A,10,15,0x480,0);} // Identified shared cold marker.
    }
    // Keep the existing native regeneration clock. Source one-frame
    // regen rescheduling still needs that clock's separate actor adapter.
    internal override void SurvivedDamage(){}
    // Native death/loot/credit callbacks remain the existing host adapter;
    // this does not certify arbitrary source item kill-event subscribers.
    internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p)=>p;
    internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p)=>p;
    internal override void PresentOverlay(int overlay)=>throw new InvalidOperationException(kind+" has an unexpected overlay");
    internal override int BaseHitClass()=>throw new InvalidOperationException(kind+" supplies its own hit class");
    internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException(kind+" is not a melee attempt");
    internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException(kind+" is not an incoming melee attempt");
    internal override bool AttackerInActiveMode()=>throw new InvalidOperationException(kind+" has no melee active-mode check");
    internal override void BeforeThorns(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException(kind+" must not run Thorns");
    internal override void Return(ref PreparedMonsterDamagePacket p,Action death)
        =>PreparedMonsterReturn.Dispatch(ref p,new NativeMonsterReturn(Attacker,owner,target,metadata,death,()=>EliteCombat.OnStruck(target,Attacker,Loss),knockback,defenderRoll),true);
    internal override void FreePacket()
    {if(Freed){throw new InvalidOperationException(kind+" packet freed twice");}Freed=true;NativeMonsterReturn.Session(target).Packets++;}
}
