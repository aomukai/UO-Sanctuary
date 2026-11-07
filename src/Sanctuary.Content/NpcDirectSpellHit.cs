// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// Direct physical/magic spells share the existing typed HP/credit/return
// receiver. Keep the source event and missile-return stages separate so the
// skill can add its post-damage flags without moving its RNG before HP.
internal static class NpcDirectSpellHit
{
    internal sealed record Pending(BaseCreature Emitter,BaseCreature Target,InstantSpellNpcDamage Services,PreparedMonsterCommitResult Commit);
    internal static Pending Commit(BaseCreature emitter,PlayerMobile owner,BaseCreature target,GearDamage raw,DateTime at,string kind,Func<string,int?> pierce=null,
        int stunFrames=0,bool knockback=false,Func<int,int> limited=null)
    {
        if(raw.Physical<0||raw.Magic<0||raw.Fire!=0||raw.Cold!=0||raw.Lightning!=0||raw.Poison!=0||stunFrames<0||stunFrames>0&&limited==null)
        {throw new NotSupportedException("Direct NPC spell needs its physical/magic packet");}
        var metadata=NpcElementSpellHit.Require(emitter,owner,target,at,kind);
        bool boss=false;int velocity=metadata.Velocity;
        if(stunFrames>0&&!CombatStuns.Rules(target,out boss,out velocity))
        {throw new NotSupportedException(kind+" needs explicit recipient stun metadata");}
        PreparedMonsterDamagePacket Mitigate(PreparedMonsterDamagePacket packet)
        {
            var parts=SkillCombat.MitigatePrepared(emitter,target,new(packet.Physical256/256.0,0,0,0,0,Magic:packet.Magic256/256.0),out int absorb,pierceOverride:pierce);
            return packet with{Physical256=NamedElements.To256(parts.Physical),Magic256=NamedElements.To256(parts.Magic),Total256=NamedElements.To256(parts.Total),AbsorbedLife256=absorb};
        }
        NativeMonsterReturn.Session(target);
        var services=new InstantSpellNpcDamage(owner,target,metadata,null,at,kind,()=>TelekinesisCombat.DefenderRoll(),
            knockback:SourceKnockbacks.BeginPrepared,emitter:emitter,npcMitigation:Mitigate);
        int physical=NamedElements.To256(raw.Physical),magic=NamedElements.To256(raw.Magic);
        // This transport requires the known eventual successful-spell flag;
        // hitflags0 has no melee/missile item callbacks during commitment.
        var commit=PreparedMonsterDamage.CommitQualified(emitter,target,new(physical,checked(physical+magic),stunFrames,0,knockback?9:1,Magic256:magic),
            new(PreparedMonsterStun.IsUnique(target),()=>boss,()=>velocity,()=>target is BaseHire),limited??(_=>throw new InvalidOperationException(kind+" has no stun")),services,at,missile:true);
        return new(emitter,target,services,commit);
    }
    internal static PreparedMonsterDamageResult Finish(Pending pending,int additionalFlags=0)
    {
        if((additionalFlags&~8)!=0){throw new NotSupportedException("Direct spell return has an unqualified result flag");}
        var c=pending.Commit;var result=PreparedMonsterDamage.Finish(pending.Emitter,pending.Target,c.Packet with{ResultFlags=c.Packet.ResultFlags|additionalFlags},pending.Services,false,true);
        return new(result.Packet,c.Before256,c.AfterHealth256,c.StunFrames,result.DeathModeAttempted);
    }
}
