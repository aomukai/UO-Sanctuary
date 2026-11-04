// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public sealed record ReviveAttackDefenseBases(int Version,string Kind,int? AttackRating,int? Defense,string Issue=null);

// Frozen item additions use the existing NPC consumers. Native attributes are
// captured separately from equipment; authored difficulty fields remain live.
internal static class ReviveEquipmentCombat
{
    internal const string Profile="frozen-equipment-combat-v1";
    private const string Attributes="native-attributes",SourceBody="source-body";
    private static bool HasSourceBody(BaseCreature actor)=>actor is GraveyardUndead or AssassinSentryBody or BladeSentinelBody
        or SkeletonGuardian or BoneBarrierSegment or AmazonDecoy;
    internal static ReviveAttackDefenseBases Capture(BaseCreature actor,FrozenEquipmentSnapshot[] equipment)
    {
        try
        {
            if(HasSourceBody(actor)){return new(0,SourceBody,CreatureProfiles.NativeAttack(actor),CreatureProfiles.NativeDefense(actor));}
            if(actor.GetType().GetProperty(nameof(Mobile.Dex))?.GetMethod?.DeclaringType!=typeof(Mobile)
                ||actor.GetType().GetProperty(nameof(Mobile.ArmorRating))?.GetMethod?.DeclaringType!=typeof(Mobile)||actor.ArmorRating!=0)
            {throw new NotSupportedException("Custom native Dexterity/armor requires a separate gear-free combat basis");}
            int offset=equipment.Sum(e=>actor.GetStatMod(((Serial)e.OriginalItem)+"Dex")?.Offset??0);
            if(actor.GetStatOffset(StatType.Dex)!=offset)
            {throw new NotSupportedException("Non-equipment native Dexterity modifiers need their own combat restoration policy");}
            int dex=Math.Clamp(actor.RawDex,0,65000);
            return new(0,Attributes,Math.Max(5,checked(dex*2)),dex/4);
        }
        catch(Exception e)when(e is NotSupportedException or OverflowException)
        {return new(0,null,null,null,e.Message);}
    }
    internal static void ValidateBases(ReviveAttackDefenseBases basis,ReviveCreatureSnapshot creature)
    {
        if(basis==null){return;}
        if(basis.Version!=0||basis.Issue!=null&&(string.IsNullOrWhiteSpace(basis.Issue)||basis.Kind!=null||basis.AttackRating!=null||basis.Defense!=null)
            ||basis.Issue==null&&(basis.Kind is not(Attributes or SourceBody)||basis.AttackRating is not>=0||basis.Defense is not>=0))
        {throw new InvalidDataException("Invalid captured Revive attack/defense bases");}
        if(basis.Issue==null&&basis.Kind==Attributes)
        {
            if(creature==null){throw new InvalidDataException("Missing native combat base descriptor");}
            int dex=Math.Clamp(creature.RawDexterity,0,65000);
            if(basis.AttackRating!=Math.Max(5,dex*2)||basis.Defense!=dex/4)
            {throw new InvalidDataException("Native combat bases disagree with captured raw Dexterity");}
        }
    }
    internal static ReviveNativeSetup Prepare(ReviveNativeSetup setup,ReviveOriginalState original)
    {
        var resources=ReviveEquipmentResources.Prepare(setup,original);var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        ValidateBases(before.AttackDefenseBases,before.Creature);
        if(before.AttackDefenseBases is not{Issue:null})
        {throw new NotSupportedException(before.AttackDefenseBases?.Issue??"Historical original has no gear-free attack/defense bases");}
        foreach(var snapshot in ReviveBeforeDeath.Equipment(before))
        {
            var item=FrozenEquipmentEffects.Bind(snapshot);int level=resources.CombatPlan.Level;
            _=item.At(level).Defense;_=item.Bonus("dex",level);_=item.Bonus("att",level);_=item.Bonus("att%",level);
        }
        return resources with{EquipmentLifeProfile=Profile};
    }
    internal static void Validate(ReviveNativeState state)
    {
        try
        {if(state.Setup.EquipmentLifeProfile!=Profile||Prepare(state.Setup,state.Original)!=state.Setup){throw new InvalidDataException("Invalid frozen Revive combat profile");}}
        catch(Exception e)when(e is NotSupportedException or OverflowException){throw new InvalidDataException("Unavailable frozen Revive combat suppliers",e);}
    }
    private static ReviveNativeState State(BaseCreature actor)=>!World.Loading&&ReviveNativeActivation.State(actor)is{Setup.EquipmentLifeProfile:Profile} state?state:null;
    private static int Basis(BaseCreature actor,ReviveNativeState state,bool defense)
    {
        var row=CreatureProfiles.For(actor);
        if(!defense&&ExtraStrongAttackPercent(actor)!=0&&actor is EncounterCreature strong)
        {return strong.RolledAttack==0?strong.Basis.AttackRating.Min:strong.RolledAttack;}
        if(row.ApplyStats&&(defense?row.Defense:row.Attack1.AttackRating)is{} authored){return authored;}
        var basis=ReviveEquipmentLife.AttackDefenseBases(state);
        if(basis is not{Issue:null}){throw new NotSupportedException("Missing captured Revive combat bases");}
        if(basis.Kind==SourceBody&&HasSourceBody(actor)){return defense?CreatureProfiles.NativeDefense(actor):CreatureProfiles.NativeAttack(actor);}
        if(basis.Kind!=Attributes||HasSourceBody(actor)){throw new NotSupportedException("Native Revive combat basis does not match its body");}
        return(defense?basis.Defense:basis.AttackRating).Value;
    }
    internal static int? AttackBasis(BaseCreature actor)=>State(actor)is{} state?Basis(actor,state,false):null;
    internal static int? DefenseBasis(BaseCreature actor)=>State(actor)is{} state?Basis(actor,state,true):null;
    private static int Bonus(ReviveNativeState state,string code)
    {
        int level=(state.FacetPlan?.Combat??state.Setup.CombatPlan).Level,total=0;
        foreach(var item in ReviveEquipmentLife.Items(state)){total=checked(total+item.Bonus(code,level));}return total;
    }
    internal static int? AttackFlat(BaseCreature actor)=>State(actor)is{} state?checked(Bonus(state,"att")+5*Bonus(state,"dex")):null;
    internal static int ExtraStrongAttackPercent(BaseCreature actor)=>actor is EncounterCreature e&&e.Grade is EncounterGrade.Ordinary or EncounterGrade.Minion
        &&e.Inherits(EliteModifier.ExtraStrong)?EncounterProfiles.ExtraStrong(MonsterVariants.Difficulty(e),e.Grade==EncounterGrade.Minion).AttackPercent:0;
    internal static int? AttackPercent(BaseCreature actor)=>State(actor)is{} state?checked(Bonus(state,"att%")+ExtraStrongAttackPercent(actor)):null;
    internal static int? Defense(BaseCreature actor)
    {
        if(State(actor)is not{} state){return null;}
        int level=(state.FacetPlan?.Combat??state.Setup.CombatPlan).Level,armor=0;
        foreach(var item in ReviveEquipmentLife.Items(state)){armor=checked(armor+item.At(level).Defense);}
        return checked(armor+Bonus(state,"dex")/4);
    }
}
