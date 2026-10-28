// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal readonly record struct CreeperHeadRequest(int X,int Y,int OffsetX,int OffsetY,int InitializerIndex);
internal readonly record struct CreeperTrailContact(bool Damage,bool Remove,bool HitEvent,uint LastCollision);

// Prepared legacy callbacks only. Current Range evaluation, owner stats, motion,
// world recipient selection and the learned actor are deliberately separate.
internal static class DruidCreeperVines
{
    internal static CreeperHeadRequest[] Heads(int rank,int targetX,int targetY)
    {
        if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}
        if(NecromancerSummons.Source("Vine Attack","srvdofunc")!=130
            || NecromancerSummons.Source("Vine Attack","Param1")!=3
            || NecromancerSummons.Source("Vine Attack","Param2")!=1)
        {throw new NotSupportedException("Unsupported Vine Attack route");}
        // NPC row294 has its own ln12 parameters, not player row222's params.
        int count=Math.Min(NecromancerSummons.Source("Vine Attack","Param1")
            +checked((rank-1)*NecromancerSummons.Source("Vine Attack","Param2")),24);
        var heads=new CreeperHeadRequest[count];
        for(int i=0;i<count;i++)
        {
            var offset=(i%4) switch{0=>(0,1),1=>(0,-1),2=>(1,0),_=>(-1,0)};
            heads[i]=new(targetX,targetY,offset.Item1,offset.Item2,i);
        }
        return heads;
    }
    // Legacy MISSILE_GetRemainingFrames is total minus current countdown:
    // elapsed age, despite its name. Do not pass native Remaining here.
    internal static bool EmitsTrail(int ageFrames,int parameter)
        =>ageFrames%Math.Max(parameter,1)==0;
    internal static bool EmitsTrail(int ageFrames)
    {
        if(NecromancerSummons.Source("plague vines","pSrvDoFunc")!=26)
        {throw new NotSupportedException("Unsupported plague-vines head callback");}
        return EmitsTrail(ageFrames,NecromancerSummons.Source("plague vines","Param1"));
    }
    internal static CreeperTrailContact Contact(int totalFrames,int ageFrames,uint lastCollision,uint victim,
        bool hasOwner,bool hostile,bool ending)
    {
        if(NecromancerSummons.Source("plague vines trail","pSrvHitFunc")!=50
            || NecromancerSummons.Source("plague vines trail","LastCollide")!=1
            || NecromancerSummons.Source("plague vines trail","AlwaysExplode")!=1)
        {throw new NotSupportedException("Unsupported plague-vines trail callback");}
        if(victim!=0)
        {
            if(victim==lastCollision){return new(false,false,false,lastCollision);}
            lastCollision=victim;
            if(hasOwner && !hostile){return new(false,false,false,lastCollision);}
        }
        // Full hit50/router permits early contacts and excludes the last15
        // frames. Its misleading RemainingFrames getter returns elapsed age.
        // LastCollide has already changed, including on inactive/refused hits.
        bool damage=victim!=0 && (long)ageFrames<(long)totalFrames-NecromancerSummons.Source("plague vines trail","sHitPar1");
        return new(damage,ending,true,lastCollision);
    }
}
