// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sanctuary;

// Shared format100,spawnable source pool. This extracts Valkyrie's existing
// rules; it does not turn player supported-property filtering into NPC policy.
internal sealed class SummonAffixPool
{
    private readonly SummonAffix[] Rows;
    private readonly Dictionary<string,SummonAffix> ById;
    private readonly Dictionary<string,SummonAffixBase> Bases;
    private readonly Dictionary<string,GearItemType> Types;
    internal SummonAffixPool(SummonAffixCatalogue catalog)
    {
        Rows=catalog.Affixes;ById=Rows.ToDictionary(x=>x.Definition.Id,StringComparer.Ordinal);
        Bases=catalog.Bases.ToDictionary(x=>x.Code,StringComparer.Ordinal);Types=catalog.Types.ToDictionary(x=>x.Code,StringComparer.Ordinal);
        if(catalog.ReferenceVersion!="3.3.93847"||Rows.Any(r=>r.Row<0||r.Definition.Frequency<=0||r.Definition.Group<=0
            ||r.Definition.Include.Length==0||r.Definition.Include.Concat(r.Definition.Exclude).Any(t=>!Types.ContainsKey(t)))
            ||Bases.Values.Any(b=>!Types.ContainsKey(b.Type)||b.QualityLevel is <0 or >99||b.MagicLevel<0))
        {throw new InvalidDataException("Invalid current NPC affix pool");}
    }
    internal SummonAffixBase Basis(string code)=>Bases.TryGetValue(code,out var r)?r:throw new InvalidDataException("Unknown current NPC base "+code);
    internal bool CanBeRare(string code)=>Types[Basis(code).Type].Rare;
    internal SummonAffix Find(string id)=>ById.TryGetValue(id,out var r)?r:throw new InvalidDataException("Unknown current NPC affix "+id);
    internal bool IsType(string code,string ancestor)=>EquipmentBalance.IsType(Types,Basis(code).Type,ancestor);
    internal int Level(string code,int level)
    {
        if(level is <1 or >787){throw new ArgumentOutOfRangeException(nameof(level));}
        // At qlvl0, positive item level already dominates max(ilvl,qlvl)
        // and integer qlvl/2 is zero for both0and1. Keep raw qlvl0 in data;
        // reuse the player helper without relaxing its validation contract.
        var b=Basis(code);return AffixBalance.Level(Math.Min(level,99),Math.Max(b.QualityLevel,1),b.MagicLevel);
    }
    internal SummonAffix[] Pool(string code,int level,GearRarity rarity,AffixKind kind,IReadOnlyList<string> existing=null)
    {
        if(rarity is not(GearRarity.Magic or GearRarity.Rare)||!Enum.IsDefined(kind)){throw new ArgumentOutOfRangeException();}
        var b=Basis(code);int affixLevel=Level(code,level);
        var groups=(existing??Array.Empty<string>()).Select(id=>Find(id).Definition.Group).ToHashSet();
        return Rows.Where(r=>
        {
            var d=r.Definition;
            // The literal older first-socket-property rejection is retained;
            // modern socket eligibility remains explicitly unverified.
            return d.Kind==kind&&d.Level<=affixLevel&&(d.MaxLevel==0||affixLevel<=d.MaxLevel)
                &&(rarity!=GearRarity.Rare||d.Rare)&&!r.FirstPropertySockets&&!groups.Contains(d.Group)
                &&(string.IsNullOrEmpty(d.ClassSpecific)||string.IsNullOrEmpty(b.RequiredClass)||d.ClassSpecific==b.RequiredClass)
                &&!d.Exclude.Any(t=>IsType(code,t))&&d.Include.Any(t=>IsType(code,t));
        }).OrderBy(r=>r.Row).ToArray();
    }
    internal int Weight(SummonAffix row,string code)=>checked(row.Definition.Frequency*(Basis(code).MagicLevel>0?row.Definition.Level:1));
    internal SummonAffix Select(string code,int level,GearRarity rarity,AffixKind kind,ref ulong seed,bool force=true,IReadOnlyList<string> existing=null,int preferred=0)
        =>SourceAffixPicker.Select(()=>Pool(code,level,rarity,kind,existing),r=>Weight(r,code),ref seed,force,preferred>0?r=>r.Row==preferred-1:null);
    internal SummonAffix Select(string code,int level,GearRarity rarity,AffixKind kind,Func<int,uint> roll,bool force=true,IReadOnlyList<string> existing=null,int preferred=0)
        =>SourceAffixPicker.Select(()=>Pool(code,level,rarity,kind,existing),r=>Weight(r,code),roll,force,preferred>0?r=>r.Row==preferred-1:null);
}

internal static class NpcItemAffixes
{
    private static readonly Lazy<SummonAffixCatalogue> Catalog=new(()=>
    {
        var c=BalanceCatalog.ReadObject<SummonAffixCatalogue>("npc-item-affix-catalogue");
        if(c is not{ReferenceVersion:"3.3.93847",Affixes:{Length:991},Bases:{Length:692},Types:{Length:105},RareNames:{Length:201},RareBases:{Length:692}})
        {throw new InvalidDataException("Invalid complete NPC item catalogue");}
        return c;
    });
    private static readonly Lazy<SummonAffixPool> Shared=new(()=>new(Catalog.Value));
    internal static SummonAffixPool Pool=>Shared.Value;
    internal static IReadOnlyList<SummonAffix> All=>Catalog.Value.Affixes;
    internal static IReadOnlyList<SummonRareName> Names=>Catalog.Value.RareNames;
    internal static SummonRareBase RareBasis(string code)=>Catalog.Value.RareBases.Single(r=>r.Code==code);
    internal static SummonRareName RareName(string code,bool prefix,ref ulong seed,int format=100)
    {
        var b=RareBasis(code);
        return SelectRare(Names,prefix,format,b.Stackable,b.Throwable,t=>Pool.IsType(code,t),ref seed);
    }
    internal static SummonRareName SelectRare(IEnumerable<SummonRareName> rows,bool prefix,int format,bool stackable,bool throwable,Func<string,bool> isType,ref ulong seed)
    {
        ulong current=seed;
        try{return SelectRare(rows,prefix,format,stackable,throwable,isType,bound=>(int)SourceRandom.Limited(ref current,bound));}
        finally{seed=current;}
    }
    internal static SummonRareName SelectRare(IEnumerable<SummonRareName> rows,bool prefix,int format,bool stackable,bool throwable,Func<string,bool> isType,Func<int,int> roll)
    {
        var pool=RarePool(rows,prefix,format,stackable,throwable,isType);
        return pool.Length==0?null:pool[roll(pool.Length)];
    }
    internal static SummonRareName[] RarePool(IEnumerable<SummonRareName> rows,bool prefix,int format,bool stackable,bool throwable,Func<string,bool> isType)
    {
        if(format is <0 or >65535){throw new ArgumentOutOfRangeException(nameof(format));}
        if(format<100&&(stackable||throwable)){return [];}
        return rows.Where(r=>r.Prefix==prefix&&(r.Version<100||format>=100)&&!r.Exclude.Any(isType)&&r.Include.Any(isType)).Take(511).ToArray();
    }
}
