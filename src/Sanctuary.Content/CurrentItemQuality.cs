// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server;
namespace Sanctuary;

internal sealed record PreparedItemRequest(string Code,int Level,int Quality,int Difficulty,ulong UnitSeed,ulong ItemSeed,uint StartSeed);
internal sealed record PreparedItemState(string Code,int Quality,int DropQuality,uint StartSeed,ulong UnitSeed,int Variant,int File,int AutoAffix,uint Flags,CurrentAffixState Affixes,int[] AutoApplied);
internal sealed record PreparedItemResult(ItemPropertyCreationContext Graph,PreparedItemState State,int BaseDraws);

// Source initialization/quality/finalization with explicitly supplied separate
// seed streams. No allocation-seed provenance,materialization,NPC equipment
// delivery or actor admission is inferred from this prepared component.
internal sealed class CurrentItemQuality:ItemQualityServices
{
    private readonly Item Item;
    private readonly ItemPropertyCreationContext Graph;
    private readonly EquipmentStatValues Values;
    private readonly PreparedItemRequest Request;
    private readonly CurrentItemInitialization Facts;
    private readonly CurrentQualityType Policy;
    private CurrentAffixState Affixes=new(new int[3],new int[3],0,0,false,[]);
    private readonly List<int> AutoApplied=new();
    private int CurrentQuality,CurrentDropQuality,File,Auto;
    private uint Start,Flags;
    private ulong Unit;
    private readonly int Variant;
    private CurrentItemQuality(Item item,ItemPropertyCreationContext graph,EquipmentStatValues values,PreparedItemRequest request,CurrentItemInitialization facts,int variant,ulong unit,uint initialFlags)
    {Item=item;Graph=graph;Values=values;Request=request;Facts=facts;Policy=CurrentAutoMagic.Type(request.Code);CurrentDropQuality=request.Quality;Start=request.StartSeed;Unit=unit;Variant=variant;Flags=initialFlags;Affixes=Affixes with{Identified=(initialFlags&16)!=0};}
    internal static void Require(PreparedItemRequest r)
    {
        ArgumentNullException.ThrowIfNull(r);var b=NpcItemAffixes.Pool.Basis(r.Code);var raw=b.SourceFields;var facts=CurrentItemInitialization.For(r.Code);
        if(r.Level is<1 or>787||r.Difficulty is<0 or>2||r.Quality is not(1 or 2 or 3 or 4 or 6)||r.ItemSeed==0)
        {throw new NotSupportedException("Prepared NPC item generation requires explicit initialized seeds,format100 item level/difficulty and supported requested quality");}
        if(!(facts.Weapon||facts.Armor||b.Type is"ring"or"amul")||facts.Base.Quest||raw.TryGetProperty("unique",out var unique)&&unique.GetInt32()!=0)
        {throw new NotSupportedException("Prepared NPC body equipment does not supply special-normal/quest/unique/drop-world creation services");}
    }
    internal static PreparedItemResult Prepare(Item item,EquipmentStatValues values,PreparedItemRequest request,uint initialFlags=0)
    {
        if(initialFlags!=0&&initialFlags!=NpcItemAllocations.InitialFlags){throw new NotSupportedException("Unsupported item factory flags");}
        Require(request);var facts=CurrentItemInitialization.For(request.Code);
        // Allocated source item data starts at quality0;requested quality is
        // resolved only after base initialization,so stack magic=false here.
        var fresh=facts.Prepare(item,values,request.UnitSeed,request.ItemSeed,false);
        var s=new CurrentItemQuality(item,fresh.Context,values,request,facts,Math.Max(fresh.Variant,0),fresh.UnitSeed,initialFlags);
        if(SourceItemQuality.Run(s)==0){throw new InvalidOperationException("Qualified source item quality generation failed");}
        // Complete outer CreateItemUnit marks the result identified. NOSSELL
        // and full durability belong to successful later NPC equipment delivery.
        s.Identify(true);
        return new(fresh.Context,s.Capture(),fresh.BaseDraws);
    }
    private ItemFinishingParameters Parameters()=>new(Request.Code,100,CurrentQuality,Request.Level,Request.Difficulty,0,10,Start,Unit);
    private CurrentItemFinishing Physical()=>new(Item,Graph,Values,Parameters());
    internal PreparedItemState Capture()=>new(Request.Code,CurrentQuality,CurrentDropQuality,Start,Unit,Variant,File,Auto,Flags,Affixes,AutoApplied.ToArray());
    internal override bool HasItem=>Item is{Deleted:false};internal override bool IsItem=>true;
    internal override ItemQualityBase Basis
    {
        get
        {
            uint code=0;byte[] text=Encoding.ASCII.GetBytes(Request.Code.PadRight(4));for(int i=0;i<4;i++){code|=(uint)text[i]<<(8*i);}
            var raw=NpcItemAffixes.Pool.Basis(Request.Code).SourceFields;return new(code,false,raw.TryGetProperty("auto prefix",out var p)?p.GetInt32():0);
        }
    }
    internal override int Quality{get=>CurrentQuality;set=>CurrentQuality=value;}
    internal override int DropQuality{get=>CurrentDropQuality;set=>CurrentDropQuality=value;}
    internal override int Format=>100;internal override bool CanMagic=>Policy.Magic;internal override bool Quest=>false;
    internal override bool MagicOrBetter=>CurrentQuality is>=4 and<=9;internal override bool CanRare=>Policy.Rare;internal override bool UniqueColumn=>false;internal override bool CanNormal=>Policy.Normal;
    internal override int RollQuality()=>CurrentDropQuality!=0?CurrentDropQuality:throw new NotSupportedException("Unforced quality ratios/world drop services remain separate");
    internal override uint LowSeed=>(uint)Graph.Seed;
    internal override void StartSeed(uint seed)=>Start=seed;
    internal override void InitLow(uint seed)=>Graph.InitLow(seed);
    internal override void AutoAffix(int id)=>Auto=id;
    internal override void Affix(bool prefix,int slot,int id){(prefix?Affixes.Prefixes:Affixes.Suffixes)[slot]=id;}
    internal override void RareName(bool prefix,int id)=>Affixes=prefix?Affixes with{RarePrefix=id}:Affixes with{RareSuffix=id};
    internal override void FileIndex(int id)=>File=id;
    private int PhysicalQuality(bool inferior)
    {
        var s=Physical();int result=inferior?SourceItemFinishing.Inferior(s):SourceItemFinishing.Superior(s);var state=s.Capture();File=state.File;Unit=state.Parameters.UnitSeed;Flags|=state.ItemFlags;return result;
    }
    internal override int Inferior()=>PhysicalQuality(true);internal override int Superior()=>PhysicalQuality(false);
    internal override void Normal(bool legacy)
    {if(legacy){throw new NotSupportedException("Legacy normal generation is not format100");}SourceItemFinishing.NormalModern(Physical());}
    internal override void NormalFallback()=>Normal(false);
    private int GeneratedAffixes(bool rare)
    {
        var s=new CurrentItemAffixes(Graph,Parameters(),Affixes.Identified);int result=rare?SourceItemAffixes.Rare(s,[0,0,0],[0,0,0]):SourceItemAffixes.Magic(s,0,0);Affixes=s.Capture();Identify(Affixes.Identified);return result;
    }
    internal override int Rare()=>GeneratedAffixes(true);internal override int Magic()=>GeneratedAffixes(false);
    private static NotSupportedException Missing()=>new("Unique/set/craft/tempered/drop-world services are not admitted by prepared NPC equipment");
    internal override int Craft()=>throw Missing();internal override int Tempered(bool p)=>throw Missing();internal override int Set()=>throw Missing();
    internal override bool HasDurability=>Physical().HasDurability;
    internal override int FullStat(int stat)=>Values.Query(Graph.Unit.Stats,EquipmentStatKey.Pack(stat));
    internal override int BaseStat(int stat)=>Graph.BaseValue(stat);
    internal override void WriteStat(int stat,int value)=>Graph.SetBase(stat,value);
    internal override bool Force=>false;internal override bool GameAllowsLadder=>throw Missing();internal override int ItemLevel=>Request.Level;internal override int ForcedIndex=>throw Missing();internal override int UniqueCount=>throw Missing();
    internal override ItemQualityUnique UniqueAt(int i)=>throw Missing();internal override ItemQualityUnique FindUnique(int i)=>throw Missing();internal override bool Dropped(int i)=>throw Missing();internal override bool CanDropUnique()=>throw Missing();
    internal override int Limited(int bound)=>Graph.Roll(bound);
    internal override void Identify(bool value){Flags=value?Flags|16:Flags&~16u;Affixes=Affixes with{Identified=value};}
    internal override void MakeEthereal()=>SourceItemFinishing.Ethereal(Physical());
    internal override void FinishNormal()=>SourceItemFinishing.Sockets(Physical());
    internal override int PickAuto(int spawn,int force,int properties,int prefix,int preferred,int family)
    {
        if((spawn,force,properties,prefix,preferred)!=(0,1,0,1,0)){throw new NotSupportedException("Unexpected prepared automagic context");}
        var row=CurrentAutoMagic.Select(Request.Code,Request.Level,CurrentQuality,family,Affixes,Graph);return row==null?0:CurrentAutoMagic.Id(row);
    }
    internal override object MagicRecord(int id)=>CurrentAutoMagic.Record(id)??throw new NotSupportedException("Unknown automagic reference ID");
    internal override void AssignProperties(int mode,object record)
    {
        if(mode!=0||record is not AutoMagicRow row){throw Missing();}AutoApplied.Add(CurrentAutoMagic.Id(row));CurrentItemAffixes.ApplyModifiers(row.Modifiers,Graph,Request.Level);
    }
}
