// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;

namespace Sanctuary;

public sealed record ClassItemType(string Code,string[] Parents);
public static class ClassItemTypes
{
    private static readonly Dictionary<string,ClassItemType> Types=new(StringComparer.Ordinal);
    public static void Load()
    {
        Types.Clear();foreach(var row in BalanceCatalog.Read<ClassItemType>("class-item-types")){Types.Add(row.Code,row);}
        foreach(var key in Types.Keys){Validate(key,new());}
    }
    private static void Validate(string code,HashSet<string> path)
    {
        if(!Types.TryGetValue(code,out var row) || !path.Add(code)){throw new InvalidDataException("Missing/cyclic class item type: "+code);}
        foreach(string parent in row.Parents){Validate(parent,path);}path.Remove(code);
    }
    public static bool Is(string code,string ancestor)=>Types.TryGetValue(code??"",out var row)
        && (code==ancestor || Array.Exists(row.Parents,parent=>Is(parent,ancestor)));
}
