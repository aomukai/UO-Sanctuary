// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record FireBirthStats(int Level,int Life256,int Armor,int Attack,int Regeneration256,int VelocityPercent,int FireResistance,int FireAbsorb,int FireMinimum,int FireMaximum,int HolyFireRank,int RequestedEquipmentLevel);
internal static class NecromancerFireBirth
{
    internal static void Require(PlayerMobile owner)
    {
        if(ClassGear.Bonus(owner,"pierce-immunity-fire")!=0)
        {throw new NotSupportedException("Fire Golem inherited fire Sunder delivery is not verified");}
        Preflight(SkillMechanics.Values(owner,CombatAbility.FireGolem),ClassStats.Level(owner));
    }
    internal static void Preflight(SkillValues values,int ownerLevel)
    {
        foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
        {Prepare(values,ownerLevel,difficulty);}
    }
    internal static FireBirthStats Prepare(SkillValues values,int ownerLevel,MonsterDifficulty difficulty)
    {
        var row=SkillMechanics.For(CombatAbility.FireGolem);
        if(values.Ability!=CombatAbility.FireGolem||values.Rank is <1 or >255||row.SourceId!=94||row.Number("srvdofunc")!=56
            ||row.Text("summon")!="FireGolem"||row.Text("pettype")!="golem"||row.Text("summode")!="S1"
            ||row.Text("passivestat1")!="velocitypercent"||row.Text("passivestat2")!="tohit"||row.Text("passivestat3")!="armorclass"
            ||row.Text("passivestat4")!="item_pierce_fire_immunity"||row.Text("aurastat1")!="fireresist"||row.Text("aurastat2")!="item_absorbfire_percent"
            ||row.Text("aurastat3")!="firemindam"||row.Text("aurastat4")!="firemaxdam"||!row.Text("sumskill1").Equals("holy fire",StringComparison.OrdinalIgnoreCase)
            ||NecromancerSummons.Source("firegolem","noRatio")!=1||values.Eval("petmax")!=1)
        {throw new NotSupportedException("Unmapped Fire Golem birth route");}
        int F(string field)=>NecromancerSummons.Source("firegolem",field,difficulty);
        int initial=checked(F("minHP")*256);
        if(initial<=0||F("minHP")!=F("maxHP")){throw new NotSupportedException("Variable Fire Golem life requires a source roll");}
        var resource=NecromancerMageBirth.Resources(initial,0,checked((int)values.Eval("calc1")),NecromancerSummons.Source("firegolem","DamageRegen"));
        var basis=ValkyrieCreation.Base(ownerLevel,values.Rank,(int)difficulty,true,new(initial,F("AC"),F("A1TH")));
        return new(basis.Level,resource.Life256,checked(basis.Defense+(int)values.Eval("passivecalc3")),checked(basis.Attack+(int)values.Eval("passivecalc2")),
            resource.Regeneration256,checked((int)values.Eval("passivecalc1")),checked(F("ResFi")+(int)values.Eval("aurastatcalc1")),checked((int)values.Eval("aurastatcalc2")),
            checked((int)values.Eval("aurastatcalc3")),checked((int)values.Eval("aurastatcalc4")),checked((int)values.Eval("sumsk1calc")),Math.Clamp(checked(3*values.Rank),1,ownerLevel));
    }
}
