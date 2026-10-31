// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record SkeletonBirthStats(int Level,int Life256,int Armor,int Attack,int Regeneration256,int FlatDamage,int DamagePercent,int RequestedEquipmentLevel);
internal static class NecromancerSkeletonBirth
{
    internal static void Require(PlayerMobile owner)
    {
        if(ClassGear.Bonus(owner,"pierce-immunity-damage")!=0)
        {throw new NotSupportedException("Raise Skeleton inherited physical Sunder delivery is not verified");}
        Preflight(ClassStats.Level(owner),SkeletonPets.Rank(owner),NamedSkills.Rank(owner,"necromancer.skeletonmastery"));
    }
    internal static void Preflight(int ownerLevel,int rank,int mastery)
    {
        foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
        {Prepare(ownerLevel,rank,mastery,difficulty);}
    }
    internal static SkeletonBirthStats Prepare(int ownerLevel,int rank,int mastery,MonsterDifficulty difficulty)
    {
        var row=SkillMechanics.For(CombatAbility.RaiseSkeleton);
        if(rank is <1 or >255||mastery is <0 or >255||row.SourceId!=70||row.Number("srvdofunc")!=31||row.Text("summon")!="necroskeleton"
            ||row.Text("passivestat1")!="maxhp"||row.Text("passivestat2")!="item_normaldamage"||row.Text("aurastat1")!="damagepercent"
            ||row.Text("aurastat2")!="tohit"||row.Text("aurastat3")!="armorclass"||NecromancerSummons.Source("necroskeleton","noRatio")!=1)
        {throw new NotSupportedException("Unmapped Raise Skeleton birth route");}
        var values=new SkillValues(row,key=>key=="Raise Skeleton"?rank:key=="Skeleton Mastery"?mastery:0,_=>0,ownerLevel);
        int F(string name)=>NecromancerSummons.Source("necroskeleton",name,difficulty);
        int initial=checked(F("minHP")*256);
        if(initial<=0||F("minHP")!=F("maxHP")){throw new NotSupportedException("Variable skeleton spawn life requires a source roll");}
        var resource=NecromancerMageBirth.Resources(initial,checked((int)values.Eval("passivecalc1")),checked((int)values.Eval("calc1")),NecromancerSummons.Source("necroskeleton","DamageRegen"));
        var basis=ValkyrieCreation.Base(ownerLevel,rank,(int)difficulty,true,new(initial,F("AC"),F("A1TH")));
        return new(basis.Level,resource.Life256,checked(basis.Defense+(int)values.Eval("aurastatcalc3")),checked(basis.Attack+(int)values.Eval("aurastatcalc2")),
            resource.Regeneration256,checked((int)values.Eval("passivecalc2")),checked((int)values.Eval("aurastatcalc1")),Math.Clamp(checked(3*rank),1,ownerLevel));
    }
}
