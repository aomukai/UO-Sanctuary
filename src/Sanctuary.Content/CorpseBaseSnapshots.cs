// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;

namespace Sanctuary;

// Original monster identity is distinct from the native Corpse item serial.
// Life is the pinned source base, before spawned-life/player-count/elite rolls.
public sealed record CorpseBaseSnapshot(uint OriginalMonsterSerial,int Level,MonsterDifficulty Difficulty,int Base256,ulong? Seed=null);

internal static class CorpseBaseSnapshots
{
    private static CorpseBaseCatalogue _catalogue;
    // Explicit native allocation adapter, shared with the established missile
    // seed convention. This is not the D2R executable's monster spawn stream.
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
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
        return new(creature.Serial.Value,level,difficulty,result.Base256,SourceRandom.InitLow(SeedLow()));
    }
}
