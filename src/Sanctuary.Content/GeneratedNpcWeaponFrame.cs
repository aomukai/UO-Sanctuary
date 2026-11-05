// SPDX-License-Identifier: GPL-3.0-or-later
using System;
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
    private readonly uint Flags;
    internal GeneratedNpcWeaponFrame(GeneratedNpcStatGraph graph,int kind,uint primary,uint secondary,bool hasSkill,bool hasRow,int selection,int sequenceFrame,uint skillFlags)
    {
        Graph=graph;Kind=kind;HasSkill=hasSkill;HasRow=hasRow;Select=selection;Frame=sequenceFrame;Flags=skillFlags;
        First=primary==0?null:graph.Item(primary).Unit;Second=secondary==0?null:graph.Item(secondary).Unit;
        foreach(var item in new[]{First,Second})
        {if(item!=null&&(Original(item).Snapshot.Equipment.Body is not(4 or 5)||!Weapon(item))){throw new NotSupportedException("NPC primary/secondary services require original equipped weapon identities");}}
        if(First!=null&&First==Second){throw new NotSupportedException("NPC primary and secondary identities must be distinct");}
    }
    private static GeneratedNpcItem Original(EquipmentStatUnit item)=>item?.Entity as GeneratedNpcItem;
    private void Current(){_=Graph.Stats;}
    internal uint Selected{get{Current();return( SourceWeaponSelection.Select(this,Graph.Actor)?.Entity as GeneratedNpcItem)?.Serial.Value??0;}}
    internal void Enter(bool reset=false){Current();SourceWeaponSelection.Enter(this,Graph.Actor,reset);}
    internal void Restore(bool enabled=true){Current();SourceWeaponSelection.RestorePrimary(this,Graph.Actor,enabled);}
    internal override bool Inventory(EquipmentStatUnit actor)=>true;
    internal override bool Dual(EquipmentStatUnit actor)=>SourceItemStatUpdates.CanDualWield(actor.Kind,Kind);
    internal override EquipmentStatUnit Equipped(EquipmentStatUnit actor)=>First;
    internal override EquipmentStatUnit Primary(EquipmentStatUnit actor)=>First;
    internal override EquipmentStatUnit Secondary(EquipmentStatUnit actor)=>Second;
    internal override EquipmentStatUnit Composite(EquipmentStatUnit actor,int part)
    {
        int body=part==5?4:part==6?5:throw new NotSupportedException("Unknown source weapon composite part");
        // The original graph validates its physical inventory and payloads.
        foreach(var item in ((Server.Mobiles.BaseCreature)Graph.Actor.Entity).Items)
        {if(item is GeneratedNpcItem original&&original.Snapshot.Equipment?.Body==body){return Graph.Item(original.Serial.Value).Unit;}}
        return null;
    }
    internal override bool Skill(EquipmentStatUnit actor)=>HasSkill;
    internal override bool SkillRow(EquipmentStatUnit actor)=>HasRow;
    internal override int Selection(EquipmentStatUnit actor)=>Select;
    internal override int SequenceFrame(EquipmentStatUnit actor)=>Frame;
    internal override uint SkillFlags(EquipmentStatUnit actor)=>Flags;
    internal override bool Weapon(EquipmentStatUnit item)=>Original(item)is{} original&&NpcItemAffixes.Pool.IsType(original.Snapshot.Metadata.Code,"weap");
    // Fresh generated metadata is identified and has neither BROKEN nor
    // NOEQUIP. Mutable flag admission belongs to the item-state work.
    internal override bool Equippable(EquipmentStatUnit item)
    {
        if(Original(item)is not{Deleted:false} original){return false;}
        var saved=original.Snapshot;
        return SourceWeaponSelection.Equippable(true,item.Kind,true,checked((uint)saved.Metadata.Flags)|saved.Equipment.Flags);
    }
    internal override EquipmentStatUnit Owner(EquipmentStatUnit item)=>EquipmentStatGraph.Owner(item,out _);
    internal override void Merge(EquipmentStatUnit actor,EquipmentStatUnit item,bool damage)=>EquipmentStatGraph.Merge(actor,item,damage,Graph.Values);
}
