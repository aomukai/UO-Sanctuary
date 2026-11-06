// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// Shared post-defense elemental spell receiver. Selection, avoidance,
// intrinsic critical and projectile RNG belong to the producer.
internal static class NpcElementSpellHit
{
    internal static MonsterReturnMetadata Require(BaseCreature emitter,PlayerMobile owner,BaseCreature target,DateTime at,string kind)
    {
        if(emitter==null||target is not{Alive:true,Deleted:false}||at.Kind!=DateTimeKind.Utc||emitter.Map!=target.Map||!target.CanBeDamaged())
        {throw new NotSupportedException("Unqualified NPC elemental spell recipient");}
        PreparedMonsterNativeDamage.RequireIncoming(owner,target,emitter);
        if(MonsterUninterruptibles.Active(target)||MonsterUninterruptibles.Delayed(target))
        {throw new NotSupportedException(kind+" uninterruptible/death-delay recipient needs its actor adapter");}
        var metadata=NativeMonsterReturn.Metadata(target);
        if(!metadata.Killable||metadata.Base==78){throw new NotSupportedException(kind+" recipient killability/sand-leaper completion is unsupported");}
        return metadata;
    }
    internal static PreparedMonsterDamageResult Deliver(BaseCreature emitter,PlayerMobile owner,BaseCreature target,string element,
        int raw256,int coldFrames,DateTime at,string kind,uint hitFlags=0,int hitClass=0,Func<string,int?> pierce=null)
    {
        if(element is not("fire"or"cold"or"ltng")||raw256<0||coldFrames<0||element!="cold"&&coldFrames!=0||hitFlags is not(0 or 2)||hitClass is not(0 or 16))
        {throw new NotSupportedException("Unqualified NPC elemental spell packet");}
        var metadata=Require(emitter,owner,target,at,kind);
        PreparedMonsterDamagePacket Mitigate(PreparedMonsterDamagePacket packet)
        {
            var parts=SkillCombat.MitigatePrepared(emitter,target,new(0,packet.Fire256/256.0,packet.Cold256/256.0,packet.Lightning256/256.0,0),out int absorb,pierceOverride:pierce);
            int frames=packet.ColdFrames;
            if(element=="cold"&&frames>0){frames=ColdStates.LengthFrames(frames,NamedElements.Resistance(emitter,target,"cold",target.ColdResistance,pierce?.Invoke("cold")),ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target));}
            return packet with{Total256=NamedElements.To256(parts.Total),Fire256=NamedElements.To256(parts.Fire),Cold256=NamedElements.To256(parts.Cold),Lightning256=NamedElements.To256(parts.Lightning),AbsorbedLife256=absorb,ColdFrames=frames};
        }
        NativeMonsterReturn.Session(target);
        var services=new InstantSpellNpcDamage(owner,target,metadata,null,at,kind,()=>TelekinesisCombat.DefenderRoll(),cold:element=="cold",emitter:emitter,npcMitigation:Mitigate);
        return PreparedMonsterDamage.ReleaseMissile(emitter,target,new(0,raw256,0,coldFrames,5,HitClass:hitClass,
            Fire256:element=="fire"?raw256:0,Cold256:element=="cold"?raw256:0,Lightning256:element=="ltng"?raw256:0,HitFlags:hitFlags),
            new(NamedCombat.Elite(target),()=>false,()=>metadata.Velocity,()=>false),_=>throw new InvalidOperationException(kind+" has no stun"),services,at);
    }
}
