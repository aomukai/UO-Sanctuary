// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

// This explicit component profile does not enable equipped learned Revive.
// Remaining equipment consumers/events must be composed before that gate lifts.
internal static class ReviveEquipmentLife
{
    internal const string Profile="frozen-equipment-life-v1";
    private sealed record Bound(string Json,FrozenEquipmentContext[] Items,ReviveResourceBases Bases,ReviveAttackDefenseBases CombatBases,string Identity);
    private static readonly Dictionary<uint,Bound> Cache=new();
    internal static ReviveNativeSetup Prepare(ReviveNativeSetup setup,ReviveOriginalState original)
    {
        if(setup.LifePlan==null||setup.CombatPlan==null){throw new NotSupportedException("Frozen equipment life requires captured source life/combat");}
        var items=Read(original.BeforeDeathJson);if(items.Length==0){throw new NotSupportedException("No captured equipment life suppliers");}
        foreach(var item in items){_=item.ResourceInput("hp",216,setup.CombatPlan.Level);}
        return setup with{EquipmentLifeProfile=Profile};
    }
    private static FrozenEquipmentContext[] Read(string json)=>ReviveBeforeDeath.Equipment(ReviveBeforeDeath.Decode(json))
        .OrderBy(s=>s.OriginalItem).Select(FrozenEquipmentEffects.Bind).ToArray();
    private static Bound Context(ReviveNativeState state)
    {
        string json=state.Original.BeforeDeathJson;
        if(!Cache.TryGetValue(state.Actor,out var held)||held.Json!=json)
        {var before=ReviveBeforeDeath.Decode(json);held=new(json,Read(json),before.ResourceBases,before.AttackDefenseBases,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))).ToLowerInvariant());Cache[state.Actor]=held;}
        return held;
    }
    internal static FrozenEquipmentContext[] Items(ReviveNativeState state)=>Context(state).Items;
    internal static ReviveResourceBases ResourceBases(ReviveNativeState state)=>Context(state).Bases;
    internal static string Identity(ReviveNativeState state)=>Context(state).Identity;
    internal static ReviveAttackDefenseBases AttackDefenseBases(ReviveNativeState state)=>Context(state).CombatBases;
    internal static void Validate(ReviveNativeState state)
    {
        if(state.Setup.EquipmentLifeProfile==null){return;}
        if(state.Setup.EquipmentLifeProfile==ReviveEquipmentCombat.Profile){ReviveEquipmentCombat.Validate(state);return;}
        if(state.Setup.EquipmentLifeProfile==ReviveEquipmentResources.Profile){ReviveEquipmentResources.Validate(state);return;}
        try
        {
            if(state.Setup.EquipmentLifeProfile!=Profile||Prepare(state.Setup,state.Original)!=state.Setup)
            {throw new InvalidDataException("Invalid equipped Revive life profile");}
        }
        catch(Exception e)when(e is NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unavailable equipped Revive life suppliers",e);}
    }
    internal static int Maximum256(ReviveNativeState state,ReviveLifePlan source,int battleOrdersPercent)
    {
        if(state.Setup.EquipmentLifeProfile is not(Profile or ReviveEquipmentResources.Profile or ReviveEquipmentCombat.Profile)){throw new NotSupportedException("No equipped Revive life profile");}
        if(state.Setup.EquipmentGraphProfile==ReviveEquipmentGraph.Profile){return ReviveEquipmentGraph.Maximum256(state,0,battleOrdersPercent);}
        int level=(state.FacetPlan?.Combat??state.Setup.CombatPlan).Level;
        int flat=0,coefficient=0,percent=0;
        foreach(var item in Items(state))
        {
            var input=item.ResourceInput("hp",216,level);
            flat=checked(flat+input.Flat256);coefficient=checked(coefficient+input.LevelCoefficientRaw);percent=checked(percent+item.Bonus("hp%",level));
        }
        // Kind1: item vitality never gains player life-per-vitality. Item HP%
        // and Battle Orders share the existing source percentage pool.
        return NpcResourceValues.Calculate(new(1,0,level,source.Maximum256,flat,coefficient,percent,battleOrdersPercent));
    }
    internal static void Forget(uint actor){Cache.Remove(actor);ReviveEquipmentGraph.Forget(actor);}
    internal static void Clear(){Cache.Clear();ReviveEquipmentGraph.Clear();}
}
