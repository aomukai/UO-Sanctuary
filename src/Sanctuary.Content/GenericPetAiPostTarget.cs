// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Only the three caller-initialized search fields are represented. No values
// are inferred for the remaining unwritten native context bytes.
internal sealed class GenericPetAiPostSearch(int previous)
{internal int Previous=previous,Found=0,Distance=int.MaxValue;}
internal sealed record GenericPetAiPostServices(Func<byte> Selector,Func<int,int> ClassId,
    Func<int,GenericPetAiTick,bool> First,Func<int,GenericPetAiTick,bool> Second,
    Func<byte,int,GenericPetSearchMonster> Primary,Func<byte,int,bool?> Walk,
    Func<int,int?> RoomLevel,Func<byte,int,GenericPetSearchLevel> Level,Func<int,int,int> Special,
    Action<int,int,GenericPetAiPostSearch> Search,Action<int,byte> Wander);
internal static class GenericPetAiPostTarget
{
    // Complete43af30 orchestration. Existing primary/level metadata and shared
    // special/search/wander service identities are retained. The two earlier
    // callbacks and native descriptor9 implementation remain explicit leaves.
    internal static bool Run(int actor,GenericPetAiTick tick,GenericPetAiPostServices services)
    {
        if(tick.Target==0){return false;}
        if(services.First(actor,tick)||services.Second(actor,tick)){return true;}
        int Class()=>actor==0?-1:services.ClassId(actor);
        var primary=services.Primary(services.Selector(),Class());
        if(primary==null||(primary.Flags60&2)==0){return false;}
        if(services.Walk(services.Selector(),Class())!=true){return false;}
        int? level=services.RoomLevel(actor);if(level==null){return false;}
        var row=services.Level(services.Selector(),level.Value)
            ??throw new NotSupportedException("AI post-target stage dereferences its level row");
        if(row.Walk63==0||(uint)tick.Distance<=row.Walk63){return false;}
        int previous=tick.Target;if(services.Special(actor,previous)!=0){return false;}
        var search=new GenericPetAiPostSearch(previous);services.Search(actor,9,search);
        if(search.Found==0){services.Wander(actor,4);return true;}
        tick.Target=search.Found;tick.Distance=search.Distance;return false;
    }
}
