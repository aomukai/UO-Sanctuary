// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace Sanctuary;

public sealed record GenericPetPathTargetMetadata(int Id,string Name,byte Flags61,byte? Flags62=null);
internal sealed class GenericPetPathTargetView
{
    private readonly byte Selector;
    private readonly GenericPetPathTargetMetadata[] Rows;
    internal GenericPetPathTargetView(GenericPetPathTargetMetadata[] rows,byte selector)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if(selector is <1 or >3){throw new NotSupportedException("Path target metadata requires an explicit selector1..3");}
        Rows=(GenericPetPathTargetMetadata[])rows.Clone();Selector=selector;
        for(int i=0;i<Rows.Length;i++)
        {if(Rows[i] is not{} row||row.Id!=i||string.IsNullOrWhiteSpace(row.Name)){throw new InvalidDataException("Invalid primary path target metadata");}}
    }
    internal byte? Flags(byte selector,int id)
    {
        Require(selector);return id>=0&&id<Rows.Length?Rows[id].Flags61:null;
    }
    internal byte? CommandFlags(byte selector,int id)
    {
        Require(selector);return id>=0&&id<Rows.Length?Rows[id].Flags62??throw new NotSupportedException("Missing pinned primary command flags"):null;
    }
    internal void Require(byte selector)
    {if(selector!=Selector){throw new NotSupportedException("Path target selector is not bound to this view");}}
}
public static class GenericPetPathTargets
{
    private static GenericPetPathTargetMetadata[] Rows;
    public static void Configure()
    {
        Rows=BalanceCatalog.Read<GenericPetPathTargetMetadata>("generic-pet-path-targets");
        if(Rows.Length!=751){throw new InvalidDataException("Missing pinned path target rows");}
        _=new GenericPetPathTargetView(Rows,1);
    }
    // One caller-supplied binding, not an inferred difficulty/selector alias.
    internal static GenericPetPathTargetView Bind(byte selector)=>new(Rows,selector);
}
