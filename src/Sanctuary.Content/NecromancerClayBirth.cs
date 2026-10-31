// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ClayBirthStats(int Level,int Life256,int Armor,int Attack,int Regeneration256,int VelocityPercent,int DamagePercent,int SlowPercent,int RequestedEquipmentLevel);
internal static class NecromancerClayBirth
{
    internal static void Require(PlayerMobile owner)
    {
        if(ClassGear.Bonus(owner,"pierce-immunity-damage")!=0)
        {throw new NotSupportedException("Clay Golem inherited physical Sunder delivery is not verified");}
        Preflight(SkillMechanics.Values(owner,CombatAbility.ClayGolem),ClassStats.Level(owner));
    }
    internal static void Preflight(SkillValues values,int ownerLevel)
    {
        foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
        {Prepare(values,ownerLevel,difficulty);}
    }
    internal static ClayBirthStats Prepare(SkillValues values,int ownerLevel,MonsterDifficulty difficulty)
    {
        var row=SkillMechanics.For(CombatAbility.ClayGolem);
        if(values.Ability!=CombatAbility.ClayGolem||values.Rank is <1 or >255||row.SourceId!=75||row.Number("srvdofunc")!=56
            ||row.Text("summon")!="ClayGolem"||row.Text("pettype")!="golem"||row.Text("summode")!="S1"
            ||row.Text("passivestat1")!="velocitypercent"||row.Text("passivestat2")!="tohit"||row.Text("passivestat3")!="damagepercent"
            ||row.Text("passivestat4")!="armorclass"||row.Text("passivestat5")!="item_pierce_damage_immunity"
            ||row.Text("aurastat1")!="item_slow"||row.Text("auraevent1")!="damagedinmelee"||row.Number("auraeventfunc1")!=27
            ||NecromancerSummons.Source("claygolem","noRatio")!=1||values.Eval("petmax")!=1)
        {throw new NotSupportedException("Unmapped Clay Golem birth route");}
        int F(string field)=>NecromancerSummons.Source("claygolem",field,difficulty);
        int initial=checked(F("minHP")*256);
        if(initial<=0||F("minHP")!=F("maxHP")){throw new NotSupportedException("Variable Clay Golem life requires a source roll");}
        var resource=NecromancerMageBirth.Resources(initial,0,checked((int)values.Eval("calc1")),NecromancerSummons.Source("claygolem","DamageRegen"));
        var basis=ValkyrieCreation.Base(ownerLevel,values.Rank,(int)difficulty,true,new(initial,F("AC"),F("A1TH")));
        return new(basis.Level,resource.Life256,checked(basis.Defense+(int)values.Eval("passivecalc4")),checked(basis.Attack+(int)values.Eval("passivecalc2")),
            resource.Regeneration256,checked((int)values.Eval("passivecalc1")),checked((int)values.Eval("passivecalc3")),checked((int)values.Eval("aurastatcalc1")),Math.Clamp(checked(3*values.Rank),1,ownerLevel));
    }
}
