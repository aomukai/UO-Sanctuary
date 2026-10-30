// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
namespace Sanctuary;

internal sealed record IronGolemGraphBirth(IronGolemBirthStats Writes,int LifeBeforePassiveWrite256,
    int MaximumLife256,int CurrentLife256,int Armor,int Attack,int AuraState);

// Apply creation to an already equipped numeric graph. Original-item birth,
// item-event registration and live Mobile synchronization remain caller work.
// Value-change effects are supplied by EquipmentStatValues, not bypassed with
// assignments to FullValues. A failed application requires a fresh graph.
internal static class IronGolemBirthGraph
{
    private sealed class Applied {}
    private static readonly ConditionalWeakTable<EquipmentStatUnit,Applied> Used=new();
    private static readonly Lazy<IReadOnlyDictionary<string,int>> Ids=new(()=>Enumerable.Range(0,368)
        .Select(EquipmentStatValues.Definition).ToDictionary(x=>x.Name,x=>x.Id,StringComparer.Ordinal));
    private static int Id(string name)=>Ids.Value.TryGetValue(name,out int id)?id:throw new NotSupportedException("Unmapped summon stat: "+name);

    internal static IronGolemGraphBirth Apply(SkillValues skill,MonsterDifficulty difficulty,bool expansion,
        EquipmentStatUnit actor,EquipmentStatUnit original,EquipmentStatValues values,Func<string,int> accrued,int summonResistance)
    {
        ArgumentNullException.ThrowIfNull(values);ArgumentNullException.ThrowIfNull(accrued);
        if(actor is not{Kind:1,Stats:{IsExtended:true,OwnerKind:1} root} || root.Owner!=actor
            || original is not{Kind:4,Stats:{IsExtended:true,OwnerKind:4} item} || item.Owner!=original
            || item.Parent!=root || item.Recipient!=actor || item.Is(EquipmentStatNode.Dynamic))
        {throw new NotSupportedException("Iron Golem birth requires its original item already merged into the NPC graph");}
        if(Used.TryGetValue(actor,out _)){throw new InvalidOperationException("Iron Golem birth already started on this graph");}
        int Read(int stat)=>EquipmentStatValues.Total(root,EquipmentStatKey.Pack(stat));
        void Set(int stat,int amount)=>values.SetBase(root,EquipmentStatKey.Pack(stat),amount);
        void Add(int stat,int amount)
        {if(amount!=0){Set(stat,checked(EquipmentStatValues.Base(root,EquipmentStatKey.Pack(stat),false)+amount));}}
        var initial=new SummonSpawnStats(Read(7),Read(31),Read(19));
        var basis=IronGolemBirth.Base(skill,difficulty,expansion,initial);
        var metadata=GenericPetMetadata.Table?.Skill(90);
        if(metadata is not{Name:"IronGolem",AuraState:>0} || GenericPetMetadata.Table.State(metadata.AuraState)?.Name!="thorns")
        {throw new NotSupportedException("Unmapped Iron Golem thorns state metadata");}
        // Resolve all declared fields before making the first graph write.
        var definition=SkillMechanics.For(CombatAbility.IronGolem);
        for(int i=1;i<=10;i++){string name=definition.Text("passivestat"+i);if(name!="0"){Id(name);}}
        for(int i=1;i<=6;i++){string name=definition.Text("aurastat"+i);if(name!="0"){Id(name);}}
        Used.Add(actor,new());
        Set(12,basis.Level);Add(31,checked(basis.Defense-initial.Defense));Add(19,checked(basis.Attack-initial.Attack));
        // Read life only after the level change has recalculated attached
        // per-level properties. The returned base additions are not replayed.
        int life=Read(7);
        var writes=IronGolemBirth.Prepare(skill,difficulty,expansion,new(life,Read(31),Read(19)),accrued,summonResistance,name=>Read(Id(name)));
        foreach(var stat in writes.Passives){Add(Id(stat.Name),stat.Value);}
        var aura=new EquipmentStatNode{OwnerKind=1,State=metadata.AuraState};
        EquipmentStatGraph.Post(actor,aura,true,values);
        foreach(var stat in writes.Auras){values.SetBase(aura,EquipmentStatKey.Pack(Id(stat.Name)),stat.Value);}
        Set(7,writes.MaximumLifeBaseWrite256);Set(6,writes.CurrentLifeWrite256);
        if(writes.Resistances.Length!=0)
        {
            var resistances=new EquipmentStatNode{OwnerKind=1};EquipmentStatGraph.Post(actor,resistances,true,values);
            foreach(var stat in writes.Resistances){values.SetBase(resistances,EquipmentStatKey.Pack(Id(stat.Name)),stat.Value);}
        }
        return new(writes,life,Read(7),Read(6),Read(31),Read(19),metadata.AuraState);
    }
}
