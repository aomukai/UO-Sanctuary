// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal sealed record SkillMissileRange(int Id,string Name,int? Range,string Expression);
internal sealed record SkillMissileRangeData(string ReferenceVersion,string SourceSha256,SkillMissileRange[] Rows);
// Shared base range view. Sparse summon-specific field aliases are not a
// complete missile table;all IDs/names/source hashes must match execution.
internal static class SkillMissileRanges
{
    private static readonly Lazy<SkillMissileRange[]> Rows=new(()=>
    {
        var data=BalanceCatalog.ReadObject<SkillMissileRangeData>("skill-missile-ranges");
        if(data is not{ReferenceVersion:"3.3.93847",Rows.Length:742}||data.SourceSha256!=ItemSkillExecution.MissileSourceHash
            ||data.Rows.Where((r,i)=>r==null||r.Id!=i||r.Range is<0 or>65535||r.Range.HasValue==!string.IsNullOrEmpty(r.Expression)||r.Name!=ItemSkillExecution.MissileName(i)).Any())
        {throw new InvalidDataException("Missile base ranges do not match pinned skill execution metadata");}
        return data.Rows;
    });
    // Explicit prepared evaluation,not an assertion of modern NPC-AI context.
    // Callers must approve the meaning of lvl before using this in gameplay.
    internal static int? PreparedAtRank(int id,int rank)
    {
        if(rank is<0 or>255){throw new ArgumentOutOfRangeException(nameof(rank));}
        if(id<0||id>=Rows.Value.Length){return null;}var row=Rows.Value[id];
        if(row.Range is{} literal){return literal;}
        try
        {
            long value=SkillFormula.Evaluate(row.Expression,(name,argument,property)=>name=="lvl"?rank
                :throw new NotSupportedException("Missile range needs an explicit scope variable:"+name));
            if(value is<0 or>65535){throw new NotSupportedException("Missile range leaves its source word domain");}
            return(int)value;
        }
        catch(Exception e)when(e is FormatException or OverflowException or ArgumentException)
        {throw new NotSupportedException("Missile range formula remains unqualified:"+row.Name,e);}
    }
    internal static int? Range(int id)
    {
        if(id<0||id>=Rows.Value.Length){return null;}var row=Rows.Value[id];
        return row.Range??throw new NotSupportedException("Current missile range is an expression;NPC AI interpretation is unverified: "+row.Name+" = "+row.Expression);
    }
}
