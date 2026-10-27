// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record DireWolfBirth(int Rank,int Level,int Life256,int BaseDefense,int BaseAttack,int Regeneration256,
    int ArmorPercent,int AttackBonus,int DamagePercent,int FireResistance,int ColdResistance,int LightningResistance,
    int PoisonResistance,int PhysicalSunder,int RequestedEquipmentLevel);
internal sealed record DireWolfSnapshot(DireWolfBirth Birth,int SpiritRank,int GrizzlyRank,bool Expansion);

// do119 birth and copied passive composition. Attack-mode owner-derived
// endpoints/AR and corpse rage are separate consumers, not birth fields.
internal static class DruidDireWolfStats
{
    private static void Route(SkillValues values)
    {
        var row=SkillMechanics.For(CombatAbility.SummonDireWolf);
        if(values.Ability!=CombatAbility.SummonDireWolf||values.Rank is <1 or >255||row.SourceId!=237
            ||row.Number("srvdofunc")!=119||row.Text("summon")!="fenris"||row.Text("pettype")!="fenris"
            ||values.Mana256!=20*256||row.Text("passivestat3")!="damagepercent"
            ||row.Text("passivestat4")!="item_pierce_damage_immunity"||row.Text("passivestat5")!="0")
        {throw new NotSupportedException("Unsupported Dire Wolf source route");}
    }
    internal static SpiritWolfSpawn Spawn(MonsterDifficulty difficulty)
    {
        if(difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell){throw new ArgumentOutOfRangeException(nameof(difficulty));}
        int F(string name)=>NecromancerSummons.Source("fenris",name,difficulty);
        if(NecromancerSummons.Source("fenris","noRatio")!=1||F("minHP")!=216||F("maxHP")!=216||F("AC")!=116||F("A1TH")!=150)
        {throw new NotSupportedException("Unsupported Dire Wolf noRatio spawn");}
        int life=checked(F("minHP")*256);
        // The shared initializer flags7 do not write the weapon-AR lane.
        return new(new(life,F("AC"),0),checked(life*NecromancerSummons.Source("fenris","DamageRegen"))>>12);
    }
    internal static DireWolfBirth Birth(SkillValues values,MonsterDifficulty difficulty,bool expansion,Func<string,int> accrued)
        =>Apply(values,difficulty,expansion,Spawn(difficulty),accrued);
    internal static DireWolfBirth Apply(SkillValues values,MonsterDifficulty difficulty,bool expansion,SpiritWolfSpawn spawn,Func<string,int> accrued)
    {
        Route(values);ArgumentNullException.ThrowIfNull(accrued);
        if(spawn?.Stats is not{Life256:>0,Defense:>=0,Attack:>=0}||spawn.Regeneration256<0)
        {throw new ArgumentException("Invalid supplied Dire Wolf spawn");}
        int ownerLevel=checked((int)values.Formula("ulvl"));
        if(ownerLevel is <1 or >99){throw new ArgumentOutOfRangeException(nameof(values));}
        var basis=ValkyrieCreation.BaseAtLevel(Math.Max(1,checked((int)values.Eval("calc2"))),(int)difficulty,expansion,spawn.Stats);
        int life=checked(spawn.Stats.Life256+SourcePercentage.Calculate(spawn.Stats.Life256,checked((int)values.Eval("calc1")),100));
        return new(values.Rank,basis.Level,life,basis.Defense,basis.Attack,spawn.Regeneration256,
            checked((int)values.Eval("passivecalc1")),checked((int)values.Eval("passivecalc2")),checked((int)values.Eval("passivecalc3")),
            checked((int)values.Eval("aurastatcalc1")),checked((int)values.Eval("aurastatcalc2")),
            checked((int)values.Eval("aurastatcalc3")),checked((int)values.Eval("aurastatcalc4")),
            checked((int)values.StatEval("passivecalc4",accrued)),Math.Clamp(checked(3*values.Rank),1,ownerLevel));
    }
    internal static int Limit(SkillValues values){Route(values);return checked((int)values.Eval("petmax"));}
    internal static DireWolfSnapshot Capture(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)
            ||profile.Class!=DiabloClass.Druid||SkillMechanics.Rank(owner,CombatAbility.SummonDireWolf)<1)
        {throw new InvalidOperationException("Dire Wolf needs a live learned Druid owner");}
        return new(Birth(SkillMechanics.Values(owner,CombatAbility.SummonDireWolf),SummonDifficulties.For(owner.Map),true,
            name=>name=="item_pierce_damage_immunity"?ClassGear.Bonus(owner,"pierce-immunity-damage")
                :throw new NotSupportedException("Unmapped inherited Dire Wolf stat: "+name)),
            SkillMechanics.Rank(owner,CombatAbility.SummonSpiritWolf),SkillMechanics.Rank(owner,CombatAbility.SummonGrizzly),true);
    }
    internal static DireWolfSnapshot Rescale(DireWolfSnapshot snapshot,MonsterDifficulty difficulty)
    {
        ArgumentNullException.ThrowIfNull(snapshot);var birth=snapshot.Birth??throw new ArgumentException("Missing Dire Wolf birth");
        if(snapshot.SpiritRank is <0 or >255||snapshot.GrizzlyRank is <0 or >255){throw new ArgumentException("Invalid captured wolf/bear ranks");}
        var values=new SkillValues(SkillMechanics.For(CombatAbility.SummonDireWolf),
            key=>key=="Summon Fenris"?birth.Rank:key=="Summon Spirit Wolf"?snapshot.SpiritRank:key=="Summon Grizzly"?snapshot.GrizzlyRank:0,_=>0,birth.Level);
        return snapshot with{Birth=Birth(values,difficulty,snapshot.Expansion,_=>birth.PhysicalSunder)};
    }
}
