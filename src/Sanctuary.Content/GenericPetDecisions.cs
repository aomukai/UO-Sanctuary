// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

// Detached current-code decision inputs. Object/missile/tile paths are static;
// player/monster/item paths use the source unsigned16 coordinate fields.
internal sealed record GenericPetDecisionUnit(int Kind,bool HasPath,int X,int Y,bool HasPlayerData=false,int DestinationX=0,int DestinationY=0)
{
    internal (int X,int Y) Coordinates()
    {
        if(Kind is 2 or 4 or 5)
        {
            if(!HasPath){throw new NotSupportedException("Current static-path caller requires a path");}
            return(X,Y);
        }
        return HasPath?((int)unchecked((ushort)X),(int)unchecked((ushort)Y)):(0,0);
    }
}
internal sealed class GenericPetDecisionMemory
{
    internal int Target,Value0,Value1,Value2;
}
internal sealed record GenericPetDecisionFrame(GenericPetParameters Parameters,GenericPetDecisionMemory Memory);
internal sealed record GenericPetRootContext(GenericPetDecisionUnit Pet,bool HasMonsterData,bool HasAiControl,bool HasOwnerGame,IReadOnlyList<byte> EventTypes);
internal sealed record GenericPetRootServices(Func<GenericPetDecisionUnit> ResolveOwner,
    Func<GenericPetDecisionUnit,GenericPetDecisionFrame> Initialize,Action<int> RemoveEvent,
    Func<GenericPetDecisionUnit,int,int,int> Distance,Func<GenericPetDecisionUnit,int?> Level,
    Func<GenericPetDecisionUnit,GenericPetDecisionFrame,bool> Follow,
    Func<GenericPetDecisionUnit,GenericPetDecisionFrame,bool> Combat,Action<int> Idle,
    Action<GenericPetDecisionMemory> Finish,Action<int,int,int> Persist);

// Current root policy with explicit unresolved game services. It does not
// schedule a world pet, resolve targets, execute movement or select skills.
internal static class GenericPetDecisions
{
    private static bool Town(int? level)=>level is 1 or 40 or 75 or 103 or 109;
    internal static void Think(GenericPetRootContext c,GenericPetRootServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(c.Pet);ArgumentNullException.ThrowIfNull(c.EventTypes);ArgumentNullException.ThrowIfNull(s);
        var owner=c.Pet.Kind==1&&c.HasMonsterData&&c.HasAiControl&&c.HasOwnerGame?s.ResolveOwner():null;
        var frame=s.Initialize(owner);ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(frame.Parameters);ArgumentNullException.ThrowIfNull(frame.Memory);
        // The source saves next before removing each type2(AITHINK) event.
        // Snapshot indices identify distinct duplicate entries to the service.
        var events=new byte[c.EventTypes.Count];for(int i=0;i<events.Length;i++){events[i]=c.EventTypes[i];}
        for(int i=0;i<events.Length;i++){if(events[i]==2){s.RemoveEvent(i);}}
        bool done=false;
        if(owner!=null)
        {
            var(x,y)=owner.Coordinates();
            if(s.Distance(c.Pet,x,y)>frame.Parameters.LeashSquared){done=s.Follow(owner,frame);}
        }
        if(!done)
        {
            bool town=Town(s.Level(c.Pet));
            if(!town&&owner!=null){town=Town(s.Level(owner));}
            if(!town){done=s.Combat(owner,frame);}
            // A failed over-leash follow can legitimately be retried here.
            if(!done&&!s.Follow(owner,frame)){s.Idle(15);}
        }
        s.Finish(frame.Memory);
        // Finish precedes all three current AI-control writes.
        s.Persist(frame.Memory.Value0,frame.Memory.Value1,frame.Memory.Value2);
    }
}

internal sealed record GenericPetRegionLookup(bool HasRoom,bool HasRegion,int Id)
{
    internal int Value=>!HasRoom?0:HasRegion?Id:-1;
}
internal sealed record GenericPetFollowContext(GenericPetDecisionUnit Pet,GenericPetDecisionUnit Owner,int WarpSquared,int WanderRaw);
internal sealed record GenericPetFollowServices(Func<GenericPetDecisionUnit,int,int,int> Distance,
    Func<GenericPetDecisionUnit,bool> OwnerMoving,
    Func<GenericPetDecisionUnit,int,int,GenericPetRegionLookup> Region,
    Func<int,bool> Execute);
internal static class GenericPetFollow
{
    // Numeric modes are the actual current executor arguments. Their movement
    // meanings and executor behavior remain unverified; no labels are invented.
    internal static bool Try(GenericPetFollowContext c,GenericPetFollowServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(s);
        if(c.Pet==null){return false;}
        int mode=2;
        if(c.Owner!=null)
        {
            var(x,y)=c.Owner.Coordinates();int distance=s.Distance(c.Pet,x,y);bool moving=s.OwnerMoving(c.Owner);
            int ownerRegion=s.Region(c.Owner,x,y).Value;
            var(px,py)=c.Pet.Coordinates();int petRegion=s.Region(c.Pet,px,py).Value;
            if(distance>c.WarpSquared){mode=3;}
            else if(distance>100||ownerRegion!=petRegion){mode=1;}
            else if(moving){mode=0;}
            else if(c.Owner.Kind==0&&c.Owner.HasPlayerData&&s.Distance(c.Pet,c.Owner.DestinationX,c.Owner.DestinationY)<36){mode=4;}
            else if(distance<=4){mode=5;}
        }
        if(mode==2&&c.WanderRaw<=0){return false;}
        return s.Execute(mode);
    }
}
