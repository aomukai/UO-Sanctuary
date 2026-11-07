// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
namespace Sanctuary;

internal sealed record GeneratedNpcItemState(int Version,string ReferenceVersion,PreparedItemRequest Request,PreparedItemState Metadata,ItemPropertyCreationState Graph,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]GeneratedNpcEquipState Equipment=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]NpcItemAllocation Allocation=null);

// Native item-owned state,not a player GearState projection or a fixture
// manifest. This qualified creation entry point supplies explicit seed streams;
// allocation provenance,NPC equip and live recipient consumers remain separate.
[SerializationGenerator(0)]
public partial class GeneratedNpcItem:Item
{
    [SerializableField(0,setter:"private")]private string _saved;
    private static readonly Lazy<IReadOnlyDictionary<int,SummonAffix>> Affixes=new(()=>NpcItemAffixes.All.ToDictionary(CurrentItemAffixes.Id));
    private GeneratedNpcItem():base(0x14F0)
    {Visible=false;Movable=false;Name="Generated equipment [art placeholder]";Layer=Layer.Invalid;}
    internal string SavedText=>_saved;
    internal GeneratedNpcItemState Snapshot=>Decode(_saved);
    internal static EquipmentStatValues CreationValues()=>new(_=>0,_=>null,_=>null,(_,_,_,_,_)=>{});
    internal static GeneratedNpcItem Create(PreparedItemRequest request)
    {
        CurrentItemQuality.Require(request);var item=new GeneratedNpcItem();
        try{item.Prepare(request,null);return item;}catch{item.Delete();throw;}
    }
    internal static GeneratedNpcItem CreateWorldAllocated(string code,int level,int quality,int difficulty)
    {
        CurrentItemQuality.Require(new(code,level,quality,difficulty,1,1,0));
        return CreateAllocated(code,level,quality,difficulty,NpcWorldAllocation.RequireStream());
    }
    internal static GeneratedNpcItem CreateAllocated(string code,int level,int quality,int difficulty,SourceSeedStream game)
    {
        // Domain rejection precedes native allocation. A failed allocation
        // cannot consume source game RNG. Generation failures keep its draws.
        ArgumentNullException.ThrowIfNull(game);
        CurrentItemQuality.Require(new(code,level,quality,difficulty,1,1,0));
        var item=new GeneratedNpcItem();
        try
        {
            var allocation=NpcItemAllocations.Take(ref game.Value);
            item.Prepare(NpcItemAllocations.Request(code,level,quality,difficulty,allocation),allocation);return item;
        }
        catch{item.Delete();throw;}
    }
    private void Prepare(PreparedItemRequest request,NpcItemAllocation allocation)
    {
        var prepared=CurrentItemQuality.Prepare(this,CreationValues(),request,allocation==null?0:NpcItemAllocations.InitialFlags);
        var state=new GeneratedNpcItemState(0,"3.3.93847",request,prepared.State,prepared.Graph.Capture(),Allocation:allocation);
        Validate(state);_saved=JsonSerializer.Serialize(state);
        string name=NpcItemAffixes.Pool.Basis(request.Code).SourceFields.GetProperty("name").GetString();
        Name=name+" [generated equipment; art placeholder]";
    }
    // Return isolated arrays so callers cannot edit saved metadata or graphs
    // through a snapshot reference. Restoring never invokes a property writer.
    private static GeneratedNpcItemState Decode(string text)
    {
        try{var state=JsonSerializer.Deserialize<GeneratedNpcItemState>(text??"null");Validate(state);return state;}
        catch(JsonException e){throw new InvalidDataException("Invalid generated NPC item payload",e);}
    }
    internal static void Validate(GeneratedNpcItemState state)
    {
        if(state is not{Version:0 or 1,ReferenceVersion:"3.3.93847",Request:not null,Metadata:not null,Graph:not null}
            ||(state.Version==0)!=(state.Equipment==null))
        {throw new InvalidDataException("Missing or unknown generated NPC item state");}
        CurrentItemQuality.Require(state.Request);var m=state.Metadata;var g=state.Graph;var a=m.Affixes;
        if(m.Code!=state.Request.Code||m.Quality is not(1 or 2 or 3 or 4 or 6)||m.DropQuality is not(1 or 2 or 3 or 4 or 6)
            ||m.File is<-1 or>7||m.Flags!=(state.Allocation==null?16u:NpcItemAllocations.InitialFlags)||a is not{Identified:true,Prefixes.Length:3,Suffixes.Length:3,Applied:not null}
            ||m.AutoApplied==null||m.AutoApplied.Length>1||a.Applied.Length>6||g.Seed==0||g.Ethereal
            ||g.ItemBase==null||g.Properties==null||!g.HasProperties&&g.Properties.Length!=0)
        {throw new InvalidDataException("Invalid prepared generated NPC item metadata/graph");}
        if(state.Allocation is{} allocation)
        {try{NpcItemAllocations.Validate(allocation,state.Request);}catch(ArgumentException e){throw new InvalidDataException("Invalid generated item allocation context",e);}}
        int varieties=CurrentItemInitialization.For(m.Code).Varieties;
        if(m.Variant<0||m.Variant>=Math.Max(varieties,1))
        {throw new InvalidDataException("Invalid generated item graphic variant");}
        foreach(var fields in new[]{g.ItemBase,g.Properties})
        {
            if(fields.Select(v=>v.Packed).Distinct().Count()!=fields.Length||fields.Any(v=>EquipmentStatValues.Definition(v.Stat)==null))
            {throw new InvalidDataException("Generated item contains unknown or duplicate raw stat fields");}
        }
        bool ValidAffix(int id,AffixKind? kind=null)=>id!=0&&Affixes.Value.TryGetValue(id,out var row)&&(kind==null||row.Definition.Kind==kind);
        if(a.Prefixes.Any(id=>id!=0&&!ValidAffix(id,AffixKind.Prefix))||a.Suffixes.Any(id=>id!=0&&!ValidAffix(id,AffixKind.Suffix))
            ||a.Applied.Any(id=>!ValidAffix(id))||a.RarePrefix!=0&&!NpcItemAffixes.Names.Any(r=>r.Prefix&&r.Row+1+CurrentItemAffixes.Layout.RareSuffixRows==a.RarePrefix)
            ||a.RareSuffix!=0&&!NpcItemAffixes.Names.Any(r=>!r.Prefix&&r.Row+1==a.RareSuffix))
        {throw new InvalidDataException("Generated item contains unknown or misclassified reference affix/name IDs");}
        var automatic=m.AutoAffix==0?null:CurrentAutoMagic.Record(m.AutoAffix);
        int family=NpcItemAffixes.Pool.Basis(m.Code).SourceFields.TryGetProperty("auto prefix",out var p)?p.GetInt32():0;
        if(m.AutoAffix!=0&&(automatic==null||automatic.Group!=family)||!m.AutoApplied.SequenceEqual(m.AutoAffix==0?Array.Empty<int>():[m.AutoAffix]))
        {throw new InvalidDataException("Invalid generated item automagic reference identity");}
        if(state.Equipment is{} equipped)
        {
            var t=GeneratedNpcEquipment.Type(m.Code);
            if(!((Serial)equipped.Actor).IsMobile||!((Serial)equipped.Owner).IsMobile||equipped.Actor==equipped.Owner
                ||equipped.Body is<1 or>10||equipped.Body!=t.Body1&&equipped.Body!=t.Body2||!Enum.IsDefined(equipped.Kind)||equipped.Flags!=0x1000)
            {throw new InvalidDataException("Invalid generated NPC equipment binding");}
        }
    }
    internal ItemPropertyCreationContext RestoreGraph(EquipmentStatValues values)
    {
        if(Deleted){throw new InvalidOperationException("Generated item was deleted");}
        var state=Snapshot;var d=CurrentItemInitialization.For(state.Metadata.Code);
        return ItemPropertyCreationContext.Restore(this,values,d.PropertyBase,d.Weapon,d.Armor,d.Throwable,state.Graph);
    }
    internal void BeginEquipment(GeneratedNpcEquipState binding)
    {
        var state=Snapshot;if(Deleted||Parent!=null||state.Equipment!=null){throw new InvalidOperationException("Generated item is already bound");}
        var next=state with{Version=1,Equipment=binding};Validate(next);_saved=JsonSerializer.Serialize(next);
    }
    internal void CompleteEquipment()
    {
        var state=Snapshot;
        if(Deleted||state.Equipment is not{} e||Parent is not Mobile actor||actor.Serial.Value!=e.Actor)
        {throw new InvalidOperationException("Generated item lost its successful equipment delivery");}
        var values=CreationValues();var graph=RestoreGraph(values);
        // GetMaxDurability -> GetTotalStatValue_Layer0 uses the source cached
        // operative reader,not a separate on-demand query path.
        graph.SetBase(72,EquipmentStatValues.SourceRead(graph.Unit.Stats,EquipmentStatKey.Pack(73),EquipmentStatValues.Definition(73)));
        var next=state with{Graph=graph.Capture()};Validate(next);_saved=JsonSerializer.Serialize(next);
    }
    // Player acquisition and native UO equipment effects are not routes into
    // this raw D2 NPC item. The later NPC equipment adapter owns delivery.
    public override bool OnDragLift(Mobile from)=>false;
    public override bool CanEquip(Mobile from)=>false;
    public override bool AllowSecureTrade(Mobile from,Mobile to,Mobile newOwner,bool accepted)=>false;
    public override DeathMoveResult OnParentDeath(Mobile parent)=>DeathMoveResult.RemainEquipped;
    [AfterDeserialization]private void AfterGeneratedItemLoad()
    {_ = Snapshot;}
}
