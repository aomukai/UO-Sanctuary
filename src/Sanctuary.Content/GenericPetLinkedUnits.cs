// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Caller-owned stable backing storage. Count is distinct from capacity,as in
// the captured AI vector. Callbacks may edit entries/count;reallocation and
// arbitrary native functor/allocator ABI are not modeled.
internal sealed class GenericPetLinkedVector(int[] values,int count)
{
    internal readonly int[] Values=values;
    internal int Count=count;
    internal void Validate()
    {if(Values==null||Count<0||Count>Values.Length){throw new NotSupportedException("Invalid linked-unit vector");}}
}
internal sealed record GenericPetLinkedServices(Func<int,GenericPetTraversalUnit> Unit,
    Func<int,GenericPetLinkedVector> Links,Func<int,int,int,int> Resolve);
internal static class GenericPetLinkedUnits
{
    // Complete333b60,using the existing unit and registry contracts. Live
    // players/nonmonster kinds can match only the null origin;monster links
    // compare the resolved direct owner,not mere faction friendliness.
    internal static bool Valid(int origin,int candidate,GenericPetLinkedServices services)
    {
        if(candidate==0){return false;}var unit=services.Unit(candidate);
        if((unit.Flags124&0x10000)!=0){return false;}
        if(unit.Kind==0){return unit.Mode is not(0 or 17)&&origin==0;}
        if(unit.Kind==1)
        {
            if(unit.Mode is 0 or 12){return false;}
            int owner=unit.HasMonsterData&&unit.Ai is{Game:not 0} ai?services.Resolve(ai.Game,ai.Type,ai.Id):0;
            return owner==origin;
        }
        return (unit.Flags128&0x4000000)==0&&origin==0;
    }
    private static GenericPetLinkedVector Vector(int actor,GenericPetLinkedServices services)
    {
        if(actor==0){return null;}var unit=services.Unit(actor);
        if(unit.Kind!=1||!unit.HasMonsterData){return null;}
        var vector=services.Links(actor);vector?.Validate();return vector;
    }
    // Complete333e10 logical traversal/lifetime. Capture storage and end once;
    // read each ID live. Normal returns release the supplied callback exactly
    // once;an unsupported fatal boundary does not pretend normal cleanup ran.
    internal static void Notify(int game,int actor,Action<int> callback,Action release,GenericPetLinkedServices services)
    {
        ArgumentNullException.ThrowIfNull(callback);ArgumentNullException.ThrowIfNull(release);
        var vector=Vector(actor,services);
        if(vector!=null)
        {
            var ids=vector.Values;int end=vector.Count;
            for(int i=0;i<end;i++)
            {
                if(game==0){throw new NotSupportedException("Nonempty linked-unit traversal requires a game");}
                int target=services.Resolve(game,1,ids[i]);if(Valid(actor,target,services)){callback(target);}
            }
        }
        release();
    }
    // Complete333c20 stable partition/erase over logical managed storage.
    // Preserve the invalid tail in backing storage just like the source;only
    // the live count shrinks. Read entries again after validity callbacks.
    internal static int Clear(int game,int actor,GenericPetLinkedServices services)
    {
        var vector=Vector(actor,services);if(vector==null){return 0;}
        var ids=vector.Values;int end=vector.Count,kept=0,rejected=0;var scratch=new int[end];
        for(int i=0;i<end;i++)
        {
            if(game==0){throw new NotSupportedException("Nonempty linked-unit cleanup requires a game");}
            int target=services.Resolve(game,1,ids[i]);
            if(Valid(actor,target,services)){if(kept!=i){ids[kept]=ids[i];}kept++;}
            else{scratch[rejected++]=ids[i];}
        }
        Array.Copy(scratch,0,ids,kept,rejected);vector.Count=kept;return kept;
    }
    // The pinned1722cd0 functor's21-byte Invoke tail-calls392be0 with its
    // captured game,linked child,killer0,detach1. Reuse that same policy rather
    // than replacing recursive death with native Delete/Kill.
    internal static GenericPetOwnerLossServices Bind(GenericPetOwnerLossServices original,int game,GenericPetLinkedServices linked)
    {
        GenericPetOwnerLossServices bound=null;
        bound=original with
        {
            Notify333e10=(actor,token)=>
            {
                if(token!=0x1722cd0){throw new NotSupportedException("Unknown linked death callback");}
                Notify(game,actor,child=>GenericPetOwnerLoss.Run(child,0,1,bound),()=>{},linked);
            },
            Clear333c20=actor=>Clear(game,actor,linked)
        };
        return bound;
    }
}
