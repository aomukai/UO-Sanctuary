// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal sealed record NpcSkillItemTypeTable(string ReferenceVersion,string[] Codes);
// Owned linker row IDs, shared by skill/passive/mastery layers. Public JSON
// lineNumber includes marker/layout differences and is not this ID domain.
internal static class NpcSkillItemTypes
{
    private static readonly Lazy<string[]> Types=new(()=>
    {
        var table=BalanceCatalog.ReadObject<NpcSkillItemTypeTable>("npc-skill-item-types");
        if(table is not{ReferenceVersion:"3.3.93847",Codes.Length:109}||table.Codes.Any(c=>c==null)
            ||table.Codes.Count(c=>c.Length==0)!=4||table.Codes.Where(c=>c.Length>0).Distinct().Count()!=105||table.Codes[68]!="h2h")
        {throw new InvalidDataException("Invalid owned NPC skill item-type linker table");}
        return table.Codes;
    });
    internal static string Code(int id)=>id>=0&&id<Types.Value.Length?string.IsNullOrEmpty(Types.Value[id])?null:Types.Value[id]:null;
}
