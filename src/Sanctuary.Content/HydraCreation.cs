// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record HydraCreationStats(int Rank,int CasterLevel,int DurationFrames,int MaximumHeads,
    SummonStat[] Passives,SummonStat[] Skills)
{
    internal int Passive(string name)=>Passives.Where(x=>x.Name==name).Sum(x=>x.Value);
    internal int Skill(string name)=>Skills.Where(x=>x.Name==name).Sum(x=>x.Value);
}

// Birth suppliers, not a second summon controller. No inherited spawn HP or
// per-shot caster refresh is invented for the source's untargetable heads.
internal static class HydraCreation
{
    internal static HydraCreationStats Prepare(SkillValues values,int casterLevel,Func<string,int> accrued)
    {
        ArgumentNullException.ThrowIfNull(values);ArgumentNullException.ThrowIfNull(accrued);
        if(values.Ability!=CombatAbility.Hydra||values.Rank is <1 or >255||casterLevel is <1 or >99)
        {throw new ArgumentException("Hydra needs a supported rank and caster level");}
        var row=SkillMechanics.For(CombatAbility.Hydra);
        if(row.SourceId!=62||row.Number("srvstfunc")!=14||row.Number("srvdofunc")!=144||row.Text("summon")!="hydra1"
            ||row.Text("pettype")!="hydra"||row.Text("summode")!="S2"||row.Left||!row.Right||row.Number("localdelay")!=0)
        {throw new NotSupportedException("Unsupported pinned Hydra creation route");}
        var passives=Enumerable.Range(1,5).Where(i=>row.Text("passivestat"+i)!="0")
            .Select(i=>new SummonStat(row.Text("passivestat"+i),checked((int)values.StatEval("passivecalc"+i,accrued)))).ToArray();
        var skills=Enumerable.Range(1,5).Where(i=>row.Text("sumskill"+i)!="0")
            .Select(i=>new SummonStat(row.Text("sumskill"+i),checked((int)values.Eval("sumsk"+i+"calc")))).Where(x=>x.Value>0).ToArray();
        var result=new HydraCreationStats(values.Rank,casterLevel,checked((int)values.Eval("calc1")),checked((int)values.Eval("petmax")),passives,skills);
        Validate(result);return result;
    }
    internal static HydraCreationStats Capture(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Sorceress)
        {throw new InvalidOperationException("Hydra requires a living Sorceress owner");}
        return Prepare(SkillMechanics.Values(owner,CombatAbility.Hydra),ClassStats.Level(owner),name=>name switch
        {
            // Sorceress Fire Mastery participates in the accrued stat; the
            // Assassin-only supplier has no class mastery to collect.
            "passive_fire_mastery"=>NamedElements.MasteryPercent(owner,"fire"),
            "passive_fire_pierce"=>ClassGear.Bonus(owner,"pierce-fire"),
            "item_pierce_fire_immunity"=>ClassGear.Bonus(owner,"pierce-immunity-fire"),
            _=>throw new NotSupportedException("Unmapped Hydra inherited stat: "+name)
        });
    }
    internal static SkillValues Values(HydraCreationStats birth)
    {
        Validate(birth);var row=SkillMechanics.For(CombatAbility.Hydra);
        return new(row,name=>name==row.SourceKey?birth.Rank:birth.Skill(name),name=>birth.Skill(name),birth.CasterLevel);
    }
    internal static (int Min,int Max) Fire(HydraCreationStats birth,SlowMissilePayload slow)
        =>DruidFirestorm.Endpoints(Values(birth),Math.Max(0,birth.Passive("passive_fire_mastery")),slow);
    internal static void Validate(HydraCreationStats birth)
    {
        if(birth is not{Rank:>=1 and <=255,CasterLevel:>=1 and <=99,DurationFrames:250,MaximumHeads:18}
            ||birth.Passives is not{Length:3}||birth.Skills==null||birth.Passives.Select(x=>x.Name).Distinct().Count()!=3
            ||!birth.Passives.Select(x=>x.Name).Order().SequenceEqual(new[]{"item_pierce_fire_immunity","passive_fire_mastery","passive_fire_pierce"})
            ||birth.Skills.Select(x=>x.Name).Distinct().Count()!=birth.Skills.Length||birth.Skills.Any(x=>x.Value is <1 or >255)
            ||birth.Skill("HydraMissile")!=birth.Rank||birth.Skills.Any(x=>x.Name is not("HydraMissile" or "Fire Bolt" or "Fire Ball")))
        {throw new ArgumentException("Invalid captured Hydra creation state");}
    }
}
