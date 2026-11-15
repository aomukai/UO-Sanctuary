// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record NativeCorpseSource(string RuntimeType,string Source,int Level,int MinimumLife,int MaximumLife,bool? Soft,string ReviveIssue,string Basis);

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
        }
        Types.Clear();Sources.Clear();foreach(var pair in types){Types.Add(pair.Key,pair.Value);}foreach(var pair in sources){Sources.Add(pair.Key,pair.Value);}
    }
    internal static NativeCorpseSource For(BaseCreature actor)=>actor==null?null:Types.GetValueOrDefault(actor.GetType().FullName);
    internal static NativeCorpseSource ForSource(string source)=>source==null?null:Sources.GetValueOrDefault(source);
    internal static bool TryCalculate(string source,MonsterDifficulty difficulty,int level,out CorpseBaseLifeResult result)
    {
        var row=ForSource(source);result=null;
        if(row==null||!Enum.IsDefined(difficulty)||level!=row.Level){return false;}
        result=new(true,row.MinimumLife,row.MaximumLife,checked((row.MinimumLife+row.MaximumLife)*128));return true;
    }
}
