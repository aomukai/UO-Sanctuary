// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.CompilerServices;
namespace Sanctuary;

// Numeric birth before original item replay. This does not activate a native
// actor, resource/skill/event consumers, UMod42's death action or the cast.
internal static class ShadowBirthGraph
{
    private sealed class Applied{}
    private static readonly ConditionalWeakTable<EquipmentStatUnit,Applied> Used=new();
    internal static ShadowCreationStats Apply(SkillValues owner,SkillValues pet,GeneratedNpcStatGraph graph,
        Func<string,int> ownerStats,Func<string,int> petStats)
    {
        ArgumentNullException.ThrowIfNull(graph);var actor=graph.Actor;var values=graph.Values;
        if(actor is not{Kind:1,Stats:{IsExtended:true,OwnerKind:1} root}||root.Owner!=actor||root.Last!=null||root.Sets!=null)
        {throw new NotSupportedException("Shadow birth requires an initialized NPC graph before item replay");}
        if(Used.TryGetValue(actor,out _)){throw new InvalidOperationException("Shadow birth already started on this graph");}
        int life=EquipmentStatValues.SourceRead(root,EquipmentStatKey.Pack(7),EquipmentStatValues.Definition(7));
        var writes=ShadowCreation.Prepare(owner,pet,life,true,ownerStats,petStats);
        Used.Add(actor,new());
        values.SetBase(root,EquipmentStatKey.Pack(7),writes.MaximumLifeWrite256,actor);
        values.SetBase(root,EquipmentStatKey.Pack(6),writes.CurrentLifeWrite256,actor);
        var list=new EquipmentStatNode{OwnerKind=1};EquipmentStatGraph.Post(actor,list,true,values);
        graph.RegisterBirthList(list);
        foreach(var stat in writes.Auras){Set(stat);}
        foreach(var stat in writes.Passives){Set(stat);}
        void Set(SummonStat stat)=>values.SetBase(list,EquipmentStatKey.Pack(EquipmentStatValues.Definition(stat.Name).Id),stat.Value);
        // The modifier request is retained in the result. Recording it is not
        // execution of the separately qualified unique/death-event pipeline.
        return writes;
    }
}
