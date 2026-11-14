// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ConsumeBonusRow(string Monster,int SourceIndex,string FirstStat,string FirstFormula,string SecondStat,string SecondFormula,int UnderlingCount);
internal sealed record ConsumeBonusValues(int DamagePercent=0,int CrushingChance=0,int FireMastery=0,int FirePierce=0,int MagicMastery=0,int MagicPierce=0);
internal sealed record ConsumeSuppliers(int Rank,int HardDeathMark,string Monster,MonsterDifficulty Difficulty,int Mana256,int DurationFrames,
    int LifePercent,int VelocityPercent,ConsumeBonusValues Bonus);

// Numerical capture only. In this explicit source context,sksrc means the
// supplied caster's Consume rank/Death Mark hard points;diff uses1/2/3.
// No consumed pet,owner buff,stat writer,pool entry or learned input is added.
internal static class ConsumePrepared
{
    private static readonly Lazy<ConsumeBonusRow[]> Rows=new(()=>
    {
        var rows=BalanceCatalog.Read<ConsumeBonusRow>("consume-bonuses");
        if(rows.Length!=3||!rows.Select(r=>r.Monster).SequenceEqual(new[]{"wargoatman","warbighead","warputriddefiler"})
            ||!rows.Select(r=>r.SourceIndex).SequenceEqual(new[]{742,743,744})||rows.Any(r=>r.UnderlingCount!=0))
        {throw new InvalidDataException("Unsupported Consume monster bonus table");}return rows;
    });
    internal static ConsumeSuppliers Capture(PlayerMobile owner,string monster,MonsterDifficulty difficulty)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var p)||p.Class!=DiabloClass.Warlock
            ||SkillMechanics.Rank(owner,CombatAbility.Consume)<1)
        {throw new InvalidOperationException("Consume needs a living Warlock with an available skill rank");}
        return Capture(SkillMechanics.Values(owner,CombatAbility.Consume),ClassCreation.Profiles.GetRank(owner.Serial,"warlock.deathmark"),monster,difficulty);
    }
    internal static ConsumeSuppliers Capture(SkillValues values,int hardDeathMark,string monster,MonsterDifficulty difficulty)
    {
        ArgumentNullException.ThrowIfNull(values);var row=SkillMechanics.For(CombatAbility.Consume);
        if(values.Ability!=CombatAbility.Consume||values.Rank is<1 or>255||hardDeathMark is<0 or>20||!Enum.IsDefined(difficulty)
            ||row.SourceId!=381||row.Number("srvstfunc")!=69||row.Number("srvdofunc")!=159||row.Text("aurastate")!="consume"
            ||row.Text("auralencalc")!="ln12"||row.Text("aurastat1")!="item_maxhp_percent"||row.Text("aurastat2")!="velocitypercent"
            ||row.Number("TargetPet")!=1||row.Number("InTown")!=1||row.Number("leftskill")!=0||row.Number("rightskill")!=1)
        {throw new NotSupportedException("Unqualified Consume supplier context");}
        var source=Rows.Value.SingleOrDefault(r=>r.Monster==monster)??throw new NotSupportedException("No qualified Consume bonus for monster: "+monster);
        int rank=values.Rank;
        int Eval(string expression)=>checked((int)SkillFormula.Evaluate(expression.Replace("sksrc(","skill(",StringComparison.Ordinal),
            (name,argument,property)=>name=="diff"?(int)difficulty+1:name=="skill"&&argument=="Consume"&&property=="lvl"?rank
                :name=="skill"&&argument=="Death Mark"&&property=="blvl"?hardDeathMark
                :throw new NotSupportedException("Unmapped Consume source context: "+name+"/"+argument+"/"+property)));
        var bonus=new ConsumeBonusValues();
        void Put(string stat,int amount)
        {
            if(amount<0){throw new NotSupportedException("Negative Consume stat");}
            bonus=stat switch
            {
                "damagepercent"=>bonus with{DamagePercent=amount},"item_crushingblow"=>bonus with{CrushingChance=amount},
                "passive_fire_mastery"=>bonus with{FireMastery=amount},"passive_fire_pierce"=>bonus with{FirePierce=amount},
                "passive_mag_mastery"=>bonus with{MagicMastery=amount},"passive_mag_pierce"=>bonus with{MagicPierce=amount},
                _=>throw new NotSupportedException("Unmapped Consume stat writer: "+stat)
            };
        }
        Put(source.FirstStat,Eval(source.FirstFormula));Put(source.SecondStat,Eval(source.SecondFormula));
        return new(rank,hardDeathMark,monster,difficulty,values.Mana256,checked((int)values.Eval("auralencalc")),
            checked((int)values.Eval("aurastatcalc1")),checked((int)values.Eval("aurastatcalc2")),bonus);
    }
    internal static void Validate(ConsumeSuppliers captured)
    {
        if(captured==null){throw new InvalidDataException("Missing Consume capture");}
        try
        {
            var values=new SkillValues(SkillMechanics.For(CombatAbility.Consume),_=>captured.Rank,_=>0,99);
            if(Capture(values,captured.HardDeathMark,captured.Monster,captured.Difficulty)!=captured)
            {throw new InvalidDataException("Consume suppliers differ from their captured source context");}
        }
        catch(Exception e)when(e is ArgumentException or NotSupportedException or OverflowException)
        {throw new InvalidDataException("Invalid captured Consume suppliers",e);}
    }
}
