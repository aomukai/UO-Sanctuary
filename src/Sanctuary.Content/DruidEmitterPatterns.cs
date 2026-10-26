// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace Sanctuary;

// Shared SkillDruid sub_6FCFE4C0 launch requests: Firestorm starts with an
// ordinary emitter; Twister/Tornado use only the seeded charged-path requests.
// This kernel does not enable a learned skill or substitute for missile motion.
internal sealed record DruidEmitterAim(int OriginX,int OriginY,int GroundX,int GroundY,
    bool FanTargetAvailable,int? TargetX=null,int? TargetY=null);
internal sealed record DruidEmitterRequest(int Flags,int Missile,int X,int Y,int AimX,int AimY,
    bool Charged,int Ordinal,int Loops,int Rank)
{public int? RangeOverride{get;init;}}
internal sealed record DruidChargedInitialization(int Frames,int? SetCurrentFrame,uint Seed,int Distance);
internal sealed record DruidMakerStep(int Result,bool ProcessCollision,DruidEmitterRequest Child);
internal static class DruidEmitterPatterns
{
    internal static DruidEmitterRequest[] Launch(int count,bool initialStraight,DruidEmitterAim aim,int missile,int rank)
    {
        if(aim==null || aim.TargetX.HasValue!=aim.TargetY.HasValue || count is < -256 or >256 || missile<0 || rank is <1 or >255)
        {throw new NotSupportedException("Unmapped Druid emitter context");}
        var result=new List<DruidEmitterRequest>();
        if(initialStraight)
        {
            int x=aim.TargetX??aim.GroundX,y=aim.TargetY??aim.GroundY;
            // The ordinary factory rejects a missing X or Y. The subsequent
            // fan query is separate and can fail after this first request.
            if(x!=0 && y!=0){result.Add(new(33,missile,aim.OriginX,aim.OriginY,x,y,false,0,0,rank));}
            count--;
            if(count<=0){return result.ToArray();}
        }
        if(!aim.FanTargetAvailable){return result.ToArray();}
        for(int ordinal=0;ordinal<count;ordinal++)
        {result.Add(new(33,missile,aim.OriginX,aim.OriginY,aim.GroundX,aim.GroundY,true,ordinal,0,rank));}
        return result.ToArray();
    }
    internal static DruidChargedInitialization InitializeCharged(int frames,int aimX,int ordinal)
    {
        if(frames is <1 or >255 || ordinal is <0 or >255){throw new NotSupportedException("Unmapped Druid charged initializer");}
        int total=Math.Min(frames,77);
        return new(total,frames>=78?77:null,unchecked((uint)(aimX+ordinal)),total);
    }
    internal static DruidMakerStep Maker(bool hasMissile,bool hasRow,int childMissile,int pathFlags,int loops,
        int x,int y,int rank,int collisionResult)
    {
        // Complete do23 checks only zero, not a positive child ID. The real
        // factory owns ID validation; keep that boundary visible in this plan.
        if(!hasMissile || !hasRow || childMissile==0){return new(2,false,null);}
        DruidEmitterRequest child=(pathFlags&8)==0?null:new(loops>0?9:1,childMissile,x,y,0,0,false,0,loops>0?loops:0,rank);
        // Child creation precedes collision/lifetime handling, even if that
        // handler returns terminal removal. World integration must retain it.
        return new(collisionResult,true,child);
    }
}
