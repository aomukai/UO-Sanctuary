// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Cfg+0x70 remains a neutral source field until its producer is established.
internal sealed record GenericPetFollowExecution(GenericPetDecisionUnit Pet,GenericPetDecisionUnit Owner,
    int Mode,int CloseRaw,int WanderRaw,int Word70);
// Opaque room handles and leaf actions preserve the current calling contract;
// these are not a substitute for the eventual native world/path bindings.
internal sealed record GenericPetFollowExecutionServices(Func<(int X,int Y)> OwnerDestination,
    Func<int> OwnerRoom,Func<int,int,int,int> RoomAt,Func<int,int,int,int?> Region,
    Func<int,int,int> Direction,Func<int> MonsterFlags,
    Action<int,byte,int> Prepare,Action EnsureThink,Func<int,int,int> Point,
    Func<int,int,byte,int,ushort,int> Move,
    Func<int,int,int,byte,int,byte,int,int> Bound,
    Func<int,int,int,int,byte,int,int> Alternate,
    Func<int,int,byte,byte,int> Toward,Func<int> Warp);
internal static class GenericPetFollowExecutor
{
    // Pinned immutable image RVAs17195d0,1724b28,17195b0,1719650.
    private static ReadOnlySpan<byte> Heading=>[0,0,0,0,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,3,3,3,3,3,3,3,3,4,4,4,4,4,4,4,4,5,5,5,5,5,5,5,5,6,6,6,6,6,6,6,6,7,7,7,7,7,7,7,7,0,0,0,0];
    private static ReadOnlySpan<byte> Order=>[4,3,2,1,0,7,6,5];
    private static ReadOnlySpan<sbyte> OffsetX=>[0,-1,-1,-1,0,1,1,1];
    private static ReadOnlySpan<sbyte> OffsetY=>[-1,-1,0,1,1,1,0,-1];
    private static byte Low(int value)=>unchecked((byte)value);
    private static int Mid(int a,int b)=>(int)(unchecked((uint)(a+b))>>1);
    private static byte Radius(int word,int add,int maximum)
    {int raw=unchecked(word/2+add);return raw<maximum?Low(raw):(byte)maximum;}

    // Preserve the source's low-byte result, including its unnormalized final
    // wrapper returns. Bool-facing callers test this byte, not the full int.
    internal static byte Execute(GenericPetFollowExecution c,GenericPetActionRandom random,GenericPetFollowExecutionServices s)
    {
        if(c.Pet==null||(c.Owner==null&&c.Mode!=2)){return 0;}
        var(px,py)=c.Pet.Coordinates();var(ox,oy)=c.Owner?.Coordinates()??(0,0);
        var(dx,dy)=c.Owner!=null?s.OwnerDestination():(0,0);
        if((uint)c.Mode>5){return 0;}
        byte Finish(int result){if(result==0){return 0;}s.EnsureThink();return 1;}
        int Region(int room,int x,int y)=>room==0?0:s.Region(room,x/5,y/5)??-1;
        if(c.Mode==0)
        {
            int room=s.OwnerRoom();int region=Region(s.RoomAt(room,ox,oy),ox,oy);
            byte direction=Low(s.Direction(dx,dy));
            if(direction>=Heading.Length){throw new NotSupportedException("Current follow heading outside captured64-entry table");}
            int start=Heading[direction];
            for(int i=0;i<8;i++)
            {
                int offset=Order[(start+i)&7];int x=unchecked(dx+OffsetX[offset]*8),y=unchecked(dy+OffsetY[offset]*8);
                int candidate=s.RoomAt(room,x,y);candidate=s.RoomAt(candidate,x,y);
                if(Region(candidate,x,y)!=region){continue;}
                s.Prepare(0,0,0);int result=s.Point(x,y);if(result!=0){return Finish(result);}
                s.Prepare(0,0,0);result=s.Point(Mid(x,px),Mid(y,py));if(result!=0){return Finish(result);}
            }
            // After the ring is exhausted the source rereads the owner's
            // current coordinates. Its final point is current, not destination.
            (ox,oy)=c.Owner.Coordinates();s.Prepare(0,10,0);
            int moved=s.Move(ox,oy,4,0,4);if(moved!=0){return Finish(moved);}
            s.Prepare(0,10,0);int midpoint=s.Point(Mid(ox,px),Mid(oy,py));if(midpoint!=0){return Finish(midpoint);}
            s.Prepare(0,10,0);return Finish(s.Point(ox,oy));
        }
        if(c.Mode==1)
        {
            int flags=s.MonsterFlags(),hint=flags>=0&&(flags&128)!=0?1:0;s.Direction(dx,dy);
            byte radius=Radius(c.Word70,2,12);
            foreach(int options in new[]{0,15,1})
            {if(Low(s.Bound(dx,dy,hint,radius,c.CloseRaw,100,options))!=0){return 1;}}
            if(dx==ox&&dy==oy){return 0;}
            foreach(int options in new[]{0,15})
            {if(Low(s.Alternate(ox,oy,hint,c.CloseRaw,100,options))!=0){return 1;}}
            return Low(s.Alternate(ox,oy,hint,c.CloseRaw,100,1));
        }
        if(c.Mode==2)
        {
            if((int)(SourceRandom.Roll(ref random.Seed)%100)>=c.WanderRaw){return 0;}
            if(c.Owner!=null)
            {
                byte step=(byte)(SourceRandom.Roll(ref random.Seed)%3+3);s.Prepare(0,0,0);
                int moved=s.Move(ox,oy,step,0,4);if(moved!=0){return Finish(moved);}
                s.Prepare(0,0,0);int point=s.Point(dx,dy);if(point!=0){return Finish(point);}
            }
            byte last=(byte)(SourceRandom.Roll(ref random.Seed)%3+3);s.Prepare(0,0,0);
            return Finish(s.Move(px,py,last,0,4));
        }
        if(c.Mode==3){return (byte)(s.Warp()!=0?1:0);}
        byte wide=Radius(c.Word70,7,36);
        if(c.Mode==4)
        {
            if(c.Owner.Kind==0&&c.Owner.HasPlayerData&&Low(s.Toward(c.Owner.DestinationX,c.Owner.DestinationY,wide,wide))!=0){return 1;}
            (ox,oy)=c.Owner.Coordinates();return Low(s.Bound(ox,oy,0,wide,0,wide,0));
        }
        (ox,oy)=c.Owner.Coordinates();if(Low(s.Bound(ox,oy,0,wide,0,wide,7))!=0){return 1;}
        (ox,oy)=c.Owner.Coordinates();if(Low(s.Toward(ox,oy,wide,wide))!=0){return 1;}
        s.Prepare(0,36,0);return Finish(s.Point(dx,dy));
    }
    internal static GenericPetFollowServices Bind(GenericPetFollowServices selector,GenericPetFollowExecution context,
        GenericPetActionRandom random,GenericPetFollowExecutionServices services)
        =>selector with{Execute=mode=>Execute(context with{Mode=mode},random,services)!=0};
}
