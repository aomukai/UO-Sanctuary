// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record NpcItemSpellPacket(int Skill,int Rank,string Missile,string Element,int Minimum256,int Maximum256,
    int ColdFrames,int Emissions,int FirePierce,int ColdPierce,int LightningPierce);

// Numeric capture for the original Valkyrie's equipment proc spells.
// No selected/learned Sorceress skill exists on this NPC: explicit proc rank
// is not increased by +skills, and owner hard synergies/gear are not inputs.
// This does not execute a proc, allocate a missile or admit its item callback.
internal static class NpcItemSpellDamage
{
    internal static CombatAbility Ability(int skill)=>skill switch
    {
        36=>CombatAbility.FireBolt,38=>CombatAbility.ChargedBolt,39=>CombatAbility.IceBolt,44=>CombatAbility.FrostNova,
        48=>CombatAbility.Nova,49=>CombatAbility.Lightning,53=>CombatAbility.ChainLightning,66=>CombatAbility.AmplifyDamage,
        _=>throw new NotSupportedException("NPC item spell requires its own damage/execution adapter: "+skill)
    };
    internal static ItemExecutionMetadata Require(int skill)
    {
        if(skill==66){return ItemSkillExecution.RequireCurse(skill);}
        var ability=Ability(skill);var row=ItemSkillExecution.Skill(skill);var definition=SkillMechanics.For(ability);
        int function=skill switch{36 or 39 or 49=>0,38=>17,44 or 48=>22,_=>26};
        int effect=skill is 44 or 48?36:skill==53?151:1;
        string missile=skill switch{36=>"firebolt",38=>"chargedbolt",39=>"icebolt",44=>"frostnova",48=>"nova",49=>"lightningbolt",_=>"chainlightning"};
        if(row is not{Start:0,ItemTarget:0,ItemTargetDo:false,ItemCheckStart:false,InTown:false,UseManaOnDo:false,Lob:false,DecrementQuantity:false}
            ||row.Do!=function||row.ItemEffect!=effect||row.Animation!=(skill is 49 or 53?18:10)||row.Missile!=(function==0?ItemSkillExecution.MissileId(missile):-1)
            ||row.HasDelay("delay")||row.HasDelay("localdelay")||row.HasDelay("globaldelay")||row.HasDelay("perdelay")
            ||definition.SourceId!=skill||definition.Number("SrcDam")!=0||definition.Text(function==0?"srvmissile":"srvmissilea")!=missile
            ||NecromancerSummons.Source(missile,"SrcDamage")!=0||NecromancerSummons.Source(missile,"SrcMissDmg")!=0
            ||NecromancerSummons.Source(missile,"MissileSkill")!=0||NecromancerSummons.Source(missile,"ApplyMastery")!=0)
        {throw new NotSupportedException("NPC proc spell has unqualified execution/damage metadata");}
        return row;
    }
    internal static NpcItemSpellPacket Calculate(int skill,int rank,int level,int mastery,int firePierce=0,int coldPierce=0,int lightningPierce=0)
    {
        _=Require(skill);
        if(skill==66){throw new NotSupportedException("Original NPC curse uses its do30 effect,not a missile damage packet");}
        if(rank is<1 or>63||level is<1 or>99||mastery is<-100 or>10000)
        {throw new NotSupportedException("NPC proc damage needs its explicit six-bit rank,source level and supported mastery domain");}
        var definition=SkillMechanics.For(Ability(skill));
        var values=new SkillValues(definition,key=>key.Equals(definition.SourceKey,StringComparison.OrdinalIgnoreCase)?rank:0,_=>0,level);
        var endpoints=DruidFirestorm.Endpoints(values,mastery,SlowMissilePayload.Normal);
        int emissions=skill is 44 or 48?64:skill is 38 or 53?checked((int)values.Eval("calc1")):1;
        return new(skill,rank,definition.Text(skill is 36 or 39 or 49?"srvmissile":"srvmissilea"),definition.Text("EType"),
            endpoints.Min,endpoints.Max,skill is 39 or 44?values.DurationFrames:0,emissions,firePierce,coldPierce,lightningPierce);
    }
    internal static NpcItemSpellPacket CaptureValkyrie(AmazonValkyrieBody body,int skill,int rank)
    {
        // Also rejects unmaterialized item-granted skills: silently assuming
        // those new skill-list entries are absent would corrupt synergies.
        _=ValkyrieEquipmentDamage.Passives(body);var graph=body.EquipmentNumbers;
        int mastery=graph.Read(skill==36?329:skill is 39 or 44?331:330);
        return Calculate(skill,rank,body.BirthStats.Level,mastery,graph.Read(333),graph.Read(335),graph.Read(334));
    }
}
