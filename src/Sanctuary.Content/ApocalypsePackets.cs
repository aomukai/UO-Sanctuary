// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ApocalypsePacket(uint Owner,int Rank,int Minimum256,int Maximum256,int Fire256,ulong SeedBefore,ulong SeedAfter);

// The current outer do163 rolls once before enumeration;392350 copies its
// packet per target. The approved table fallback supplies endpoints through
// shared SkillValues;the protected current minimum getter remains unverified.
// Capture belongs at explosion,not cast.
internal static class ApocalypsePackets
{
    internal static int Endpoint(SkillValues values,bool maximum,int mastery)
    {
        int value=values.Damage256(maximum,sourcePercentage:true);
        // Current maximum getter adds mastery after synergy,using the same
        // percentage primitive. Minimum uses the approved table fallback.
        return (int)Math.Clamp((long)value+SourcePercentage.Calculate(value,mastery,100),0,int.MaxValue);
    }
    internal static ApocalypsePacket Capture(PlayerMobile owner,ref ulong seed)
    {
        var values=SkillMechanics.Values(owner,CombatAbility.Apocalypse);
        return CapturePrepared(owner,values.Rank,
            Endpoint(values,false,NamedElements.MasteryPercent(owner,"fire")),
            Endpoint(values,true,NamedElements.MasteryPercent(owner,"fire")),ref seed);
    }
    internal static ApocalypsePacket CapturePrepared(PlayerMobile owner,int rank,int enhancedMinimum256,int enhancedMaximum256,ref ulong seed)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Warlock
            ||rank is <1 or >255||enhancedMinimum256<0||enhancedMaximum256<0){throw new NotSupportedException("Unqualified Apocalypse supplied fire range");}
        var row=SkillMechanics.For(CombatAbility.Apocalypse);
        if(row.SourceId!=401||row.Text("EType")!="fire"||row.Number("SrcDam")!=0||row.Number("MinDam")!=0||row.Number("MaxDam")!=0)
        {throw new NotSupportedException("Apocalypse physical/element binding changed");}
        ulong before=seed;var damage=SourceSpellPacketRoll.PhysicalFire(ref seed,0,0,enhancedMinimum256,enhancedMaximum256);
        return new(owner.Serial.Value,rank,enhancedMinimum256,enhancedMaximum256,damage.Fire256,before,seed);
    }
    internal static void Validate(ApocalypsePacket packet)
    {
        if(packet==null||!((Serial)packet.Owner).IsMobile||packet.Rank is <1 or >255||packet.Minimum256<0||packet.Maximum256<0)
        {throw new InvalidDataException("Invalid Apocalypse supplied packet");}
        ulong seed=packet.SeedBefore;var value=SourceSpellPacketRoll.PhysicalFire(ref seed,0,0,packet.Minimum256,packet.Maximum256);
        if(value.Fire256!=packet.Fire256||seed!=packet.SeedAfter){throw new InvalidDataException("Apocalypse packet roll/provenance changed");}
    }
    internal static ApocalypseImpactResult ReleasePrepared(PlayerMobile owner,BaseCreature target,ApocalypsePacket packet,bool allocation=true,int? pierceOverride=null)
    {
        Validate(packet);if(owner==null||owner.Serial.Value!=packet.Owner){return default;}
        return ApocalypseImpact.ReleasePrepared(owner,target,packet.Fire256,packet.Rank,allocation,pierceOverride);
    }
}
