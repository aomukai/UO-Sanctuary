// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal static class GenericPetRegistryLayout
{
    internal static int Bucket(int id)=>id&127;
    // Complete112-byte370610:source group addresses are not type*stride.
    internal static int? Offset(int kind,int id)
    {
        int start=kind switch{0=>0x2230,1=>0x2630,2=>0x2a30,3=>0x3230,4=>0x2e30,_=>-1};
        return start<0?null:start+Bucket(id)*8;
    }
}
internal sealed record GenericPetPlayerIterationServices(Func<int,int,int> First,
    Func<int,GenericPetSearchWorldUnit> Unit,Func<byte,int> StateCount,Func<int,int> Next158);
internal static class GenericPetPlayerIteration
{
    // Complete469-byte3722a0. Group0is established by current370610;metadata
    // identifies exclusions7/playerbody and186/sharedstash. The callback and
    // registry membership/order remain explicit;no network payload is inferred.
    internal static void Run(int game,Action<int> callback,GenericPetPlayerIterationServices services)
    {
        if(game==0||callback==null){throw new NotSupportedException("Living-player iteration requires game and callback");}
        int Count(byte selector)
        {
            if(selector is <1 or >3){throw new NotSupportedException("Living-player iteration requires selector1..3");}
            return services.StateCount(selector);
        }
        for(int bucket=0;bucket<128;bucket++)
        {
            int actor=services.First(game,bucket);
            while(actor!=0)
            {
                if(GenericPetSearchWorld.HasState(services.Unit(actor),7,Count)==0
                    &&GenericPetSearchWorld.HasState(services.Unit(actor),186,Count)==0){callback(actor);}
                // Current source reads next after the callback,not a snapshot.
                actor=services.Next158(actor);
            }
        }
    }
}
