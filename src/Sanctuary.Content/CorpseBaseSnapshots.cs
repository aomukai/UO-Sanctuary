// SPDX-License-Identifier: GPL-3.0-or-later
using Server.Mobiles;

namespace Sanctuary;

// Original monster identity is distinct from the native Corpse item serial.
// Life is the pinned source base, before spawned-life/player-count/elite rolls.
public sealed record CorpseBaseSnapshot(uint OriginalMonsterSerial,int Level,MonsterDifficulty Difficulty,int Base256);

internal static class CorpseBaseSnapshots
{
    private static CorpseBaseCatalogue _catalogue;
    internal static void Load()=>_catalogue=new(
        BalanceCatalog.Read<CorpseBaseMonster>("corpse-base-monsters"),
        BalanceCatalog.Read<CorpseBaseLevel>("corpse-base-levels"));

    internal static CorpseBaseSnapshot Capture(BaseCreature creature,string source)
    {
        int level=CreatureProfiles.Level(creature);
        var difficulty=MonsterVariants.Difficulty(creature);
        // Sanctuary uses the pinned D2R expansion HP columns. Unknown source
        // mappings and invalid native levels remain explicitly unsupported.
        if(level<1 || _catalogue==null || !_catalogue.TryCalculate(source,true,(int)difficulty,level,out var result))
        {return null;}
        return new(creature.Serial.Value,level,difficulty,result.Base256);
    }
}
