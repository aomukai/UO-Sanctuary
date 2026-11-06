// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
namespace Sanctuary;

// Mandatory supplied post-initializer resource context. The running modern
// initializer is not inferred from native UO's mana/stamina defaults.
internal sealed record ValkyrieResourceBirth(ValkyrieBirth Birth,MonsterDifficulty Difficulty,
    int Life256,int LifeMaximum256,int Mana256,int ManaMaximum256,int Stamina256,int StaminaMaximum256,int RegenerationBase);
internal static class ValkyrieEquipmentResources
{
    internal static void Validate(ValkyrieResourceBirth source)
    {
        if(source==null||source.LifeMaximum256 is<1 or>16640000||source.Life256<1||source.Life256>source.LifeMaximum256
            ||source.ManaMaximum256 is<0 or>16640000||source.Mana256<0||source.Mana256>source.ManaMaximum256
            ||source.StaminaMaximum256 is<0 or>16640000||source.Stamina256<0||source.Stamina256>source.StaminaMaximum256||source.RegenerationBase<0)
        {throw new InvalidDataException("Invalid supplied Valkyrie resource birth");}
        try{_=ValkyrieBirthNumbers.Calculate(source.Birth,source.Difficulty);}
        catch(Exception e)when(e is ArgumentException or NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unsupported captured Valkyrie resource identity",e);}
    }
    internal static EquipmentStatValue[] Basis(AmazonValkyrieBody body,ValkyrieResourceBirth source,MonsterDifficulty difficulty)
    {
        var fields=ValkyrieEquipmentStats.Basis(body,difficulty).ToDictionary(v=>v.Packed,v=>v.Value);
        foreach(var p in new[]{(6,source.Life256),(8,source.Mana256),(9,source.ManaMaximum256),(10,source.Stamina256),(11,source.StaminaMaximum256),(74,source.RegenerationBase)})
        {fields[EquipmentStatKey.Pack(p.Item1)]=p.Item2;}
        return fields.Where(p=>p.Value!=0).OrderBy(p=>p.Key).Select(p=>new EquipmentStatValue(p.Key,p.Value)).ToArray();
    }
    internal static void Require(AmazonValkyrieBody body,GeneratedNpcResourceState state,MonsterDifficulty? preview=null)
    {
        var source=state.Valkyrie;Validate(source);var saved=body.Prepared;
        if(saved is not{Equipment:not null,EquipmentStatProfile:ValkyrieEquipmentStats.Profile}||saved.Birth!=source.Birth
            ||(preview??saved.Difficulty)!=source.Difficulty||state.DamageRegeneration!=NecromancerSummons.Source("valkyrie","DamageRegen")
            ||!state.Basis.SequenceEqual(Basis(body,source,source.Difficulty)))
        {throw new NotSupportedException("Valkyrie resources lost their captured birth/numeric profile or source recipe");}
        _=ValkyrieEquipment.Originals(body);
    }
    internal static void Activate(AmazonValkyrieBody body,int mana256,int maximumMana256,int stamina256,int maximumStamina256,int regenerationBase)
    {
        if(body?.Prepared is not{Equipment:not null,EquipmentStatProfile:ValkyrieEquipmentStats.Profile} saved)
        {throw new NotSupportedException("Valkyrie source resources need the original numeric equipment profile");}
        var source=new ValkyrieResourceBirth(saved.Birth,saved.Difficulty,SkillHealth.Balance256(body),SkillHealth.Maximum256(body),
            mana256,maximumMana256,stamina256,maximumStamina256,regenerationBase);Validate(source);
        GeneratedNpcLiveResources.ActivateValkyrie(body,source);
    }
}
