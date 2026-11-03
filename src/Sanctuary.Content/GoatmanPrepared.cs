// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;
internal sealed record GoatmanBase(string Difficulty,int Life,int MinimumDamage,int MaximumDamage,int Armor,int Attack);
internal sealed record GoatmanSuppliers(int Rank,int LifePercent,int AttackSpeed,int VelocityPercent,int AttackBonus,int DisplayAttackBonus,int DefenseBonus,
    int DamagePercent,int FlatDamage,int PhysicalResistBonus,int CrushingChance,int InheritedPhysical5,int InheritedPhysical7,int PetMaximum,int Mana256,int DelayFrames,
    int StunRank,int BerserkRank,int FrenzyRank,int CleaveRank);
internal sealed record GoatmanBirth(int OwnerLevel,int PetLevel,MonsterDifficulty Difficulty,int BaseLife256,int MaximumLife256,int MinimumDamage256,int MaximumDamage256,
    int BaseAttack,int LevelAttack,int BaseArmor,int LevelArmor,GoatmanSuppliers Suppliers)
{
    internal int ProjectedAttack=>checked(BaseAttack+LevelAttack+Suppliers.AttackBonus);
    internal int ProjectedArmor=>checked(BaseArmor+LevelArmor+Suppliers.DefenseBonus);
}
internal sealed record GoatmanSnapshot(GoatmanBirth Birth,int MasteryRank,int OathRank,int HardMastery,int HardDeathMark);

// Prepared creation/stat projection only. GenericPet, the extended passive
// writer, summon actor and learned cast remain unsupported. Duplicate sunder
// slots stay separate; no aggregation or current AI behavior is invented.
internal static class GoatmanPrepared
{
    internal static GoatmanSnapshot Capture(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)
            ||profile.Class!=DiabloClass.Warlock||ClassCreation.Profiles.GetRank(owner.Serial,"warlock.summongoatman")<1)
        {throw new InvalidOperationException("Goatman needs a live learned Warlock owner");}
        var values=SkillMechanics.Values(owner,CombatAbility.SummonGoatman);
        return new(Birth(values,SummonDifficulties.For(owner.Map),profile.Level,
            name=>name=="item_pierce_damage_immunity"?ClassGear.Bonus(owner,"pierce-immunity-damage")
                :throw new NotSupportedException("Unmapped inherited Goatman stat: "+name)),
            SkillMechanics.Rank(owner,CombatAbility.DemonicMastery),SkillMechanics.Rank(owner,CombatAbility.BloodOath),
            ClassCreation.Profiles.GetRank(owner.Serial,"warlock.demonicmastery"),ClassCreation.Profiles.GetRank(owner.Serial,"warlock.deathmark"));
    }
    internal static GoatmanSnapshot Rescale(GoatmanSnapshot snapshot,MonsterDifficulty difficulty)
    {
        ArgumentNullException.ThrowIfNull(snapshot);var birth=snapshot.Birth??throw new ArgumentException("Missing Goatman birth");
        if(snapshot.MasteryRank is <0 or >255||snapshot.OathRank is <0 or >255||snapshot.HardMastery is <0 or >20||snapshot.HardDeathMark is <0 or >20)
        {throw new ArgumentException("Invalid captured demon passive ranks");}
        if(birth.Suppliers.InheritedPhysical5!=birth.Suppliers.InheritedPhysical7)
        {throw new NotSupportedException("Independent inherited Goatman sunder fields need an explicit source supplier");}
        var values=new SkillValues(SkillMechanics.For(CombatAbility.SummonGoatman),
            key=>key=="Summon Goatman"?birth.Suppliers.Rank:key=="Demonic Mastery"?snapshot.MasteryRank:key=="Blood Oath"?snapshot.OathRank:0,
            key=>key=="Demonic Mastery"?snapshot.HardMastery:key=="Death Mark"?snapshot.HardDeathMark:0,birth.OwnerLevel);
        return snapshot with{Birth=Birth(values,difficulty,birth.OwnerLevel,_=>birth.Suppliers.InheritedPhysical5)};
    }
    internal static GenericPetParameters RawAiParameters=>GenericPetProfiles.Raw(CombatAbility.SummonGoatman);
    private static readonly Lazy<GoatmanBase[]> Bases=new(()=>
    {
        var rows=BalanceCatalog.Read<GoatmanBase>("goatman-base");
        if(rows.Length!=3||!rows.Select(x=>x.Difficulty).SequenceEqual(new[]{"Normal","Nightmare","Hell"})||rows.Any(x=>x.Life<=0||x.MinimumDamage<0||x.MaximumDamage<x.MinimumDamage))
        {throw new System.IO.InvalidDataException("Invalid Goatman bases");}return rows;
    });
    internal static GoatmanSuppliers Capture(SkillValues v,Func<string,int> accrued)
    {
        ArgumentNullException.ThrowIfNull(accrued);var s=SkillMechanics.For(CombatAbility.SummonGoatman);
        if(v.Ability!=CombatAbility.SummonGoatman||v.Rank is <1 or >255||s.SourceId!=373||s.Number("srvdofunc")!=56||s.Text("summon")!="wargoatman"||s.Text("pettype")!="demon"
            ||s.Text("summode")!="S1"||s.Text("aurastate")!="warlockdemonground"||s.Text("aurastat1")!="attackrate"||s.Text("auraevent1")!="domeleedamage"||s.Number("auraeventfunc1")!=16
            ||s.Text("passivestat5")!="item_pierce_damage_immunity"||s.Text("passivestat7")!="item_pierce_damage_immunity"||s.Text("passivestat12")!="item_crushingblow")
        {throw new NotSupportedException("Unsupported Goatman route");}
        int Eval(string field)=>checked((int)v.Eval(field));
        return new(v.Rank,Eval("calc1"),Eval("aurastatcalc1"),Eval("passivecalc1"),Eval("passivecalc2"),checked((int)v.Formula("(lvl*par8)+skill('Demonic Mastery'.ln56)")),
            Eval("passivecalc4"),Eval("passivecalc3"),Eval("passivecalc9"),Eval("passivecalc8"),Eval("passivecalc12"),checked((int)v.StatEval("passivecalc5",accrued)),
            checked((int)v.StatEval("passivecalc7",accrued)),Eval("petmax"),v.Mana256,s.Number("localdelay"),Eval("sumsk1calc"),Eval("sumsk2calc"),Eval("sumsk3calc"),Eval("sumsk4calc"));
    }
    internal static GoatmanBirth Birth(SkillValues values,MonsterDifficulty difficulty,int ownerLevel,Func<string,int> accrued)
    {
        if(ownerLevel is <1 or >99||!Enum.IsDefined(difficulty)){throw new NotSupportedException("Unsupported Goatman owner level/difficulty");}
        var v=Capture(values,accrued);var basis=Bases.Value[(int)difficulty];
        int level=Math.Min(ownerLevel,values.Rank+3*ownerLevel/4),life=checked(basis.Life*256);
        int min=checked((basis.MinimumDamage+v.FlatDamage)*256),max=checked((basis.MaximumDamage+v.FlatDamage)*256);
        // Ordinary no-item/no-strength NPC physical endpoints. Exact current
        // mode/critical/aura/skill packet composition remains separate.
        return new(ownerLevel,level,difficulty,life,checked(life+SourcePercentage.Calculate(life,v.LifePercent,100)),
            checked(min+SourcePercentage.Calculate(min,v.DamagePercent,100)),checked(max+SourcePercentage.Calculate(max,v.DamagePercent,100)),
            basis.Attack,NecromancerSummons.Source("level-"+level,"L-TH",difficulty),basis.Armor,NecromancerSummons.Source("level-"+level,"L-AC",difficulty),v);
    }
}
