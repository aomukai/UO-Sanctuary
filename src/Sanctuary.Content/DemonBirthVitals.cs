// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record DemonBirthVitals(int OwnerLevel,int PetLevel,MonsterDifficulty Difficulty,int BaseLife256,int MaximumLife256,
    int BaseAttack,int LevelAttack,int BaseArmor,int LevelArmor);

// Shared noRatio life/level/base projection. Existing DTOs retain their saved
// shape; ordinary offense and summon/AI transactions remain separate.
internal static class DemonBirthNumbers
{
    internal static DemonBirthVitals Create(string source,int rank,int ownerLevel,MonsterDifficulty difficulty,int lifePercent)
    {
        if(source is not("warbighead" or "warputriddefiler")||rank is <1 or >255||ownerLevel is <1 or >99
            ||!Enum.IsDefined(difficulty)||lifePercent<0||NecromancerSummons.Source(source,"noRatio")!=1)
        {throw new NotSupportedException("Unsupported noRatio demon vital context");}
        int level=Math.Min(ownerLevel,rank+3*ownerLevel/4),life=checked(NecromancerSummons.Source(source,"minHP",difficulty)*256);
        if(life<=0||life!=NecromancerSummons.Source(source,"maxHP",difficulty)*256){throw new NotSupportedException("Demon life range needs its source initialization roll");}
        return new(ownerLevel,level,difficulty,life,checked(life+SourcePercentage.Calculate(life,lifePercent,100)),
            NecromancerSummons.Source(source,"A1TH",difficulty),NecromancerSummons.Source("level-"+level,"L-TH",difficulty),
            NecromancerSummons.Source(source,"AC",difficulty),NecromancerSummons.Source("level-"+level,"L-AC",difficulty));
    }
}
