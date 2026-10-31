// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record MageBirthStats(int Level,int Life256,int Armor,int Attack,int Regeneration256,int MissileRank,int RequestedEquipmentLevel);
// Current noRatio mage spawn plus the shared summon base/passive/MAXHP
// callbacks. Modern immunity-pierce consumers remain explicitly unsupported.
internal static class NecromancerMageBirth
{
    internal static void Require(PlayerMobile owner)
    {
        foreach(string element in new[]{"cold","fire","light","poison"})
        {if(ClassGear.Bonus(owner,"pierce-immunity-"+element)!=0){throw new NotSupportedException("Skeletal Mage inherited immunity-pierce delivery is not verified: "+element);}}
    }
    internal static MageBirthStats Prepare(SkillValues values,int ownerLevel,MonsterDifficulty difficulty)
    {
        var row=SkillMechanics.For(CombatAbility.RaiseSkeletalMage);
        if(values.Ability!=CombatAbility.RaiseSkeletalMage||values.Rank is <1 or >255||row.SourceId!=80||row.Number("srvdofunc")!=31
            ||row.Text("summon")!="necromage"||row.Text("passivestat1")!="maxhp"||row.Text("aurastat1")!="armorclass"
            ||row.Text("sumskill1")!="NecromageMissile"||NecromancerSummons.Source("necromage","noRatio")!=1)
        {throw new NotSupportedException("Unmapped Skeletal Mage birth route");}
        int baseLife=NecromancerSummons.Source("necromage","minHP",difficulty);
        if(baseLife<=0||baseLife!=NecromancerSummons.Source("necromage","maxHP",difficulty))
        {throw new NotSupportedException("Skeletal Mage variable spawn life needs its source roll");}
        int initial=checked(baseLife*256),flat=checked((int)values.Eval("passivecalc1"));
        int afterPassive=checked(initial+flat),life=checked(afterPassive+SourcePercentage.Calculate(afterPassive,checked((int)values.Eval("calc1")),100));
        var basis=ValkyrieCreation.Base(ownerLevel,values.Rank,(int)difficulty,true,new(initial,
            NecromancerSummons.Source("necromage","AC",difficulty),NecromancerSummons.Source("necromage","A1TH",difficulty)));
        int rate=NecromancerSummons.Source("necromage","DamageRegen");
        int regeneration=checked(initial*rate)>>12;
        regeneration=BattleOrders.RegenerationAfter(initial,afterPassive,initial,rate,regeneration);
        regeneration=BattleOrders.RegenerationAfter(afterPassive,life,afterPassive,rate,regeneration);
        return new(basis.Level,life,checked(basis.Defense+(int)values.Eval("aurastatcalc1")),basis.Attack,regeneration,
            checked((int)values.Eval("sumsk1calc")),Math.Clamp(checked(3*values.Rank),1,ownerLevel));
    }
}
