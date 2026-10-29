// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal sealed record GenericPetSearchWrapperUnit(GenericPetSearchUnit Unit,int PathTarget,uint Flags128);
internal sealed record GenericPetSearchWrapperContext(byte GameSelector,int Pet,int Owner,GenericPetParameters Parameters,IReadOnlyList<GenericPetSearchSwarm> Swarm);
internal sealed record GenericPetSearchWrapperServices(Func<int,GenericPetSearchWrapperUnit> Unit,
    Action<int> Refresh371b10,GenericPetSearchStateServices Metadata,GenericPetSearchServices Callback,
    Action<int,GenericPetSearchContext,byte,Func<int,int>> Traverse40d9f0);
internal static class GenericPetSearchGrid
{
    internal static GenericPetSearchCell[] Create()
    {var cells=new GenericPetSearchCell[64];for(int i=0;i<cells.Length;i++){cells[i]=new();}return cells;}
    internal static void Heapify(IList<int> indices,IReadOnlyList<GenericPetSearchCell> cells)
    {
        ArgumentNullException.ThrowIfNull(indices);ArgumentNullException.ThrowIfNull(cells);
        bool Before(int a,int b)
        {
            var left=cells[a];var right=cells[b];
            if(left.Distance!=right.Distance){return left.Distance<right.Distance;}
            if(left.Count!=right.Count){return left.Count>right.Count;}
            // Source pointers refer to one contiguous16-byte cell array.
            return a<b;
        }
        int count=indices.Count;if(count<2){return;}
        for(int parent=(count-2)/2;;parent--)
        {
            int saved=indices[parent],hole=parent,child=2*parent+2;
            while(child<count)
            {
                int selected=Before(indices[child],indices[child-1])?child:child-1;
                indices[hole]=indices[selected];hole=selected;child=2*selected+2;
            }
            if(child==count){indices[hole]=indices[child-1];hole=child-1;}
            int above=(hole-1)/2;
            while(hole>parent)
            {
                if(Before(indices[above],saved)){break;}
                indices[hole]=indices[above];hole=above;above=(above-1)/2;
            }
            indices[hole]=saved;if(parent==0){break;}
        }
    }
}
internal static class GenericPetSearchWrapper
{
    private static bool Dead(GenericPetSearchWrapperUnit value)
    {
        var u=value.Unit;if((u.Flags124&0x10000)!=0){return true;}
        if(u.Position.Kind==0){return u.Mode is 0 or 17;}
        if(u.Position.Kind==1){return u.Mode is 0 or 12;}
        return (value.Flags128&0x4000000)!=0;
    }
    private static long Pack(int low,int high)=>unchecked((long)(((ulong)(uint)high<<32)|(uint)low));
    internal static GenericPetCombatSelection Run(GenericPetSearchWrapperContext input,IList<GenericPetCombatCandidate> rows,int mask,int explicitTarget,GenericPetSearchWrapperServices s)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(s);
        int bias=0;
        if(input.Owner!=0)
        {
            s.Refresh371b10(input.Owner);var owner=s.Unit(input.Owner);
            if(owner.Unit.Position.Kind==5){throw new NotSupportedException("Current search wrapper has a tile-owner assertion boundary");}
            if(owner.Unit.Position.HasPath&&owner.PathTarget!=0&&owner.PathTarget!=input.Owner&&!Dead(s.Unit(owner.PathTarget))
                &&s.Callback.Valid372ec0(input.Pet,owner.PathTarget,1)!=0)
            {
                // Current code writes the owner,not the validated path target.
                // Preserve that literal identity until real world validation.
                bias=input.Owner;
            }
        }
        var c=new GenericPetSearchContext{GameSelector=input.GameSelector,Mask=mask,Owner=input.Owner,Bias=bias,Parameters=input.Parameters,Grid=GenericPetSearchGrid.Create(),Swarm=input.Swarm??[]};
        if((mask&32)!=0)
        {
            var prepared=new GenericPetSearchStates();prepared.Prepare(input.GameSelector,rows,s.Metadata);
            c.Groups=GenericPetSearch.InlineGroups(prepared);
        }
        int Visit(int unit)=>GenericPetSearch.Visit(c,input.Pet,unit,rows,s.Callback);
        if(explicitTarget!=0){Visit(explicitTarget);}
        else{s.Traverse40d9f0(input.Pet,c,1,Visit);}
        int gridTarget=0;
        if((mask&4)!=0)
        {
            var indices=new List<int>();for(int i=0;i<64;i++){if(c.Grid[i].Count>0){indices.Add(i);}}
            if(indices.Count!=0){GenericPetSearchGrid.Heapify(indices,c.Grid);gridTarget=c.Grid[indices[0]].Target;}
        }
        return new([c.Target20,c.Target28,c.Target30,c.Target38,gridTarget],Pack(c.Count58,c.Count5c),Pack(c.Count60,c.Count64));
    }
    internal static Func<IReadOnlyList<GenericPetCombatCandidate>,int,int,GenericPetCombatSelection> Provider(GenericPetSearchWrapperContext input,GenericPetSearchWrapperServices services)
    {
        // Existing combat search receives read-only views of mutable source
        // slots. Keep those slots live; never silently copy prepared targets.
        return (rows,mask,target)=>Run(input,rows as IList<GenericPetCombatCandidate>
            ??throw new NotSupportedException("Search requires mutable source candidate slots"),mask,target,services);
    }
}
