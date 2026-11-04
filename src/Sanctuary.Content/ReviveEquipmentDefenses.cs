// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Recipient-only component. It does not admit item events, regeneration,
// source pierce, native swing replay or the public equipped Revive cast.
internal static class ReviveEquipmentDefenses
{
    internal const string Profile="frozen-equipment-defenses-v1";
    private static readonly string[] Codes=["res-physical","res-fire","res-cold","res-pois","res-ltng","res-all","res-mag",
        "magic-reduction","physical-reduction","abs-fire","abs-fire%","abs-cold","abs-cold%","abs-ltng","abs-ltng%",
        "res-pois-len","nofreeze","half-freeze","ac-miss","ac-hth"];
    private static string Name(ResistanceType type)=>"SanctuaryReviveFrozen:"+type;
    internal static ReviveNativeSetup Prepare(ReviveNativeSetup setup,ReviveOriginalState original)
    {
        var combat=ReviveEquipmentCombat.Prepare(setup,original);
        ValidateInputs(original,combat.CombatPlan.Level);
        return combat with{EquipmentDefenseProfile=Profile};
    }
    private static void ValidateInputs(ReviveOriginalState original,int level)
    {
        var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        var items=Array.ConvertAll(ReviveBeforeDeath.Equipment(before),FrozenEquipmentEffects.Bind);
        int[] totals=new int[Codes.Length];
        for(int i=0;i<Codes.Length;i++)
        {foreach(var item in items){totals[i]=checked(totals[i]+item.Bonus(Codes[i],level));}}
        for(int i=0;i<5;i++){_=NpcEquipmentResistances.Pool(0,totals[i],totals[5],i!=0);}
    }
    internal static void Validate(ReviveNativeState state)
    {
        if(state.Setup.EquipmentDefenseProfile==null){return;}
        try
        {
            if(state.Setup.EquipmentDefenseProfile!=Profile||Prepare(state.Setup,state.Original)!=state.Setup)
            {throw new InvalidDataException("Invalid frozen Revive recipient defense profile");}
            ValidateInputs(state.Original,(state.FacetPlan?.Combat??state.Setup.CombatPlan).Level);
        }
        catch(Exception e)when(e is NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unavailable frozen Revive recipient defenses",e);}
    }
    internal static bool Handles(BaseCreature actor)=>!World.Loading&&ReviveNativeActivation.State(actor)is{Setup.EquipmentDefenseProfile:Profile};
    internal static int? Bonus(BaseCreature actor,string code)
    {
        if(!Handles(actor)){return null;}
        if(Array.IndexOf(Codes,code)<0){throw new NotSupportedException("Unadmitted frozen recipient stat: "+code);}
        var state=ReviveNativeActivation.State(actor);
        int level=(state.FacetPlan?.Combat??state.Setup.CombatPlan).Level,total=0;
        foreach(var item in ReviveEquipmentLife.Items(state)){total=checked(total+item.Bonus(code,level));}
        return total;
    }
    internal static void RequireBody(BaseCreature actor)
    {
        var type=actor.GetType();
        foreach(string method in new[]{nameof(Mobile.GetResistance),nameof(Mobile.ComputeResistances),nameof(Mobile.GetMinResistance),nameof(Mobile.GetMaxResistance)})
        {
            if(type.GetMethod(method)?.DeclaringType!=typeof(Mobile))
            {throw new NotSupportedException("Original body has a custom resistance computation/cap: "+type.FullName);}
        }
    }
    internal static void Sync(BaseCreature actor)
    {
        if(!Handles(actor)){Clear(actor);return;}
        RequireBody(actor);
        var types=new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy};
        var codes=new[]{"res-physical","res-fire","res-cold","res-pois","res-ltng"};
        int[] values=new int[types.Length];
        for(int i=0;i<types.Length;i++){values[i]=NpcEquipmentResistances.Pool(0,Bonus(actor,codes[i]).Value,Bonus(actor,"res-all").Value,i!=0);}
        // Native NPC pools have no player cap. Mods are item additions only;
        // facet seeds, curse/aura modifiers and unrelated mods remain live.
        for(int i=0;i<types.Length;i++)
        {
            string name=Name(types[i]);var prior=actor.GetResistanceMod(name);
            if(prior!=null&&prior.Type==types[i]&&prior.Offset==values[i]){continue;}
            actor.RemoveResistanceMod(name);
            if(values[i]!=0){actor.AddResistanceMod(new(types[i],name,values[i]));}
        }
    }
    internal static void Clear(BaseCreature actor)
    {
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy})
        {actor.RemoveResistanceMod(Name(type));}
    }
}
