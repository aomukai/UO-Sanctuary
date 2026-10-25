// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Sanctuary;
internal sealed record CorpseBaseLifeResult(bool Success,int Minimum,int Maximum,int Base256);
internal sealed record CorpseBaseMonster(string Source,bool NoRatio,int[] Minimum,int[] Maximum);
internal sealed record CorpseBaseLevel(int Level,int[] Classic,int[] Expansion);
// Current table lookup is immutable after loading. Unknown/legacy corpse
// sources stay unsupported; no native HitsMax fallback is permitted.
internal sealed class CorpseBaseCatalogue
{
    private readonly Dictionary<string,int[]> _monsters=new(StringComparer.OrdinalIgnoreCase);
    private readonly int[][] _levels;
    internal CorpseBaseCatalogue(CorpseBaseMonster[] monsters,CorpseBaseLevel[] levels)
    {
        if(monsters is not{Length:521} || levels is not{Length:111}){throw new InvalidDataException("Incomplete corpse base catalogue");}
        _levels=new int[levels.Length][];
        foreach(var row in levels)
        {
            if(row==null || row.Level<0 || row.Level>=_levels.Length || _levels[row.Level]!=null || !Cells(row.Classic) || !Cells(row.Expansion))
            {throw new InvalidDataException("Invalid corpse base level row");}
            _levels[row.Level]=row.Classic.Concat(row.Expansion).ToArray();
        }
        foreach(var row in monsters)
        {
            if(row==null || string.IsNullOrWhiteSpace(row.Source) || !Cells(row.Minimum) || !Cells(row.Maximum))
            {throw new InvalidDataException("Invalid corpse base monster row");}
            // Retain authored minimum>maximum cells; the source calculator
            // averages them, including minionspawner1's Normal row.
            int[] values={row.NoRatio?1:0,row.Minimum[0],row.Maximum[0],row.Minimum[1],row.Maximum[1],row.Minimum[2],row.Maximum[2]};
            if(!_monsters.TryAdd(row.Source,values)){throw new InvalidDataException("Duplicate corpse base source");}
        }
    }
    private static bool Cells(int[] values)=>values is{Length:3} && values.All(x=>x>=0);
    internal bool TryCalculate(string source,bool expansion,int difficulty,int level,out CorpseBaseLifeResult result)
    {
        result=new(false,0,0,0);
        if(string.IsNullOrEmpty(source) || !_monsters.TryGetValue(source,out var monster)){return false;}
        result=CorpseBaseLife.Calculate(monster,_levels,expansion,difficulty,level);
        return result.Success;
    }
}
// Complete flags1 calculator lane shared by the validated packaged lookup.
// Actual corpse profile/level/difficulty capture remains a native service.
internal static class CorpseBaseLife
{
    internal static CorpseBaseLifeResult Calculate(int[] monster,int[][] levelHp,bool expansion,int difficulty,int level)
    {
        ArgumentNullException.ThrowIfNull(monster);ArgumentNullException.ThrowIfNull(levelHp);
        if(monster.Length!=7||levelHp.Length==0){throw new ArgumentException("Invalid corpse-life table context");}
        if(level>levelHp.Length-1){level=levelHp.Length-1;}
        if(level<0||level>=levelHp.Length){return new(false,0,0,0);}
        difficulty=Math.Clamp(difficulty,0,2);
        int minimum=monster[1+2*difficulty],maximum=monster[2+2*difficulty];
        if(monster[0]==0)
        {
            int scale=levelHp[level][difficulty+(expansion?3:0)];
            minimum=SourcePercentage.Calculate(scale,minimum,100);
            maximum=SourcePercentage.Calculate(scale,maximum,100);
        }
        return new(true,minimum,maximum,checked((int)(((long)minimum+maximum)*128)));
    }
}
