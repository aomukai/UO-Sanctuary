// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record BindDemonChanceResult(int Percent,int HealthPercent,long[] Calcs);

// Approved explicit interpretation,not recovered gate44b240. The sole
// ambiguous chained comparison is deliberately grouped by the documented
// Sanctuary Bind decision rather than silently claiming source equivalence.
internal static class BindDemonChance
{
    internal const string Ambiguous="((100-hpct)/((elte==1+elte==4)?20:((elte==2)?10:5)))";
    internal const string Grouped="((100-hpct)/(((elte==1)+(elte==4))?20:((elte==2)?10:5)))";
    internal static int HealthPercent(int life256,int maximum256)
    {
        if(maximum256<=0||life256<0||life256>maximum256){throw new ArgumentOutOfRangeException(nameof(life256));}
        return(int)((long)life256*100/maximum256);
    }
    internal static BindDemonChanceResult Evaluate(BindDemonMonster row,BindDemonSuppliers birth,int progress,int healthPercent,int quality,int maximum256)
    {
        ArgumentNullException.ThrowIfNull(row);BindDemonPrepared.Validate(birth);
        if(progress<0||healthPercent is<0 or>100||quality is<0 or>4||maximum256<=0)
        {throw new ArgumentException("Unsupported prepared Bind chance context");}
        var calcs=new long[5];
        long Resolve(string name,string key,string property)
        {
            if(name=="skill")
            {
                if(key=="Bind Demon")
                {return property switch{"sctk"=>progress,"blvl"=>birth.HardRank,"dm34"=>birth.ChanceSkillTerm-birth.HardDeathMark,_=>throw new NotSupportedException("Unknown Bind chance source property")};}
                if(key=="Death Mark"&&property=="blvl"){return birth.HardDeathMark;}
            }
            if(name=="stat"&&key=="maxhp"&&property=="accr"){return maximum256;}
            return name switch{"hpct"=>healthPercent,"elte"=>quality,"pcl1"=>calcs[0],"pcl2"=>calcs[1],"pcl3"=>calcs[2],"pcl4"=>calcs[3],"pcl5"=>calcs[4],_=>throw new NotSupportedException("Unknown Bind chance context: "+name)};
        }
        for(int i=0;i<calcs.Length;i++)
        {string formula=row.Calcs[i];// sksrc is bound only to this explicit captured caster context;
            // the general skill parser does not acquire global sksrc semantics.
            calcs[i]=SkillFormula.Evaluate((formula==Ambiguous?Grouped:formula).Replace("sksrc(","skill(",StringComparison.Ordinal),Resolve);}
        long chance=SkillFormula.Evaluate(row.BindChance,Resolve);
        return new((int)Math.Clamp(chance,0,100),healthPercent,calcs);
    }
}
