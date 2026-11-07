// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal sealed record ValkyrieInitializerRule(int Monster,int Alignment,int[] Level,bool NoRatio,int[] ComponentChoiceCounts,int TotalPieces,
    int MonsterProperty,int MonsterPropertyCount,string[] InitialSkills,bool OnInitEquipment,int[] MinimumLife,int[] MaximumLife);
internal sealed record ValkyrieInitialization(ulong WorldBefore,ulong WorldAfter,ulong UnitSeed,ulong AfterInitialization,
    MonsterDifficulty Difficulty,int SpawnLife256,int RegenerationBase)
{public string Profile{get;init;}="source-valkyrie-initialization-v1";}

// Source stats-initializer boundary with current table inputs. Sanctuary has
// no D2 region component-variant cache;this two-piece summon uses its actual
// zero-choice component row. Outer AI/animation callbacks remain separate.
internal static class ValkyrieInitializer
{
    private static readonly Lazy<ValkyrieInitializerRule> Rule=new(()=>
    {
        var r=BalanceCatalog.Read<ValkyrieInitializerRule>("valkyrie-initializer").Single();
        if(r is not{Monster:357,Alignment:1,NoRatio:true,TotalPieces:2,MonsterProperty:65535,MonsterPropertyCount:13,OnInitEquipment:false}
            ||r.ComponentChoiceCounts is not{Length:16}||r.ComponentChoiceCounts.Any(n=>n!=0)||r.InitialSkills is not{Length:0}
            ||r.Level is not{Length:3}||r.Level.Any(n=>n!=0)||r.MinimumLife is not{Length:3}||r.MinimumLife.Any(n=>n!=400)
            ||r.MaximumLife is not{Length:3}||r.MaximumLife.Any(n=>n!=480))
        {throw new InvalidDataException("Unsupported current Valkyrie initialization inputs");}
        for(int d=0;d<3;d++)
        {
            if(NecromancerSummons.Source("valkyrie","minHP",(MonsterDifficulty)d)!=r.MinimumLife[d]
                ||NecromancerSummons.Source("valkyrie","maxHP",(MonsterDifficulty)d)!=r.MaximumLife[d]
                ||NecromancerSummons.Source("valkyrie","Align")!=1||NecromancerSummons.Source("valkyrie","noRatio")!=1)
            {throw new InvalidDataException("Valkyrie initializer and existing source catalogue differ");}
        }
        return r;
    });
    internal static (ulong After,int Life256,int RegenerationBase) Roll(ulong seed,MonsterDifficulty difficulty)
    {
        if(!Enum.IsDefined(difficulty)){throw new ArgumentException("Unknown Valkyrie creation difficulty");}
        var r=Rule.Value;int d=(int)difficulty;
        // Friendly alignment avoids player-count/Classic scaling. All current
        // component bounds are zero,so those twelve queries take no draws.
        int life=checked((r.MinimumLife[d]+(int)SourceRandom.Limited(ref seed,r.MaximumLife[d]-r.MinimumLife[d]+1))*256);
        return(seed,life,checked(life*NecromancerSummons.Source("valkyrie","DamageRegen"))>>12);
    }
    private static ValkyrieInitialization Expected(ulong before,MonsterDifficulty difficulty)
    {
        ulong world=before;uint low=SourceRandom.Roll(ref world);ulong unit=SourceRandom.InitLow(unchecked((int)low));
        var rolled=Roll(unit,difficulty);return new(before,world,unit,rolled.After,difficulty,rolled.Life256,rolled.RegenerationBase);
    }
    internal static ValkyrieInitialization Allocate(MonsterDifficulty difficulty)
    {
        _=Rule.Value;var game=NpcWorldAllocation.RequireStream();ulong before=game.Value;
        ulong unit=NpcWorldAllocation.UnitSeed();var result=Expected(before,difficulty);
        if(unit!=result.UnitSeed||game.Value!=result.WorldAfter){throw new InvalidOperationException("Valkyrie unit allocation lost its shared world context");}
        return result;
    }
    internal static void Validate(ValkyrieInitialization s,ValkyrieBirth birth,ValkyrieEquipmentState equipment)
    {
        if(s==null||s.Profile!="source-valkyrie-initialization-v1"||s.WorldBefore==0||s.WorldAfter==0||!Enum.IsDefined(s.Difficulty)
            ||s!=Expected(s.WorldBefore,s.Difficulty)||s.SpawnLife256!=birth.SpawnLife256
            ||equipment!=null&&(equipment.PetBefore!=s.AfterInitialization||equipment.GameBefore!=s.WorldAfter))
        {throw new InvalidDataException("Invalid Valkyrie unit/initializer/equipment continuation");}
    }
}
