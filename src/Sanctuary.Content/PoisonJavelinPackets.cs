// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;

namespace Sanctuary;

// Parent38 includes prepared weapon/item stats. Child39 creates an independent
// source-zero packet at its retained rank with live hard synergy and mastery.
internal static class PoisonJavelinPackets
{
    internal static bool WeaponAllowed(PlayerMobile owner)=>AmazonJavelins.WeaponAllowed(owner);

    internal static MissileSourceStats Prepare(MissileSourceStats owner,SkillValues values,int mastery,bool cloud)
    {
        var packet=cloud?new MissileSourceStats():owner;
        int minimum=AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery);
        int maximum=AmazonMelee.EnhanceEndpoint(values.Damage256(true),mastery);
        return packet with
        {
            PoisonMin=checked(packet.PoisonMin+minimum),PoisonMax=checked(packet.PoisonMax+maximum),
            PoisonFrames=checked(packet.PoisonFrames+values.DurationFrames)
        };
    }
    internal static WeaponMissileDamage Cloud(PlayerMobile owner,int rank)
    {
        if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}
        var values=SkillMechanics.Values(owner,CombatAbility.PoisonJavelin,rank);
        return new(0,0,0,0,0,false,Prepare(new(),values,NamedElements.MasteryPercent(owner,"pois"),true));
    }
}
