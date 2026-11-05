// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
namespace Sanctuary;

internal sealed record ItemFinishingType(string Code,int Ordinal,string StaffMods,int MaxSockets1,int MaxSockets25,int MaxSockets40);
internal sealed record ItemFinishingCatalogue(string ReferenceVersion,SuperiorItemRecord[] Superior,string[] Inferior,ItemFinishingType[] Types);
internal sealed record ItemFinishingParameters(string Code,int Format,int Quality,int Level,int Difficulty,uint Flags1,uint Flags2,uint StartSeed,ulong UnitSeed);
internal sealed record ItemFinishingMetadata(ItemFinishingParameters Parameters,int File,uint ItemFlags);

// Actual current-table physical finishing on an existing unattached graph.
// NPC requests already exclude sockets;special-normal contexts still need
// complete bindings. Current staffmods use the approved Reign tier mapping.
internal sealed class CurrentItemFinishing:ItemFinishingServices
{
    private static readonly Lazy<ItemFinishingCatalogue> Data=new(()=>
    {
        var c=BalanceCatalog.ReadObject<ItemFinishingCatalogue>("item-finishing-catalogue");
        if(c is not{ReferenceVersion:"3.3.93847",Superior.Length:8,Inferior.Length:4,Types.Length:105}
            ||c.Superior.Where((r,i)=>r.Row!=i||r.Modifiers.Length is<1 or>2).Any())
        {throw new InvalidDataException("Invalid current finishing catalogue");}
        return c;
    });
    internal static ItemFinishingCatalogue Catalogue=>Data.Value;
    private readonly Item Item;
    private readonly ItemPropertyCreationContext Graph;
    private readonly EquipmentStatValues Values;
    private readonly CurrentItemInitialization Facts;
    private readonly ItemFinishingType Meta;
    private readonly Dictionary<int,string> Codes;
    private ItemFinishingParameters Parameters;
    private ulong Unit;
    private readonly CurrentItemStaffmods Staff;
    internal int SelectedFile{get;private set;}=-1;
    internal uint ItemFlags{get;private set;}
    internal ulong UnitSeed=>Unit;
    internal ItemFinishingMetadata Capture()=>new(Parameters with{UnitSeed=Unit},SelectedFile,ItemFlags);
    internal CurrentItemFinishing(Item item,ItemPropertyCreationContext graph,EquipmentStatValues values,ItemFinishingParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(graph);ArgumentNullException.ThrowIfNull(values);ArgumentNullException.ThrowIfNull(parameters);
        Item=item;Graph=graph;Values=values;Parameters=parameters;Unit=parameters.UnitSeed;Facts=CurrentItemInitialization.For(parameters.Code);
        var type=NpcItemAffixes.Pool.Basis(parameters.Code).Type;Meta=Data.Value.Types.Single(r=>r.Code==type);Codes=Data.Value.Types.ToDictionary(r=>r.Ordinal,r=>r.Code);
        if(item is not{Deleted:false}||graph.Seed==0||parameters.Level is<1 or>787||parameters.Difficulty is<0 or>2)
        {throw new NotSupportedException("Physical finishing needs a valid original item,initialized item RNG and source level/difficulty");}
        if((parameters.Flags2&8)==0){throw new NotSupportedException("Actual socket allocation/materialization is not bound;NPC finishing requires its source no-sockets flag");}
        if(new[]{13,40,7,22,18}.Any(IsType))
        {throw new NotSupportedException("Current charm/body-part/book/scroll finishing needs its complete context");}
        if(!string.IsNullOrEmpty(Meta.StaffMods)){Staff=new CurrentItemStaffmods(Graph,Parameters,Meta.StaffMods);}
    }
    internal override bool HasItem=>Item is{Deleted:false};
    internal override ItemFinishingBase Basis=>new(Facts.Base.Durability,NpcItemAffixes.Pool.Basis(Parameters.Code).SourceFields.TryGetProperty("nodurability",out var n)&&n.GetInt32()!=0);
    internal override int Type=>Meta.Ordinal;
    internal override bool IsType(int type)=>Codes.TryGetValue(type,out var code)&&NpcItemAffixes.Pool.IsType(Parameters.Code,code);
    internal override int Format=>Parameters.Format;internal override int Quality=>Parameters.Quality;internal override bool Throwable=>Facts.Throwable;
    internal override bool HasDurability=>!Basis.NoDurability&&Basis.Durability!=0&&FullStat(73)!=0&&FullStat(152)<=0;
    internal override bool HasSuperiorTable=>true;internal override int SuperiorCount=>Data.Value.Superior.Length;
    internal override SuperiorItemRecord SuperiorRecord(int i)=>i>=0&&i<Data.Value.Superior.Length?Data.Value.Superior[i]:null;
    // User-approved Sanctuary adaptation:apply current table modifiers in
    // their listed order with existing numeric writers and item-data RNG.
    // The older complete grade dispatcher returns without delivery.
    internal override void SuperiorProperties(SuperiorItemRecord row)
    {
        foreach(var mod in row.Modifiers)
        {
            var plan=EquipmentPropertyWriter.Plan(mod.Code);
            if(!int.TryParse(mod.Parameter,out int parameter))
            {throw new NotSupportedException("Current superior property needs its full parameter: "+mod.Code);}
            var input=new EquipmentPropertyInput(mod.Min,mod.Max,parameter);
            if(ItemDurabilityProperties.Admitted(plan)){ItemDurabilityProperties.Apply(plan,input,1,Graph,()=>FullStat(73));}
            else if(ItemPropertyCreationContext.Admitted(plan)){EquipmentPropertyWriter.Create(plan,input,1,Graph);}
            else{throw new NotSupportedException("Current superior property needs its full writer: "+mod.Code);}
        }
    }
    internal override bool HasInferiorTable=>true;internal override int InferiorCount=>Data.Value.Inferior.Length;
    internal override uint ItemLimited(int bound)=>bound<=0?0:(uint)Graph.Roll(bound);
    internal override uint UnitLimited(int bound)=>SourceRandom.Limited(ref Unit,bound);
    internal override uint ItemRandom()
    {
        // A bounded draw with Int32.MaxValue would change the value used by
        // modulo100. Use the full shared LCG rather than such a replacement.
        return Graph.Random();
    }
    internal override int BaseStat(int stat)=>EquipmentStatValues.Base(Graph.Unit.Stats,EquipmentStatKey.Pack(stat),false);
    internal override int FullStat(int stat)=>Values.Query(Graph.Unit.Stats,EquipmentStatKey.Pack(stat));
    internal override int ItemOrSkillStat(int stat)=>FullStat(stat);
    internal override void WriteStat(int stat,int value)=>Graph.SetBase(stat,value);
    internal override void FileIndex(int id)=>SelectedFile=id;
    internal override void StaffMods()
    {Staff?.Apply();}
    internal override void Ethereality(){ItemFlags|=0x400000;EquipmentPropertyWriter.Ethereality(Graph);}
    internal override bool Quest=>Facts.Base.Quest;internal override uint Flags1=>Parameters.Flags1;internal override uint Flags2=>Parameters.Flags2;
    internal override bool Socketable=>NpcItemAffixes.Pool.Basis(Parameters.Code).SourceFields.TryGetProperty("hasinv",out var h)&&h.GetInt32()!=0;
    internal override bool Stackable=>Facts.Stackable;
    internal override uint MaxSockets
    {
        get
        {
            int cap=Parameters.Level>40?Meta.MaxSockets40:Parameters.Level>25?Meta.MaxSockets25:Meta.MaxSockets1;
            var raw=NpcItemAffixes.Pool.Basis(Parameters.Code).SourceFields;int gems=raw.TryGetProperty("gemsockets",out var g)?g.GetInt32():0;
            return(uint)Math.Min(cap,gems);
        }
    }
    internal override int Difficulty=>Parameters.Difficulty;internal override uint StartSeed=>Parameters.StartSeed;
    internal override void Flag(uint flag,bool value)=>ItemFlags=value?ItemFlags|flag:ItemFlags&~flag;
    internal override void AddSockets(uint count)=>throw new NotSupportedException("Native socket materialization remains unbound");
    internal override bool HasDropUnit=>false;internal override int DropUnitClass=>throw new NotSupportedException();internal override int DropIndex=>0;
    internal override void Charm()=>throw new NotSupportedException();internal override int Book(bool scroll)=>throw new NotSupportedException();internal override void Suffix(int slot,int id)=>throw new NotSupportedException();
}
