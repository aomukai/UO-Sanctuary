// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

internal enum PetMoveKind
{
    Velocity,WalkDelete,RunDelete,Walk,Run,Wander,DeleteThink,Idle,VelocityParameters,
    MoveUnit,Refresh,MarkTeleported,CountOthers,Escape,WalkOwner
}
internal readonly record struct PetMoveRequest(PetMoveKind Kind,int A=0,int B=0,int C=0,int D=0);
internal sealed record PetMoveContext(Point2D Unit,Point2D Owner,Point2D OwnerFirst,Point2D OwnerFinal,
    int OwnerMode,int Distance,int TotalPets,int Direction8,int TrailCursor,
    Point2D[] Trail,Point2D LastTeleport,bool HasMonsterAi=true);
internal sealed record PetMoveServices(Func<PetMoveRequest,int> Action,Func<Point2D,int> RoomAt,
    Func<Point2D,int> DistanceTo,Func<int,Point2D> DirectionOffset,Func<uint> Random,
    Func<int> Percentage,Func<Point2D?> FindSkeletonTeleport,Func<Point2D,bool> TeleportRoom);

// Complete legacy common-pet policy and modes0..5. Geometry, owner history,
// tactics/mode entry, crowd enumeration, RNG and placement are explicit services;
// the policy does not replace them with ordinary native follower AI.
internal static class SourcePetMovement
{
    private static readonly int[] Opposite=[4,3,2,1,0,7,6,5];
    private static void Validate(PetMoveContext c,PetMoveServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(s);
        if(c.Distance<0 || c.TotalPets<0 || c.Direction8 is <0 or >7 || c.TrailCursor is <0 or >19 || c.Trail?.Length!=20
            || s.Action==null || s.RoomAt==null || s.DistanceTo==null || s.DirectionOffset==null || s.Random==null
            || s.Percentage==null || s.FindSkeletonTeleport==null || s.TeleportRoom==null)
        {throw new ArgumentException("Unsupported common pet movement context/services");}
    }
    internal static bool Common(PetMoveContext c,PetMoveServices s,bool hasTarget,bool town,bool combat,int flag,int leash)
    {
        Validate(c,s);
        int limit=Math.Min(checked((c.TotalPets>>1)+leash),36);
        if(c.Distance>1 || c.OwnerMode!=1 || combat)
        {
            if(hasTarget && !town){return c.Distance>80 && Move(c,s,3,false,0,limit);}
            int ownerRoom=s.RoomAt(c.Owner),unitRoom=s.RoomAt(c.Unit),mode=2;
            if(c.OwnerMode is 2 or 6 or 3){mode=0;}
            // Literal AND, not an inferred "owner path is moving" OR.
            if(c.OwnerFirst.X!=c.OwnerFinal.X && c.OwnerFirst.Y!=c.OwnerFinal.Y){mode=0;}
            if(ownerRoom!=unitRoom || c.Distance>limit){mode=1;}
            if(c.Distance>50){mode=3;}
            if(s.DistanceTo(c.LastTeleport)<28)
            {if(mode==1){if(c.Distance<30){mode=4;}}else if(mode==2){mode=4;}}
            return (flag==0 || mode!=2) && Move(c,s,mode,false,0,limit);
        }
        return Move(c,s,5,false,0,limit);
    }
    internal static bool Move(PetMoveContext c,PetMoveServices s,int mode,bool run,int velocity,int leash)
    {
        Validate(c,s);
        int A(PetMoveKind kind,int a=0,int b=0,int d=0)=>s.Action(new(kind,a,b,d));
        bool At(PetMoveKind kind,Point2D p)=>A(kind,p.X,p.Y)!=0;
        void Speed(int first,int value,int cap)=>A(PetMoveKind.Velocity,first,value,cap);
        Point2D Half(Point2D p)=>new((p.X+c.Unit.X)/2,(p.Y+c.Unit.Y)/2);
        switch(mode)
        {
            case 0:
                int index=c.Direction8,room=s.RoomAt(c.Owner);
                for(int i=0;i<7;i++)
                {
                    var offset=s.DirectionOffset(Opposite[index]);
                    var point=new Point2D(c.OwnerFinal.X+8*offset.X,c.OwnerFinal.Y+8*offset.Y);
                    if(s.RoomAt(point)==room)
                    {
                        Speed(0,velocity,40);var kind=run?PetMoveKind.RunDelete:PetMoveKind.WalkDelete;
                        if(At(kind,point) || At(kind,Half(point))){return true;}
                    }
                    index=(index+1)&7;
                }
                A(PetMoveKind.DeleteThink);
                if(A(PetMoveKind.Wander,4)!=0 || run && At(PetMoveKind.RunDelete,Half(c.Owner)) || At(PetMoveKind.WalkDelete,Half(c.Owner))){return true;}
                return At(PetMoveKind.Walk,c.Owner);
            case 1:
                if(c.OwnerMode==2)
                {
                    Speed(0,0,100);
                    if(At(PetMoveKind.WalkDelete,c.OwnerFinal) || At(PetMoveKind.WalkDelete,Half(c.OwnerFinal))){return true;}
                }
                if(velocity==0){velocity=(int)(s.Random()%40)+40;}
                int cursor=c.TrailCursor;bool setParameters=false;
                for(int i=0;i<20;i++)
                {
                    cursor=cursor!=0?cursor-1:19;var point=c.Trail[cursor];
                    if(point.X==0 || point.Y==0 || s.DistanceTo(point)<=5){continue;}
                    Speed(0,velocity,100);
                    if(At(run?PetMoveKind.Run:PetMoveKind.WalkDelete,point)){return true;}
                    A(PetMoveKind.DeleteThink);Speed(15,velocity,100);
                    if(At(run?PetMoveKind.RunDelete:PetMoveKind.WalkDelete,point)){return true;}
                    if(!setParameters)
                    {
                        setParameters=true;
                        // A null monster AI parameter is still passed to the
                        // source setter. The service owns that pointer boundary.
                        s.Action(new(PetMoveKind.VelocityParameters,1,velocity,100,c.HasMonsterAi?1:0));
                        if(At(run?PetMoveKind.RunDelete:PetMoveKind.WalkDelete,point)){return true;}
                    }
                }
                int distance=Math.Max(4,c.Distance/4);Speed(0,15,0);
                return A(PetMoveKind.Wander,distance)!=0;
            case 2:
                if(s.Percentage()>=10){A(PetMoveKind.Idle,15);return true;}
                if(A(PetMoveKind.Wander,(int)(s.Random()%3)+3)!=0){return true;}
                A(PetMoveKind.DeleteThink);Speed(0,0,40);
                if(!At(PetMoveKind.WalkDelete,c.OwnerFinal)){A(PetMoveKind.Idle,15);}
                return true;
            case 3:
                var destination=s.FindSkeletonTeleport();
                if(destination is{} point3 && s.TeleportRoom(point3) && At(PetMoveKind.MoveUnit,point3))
                {A(PetMoveKind.Refresh);A(PetMoveKind.MarkTeleported);A(PetMoveKind.Idle,5);return true;}
                return false;
            case 4:
                if(A(PetMoveKind.CountOthers)<1){A(PetMoveKind.Idle,15);return true;}
                A(PetMoveKind.DeleteThink);
                if(A(PetMoveKind.Escape,leash,1)==0)
                {A(PetMoveKind.DeleteThink);if(A(PetMoveKind.Wander,leash)==0){A(PetMoveKind.Idle,15);}}
                return true;
            case 5:
                Speed(7,0,0);
                if(A(PetMoveKind.WalkOwner,leash)!=0){return true;}
                A(PetMoveKind.DeleteThink);
                if(A(PetMoveKind.Escape,leash,1)!=0){return true;}
                Speed(0,0,40);return At(PetMoveKind.WalkDelete,c.OwnerFinal);
            default:return false;
        }
    }
}
