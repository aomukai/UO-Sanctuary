// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace Sanctuary;

public sealed record GenericPetStateMaskProfile(byte Selector,int StateCount,uint[] Words);
internal sealed class GenericPetStateMaskView
{
    private readonly byte Selector;
    private readonly uint[] Words;
    private readonly int Count;
    internal GenericPetStateMaskView(GenericPetStateMaskProfile profile)
    {Selector=profile.Selector;Count=profile.StateCount;Words=(uint[])profile.Words.Clone();}
    private void Require(byte selector)
    {if(selector!=Selector){throw new NotSupportedException("State mask selector is not bound");}}
    internal ulong StateCount(byte selector){Require(selector);return (ulong)Count;}
    // Callers receive an independent buffer; source intersection cannot mutate
    // the published catalog or another explicitly bound view.
    internal uint[] Mask(byte selector){Require(selector);return (uint[])Words.Clone();}
}
public static class GenericPetStateMasks
{
    private static GenericPetStateMaskProfile[] Profiles;
    public static void Configure()
    {
        var rows=BalanceCatalog.Read<GenericPetStateMaskProfile>("generic-pet-state-masks");
        if(rows.Length!=3){throw new InvalidDataException("Incomplete current state mask views");}
        var copy=new GenericPetStateMaskProfile[3];
        for(int i=0;i<3;i++)
        {
            var r=rows[i];
            if(r==null||r.Selector!=i+1||r.StateCount!=232||r.Words==null||r.Words.Length!=8||(r.Words[7]&0xffffff00)!=0)
            {throw new InvalidDataException("Invalid current state mask view");}
            copy[i]=r with{Words=(uint[])r.Words.Clone()};
        }
        Profiles=copy;
    }
    internal static GenericPetStateMaskView Bind(byte selector)
    {
        if(selector is <1 or >3){throw new NotSupportedException("State masks require selector1..3");}
        return new(Profiles[selector-1]);
    }
    internal static GenericPetSearchNestedServices Bind(GenericPetSearchNestedServices original,GenericPetStateMaskView view)
    {
        ArgumentNullException.ThrowIfNull(original);ArgumentNullException.ThrowIfNull(view);
        return original with{CorpseMask410=view.Mask,WideStateCount=view.StateCount};
    }
}
