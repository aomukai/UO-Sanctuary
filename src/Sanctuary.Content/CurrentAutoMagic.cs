// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal sealed record CurrentQualityType(string Code,bool Magic,bool Rare,bool Normal);
internal sealed record AutoMagicRow(int Row,string Name,int Version,bool Spawnable,bool Rare,int Level,int MaxLevel,int Frequency,int Group,string ClassSpecific,string[] Include,string[] Exclude,AffixModifier[] Modifiers);
internal sealed record CurrentQualityCatalogue(string ReferenceVersion,CurrentQualityType[] Types,AutoMagicRow[] AutoMagic,Dictionary<string,string>[] RatioReference);

internal static class CurrentAutoMagic
{
    private static readonly Lazy<CurrentQualityCatalogue> Data=new(()=>
    {
        var d=BalanceCatalog.ReadObject<CurrentQualityCatalogue>("item-quality-current");
        if(d is not{ReferenceVersion:"3.3.93847",Types.Length:105,AutoMagic.Length:44,RatioReference.Length:6}
            ||d.AutoMagic.Where((r,i)=>r.Row!=i||r.Frequency<=0||r.Group<=0||r.Modifiers.Length is<1 or>3).Any())
        {throw new InvalidDataException("Invalid owned current automagic/type-quality facts");}
        return d;
    });
    internal static CurrentQualityCatalogue Catalogue=>Data.Value;
    internal static CurrentQualityType Type(string code)=>Data.Value.Types.Single(t=>t.Code==NpcItemAffixes.Pool.Basis(code).Type);
    // Joined IDs are internal reference identities,not modern binary IDs.
    internal static int Id(AutoMagicRow row)=>CurrentItemAffixes.Layout.MagicSuffixRows+CurrentItemAffixes.Layout.MagicPrefixRows+row.Row+1;
    internal static AutoMagicRow Record(int id)=>Data.Value.AutoMagic.FirstOrDefault(r=>Id(r)==id);
    internal static AutoMagicRow Select(string code,int level,int quality,int family,CurrentAffixState state,ItemPropertyCreationContext graph)
    {
        int affixLevel=NpcItemAffixes.Pool.Level(code,level);string required=NpcItemAffixes.Pool.Basis(code).RequiredClass;
        var ids=state.Prefixes.Concat(state.Suffixes).Where(id=>id!=0).ToHashSet();
        var groups=NpcItemAffixes.All.Where(r=>ids.Contains(CurrentItemAffixes.Id(r))).Select(r=>r.Definition.Group).ToHashSet();
        return SourceAffixPicker.Select(()=>Data.Value.AutoMagic.Where(r=>r.Group==family&&r.Level<=affixLevel&&(r.MaxLevel==0||affixLevel<=r.MaxLevel)
            &&(!(quality is 6 or 8 or 9)||r.Rare)&&!groups.Contains(r.Group)
            &&(r.ClassSpecific.Length==0||required.Length==0||required==r.ClassSpecific)
            &&r.Include.Any(t=>NpcItemAffixes.Pool.IsType(code,t))&&!r.Exclude.Any(t=>NpcItemAffixes.Pool.IsType(code,t))).ToArray(),
            r=>checked(r.Frequency*(NpcItemAffixes.Pool.Basis(code).MagicLevel>0?r.Level:1)),bound=>(uint)graph.Roll(bound),true);
    }
}
