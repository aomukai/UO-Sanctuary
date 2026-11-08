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
    internal static int? Range(int id)
    {
        if(id<0||id>=Rows.Value.Length){return null;}var row=Rows.Value[id];
        return row.Range??throw new NotSupportedException("Current missile range is an expression;NPC AI interpretation is unverified: "+row.Name+" = "+row.Expression);
    }
}
