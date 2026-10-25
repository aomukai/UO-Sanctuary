// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;
internal readonly record struct PhoenixElementPacket(int Minimum256,int Maximum256,int ColdFrames);
internal static class MartialPhoenixPackets
{
    internal static PhoenixElementPacket Capture(string key,SkillValues values,int mastery)
    {
        if(values.Ability!=CombatAbility.PhoenixStrike || values.Rank is <1 or >255 || key is not("royalstrikemeteor" or "royalstrikechainlightning" or "royalstrikechaosice" or "royalstrikemeteorfire"))
        {throw new NotSupportedException("Unmapped Phoenix packet");}
        int synergy=NecromancerSummons.Formula(key,"EDmgSymPerCalc",values);
        int Enhance(int damage)=>checked((int)((long)damage*Math.Max(0,100+mastery)/100));
        int frames=key=="royalstrikechaosice"?(int)SkillValues.Tier(NecromancerSummons.Source(key,"ELen"),values.Rank,i=>NecromancerSummons.Source(key,"ELevLen"+Math.Min(i,3))):0;
        return new(Enhance(FireGround.MissileDamage256(key,values.Rank,synergy,false)),Enhance(FireGround.MissileDamage256(key,values.Rank,synergy,true)),frames);
    }
    internal static int MeteorFieldFrames(int rank)
    {
        if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}
        var row=SkillMechanics.For(CombatAbility.PhoenixStrike);return checked(row.Parameter(3)+(rank-1)*row.Parameter(4));
    }
    internal static Point2D[] MeteorOffsets()=>[new(2,-2),new(-2,-2),new(0,2),new(0,5),new(-3,3),new(0,3),new(3,3),new(-1,2),new(1,1),new(-1,-1),new(2,-1),new(-4,-2),new(-3,-2),new(-1,-3),new(0,-4),new(1,-3),new(3,-3),new(4,-2)];
}
