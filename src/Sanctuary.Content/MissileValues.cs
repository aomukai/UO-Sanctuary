// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
namespace Sanctuary;

// Distinct from skill getters: missile elemental synergy truncates BEFORE
// HitShift. parN belongs to the missile row; skill('x'.parN) belongs to x.
internal sealed class MissileValues(string key,SkillValues skill)
{
    private readonly IReadOnlyDictionary<string,JsonElement> _fields=NecromancerSummons.Fields(key);
    internal int Number(string field,int fallback=0)=>_fields.TryGetValue(field,out var v)
        ?v.ValueKind==JsonValueKind.Number&&v.TryGetInt32(out int n)?n:throw new NotSupportedException("Expected numeric missile field "+field):fallback;
    internal string Text(string field)=>_fields.TryGetValue(field,out var v)?v.ValueKind==JsonValueKind.String?v.GetString():v.GetRawText():"0";
    private long Resolve(string name,string argument,string property)
    {
        if(name=="lvl"){return skill.Rank;}
        if(name.StartsWith("par",StringComparison.Ordinal)&&int.TryParse(name.AsSpan(3),NumberStyles.None,CultureInfo.InvariantCulture,out int p))
        {return Number("Param"+p);}
        if(name.Length==4&&name.StartsWith("pa",StringComparison.Ordinal)&&int.TryParse(name.AsSpan(2),NumberStyles.None,CultureInfo.InvariantCulture,out p))
        {return Number("Param"+p);}
        if(name=="skill"){return skill.Reference(name,argument,property);}
        throw new NotSupportedException("Unsupported missile formula supplier "+name);
    }
    internal long Eval(string field)=>SkillFormula.Evaluate(Text(field),Resolve);
    internal int ElementFrames()
    {
        int rank=skill.Rank;if(rank<1){return Number("ELen");}
        return checked(Number("ELen")+Math.Min(7,rank-1)*Number("ELevLen1")
            +Math.Min(8,Math.Max(0,rank-8))*Number("ELevLen2")+Math.Max(0,rank-16)*Number("ELevLen3"));
    }
    internal int ElementDamage256(bool maximum,int defaultHitShift=8,int maximumRank=255)
    {
        if(skill.Rank<1 || skill.Rank>maximumRank){throw new NotSupportedException("Unsupported missile damage rank");}
        int shift=Number("HitShift",defaultHitShift);if(shift is <0 or >8){throw new NotSupportedException("Unsupported missile HitShift");}
        string stem=maximum?"Max":"Min";
        long raw=SkillValues.Tier(Number(maximum?"EMax":"EMin"),skill.Rank,i=>Number(stem+"ELev"+i));
        raw=checked(raw+raw*Eval("EDmgSymPerCalc")/100);
        return checked((int)(raw<<shift));
    }
}
