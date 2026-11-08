// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record RingOfFirePacket(uint Owner,int Rank,int Minimum256,int Maximum256,int Fire256,
    FirestormModifiers Modifiers,ulong SeedBefore,ulong SeedAfter);
internal sealed record RingOfFireBirth(int Rank,int Minimum256,int Maximum256,FirestormModifiers Modifiers);

// Prepared ordinary recipient only. Contact selection, explosion enumeration
// and delay identity remain outside this receiver; it publishes no missile.
internal static class RingOfFireDamage
{
    internal static RingOfFirePacket Capture(PlayerMobile owner,ref ulong seed)
        =>CapturePrepared(owner,SkillMechanics.Values(owner,CombatAbility.RingOfFire),DruidFirestorm.Modifiers(owner),ref seed);
    internal static RingOfFirePacket CapturePrepared(PlayerMobile owner,SkillValues values,FirestormModifiers modifiers,ref ulong seed)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Warlock
            ||values.Ability!=CombatAbility.RingOfFire||values.Rank is <1 or >255||modifiers==null)
        {throw new NotSupportedException("Unqualified Ring fire packet");}
        // Reuse the existing spell endpoint/roll contract. Slow Missiles does
        // not scale this CanSlow0 missile. Enhancement is captured once.
        return RollBorn(owner,Birth(values,modifiers),ref seed);
    }
    internal static RingOfFireBirth Birth(SkillValues values,FirestormModifiers modifiers)
    {
        if(values?.Ability!=CombatAbility.RingOfFire||values.Rank is <1 or >255||modifiers==null){throw new NotSupportedException("Invalid Ring birth capture");}
        var ends=DruidFirestorm.Endpoints(values,modifiers.FireMastery,SlowMissilePayload.Normal);
        return new(values.Rank,ends.Min,ends.Max,modifiers);
    }
    internal static RingOfFirePacket RollBorn(PlayerMobile owner,RingOfFireBirth birth,ref ulong seed)
    {
        if(owner==null||birth is not{Rank:>=1 and <=255,Minimum256:>=0,Modifiers:not null}||birth.Maximum256<birth.Minimum256)
        {throw new NotSupportedException("Invalid Ring birth packet");}
        int raw=SorceressSpellMissiles.Roll(birth.Minimum256,birth.Maximum256,seed,out var after);
        var packet=new RingOfFirePacket(owner.Serial.Value,birth.Rank,birth.Minimum256,birth.Maximum256,raw,birth.Modifiers,seed,after);seed=after;return packet;
    }
    internal static void Validate(RingOfFirePacket packet)
    {
        if(packet==null||!((Server.Serial)packet.Owner).IsMobile||packet.Rank is <1 or >255||packet.Minimum256<0
            ||packet.Maximum256<packet.Minimum256||packet.Modifiers==null)
        {throw new InvalidDataException("Invalid Ring fire packet");}
        int raw=SorceressSpellMissiles.Roll(packet.Minimum256,packet.Maximum256,packet.SeedBefore,out var after);
        if(raw!=packet.Fire256||after!=packet.SeedAfter){throw new InvalidDataException("Ring fire roll provenance changed");}
    }
    internal static int AtAge(RingOfFirePacket packet,int elapsedUpdates)
    {
        Validate(packet);
        Span<int> raw=stackalloc int[24];raw.Clear();raw[8]=packet.Fire256; // Qualified fire field at packet offset0x20.
        var values=new SkillValues(SkillMechanics.For(CombatAbility.RingOfFire),_=>packet.Rank,_=>0,1);
        RingOfFireMotion.ScaleAtSuppliedFrame(values,elapsedUpdates,raw);
        return raw[8];
    }
    internal static int DeliverPrepared(PlayerMobile owner,BaseCreature target,RingOfFirePacket packet,int elapsedUpdates,DateTime at)
    {
        Validate(packet);
        if(owner is not{Alive:true,Deleted:false}||owner.Serial.Value!=packet.Owner||target is not{Alive:true,Deleted:false}
            ||owner.Map==null||owner.Map!=target.Map||!ClassCombat.IsHostile(owner,target)||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("Detached Ring fire recipient");}
        TaintedFireBallMissiles.RequireOrdinaryRecipient(owner,target);
        // Approved elapsed-update growth precedes the existing spell potion,
        // mitigation and fractional-life pipeline. This is one supplied hit,
        // not a claim about current hit1's area/direct-hit dispatch.
        return SorceressSpellMissiles.ImpactPrepared(owner,target,"ringoffire","fire",AtAge(packet,elapsedUpdates),packet.Modifiers,at,missileReaction:true);
    }
}
