// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal sealed record ShadowInitialSkill(int Id,int Rank,int Mode,int Sequence);
internal sealed record ShadowInitializerRule(string Ability,int Monster,int Alignment,int[] Level,bool NoRatio,bool Inventory,
    int[] ComponentChoiceCounts,int TotalPieces,int MonsterProperty,int MonsterPropertyCount,ShadowInitialSkill[] InitialSkills,
    bool OnInitEquipment,int[] MinimumLife,int[] MaximumLife,int[] Defense,int[] Block,int[][] Resistances,
    int DamageRegeneration,int[] DifficultySkillBonus);
internal sealed record ShadowInitialization(CombatAbility Ability,ulong WorldBefore,ulong WorldAfter,ulong UnitSeed,
    ulong AfterInitialization,MonsterDifficulty Difficulty,int SpawnLife256,int RegenerationBase)
{public string Profile{get;init;}="source-shadow-initialization-v1";}
internal sealed record ShadowInitializedStats(ulong After,int[] Components,SummonStat[] Stats,ShadowInitialSkill[] Skills);

// Current owned table inputs with the separately qualified older complete
// initializer. No region component cache is fabricated: the actual one-choice
// component consumes one draw, then the fixed HP range consumes another.
internal static class ShadowInitializer
{
    private static readonly Lazy<ShadowInitializerRule[]> Rules=new(()=>
    {
        var rows=BalanceCatalog.Read<ShadowInitializerRule>("shadow-initializer");
        if(rows.Length!=2){throw new InvalidDataException("Missing current Shadow initializer rows");}
        for(int i=0;i<2;i++)
        {
            var r=rows[i];
            bool Three(int[] a,int value)=>a is{Length:3}&&a.All(n=>n==value);
            if(r.Ability!=(i==0?"ShadowWarrior":"ShadowMaster")||r.Monster!=417+i||r.Alignment!=1||!r.NoRatio||!r.Inventory
                ||r.TotalPieces!=2||r.MonsterProperty!=65535||r.MonsterPropertyCount!=13||r.OnInitEquipment
                ||r.ComponentChoiceCounts is not{Length:16}||!r.ComponentChoiceCounts.SequenceEqual(new[]{0,1}.Concat(new int[14]))
                ||!Three(r.Level,0)||!Three(r.MinimumLife,376)||!Three(r.MaximumLife,376)||!Three(r.Defense,196)||!Three(r.Block,50)
                ||r.Resistances is not{Length:6}||r.Resistances.Where((a,n)=>!Three(a,n==0?40:0)).Any()
                ||r.DamageRegeneration!=(i==0?3:2)||r.DifficultySkillBonus is not{Length:3}||!r.DifficultySkillBonus.SequenceEqual(new[]{0,3,7})
                ||r.InitialSkills is not{Length:7}||!r.InitialSkills.Select(s=>s.Id).SequenceEqual(new[]{259,266,274,260,275,269,0})
                ||r.InitialSkills.Any(s=>s.Rank!=1)||!r.InitialSkills.Select(s=>s.Mode).SequenceEqual(new[]{14,14,14,14,14,14,5})
                ||!r.InitialSkills.Select(s=>s.Sequence).SequenceEqual(new[]{49,56,49,49,53,49,-1}))
            {throw new InvalidDataException("Unsupported current Shadow initializer inputs");}
        }
        return rows;
    });
    private static ShadowInitializerRule Rule(CombatAbility ability)=>Rules.Value[ability switch
    {CombatAbility.ShadowWarrior=>0,CombatAbility.ShadowMaster=>1,_=>throw new ArgumentOutOfRangeException(nameof(ability))}];
    internal static ShadowInitializedStats Roll(CombatAbility ability,ulong seed,MonsterDifficulty difficulty)
    {
        if(!Enum.IsDefined(difficulty)){throw new ArgumentOutOfRangeException(nameof(difficulty));}
        var r=Rule(ability);int d=(int)difficulty;var components=new int[16];
        for(int i=0;i<12;i++){components[i]=(int)SourceRandom.Limited(ref seed,r.ComponentChoiceCounts[i]);}
        int life=checked((r.MinimumLife[d]+(int)SourceRandom.Limited(ref seed,r.MaximumLife[d]-r.MinimumLife[d]+1))*256);
        SummonStat[] stats=[new("level",r.Level[d]),new("monster_playercount",1),
            new("damageresist",r.Resistances[0][d]),new("magicresist",r.Resistances[1][d]),new("fireresist",r.Resistances[2][d]),
            new("lightresist",r.Resistances[3][d]),new("coldresist",r.Resistances[4][d]),new("poisonresist",r.Resistances[5][d]),
            new("toblock",r.Block[d]),new("attackrate",100),new("velocitypercent",75),new("other_animrate",100),new("last_sent_hp_pct",128),
            new("maxhp",life),new("hitpoints",life),new("armorclass",r.Defense[d]),new("experience",0),new("hpregen",checked(life*r.DamageRegeneration)>>12)];
        return new(seed,components,stats,r.InitialSkills.Select(s=>s with{Rank=s.Rank+r.DifficultySkillBonus[d]}).ToArray());
    }
    private static ShadowInitialization Expected(CombatAbility ability,ulong before,MonsterDifficulty difficulty)
    {
        ulong world=before;ulong unit=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref world)));
        var rolled=Roll(ability,unit,difficulty);
        return new(ability,before,world,unit,rolled.After,difficulty,rolled.Stats.Single(s=>s.Name=="maxhp").Value,rolled.Stats.Single(s=>s.Name=="hpregen").Value);
    }
    internal static ShadowInitialization Allocate(CombatAbility ability,MonsterDifficulty difficulty)
    {
        // Validate the entire input domain before taking a shared world draw.
        _=Roll(ability,SourceRandom.InitLow(1),difficulty);
        var world=NpcWorldAllocation.RequireStream();ulong before=world.Value;
        ulong unit=NpcWorldAllocation.UnitSeed();var result=Expected(ability,before,difficulty);
        if(unit!=result.UnitSeed||world.Value!=result.WorldAfter){throw new InvalidOperationException("Shadow unit allocation lost its shared world context");}
        return result;
    }
    internal static void Validate(ShadowInitialization s)
    {
        if(s==null||s.Profile!="source-shadow-initialization-v1"||s.WorldBefore==0||s.WorldAfter==0||!Enum.IsDefined(s.Difficulty)
            ||s!=Expected(s.Ability,s.WorldBefore,s.Difficulty))
        {throw new InvalidDataException("Invalid Shadow initializer continuation");}
    }
    internal static EquipmentStatValue[] Basis(ShadowInitialization s,int ownerLevel)
    {
        Validate(s);
        if(ownerLevel is<1 or>99){throw new ArgumentOutOfRangeException(nameof(ownerLevel));}
        // do49 replaces only level before its birth helper. In particular,
        // Valkyrie's separate level-based AC/AR helper does not run here.
        return Roll(s.Ability,s.UnitSeed,s.Difficulty).Stats.Select(v=>new EquipmentStatValue(
            EquipmentStatKey.Pack(EquipmentStatValues.Definition(v.Name)?.Id??throw new NotSupportedException(v.Name)),
            v.Name=="level"?ownerLevel:v.Value)).Where(v=>v.Value!=0).OrderBy(v=>v.Packed).ToArray();
    }
}
