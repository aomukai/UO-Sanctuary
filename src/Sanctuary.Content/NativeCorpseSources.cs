// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record NativeCorpseDifficulty(MonsterDifficulty Difficulty,int Level,int MinimumLife,int MaximumLife);
internal sealed record NativeCorpseSource(string RuntimeType,string Source,int Level,int MinimumLife,int MaximumLife,bool? Soft,string ReviveIssue,string Basis,
    NativeCorpseDifficulty[] DifficultyBases=null);

// Explicit Sanctuary corpse classifications, separate from pinned D2 species.
// No unclassified type or subclass silently acquires a reference identity.
internal static class NativeCorpseSources
{
    private static readonly Dictionary<string,NativeCorpseSource> Types=new(StringComparer.Ordinal);
    private static readonly Dictionary<string,NativeCorpseSource> Sources=new(StringComparer.Ordinal);
    internal static IEnumerable<NativeCorpseSource> All=>Sources.Values;
    internal static void Load()
    {
        var types=new Dictionary<string,NativeCorpseSource>(StringComparer.Ordinal);
        var sources=new Dictionary<string,NativeCorpseSource>(StringComparer.Ordinal);
        foreach(var row in BalanceCatalog.Read<NativeCorpseSource>("native-corpse-sources"))
        {
            if(row==null||string.IsNullOrWhiteSpace(row.RuntimeType)||string.IsNullOrWhiteSpace(row.Source)||!row.Source.StartsWith("sanctuary.native.",StringComparison.Ordinal)
                ||row.Level is <1 or >99||row.MinimumLife<1||row.MaximumLife<row.MinimumLife||row.MaximumLife>int.MaxValue/256
                ||string.IsNullOrWhiteSpace(row.ReviveIssue)||string.IsNullOrWhiteSpace(row.Basis)
                ||!types.TryAdd(row.RuntimeType,row)||!sources.TryAdd(row.Source,row))
            {throw new InvalidDataException("Invalid native corpse classification");}
            var seen=new HashSet<MonsterDifficulty>();
            foreach(var variant in row.DifficultyBases??[])
            {
                if(variant==null||!Enum.IsDefined(variant.Difficulty)||variant.Difficulty==MonsterDifficulty.Normal||!seen.Add(variant.Difficulty)
                    ||variant.Level is <1 or >99||variant.MinimumLife<1||variant.MaximumLife<variant.MinimumLife||variant.MaximumLife>int.MaxValue/256)
                {throw new InvalidDataException("Invalid native corpse difficulty classification");}
            }
        }
        Types.Clear();Sources.Clear();foreach(var pair in types){Types.Add(pair.Key,pair.Value);}foreach(var pair in sources){Sources.Add(pair.Key,pair.Value);}
    }
    internal static NativeCorpseSource For(BaseCreature actor)=>actor==null?null:Types.GetValueOrDefault(actor.GetType().FullName);
    internal static NativeCorpseSource ForSource(string source)=>source==null?null:Sources.GetValueOrDefault(source);
    internal static bool TryCalculate(string source,MonsterDifficulty difficulty,int level,out CorpseBaseLifeResult result)
    {
        var row=ForSource(source);result=null;
        if(row==null||!Enum.IsDefined(difficulty)){return false;}
        NativeCorpseDifficulty variant=null;
        if(difficulty==MonsterDifficulty.Normal){variant=new(difficulty,row.Level,row.MinimumLife,row.MaximumLife);}
        else{foreach(var candidate in row.DifficultyBases??[]){if(candidate.Difficulty==difficulty){variant=candidate;break;}}}
        if(variant==null||level!=variant.Level){return false;}
        result=new(true,variant.MinimumLife,variant.MaximumLife,checked((variant.MinimumLife+variant.MaximumLife)*128));return true;
    }
}
