// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;

namespace Sanctuary;

internal sealed record ItemInitializationType(string Code,int Ordinal,bool Quiver,int Varieties);
internal sealed record ItemInitializationTypes(string ReferenceVersion,ItemInitializationType[] Types);
internal sealed record CurrentItemInitialization(ItemInitializationBase Base,int Type,bool Quiver,bool Armor,bool Weapon,bool Stackable,
    int MinStack,int BaseMaxStack,int SpawnStack,int Varieties,bool Throwable)
{
    private static readonly Lazy<Dictionary<string,ItemInitializationType>> Types=new(()=>
    {
        var rows=BalanceCatalog.ReadObject<ItemInitializationTypes>("item-initialization-types");
        if(rows is not{ReferenceVersion:"3.3.93847",Types.Length:105}||rows.Types.Any(t=>t.Ordinal<0||t.Varieties is<0 or>255))
        {throw new InvalidDataException("Invalid current item initialization types");}
        return rows.Types.ToDictionary(t=>t.Code,StringComparer.Ordinal);
    });
    internal static CurrentItemInitialization For(string code)
    {
        var pool=NpcItemAffixes.Pool;var b=pool.Basis(code);var raw=b.SourceFields;
        int I(string key)=>raw.TryGetProperty(key,out var value)?value.GetInt32():0;
        if(!Types.Value.TryGetValue(b.Type,out var type)){throw new InvalidDataException("Missing current initialization type "+b.Type);}
        return new(new(I("block"),I("speed"),I("durability"),I("minac"),I("maxac"),I("mindam"),I("maxdam"),
            I("2handmindam"),I("2handmaxdam"),I("minmisdam"),I("maxmisdam"),I("quest")!=0,I("questdiffcheck")!=0),
            type.Ordinal,type.Quiver,pool.IsType(code,"armo"),pool.IsType(code,"weap"),I("stackable")!=0,I("minstack"),I("maxstack"),I("spawnstack"),type.Varieties,NpcItemAffixes.RareBasis(code).Throwable);
    }
    internal int MaxStack(int extra)=>Math.Min(checked(BaseMaxStack+extra),511);
    internal EquipmentPropertyBase PropertyBase=>new(Base.Minimum,Base.Maximum,Base.SecondaryMinimum,Base.SecondaryMaximum,
        Base.ThrowMinimum,Base.ThrowMaximum,Base.MinArmor,Base.MaxArmor);

    private sealed class Fresh(CurrentItemInitialization facts,ItemPropertyCreationContext context,bool magic,int extra,ulong unitSeed):ItemInitializationServices
    {
        private static NotSupportedException Missing()=>new("Fresh base initialization needs its gold/elixir,quality or quest-list service");
        internal override int Type=>facts.Type;
        internal override bool Quiver=>facts.Quiver;
        internal override bool Armor=>facts.Armor;
        internal override bool Weapon=>facts.Weapon;
        internal override bool Stackable=>facts.Stackable;
        internal override bool MagicOrBetter=>magic;
        internal override int MinStack=>facts.MinStack;
        internal override int MaxStack=>facts.MaxStack(extra);
        internal override int SpawnStack=>facts.SpawnStack;
        internal override int Varieties=>facts.Varieties;
        internal override ItemInitializationBase Record=>facts.Base;
        internal override bool HasItem=>true;
        internal ulong UnitSeed=unitSeed;
        internal int BaseDraws;
        internal override uint Limited(int bound){if(bound>0){BaseDraws++;}return SourceRandom.Limited(ref UnitSeed,bound);}
        internal override void Write(int stat,int value)=>context.SetBase(stat,value);
        internal override void Gold(int value)=>throw Missing();
        internal override void Elixir()=>throw Missing();
        internal int Variant=-1;
        internal override void Variety(int value)=>Variant=value;
        internal override int Quality()=>throw Missing();
        internal override bool HasMagicList=>throw Missing();
        internal override void AllocateMagicList(bool hasItem)=>throw Missing();
        internal override void PostMagicList()=>throw Missing();
        internal override int Difficulty=>throw Missing();
        internal override void WriteMagic(int stat,int value)=>throw Missing();
        internal override void Identify()=>throw Missing();
    }
    // Unattached fresh base graph only. Quality orchestration will consume
    // this item-property seed/graph;the unit/base seed remains separate.
    internal (ItemPropertyCreationContext Context,int Variant,ulong UnitSeed,int BaseDraws) Prepare(Item item,EquipmentStatValues values,ulong unitSeed,ulong itemSeed,
        bool magic=false,int extraStack=0,ItemInitializationDrop drop=null)
    {
        if(Type is 4 or 11){throw new NotSupportedException("Gold/elixir initialization needs its native service");}
        _=MaxStack(extraStack);
        var initial=extraStack==0?Array.Empty<EquipmentStatValue>():[new EquipmentStatValue(EquipmentStatKey.Pack(254),extraStack)];
        var context=new ItemPropertyCreationContext(item,values,PropertyBase,Weapon,Armor,Throwable,itemSeed,initial);
        var services=new Fresh(this,context,magic,extraStack,unitSeed);SourceItemInitialization.Run(services,drop,false);
        return(context,services.Variant,services.UnitSeed,services.BaseDraws);
    }
}
