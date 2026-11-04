// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public sealed record ReviveResourceBases(int Version,int? Mana256,int? Stamina256,string Issue=null);

// Exact native bases are captured before death removes equipment/stat mods.
// This component is explicitly opted in;it does not enable equipped casting.
internal static class ReviveEquipmentResources
{
    internal const string Profile="frozen-equipment-resources-v1";
    internal static ReviveResourceBases Capture(BaseCreature actor,FrozenEquipmentSnapshot[] equipment)
    {
        try
        {
            int Basis(StatType stat,int seed,int raw,string suffix)
            {
                int itemOffset=equipment.Sum(e=>actor.GetStatMod(((Serial)e.OriginalItem)+suffix)?.Offset??0);
                if(actor.GetStatOffset(stat)!=itemOffset){throw new NotSupportedException("Non-equipment native "+suffix+" modifiers need their own resource restoration policy");}
                int value=seed<0?raw:seed;
                if(value is <0 or >65000){throw new NotSupportedException("Native resource base is outside the supported range");}
                return checked(value*256);
            }
            return new(0,Basis(StatType.Int,actor.ManaMaxSeed,actor.RawInt,"Int"),Basis(StatType.Dex,actor.StamMaxSeed,actor.RawDex,"Dex"));
        }
        catch(Exception e)when(e is NotSupportedException or OverflowException)
        {return new(0,null,null,e.Message);}
    }
    internal static void ValidateBases(ReviveResourceBases bases)
    {
        if(bases==null){return;}
        if(bases.Version!=0||bases.Issue!=null&&(string.IsNullOrWhiteSpace(bases.Issue)||bases.Mana256!=null||bases.Stamina256!=null)
            ||bases.Issue==null&&(bases.Mana256 is not(>=0 and<=16640000)||bases.Stamina256 is not(>=0 and<=16640000)))
        {throw new InvalidDataException("Invalid captured Revive resource bases");}
    }
    internal static ReviveNativeSetup Prepare(ReviveNativeSetup setup,ReviveOriginalState original)
    {
        var life=ReviveEquipmentLife.Prepare(setup,original);var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        ValidateBases(before.ResourceBases);
        if(before.ResourceBases is not{Issue:null,Mana256:not null,Stamina256:not null})
        {throw new NotSupportedException(before.ResourceBases?.Issue??"Historical original has no captured native resource bases");}
        foreach(var snapshot in ReviveBeforeDeath.Equipment(before))
        {var item=FrozenEquipmentEffects.Bind(snapshot);_=item.ResourceInput("mana",217,life.CombatPlan.Level);_=item.ResourceInput("stam",242,life.CombatPlan.Level);}
        return life with{EquipmentLifeProfile=Profile};
    }
    internal static void Validate(ReviveNativeState state)
    {
        try
        {if(state.Setup.EquipmentLifeProfile!=Profile||Prepare(state.Setup,state.Original)!=state.Setup){throw new InvalidDataException("Invalid frozen Revive resource profile");}}
        catch(Exception e)when(e is NotSupportedException or OverflowException){throw new InvalidDataException("Unavailable frozen Revive resources",e);}
    }
    internal static int? Maximum256(Mobile mobile,int resource)
    {
        if(World.Loading||resource is not(1 or 2)||mobile is not BaseCreature actor
            ||ReviveNativeActivation.State(actor)is not{Setup.EquipmentLifeProfile:Profile} state){return null;}
        var bases=ReviveEquipmentLife.ResourceBases(state);
        if(bases is not{Issue:null,Mana256:not null,Stamina256:not null}){throw new NotSupportedException("Missing opted-in Revive resource bases");}
        int level=(state.FacetPlan?.Combat??state.Setup.CombatPlan).Level,flat=0,coefficient=0,percent=0;
        string code=resource==2?"mana":"stam";int stat=resource==2?217:242;
        foreach(var item in ReviveEquipmentLife.Items(state))
        {var input=item.ResourceInput(code,stat,level);flat=checked(flat+input.Flat256);coefficient=checked(coefficient+input.LevelCoefficientRaw);if(resource==2){percent=checked(percent+item.Bonus("mana%",level));}}
        int first=resource==2?percent:BattleOrders.Percent(actor)+PaladinVigor.StaminaPercent(actor),second=resource==2?BattleOrders.Percent(actor):0;
        int value=NpcResourceValues.Calculate(new(1,resource==2?1:2,level,resource==2?bases.Mana256.Value:bases.Stamina256.Value,flat,coefficient,first,second));
        return Math.Clamp(value,0,65000*256);
    }
}
