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
    internal static void Load()
    {
        _catalogue=new(BalanceCatalog.Read<CorpseBaseMonster>("corpse-base-monsters"),BalanceCatalog.Read<CorpseBaseLevel>("corpse-base-levels"));
        NativeCorpseSources.Load();
    }
    internal static int Base256(string source,MonsterDifficulty difficulty,int level)
    {
        if(NativeCorpseSources.TryCalculate(source,difficulty,level,out var native)){return native.Base256;}
        if(_catalogue==null||!_catalogue.TryCalculate(source,true,(int)difficulty,level,out var result))
        {throw new NotSupportedException("No pinned corpse base-life bounds for this source/level");}
        return result.Base256;
    }
    internal static CorpseBaseLifeResult Bounds(string source,CorpseBaseSnapshot snapshot)
    {
        if(snapshot!=null&&NativeCorpseSources.TryCalculate(source,snapshot.Difficulty,snapshot.Level,out var native)&&native.Base256==snapshot.Base256){return native;}
        if(snapshot==null||!Enum.IsDefined(snapshot.Difficulty)||snapshot.Level<1||_catalogue==null
            ||!_catalogue.TryCalculate(source,true,(int)snapshot.Difficulty,snapshot.Level,out var result)||result.Base256!=snapshot.Base256)
        {throw new NotSupportedException("Saved corpse base life does not match its pinned bounds");}
        return result;
    }

    internal static CorpseBaseSnapshot Capture(BaseCreature creature,string source)
    {
        int level=CreatureProfiles.Level(creature);
        var difficulty=MonsterVariants.Difficulty(creature);
        if(NativeCorpseSources.ForSource(source)is{} row)
        {
            if(row.RuntimeType!=creature.GetType().FullName||!NativeCorpseSources.TryCalculate(source,difficulty,level,out var native)){return null;}
            return new(creature.Serial.Value,level,difficulty,native.Base256,SourceRandom.InitLow(SeedLow()));
        }
        // Sanctuary uses the pinned D2R expansion HP columns. Unknown source
        // mappings and invalid native levels remain explicitly unsupported.
        if(level<1 || _catalogue==null || !_catalogue.TryCalculate(source,true,(int)difficulty,level,out var result))
        {return null;}
        return new(creature.Serial.Value,level,difficulty,result.Base256,SourceRandom.InitLow(SeedLow()));
    }
}
