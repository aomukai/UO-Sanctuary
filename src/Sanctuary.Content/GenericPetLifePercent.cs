// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class GenericPetLifePercent
{
    // Current27acb0/24fa20 read stats6/7 and shift EACH operand first.
    // Signed FP8 inputs leave at most24bits after shifting;times100 fits int.
    internal static int Calculate(int current256,int maximum256)
    {
        int maximum=maximum256>>8;
        return maximum==0?0:(current256>>8)*100/maximum;
    }
    private static Mobile Unit(int id)
    {
        if(id==0||World.FindMobile((Serial)(uint)id) is not{Deleted:false} mobile){return null;}
        if(mobile is PlayerMobile player&&ClassCreation.Profiles.TryGet(player.Serial,out _)
            ||mobile is SkeletonGuardian body&&WarlockDemonPlacement.Prepared(body)){return mobile;}
        throw new NotSupportedException("GenericPet life percentage needs a class player or original prepared demon");
    }
    internal static void Require(int id)=>_=Unit(id);
    internal static int For(int id)=>Unit(id) is{} mobile?Calculate(SkillHealth.Balance256(mobile),SkillHealth.Maximum256(mobile)):0;
}
