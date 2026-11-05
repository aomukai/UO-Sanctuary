// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

// Original native raw items share the actor's existing numeric graph. The
// caller supplies inventory primary/secondary identity;body4/5 alone does not
// infer a persisted primary. Item flags remain the verified fresh-item state.
internal sealed class GeneratedNpcWeaponFrame:SourceWeaponServices<EquipmentStatUnit>
{
    private readonly GeneratedNpcStatGraph Graph;
    private readonly int Kind;
    private readonly EquipmentStatUnit First,Second;
    private readonly bool HasSkill,HasRow;
    private readonly int Select,Frame;
    private readonly int Wield;
    private readonly Dictionary<uint,(EquipmentStatUnit Unit,GeneratedNpcItemState State)> Items=new();
    private readonly uint Flags;
    internal GeneratedNpcWeaponFrame(GeneratedNpcStatGraph graph,int kind,uint primary,uint secondary,bool hasSkill,bool hasRow,int selection,int sequenceFrame,uint skillFlags)
    {
        Graph=graph;Kind=kind;HasSkill=hasSkill;HasRow=hasRow;Select=selection;Frame=sequenceFrame;Flags=skillFlags;
        // The enclosing graph checks the same original identities and saved
        // payloads at every public entry. Within that immutable frame, decode
        // each raw item once rather than repeatedly during a single roll.
        foreach(var original in GeneratedNpcEquipment.Items((Server.Mobiles.BaseCreature)graph.Actor.Entity))
        {Items.Add(original.Serial.Value,(graph.Item(original.Serial.Value).Unit,original.Snapshot));}
        Wield=GeneratedNpcEquipment.WieldType((Server.Mobiles.BaseCreature)graph.Actor.Entity);
        First=primary==0?null:graph.Item(primary).Unit;Second=secondary==0?null:graph.Item(secondary).Unit;
        foreach(var item in new[]{First,Second})
        {if(item!=null&&(State(item).Equipment.Body is not(4 or 5)||!Weapon(item))){throw new NotSupportedException("NPC primary/secondary services require original equipped weapon identities");}}
        if(First!=null&&First==Second){throw new NotSupportedException("NPC primary and secondary identities must be distinct");}
    }
    private static GeneratedNpcItem Original(EquipmentStatUnit item)=>item?.Entity as GeneratedNpcItem;
    private GeneratedNpcItemState State(EquipmentStatUnit item)=>Items[Original(item).Serial.Value].State;
    private void Current(){_=Graph.Stats;}
    internal uint Selected{get{Current();return( SourceWeaponSelection.Select(this,Graph.Actor)?.Entity as GeneratedNpcItem)?.Serial.Value??0;}}
    internal void Enter(bool reset=false){Current();SourceWeaponSelection.Enter(this,Graph.Actor,reset);}
    internal void Restore(bool enabled=true){Current();SourceWeaponSelection.RestorePrimary(this,Graph.Actor,enabled);}
    // These source skill fields must be supplied by the eventual dispatcher;
    // selecting a physical hand does not establish a throw-skill context.
    internal int Mastery(int kind,int firstSkillItemType,int skillRange)
    {
        Current();return Mastery(SourceWeaponSelection.Select(this,Graph.Actor),kind,firstSkillItemType,skillRange);
    }
    private int Mastery(EquipmentStatUnit selected,int kind,int firstSkillItemType,int skillRange)
    {
        if(selected==null){return 0;}
        string code=State(selected).Metadata.Code;
        bool throwing=HasSkill&&HasRow&&CurrentItemInitialization.For(code).Throwable&&firstSkillItemType>0
            &&NpcItemAffixes.Pool.IsTypeCode(CurrentItemInitialization.TypeCode(firstSkillItemType),"thro")&&skillRange==2;
        return SourceWeaponMastery.Read(Graph.Actor.Stats.FullValues,true,true,Graph.Actor.Stats.IsExtended,throwing,kind,
            type=>CurrentItemInitialization.TypeCode(type)is{} ancestor&&NpcItemAffixes.Pool.IsType(code,ancestor));
    }
    internal NpcPhysicalStats Physical(int firstSkillItemType,int skillRange,int additionalPercent=0)
    {
        Current();var selected=SourceWeaponSelection.Select(this,Graph.Actor);var item=selected==null?null:State(selected);
        int Read(int stat)=>EquipmentStatValues.SourceRead(Graph.Actor.Stats,EquipmentStatKey.Pack(stat),EquipmentStatValues.Definition(stat));
        int coefficient(string key)
        {
            var fields=NpcItemAffixes.Pool.Basis(item.Metadata.Code).SourceFields;
            int value=fields.TryGetProperty(key,out var raw)?raw.GetInt32():0;
            if(value is <short.MinValue or >short.MaxValue){throw new NotSupportedException("Weapon coefficient exceeds the source signed-short domain");}
            return value==1?100:value;
        }
        int damage=item!=null&&Wield==2?23:21;
        return new(Read(damage),Read(damage+1),Read(111),checked(Read(25)+additionalPercent+Mastery(selected,1,firstSkillItemType,skillRange)),
            Read(0),Read(2),item!=null,item==null?0:coefficient("StrBonus"),item==null?0:coefficient("DexBonus"),Read(18),Read(17));
    }
    internal NpcFillPacket Fill(GeneratedNpcFillTarget target,NpcFillPacket packet,int firstSkillItemType,int skillRange,
        bool goodAlignment,int hireling,int intrinsicCritical,Func<int,int> limited,Func<uint> raw,
        Func<NpcFillPacket,NpcFillPacket> attackedInMelee,bool ranged=false,int sourceDamage=128)
    {
        Current();return NpcDamageFill.Fill(new GeneratedNpcFillServices(this,Graph,target,firstSkillItemType,skillRange,
            goodAlignment,hireling,intrinsicCritical,limited,raw,attackedInMelee),packet,ranged,sourceDamage);
    }
    internal bool SelectedBlunt
    {
        get{Current();var selected=SourceWeaponSelection.Select(this,Graph.Actor);return selected!=null&&NpcItemAffixes.Pool.IsType(State(selected).Metadata.Code,"blun");}
    }
    internal override bool Inventory(EquipmentStatUnit actor)=>true;
    internal override bool Dual(EquipmentStatUnit actor)=>SourceItemStatUpdates.CanDualWield(actor.Kind,Kind);
    internal override EquipmentStatUnit Equipped(EquipmentStatUnit actor)=>First;
    internal override EquipmentStatUnit Primary(EquipmentStatUnit actor)=>First;
    internal override EquipmentStatUnit Secondary(EquipmentStatUnit actor)=>Second;
    internal override EquipmentStatUnit Composite(EquipmentStatUnit actor,int part)
    {
        int body=part==5?4:part==6?5:throw new NotSupportedException("Unknown source weapon composite part");
        // The original graph validates its physical inventory and payloads.
        foreach(var item in Items.Values)
        {if(item.State.Equipment.Body==body){return item.Unit;}}
        return null;
    }
    internal override bool Skill(EquipmentStatUnit actor)=>HasSkill;
    internal override bool SkillRow(EquipmentStatUnit actor)=>HasRow;
    internal override int Selection(EquipmentStatUnit actor)=>Select;
    internal override int SequenceFrame(EquipmentStatUnit actor)=>Frame;
    internal override uint SkillFlags(EquipmentStatUnit actor)=>Flags;
    internal override bool Weapon(EquipmentStatUnit item)=>Original(item)is{} original&&NpcItemAffixes.Pool.IsType(State(item).Metadata.Code,"weap");
    // Fresh generated metadata is identified and has neither BROKEN nor
    // NOEQUIP. Mutable flag admission belongs to the item-state work.
    internal override bool Equippable(EquipmentStatUnit item)
    {
        if(Original(item)is not{Deleted:false} original){return false;}
        var saved=State(item);
        return SourceWeaponSelection.Equippable(true,item.Kind,true,checked((uint)saved.Metadata.Flags)|saved.Equipment.Flags);
    }
    internal override EquipmentStatUnit Owner(EquipmentStatUnit item)=>EquipmentStatGraph.Owner(item,out _);
    internal override void Merge(EquipmentStatUnit actor,EquipmentStatUnit item,bool damage)=>EquipmentStatGraph.Merge(actor,item,damage,Graph.Values);
}
