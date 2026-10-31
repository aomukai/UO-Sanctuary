// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record BloodBirthStats(int Level,int Life256,int Armor,int Attack,int Regeneration256,int VelocityPercent,int DamagePercent,int RequestedEquipmentLevel);
internal static class NecromancerBloodBirth
{
    internal static void Require(PlayerMobile owner)
    {
        BloodGolemHealing.RequireCurrent();
        if(ClassGear.Bonus(owner,"pierce-immunity-damage")!=0)
        {throw new NotSupportedException("Blood Golem inherited physical Sunder delivery is not verified");}
        Preflight(SkillMechanics.Values(owner,CombatAbility.BloodGolem),ClassStats.Level(owner));
    }
    internal static void Preflight(SkillValues values,int ownerLevel)
    {
        foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
        {Prepare(values,ownerLevel,difficulty);}
    }
    internal static BloodBirthStats Prepare(SkillValues values,int ownerLevel,MonsterDifficulty difficulty)
    {
        var row=SkillMechanics.For(CombatAbility.BloodGolem);
        if(values.Ability!=CombatAbility.BloodGolem||values.Rank is <1 or >255||row.SourceId!=85||row.Number("srvdofunc")!=56
            ||row.Text("summon")!="BloodGolem"||row.Text("pettype")!="golem"||row.Text("summode")!="S1"
            ||row.Text("passivestat1")!="velocitypercent"||row.Text("passivestat2")!="tohit"||row.Text("passivestat3")!="damagepercent"
            ||row.Text("passivestat4")!="armorclass"||row.Text("passivestat5")!="item_pierce_damage_immunity"
            ||row.Text("auraevent1")!="domeleedamage"||row.Number("auraeventfunc1")!=23
            ||row.Text("auraevent2")!="damagedinmelee"||row.Number("auraeventfunc2")!=26
            ||row.Text("auraevent3")!="damagedbymissile"||row.Number("auraeventfunc3")!=26
            ||NecromancerSummons.Source("bloodgolem","noRatio")!=1||values.Eval("petmax")!=1)
        {throw new NotSupportedException("Unmapped Blood Golem birth route");}
        int F(string field)=>NecromancerSummons.Source("bloodgolem",field,difficulty);
        int initial=checked(F("minHP")*256);
        if(initial<=0||F("minHP")!=F("maxHP")){throw new NotSupportedException("Variable Blood Golem life requires a source roll");}
        var resource=NecromancerMageBirth.Resources(initial,0,checked((int)values.Eval("calc1")),NecromancerSummons.Source("bloodgolem","DamageRegen"));
        var basis=ValkyrieCreation.Base(ownerLevel,values.Rank,(int)difficulty,true,new(initial,F("AC"),F("A1TH")));
        return new(basis.Level,resource.Life256,checked(basis.Defense+(int)values.Eval("passivecalc4")),checked(basis.Attack+(int)values.Eval("passivecalc2")),
            resource.Regeneration256,checked((int)values.Eval("passivecalc1")),checked((int)values.Eval("passivecalc3")),Math.Clamp(checked(3*values.Rank),1,ownerLevel));
    }
}
