// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record BarrierBirthStats(int Level,int Life256,int Defense,int Regeneration256,int Physical,int Fire,int Cold,int Poison,int Lightning);
internal sealed record BarrierBirthState(CombatAbility Ability,int Rank,int Armor,int Other,MonsterDifficulty Difficulty,BarrierBirthStats Numbers);
internal static class BoneBarrierBirth
{
    internal static BarrierBirthState Capture(SkillValues values,MonsterDifficulty difficulty)
    {
        bool wall=values.Ability==CombatAbility.BoneWall;
        int armor=(int)values.Formula("skill('Bone Armor'.blvl)");
        int other=(int)values.Formula(wall?"skill('Bone Prison'.blvl)":"skill('Bone Wall'.blvl)");
        return new(values.Ability,values.Rank,armor,other,difficulty,Prepare(values.Ability,values.Rank,armor,other,difficulty));
    }
    internal static BarrierBirthStats Prepare(CombatAbility ability,int rank,int armor,int other,MonsterDifficulty difficulty)
    {
        if(ability is not(CombatAbility.BoneWall or CombatAbility.BonePrison)||rank is<1 or>255||armor is<0 or>255||other is<0 or>255
            ||difficulty is<MonsterDifficulty.Normal or>MonsterDifficulty.Hell){throw new NotSupportedException("Unsupported bone barrier birth domain");}
        var row=SkillMechanics.For(ability);bool wall=ability==CombatAbility.BoneWall;
        if(row.SourceId!=(wall?78:88)||row.Number("srvdofunc")!=(wall?60:62)||row.Text("summon")!="bonewall"
            ||row.Number("Param1")!=25||row.Number("Param8")!=(wall?10:8)||NecromancerSummons.Source("bonewall","noRatio")!=1)
        {throw new NotSupportedException("Unmapped bone barrier birth route");}
        int F(string field)=>NecromancerSummons.Source("bonewall",field,difficulty);
        int initial=checked(F("minHP")*256);
        if(initial<=0||F("minHP")!=F("maxHP")){throw new NotSupportedException("Variable bone barrier spawn life needs a source roll");}
        int percent=checked(25*(rank-1)+(armor+other)*(wall?10:8));
        // Do60/do62 call only the passive helper, not skeleton level/base
        // adjustment or Summon Resist. Reuse its exact MAXHP callback order.
        var resource=NecromancerMageBirth.Resources(initial,0,percent,NecromancerSummons.Source("bonewall","DamageRegen"));
        return new(F("Level"),resource.Life256,F("AC"),resource.Regeneration256,F("ResDm"),F("ResFi"),F("ResCo"),F("ResPo"),F("ResLi"));
    }
    internal static void Validate(BarrierBirthState state)
    {
        if(state==null||state.Numbers!=Prepare(state.Ability,state.Rank,state.Armor,state.Other,state.Difficulty))
        {throw new InvalidDataException("Bone barrier recipe and captured stats disagree");}
    }
}
