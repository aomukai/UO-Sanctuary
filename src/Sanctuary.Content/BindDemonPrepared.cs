// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record BindDemonMonster(string Monster,int SourceIndex,string IndexScope,ImmutableArray<string> Calcs,string BindChance,
    string BoundStat,string BoundFormula,int UnderlingCount,ImmutableArray<string> ConsumeStats,ImmutableArray<string> ConsumeParameters,ImmutableArray<string> ConsumeFormulas);
internal sealed record BindDemonSuppliers(int Rank,int HardRank,int MasteryRank,int HardMastery,int OathRank,int HardDeathMark,
    int AttackSpeed,int VelocityPercent,int AttackBonus,int DamagePercent,int Armor,int FlatDamage,int SummonResist,int PhysicalResist,
    int InheritedPhysical5,int InheritedPhysical7,int BoundMaximum,int SharedMaximum,int StartMana,int Mana256,int ChanceSkillTerm,
    int Modifier5,int Modifier10,int Modifier15,int Modifier20);

// Captured skill/stat suppliers only. Raw monpet formulas are deliberately not
// promoted to an invented chance evaluator,world target gate or bound actor.
internal static class BindDemonPrepared
{
    private static readonly Lazy<BindDemonMonster[]> Rows=new(()=>
    {
        var rows=BalanceCatalog.Read<BindDemonMonster>("bind-demon-monsters");
        if(rows.Length!=171||rows.Select(r=>r.Monster).Distinct(StringComparer.Ordinal).Count()!=171
            ||rows.Count(r=>r.IndexScope=="MonsterClass")!=149||rows.Count(r=>r.IndexScope=="NamedEncounter")!=22
            ||rows.Any(r=>r==null||r.SourceIndex<0||r.Calcs.IsDefault||r.Calcs is not{Length:5}||r.Calcs.Any(string.IsNullOrWhiteSpace)
                ||string.IsNullOrWhiteSpace(r.BindChance)||r.BoundStat!="maxhp"||string.IsNullOrWhiteSpace(r.BoundFormula)
                ||r.UnderlingCount is not(0 or 5 or 7)||r.ConsumeStats.IsDefault||r.ConsumeParameters.IsDefault||r.ConsumeFormulas.IsDefault
                ||r.ConsumeStats is not{Length:5}||r.ConsumeParameters is not{Length:5}||r.ConsumeFormulas is not{Length:5}))
        {throw new InvalidDataException("Invalid pinned Bind Demon monster catalog");}return rows;
    });
    internal static BindDemonMonster Monster(string name)
        =>Rows.Value.SingleOrDefault(r=>r.Monster==name)??throw new NotSupportedException("No pinned Bind Demon monster row: "+name);
    internal static BindDemonMonster[] Catalog()=>Rows.Value.ToArray();
    internal static BindDemonSuppliers Capture(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Warlock
            ||ClassCreation.Profiles.GetRank(owner.Serial,"warlock.binddemon")<1)
        {throw new InvalidOperationException("Bind Demon suppliers need a living trained Warlock");}
        return Capture(SkillMechanics.Values(owner,CombatAbility.BindDemon),name=>name=="item_pierce_damage_immunity"?ClassGear.Bonus(owner,"pierce-immunity-damage")
            :throw new NotSupportedException("Unmapped inherited Bind Demon stat: "+name));
    }
    internal static BindDemonSuppliers Capture(SkillValues v,Func<string,int> accrued)
    {
        ArgumentNullException.ThrowIfNull(v);ArgumentNullException.ThrowIfNull(accrued);var s=SkillMechanics.For(CombatAbility.BindDemon);
        int Ref(string key,string property)=>checked((int)v.Reference("skill",key,property));
        int hard=checked((int)v.Reference("blvl",null,null)),mastery=Ref("Demonic Mastery","lvl"),hardMastery=Ref("Demonic Mastery","blvl"),oath=Ref("Blood Oath","lvl"),death=Ref("Death Mark","blvl");
        if(v.Ability!=CombatAbility.BindDemon||v.Rank is<1 or>255||hard is<0 or>20||mastery is<0 or>255||hardMastery is<0 or>20||oath is<0 or>255||death is<0 or>20
            ||s.SourceId!=382||s.Number("srvstfunc")!=64||s.Number("srvdofunc")!=158||s.Text("srvmissilea")!="binddemonchannelmaker"
            ||s.Text("pettype")!="binddemon"||s.Number("petmax")!=1||s.Number("startmana")!=9||s.Number("manashift")!=2
            ||s.Text("auratargetstate")!="bind_demon_underling"||s.Text("passivestat5")!="item_pierce_damage_immunity"
            ||s.Text("passivestat7")!="item_pierce_damage_immunity"||s.Text("passivestat9")!="item_normaldamage")
        {throw new NotSupportedException("Unqualified Bind Demon supplier context");}
        int Eval(string field)=>checked((int)v.Eval(field));
        var pool=new SkillValues(SkillMechanics.For(CombatAbility.SummonGoatman),key=>key=="Demonic Mastery"?mastery:1,
            key=>key=="Demonic Mastery"?hardMastery:0,99);
        return new(v.Rank,hard,mastery,hardMastery,oath,death,Eval("aurastatcalc1"),Eval("passivecalc1"),Eval("passivecalc2"),Eval("passivecalc3"),
            Eval("passivecalc4"),Eval("passivecalc9"),Eval("passivecalc6"),Eval("passivecalc8"),
            checked((int)v.StatEval("passivecalc5",accrued)),checked((int)v.StatEval("passivecalc7",accrued)),Eval("petmax"),checked((int)pool.Eval("petmax")),
            s.Number("startmana"),v.Mana256,checked((int)v.Formula("dm34+skill('Death Mark'.blvl)")),Eval("calc2"),Eval("calc3"),Eval("calc4"),Eval("calc5"));
    }
    internal static void Validate(BindDemonSuppliers saved)
    {
        if(saved==null){throw new InvalidDataException("Missing Bind Demon suppliers");}
        var v=new SkillValues(SkillMechanics.For(CombatAbility.BindDemon),key=>key switch
            {"Bind Demon"=>saved.Rank,"Demonic Mastery"=>saved.MasteryRank,"Blood Oath"=>saved.OathRank,_=>0},
            key=>key switch{"Bind Demon"=>saved.HardRank,"Demonic Mastery"=>saved.HardMastery,"Death Mark"=>saved.HardDeathMark,_=>0},99);
        int calls=0;
        try
        {
            if(Capture(v,_=>++calls==1?saved.InheritedPhysical5:saved.InheritedPhysical7)!=saved||calls!=2)
            {throw new InvalidDataException("Bind Demon suppliers differ from captured ranks/stats");}
        }
        catch(Exception e)when(e is ArgumentException or NotSupportedException or OverflowException)
        {throw new InvalidDataException("Invalid Bind Demon supplier capture",e);}
    }
}
