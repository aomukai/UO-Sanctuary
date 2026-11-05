// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal sealed record ItemAffixLayout(string ReferenceVersion,int MagicSuffixRows,int MagicPrefixRows,int RareSuffixRows,int RarePrefixRows);
internal sealed record CurrentAffixState(int[] Prefixes,int[] Suffixes,int RarePrefix,int RareSuffix,bool Identified,int[] Applied);

// Complete current-pool magic/rare composition on an existing fresh graph.
// Full quality fallback,materialization,NPC attachment and actor admission
// remain outer callers;unsupported properties throw rather than return0.
internal sealed class CurrentItemAffixes:SourceItemAffixServices
{
    private static readonly Lazy<ItemAffixLayout> Data=new(()=>
    {
        var d=BalanceCatalog.ReadObject<ItemAffixLayout>("item-affix-layout");
        if(d is not{ReferenceVersion:"3.3.93847",MagicSuffixRows:786,MagicPrefixRows:722,RareSuffixRows:155,RarePrefixRows:46})
        {throw new InvalidDataException("Invalid current affix table layout");}
        return d;
    });
    internal static ItemAffixLayout Layout=>Data.Value;
    private static readonly Lazy<Dictionary<int,SummonAffix>> Records=new(()=>NpcItemAffixes.All.ToDictionary(Id));
    internal static int Id(SummonAffix row)=>row.Row+1+(row.Definition.Kind==AffixKind.Prefix?Data.Value.MagicSuffixRows:0);
    private readonly ItemPropertyCreationContext Graph;
    private readonly ItemFinishingParameters Parameters;
    private readonly CurrentItemStaffmods Staff;
    private readonly int[] Prefixes=new int[3],Suffixes=new int[3];
    private int RarePrefix,RareSuffix;
    private bool Identified=true;
    private readonly List<int> Applied=new();
    internal CurrentAffixState Capture()=>new((int[])Prefixes.Clone(),(int[])Suffixes.Clone(),RarePrefix,RareSuffix,Identified,Applied.ToArray());
    internal CurrentItemAffixes(ItemPropertyCreationContext graph,ItemFinishingParameters parameters,bool identified=true)
    {
        ArgumentNullException.ThrowIfNull(graph);ArgumentNullException.ThrowIfNull(parameters);
        Graph=graph;Parameters=parameters;Identified=identified;
        if(graph.Unit.Entity is not Server.Item{Deleted:false}||graph.Seed==0||parameters.Format!=100||parameters.Level is<1 or>787||parameters.Quality is not(4 or 6))
        {throw new NotSupportedException("Current magic/rare affixes require a live fresh item,format100,initialized RNG and explicit source item level/quality");}
        var basis=NpcItemAffixes.Pool.Basis(parameters.Code);var type=CurrentItemFinishing.Catalogue.Types.Single(t=>t.Code==basis.Type);
        if(type.StaffMods.Length!=0){Staff=new(graph,parameters,type.StaffMods);}
        _=Records.Value;
    }
    internal override bool CanBeRare=>NpcItemAffixes.Pool.CanBeRare(Parameters.Code);
    internal override uint Format=>(uint)Parameters.Format;
    internal override int LegacyRare(ReadOnlySpan<int> p,ReadOnlySpan<int> s)=>throw new NotSupportedException("Legacy rare generation is not a current format100 request");
    internal override int RareName(bool prefix)
    {
        var b=NpcItemAffixes.RareBasis(Parameters.Code);
        var row=NpcItemAffixes.SelectRare(NpcItemAffixes.Names,prefix,Parameters.Format,b.Stackable,b.Throwable,t=>NpcItemAffixes.Pool.IsType(Parameters.Code,t),Graph.Roll);
        return row==null?0:row.Row+1+(prefix?Data.Value.RareSuffixRows:0);
    }
    internal override void AssignRareName(bool prefix,int id){if(prefix){RarePrefix=id;}else{RareSuffix=id;}}
    internal override bool Jewel=>NpcItemAffixes.Pool.Basis(Parameters.Code).Type=="jewl";
    internal override uint Random()=>Graph.Random();
    internal override int Limited(int bound)=>Graph.Roll(bound);
    internal override int Pick(bool spawn,bool force,bool assignProperties,bool prefix,int preferred)
    {
        if(!spawn){throw new NotSupportedException("Current NPC affix composition requires spawnable selection");}
        var previous=Prefixes.Concat(Suffixes).Where(id=>id!=0).Select(id=>Records.Value[id].Definition.Id).ToArray();
        var row=NpcItemAffixes.Pool.Select(Parameters.Code,Parameters.Level,Parameters.Quality==6?GearRarity.Rare:GearRarity.Magic,
            prefix?AffixKind.Prefix:AffixKind.Suffix,bound=>(uint)Graph.Roll(bound),force,previous,preferred);
        if(row==null){return 0;}if(assignProperties){ApplyProperties(row);}return Id(row);
    }
    internal override void Assign(bool prefix,int slot,int id){(prefix?Prefixes:Suffixes)[slot]=id;}
    internal override int Affix(bool prefix,int slot)=>(prefix?Prefixes:Suffixes)[slot];
    internal override void ClearIdentified()=>Identified=false;
    internal override object PropertyRecord(int id)=>id==0?null:Records.Value.TryGetValue(id,out var row)?row:throw new NotSupportedException("Unbound current affix ID: "+id);
    internal override void ApplyProperties(object record)
    {
        if(record is not SummonAffix row){throw new NotSupportedException("Current affix property record required");}
        Applied.Add(Id(row));
        ApplyModifiers(row.Definition.Modifiers,Graph,Parameters.Level);
    }
    internal static void ApplyModifiers(IEnumerable<AffixModifier> modifiers,ItemPropertyCreationContext graph,int level)
    {
        foreach(var m in modifiers)
        {
            if(!int.TryParse(m.Parameter.Length==0?"0":m.Parameter,out int parameter))
            {throw new NotSupportedException("Current affix parameter needs its complete compiler context: "+m.Code);}
            var input=new EquipmentPropertyInput(m.Min,m.Max,parameter);var plan=EquipmentPropertyWriter.Plan(m.Code);
            if(CurrentTreePropertyWriter.Supports(plan)){CurrentTreePropertyWriter.Apply(plan,input,graph);}
            else if(ItemSkillPropertyWriter.Supports(plan)){graph.ApplySkill(plan,input,ItemSkillProperties.ForLevel(level));}
            else if(ItemPropertyCreationContext.Admitted(plan)){graph.Apply(plan,input);}
            else{throw new NotSupportedException("Valid selected affix property remains unsupported: "+m.Code);}
        }
    }
    internal override void Finish()=>Staff?.Apply();
}
