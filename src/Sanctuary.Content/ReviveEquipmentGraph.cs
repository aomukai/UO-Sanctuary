// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Resource/item-stat ownership graph. Native combat bases remain the reviewed
// original-body adapter; UO attributes are not imported as D2 player stats.
internal static class ReviveEquipmentGraph
{
    internal const string Profile="frozen-equipment-graph-v1";
    private sealed record Key(string Identity,int Level,int Life,int Mana,int Stamina,int Orders,int Vigor);
    private static readonly Dictionary<uint,(Key Key,FrozenNpcStatGraph Graph)> Cache=new();
    private static FrozenNpcStatGraph Build(FrozenEquipmentContext[] items,ReviveResourceBases bases,ReviveLifePlan life,int level,int orders,int vigor)
    {
        if(bases is not{Issue:null,Mana256:not null,Stamina256:not null}){throw new NotSupportedException("Item graph needs captured native resource bases");}
        var graph=new FrozenNpcStatGraph(items,level,life.Maximum256,bases.Mana256.Value,bases.Stamina256.Value,orders,vigor);
        if(graph.Read(7)<=0||graph.Read(9)<0||graph.Read(11)<0){throw new NotSupportedException("Frozen resource graph is outside the supported domain");}
        return graph;
    }
    internal static FrozenNpcStatGraph Project(ReviveNativeSetup setup,ReviveOriginalState original)
    {
        var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        var items=Array.ConvertAll(ReviveBeforeDeath.Equipment(before),FrozenEquipmentEffects.Bind);
        return Build(items,before.ResourceBases,setup.LifePlan,setup.CombatPlan.Level,0,0);
    }
    internal static ReviveNativeSetup Prepare(ReviveNativeSetup setup,ReviveOriginalState original)
    {
        var incoming=ReviveEquipmentIncoming.Prepare(setup with{EquipmentGraphProfile=Profile},original);
        _=Project(incoming,original);return incoming;
    }
    internal static void Validate(ReviveNativeState state)
    {
        if(state.Setup.EquipmentGraphProfile==null){return;}
        try
        {
            if(state.Setup.EquipmentGraphProfile!=Profile||Prepare(state.Setup,state.Original)!=state.Setup)
            {throw new InvalidDataException("Invalid original Revive item-graph profile");}
            _=Build(ReviveEquipmentLife.Items(state),ReviveEquipmentLife.ResourceBases(state),state.FacetPlan?.Life??state.Setup.LifePlan,
                (state.FacetPlan?.Combat??state.Setup.CombatPlan).Level,0,0);
        }
        catch(Exception e)when(e is NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unavailable original Revive item graph",e);}
    }
    internal static bool Handles(BaseCreature actor)=>ReviveEquipmentIncoming.Handles(actor)
        &&ReviveNativeActivation.State(actor)is{Setup.EquipmentGraphProfile:Profile};
    internal static FrozenNpcStatGraph Current(ReviveNativeState state,int? ordersOverride=null)
    {
        if(state.Setup.EquipmentGraphProfile!=Profile||World.FindMobile((Serial)state.Actor)is not BaseCreature actor)
        {throw new NotSupportedException("No admitted live original Revive item graph");}
        var life=state.FacetPlan?.Life??state.Setup.LifePlan;var bases=ReviveEquipmentLife.ResourceBases(state);
        var key=new Key(ReviveEquipmentLife.Identity(state),(state.FacetPlan?.Combat??state.Setup.CombatPlan).Level,life.Maximum256,
            bases.Mana256.Value,bases.Stamina256.Value,ordersOverride??BattleOrders.Percent(actor),PaladinVigor.StaminaPercent(actor));
        if(!Cache.TryGetValue(state.Actor,out var bound)||bound.Key!=key)
        {bound=(key,Build(ReviveEquipmentLife.Items(state),bases,life,key.Level,key.Orders,key.Vigor));Cache[state.Actor]=bound;}
        return bound.Graph;
    }
    internal static int Maximum256(ReviveNativeState state,int resource,int? orders=null)
    {int value=Current(state,orders).Read(resource==0?7:resource==2?9:11);return resource==0?value:Math.Clamp(value,0,65000*256);}
    internal static int? Defense(BaseCreature actor)=>Handles(actor)?checked(Current(ReviveNativeActivation.State(actor)).Read(31)+Current(ReviveNativeActivation.State(actor)).Read(2)/4):null;
    internal static int DefensePercent(BaseCreature actor)=>Handles(actor)?Current(ReviveNativeActivation.State(actor)).Read(16):0;
    internal static void Forget(uint actor)=>Cache.Remove(actor);
    internal static void Clear()=>Cache.Clear();
}
