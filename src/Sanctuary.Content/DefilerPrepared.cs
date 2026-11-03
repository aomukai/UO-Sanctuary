// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record DefilerSuppliers(int Rank,int LifePercent,int AttackSpeed,int VelocityPercent,int DefenseBonus,
    int PhysicalSunder4,int PhysicalResistBonus,int PhysicalSunder7,int PetMaximum,int Mana256,int DelayFrames,int HealthLinkRank,int SelfSkillRank);
internal sealed record DefilerBirth(DemonBirthVitals Vitals,DefilerSuppliers Suppliers)
{
    internal int ProjectedAttack=>checked(Vitals.BaseAttack+Vitals.LevelAttack);
    internal int ProjectedArmor=>checked(Vitals.BaseArmor+Vitals.LevelArmor+Suppliers.DefenseBonus);
}
internal sealed record DefilerSnapshot(DefilerBirth Birth,int MasteryRank,int OathRank,int HardMastery);

// Independent vital suppliers only. The raw damage expression failed the
// older compiler's checked leading-symbol path. Do not normalize it here or
// manufacture a multiplier; offense and Health Link remain gated.
internal static class DefilerPrepared
{
    internal static DefilerSuppliers Suppliers(SkillValues values,Func<string,int> accrued)
    {
        ArgumentNullException.ThrowIfNull(values);ArgumentNullException.ThrowIfNull(accrued);var row=SkillMechanics.For(CombatAbility.SummonDefiler);
        if(values.Ability!=row.Ability||values.Rank is <1 or >255||row.SourceId!=377||row.Number("srvdofunc")!=56
            ||row.Text("summon")!="warputriddefiler"||row.Text("summode")!="S3"||row.Text("pettype")!="demon"
            ||row.Text("aurastate")!="warlockdemonground"||row.Text("aurastat1")!="attackrate"
            ||row.Text("passivestat1")!="velocitypercent"||row.Text("passivestat2")!="damagepercent"
            ||row.Text("passivecalc2")!="par2*((lvl - 1) + (skill('Demonic Mastery'.blvl))"
            ||row.Text("passivestat3")!="armorclass"||row.Text("passivestat4")!="item_pierce_damage_immunity"
            ||row.Text("passivestat6")!="damageresist"||row.Text("passivestat7")!="item_pierce_damage_immunity"
            ||row.Text("sumskill1")!="Health Link"||row.Text("sumskill2")!="Summon Defiler")
        {throw new NotSupportedException("Unsupported Defiler vital supplier route");}
        int Eval(string field)=>checked((int)values.Eval(field));
        return new(values.Rank,Eval("calc1"),Eval("aurastatcalc1"),Eval("passivecalc1"),Eval("passivecalc3"),
            checked((int)values.StatEval("passivecalc4",accrued)),Eval("passivecalc6"),checked((int)values.StatEval("passivecalc7",accrued)),
            Eval("petmax"),values.Mana256,row.Number("localdelay"),Eval("sumsk1calc"),Eval("sumsk2calc"));
    }
    internal static DefilerBirth Birth(SkillValues values,MonsterDifficulty difficulty,int ownerLevel,Func<string,int> accrued)
    {
        var suppliers=Suppliers(values,accrued);
        return new(DemonBirthNumbers.Create("warputriddefiler",values.Rank,ownerLevel,difficulty,suppliers.LifePercent),suppliers);
    }
    internal static DefilerSnapshot Capture(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Warlock
            ||ClassCreation.Profiles.GetRank(owner.Serial,"warlock.summondefiler")<1)
        {throw new InvalidOperationException("Defiler needs a live trained Warlock owner");}
        return new(Birth(SkillMechanics.Values(owner,CombatAbility.SummonDefiler),SummonDifficulties.For(owner.Map),profile.Level,
            stat=>stat=="item_pierce_damage_immunity"?ClassGear.Bonus(owner,"pierce-immunity-damage")
                :throw new NotSupportedException("Unmapped Defiler inherited stat")),SkillMechanics.Rank(owner,CombatAbility.DemonicMastery),
            SkillMechanics.Rank(owner,CombatAbility.BloodOath),ClassCreation.Profiles.GetRank(owner.Serial,"warlock.demonicmastery"));
    }
    internal static DefilerSnapshot Rescale(DefilerSnapshot snapshot,MonsterDifficulty difficulty)
    {
        if(snapshot?.Birth?.Vitals==null||snapshot.Birth.Suppliers==null||snapshot.MasteryRank is <0 or >255||snapshot.OathRank is <0 or >255
            ||snapshot.HardMastery is <0 or >20||snapshot.Birth.Suppliers.PhysicalSunder4!=snapshot.Birth.Suppliers.PhysicalSunder7)
        {throw new InvalidDataException("Invalid Defiler captured context");}
        var original=snapshot.Birth;var values=new SkillValues(SkillMechanics.For(CombatAbility.SummonDefiler),
            key=>key=="Summon Defiler"?original.Suppliers.Rank:key=="Demonic Mastery"?snapshot.MasteryRank:key=="Blood Oath"?snapshot.OathRank:0,
            key=>key=="Demonic Mastery"?snapshot.HardMastery:0,original.Vitals.OwnerLevel);
        int Accrued(string stat)=>stat=="item_pierce_damage_immunity"?original.Suppliers.PhysicalSunder4:throw new NotSupportedException("Unmapped captured Defiler stat");
        if(Birth(values,original.Vitals.Difficulty,original.Vitals.OwnerLevel,Accrued)!=original){throw new InvalidDataException("Inconsistent Defiler vital capture");}
        return snapshot with{Birth=Birth(values,difficulty,original.Vitals.OwnerLevel,Accrued)};
    }
}
