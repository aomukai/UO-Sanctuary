// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ConsumeBonusRow(string Monster,int SourceIndex,string FirstStat,string FirstFormula,string SecondStat,string SecondFormula,int UnderlingCount);
internal sealed record ConsumeBonusValues(int DamagePercent=0,int CrushingChance=0,int FireMastery=0,int FirePierce=0,int MagicMastery=0,int MagicPierce=0,
    int FireResist=0,int ColdResist=0,int LightningResist=0,int PoisonResist=0,int MagicResist=0,int PhysicalResist=0,
    int MaxFireResist=0,int MaxColdResist=0,int MaxLightningResist=0,int MaxPoisonResist=0,int MaxMagicResist=0,
    int ColdPierce=0,int LightningPierce=0,int AllSkills=0,string AuraSkill=null,int AuraRank=0);
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
        var source=Rows.Value.SingleOrDefault(r=>r.Monster==monster);
        int rank=values.Rank;
        int Eval(string expression)
        {
            string text=expression.Trim();
            if(text.StartsWith('"')||text.EndsWith('"'))
            {if(text.Length<2||!text.StartsWith('"')||!text.EndsWith('"')){throw new NotSupportedException("Unbalanced Consume field quotes");}text=text[1..^1];}
            text=Regex.Replace(text,@"\bsksrc\s*\(","skill(");
            return checked((int)SkillFormula.Evaluate(text,
            (name,argument,property)=>name=="diff"?(int)difficulty+1:name=="skill"&&argument=="Consume"&&property=="lvl"?rank
                :name=="skill"&&argument=="Death Mark"&&property=="blvl"?hardDeathMark
                :throw new NotSupportedException("Unmapped Consume source context: "+name+"/"+argument+"/"+property)));
        }
        var bonus=new ConsumeBonusValues();
        void Put(string stat,string parameter,int amount)
        {
            if(amount<0||stat!="item_aura"&&!string.IsNullOrEmpty(parameter)){throw new NotSupportedException("Unsupported Consume stat value/parameter");}
            bonus=stat switch
            {
                "damagepercent"=>bonus with{DamagePercent=amount},"item_crushingblow"=>bonus with{CrushingChance=amount},
                "passive_fire_mastery"=>bonus with{FireMastery=amount},"passive_fire_pierce"=>bonus with{FirePierce=amount},
                "passive_mag_mastery"=>bonus with{MagicMastery=amount},"passive_mag_pierce"=>bonus with{MagicPierce=amount},
                "fireresist"=>bonus with{FireResist=amount},"coldresist"=>bonus with{ColdResist=amount},
                "lightresist"=>bonus with{LightningResist=amount},"poisonresist"=>bonus with{PoisonResist=amount},
                "magicresist"=>bonus with{MagicResist=amount},"damageresist"=>bonus with{PhysicalResist=amount},
                "maxfireresist"=>bonus with{MaxFireResist=amount},"maxcoldresist"=>bonus with{MaxColdResist=amount},
                "maxlightresist"=>bonus with{MaxLightningResist=amount},"maxpoisonresist"=>bonus with{MaxPoisonResist=amount},
                "maxmagicresist"=>bonus with{MaxMagicResist=amount},"passive_cold_pierce"=>bonus with{ColdPierce=amount},
                "passive_ltng_pierce"=>bonus with{LightningPierce=amount},"item_allskills"=>bonus with{AllSkills=amount},
                "item_aura"when parameter is"Holy Fire"or"Meditation"or"Salvation"=>bonus with{AuraSkill=parameter,AuraRank=amount},
                _=>throw new NotSupportedException("Unmapped Consume stat writer: "+stat)
            };
        }
        if(source!=null){Put(source.FirstStat,"",Eval(source.FirstFormula));Put(source.SecondStat,"",Eval(source.SecondFormula));}
        else
        {
            var bound=BindDemonPrepared.Monster(monster);var written=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for(int i=0;i<bound.ConsumeStats.Length;i++)
            {
                string stat=bound.ConsumeStats[i],parameter=bound.ConsumeParameters[i],formula=bound.ConsumeFormulas[i];
                if(string.IsNullOrEmpty(stat))
                {if(!string.IsNullOrEmpty(parameter)||!string.IsNullOrEmpty(formula)){throw new NotSupportedException("Consume field without a stat");}continue;}
                if(!written.Add(stat)){throw new NotSupportedException("Repeated Consume stat needs its source writer order");}
                Put(stat,parameter,Eval(formula));
            }
        }
        return new(rank,hardDeathMark,monster,difficulty,values.Mana256,checked((int)values.Eval("auralencalc")),
            checked((int)values.Eval("aurastatcalc1")),checked((int)values.Eval("aurastatcalc2")),bonus);
    }
    internal static bool RuntimeSupported(ConsumeSuppliers value)=>value.Bonus.AuraRank==0
        ||value.Bonus.AuraRank is>=1 and<=99&&ConsumeAuras.Ability(value.Bonus.AuraSkill)!=CombatAbility.None;
    internal static void RequireRuntime(ConsumeSuppliers value)
    {if(!RuntimeSupported(value)){throw new NotSupportedException("Unqualified Consume item-aura context: "+value.Bonus.AuraSkill);}}
    internal static string Description(ConsumeSuppliers value)
    {
        var b=value.Bonus;var parts=new System.Collections.Generic.List<string>();
        void Add(string name,int n,string suffix="%"){if(n!=0){parts.Add(name+" +"+n+suffix);}}
        Add("Damage",b.DamagePercent);Add("Crushing Blow",b.CrushingChance);Add("Fire damage",b.FireMastery);Add("Fire pierce",b.FirePierce);
        Add("Magic damage",b.MagicMastery);Add("Magic pierce",b.MagicPierce);Add("Cold pierce",b.ColdPierce);Add("Lightning pierce",b.LightningPierce);
        Add("Fire resist",b.FireResist);Add("Cold resist",b.ColdResist);Add("Lightning resist",b.LightningResist);Add("Poison resist",b.PoisonResist);
        Add("Magic resist",b.MagicResist);Add("Physical resist",b.PhysicalResist);Add("Max fire resist",b.MaxFireResist);Add("Max cold resist",b.MaxColdResist);
        Add("Max lightning resist",b.MaxLightningResist);Add("Max poison resist",b.MaxPoisonResist);Add("Max magic resist",b.MaxMagicResist);Add("All skills",b.AllSkills,"");
        if(b.AuraRank>0){parts.Add(b.AuraSkill+" aura "+b.AuraRank);}return string.Join(", ",parts);
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
