// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace Sanctuary;

internal sealed record BoulderPhasePlan(int Result,bool Collision,DruidEmitterRequest[] Requests);
internal sealed record BoulderContactInput(bool HasMissile,bool HasRow,bool HasOwner,int? TargetType,
    bool HasSpecies,bool Large,bool HasSkill,int RowRadius,int AuraRadius,int Step,int Child,
    int X,int Y,int Rank,uint RowHitFlags,int RowResultFlags,uint PacketHitFlags,int PacketResultFlags,bool AreaSuccess);
internal sealed record BoulderAreaPlan(int Radius,uint HitFlags,int ResultFlags);
internal sealed record BoulderContactPlan(int Result,bool FillDamage,BoulderAreaPlan Area,DruidEmitterRequest[] Requests);
internal readonly record struct BoulderKnockbackPlan(int ResultFlags,ulong Seed);
internal readonly record struct BoulderAnimationPlan(int Result,bool Collision,bool SetMask,int Animation256,ulong Seed);
internal sealed record BoulderRoutePlan(int Result,bool WriteLastTarget,bool HitEvent,bool DirectDamage,
    bool ResetMask,BoulderContactPlan Contact,DruidEmitterRequest[] Requests);
internal enum BoulderImpactKind{Rolling,Ground,Area}
internal readonly record struct BoulderImpactPlan(int Flags,bool Damage,bool Push,ulong Seed);

// Complete source callback request/packet kernels. Native factories, movement,
// damage commitment, species mapping, generic hit dispatch and learned casting
// are separate consumers; this file does not enable Molten Boulder.
internal static class MoltenBoulderPackets
{
    internal const int Skill=229;
    internal static BoulderKnockbackPlan ImpactFlags(BoulderImpactKind kind,bool small,bool large,bool uninterruptible,ulong seed)
    {
        if(kind==BoulderImpactKind.Area){return new(5,seed);}
        if(kind==BoulderImpactKind.Ground){return new(1,seed);}
        if(kind!=BoulderImpactKind.Rolling){throw new NotSupportedException("Unmapped Boulder impact");}
        var custom=Knockback(true,3,true,1,true,true,small,large,1,50,0,seed);
        int flags=custom.ResultFlags|1;seed=custom.Seed;
        if(!uninterruptible)
        {
            flags|=4;
            // The independent KnockBack1 row draw is not skipped merely
            // because damage14 already set the knockback flag.
            if(SourceRandom.Roll(ref seed)%100<1){flags|=8;}
        }
        return new(flags,seed);
    }
    internal static BoulderImpactPlan ResolveImpact(BoulderImpactKind kind,BoulderKnockbackPlan initial,int defense,
        bool knockbackMode,bool uninterruptible)
    {
        int flags=initial.ResultFlags;
        if(kind==BoulderImpactKind.Area)
        {
            if((defense&2)!=0){flags|=100;}
            if((defense&4)!=0){flags|=0x80;}
            if((defense&16)!=0){flags|=0x8000;}
            if((defense&1)!=0){flags|=0x10;}
            if(defense!=0){flags&=0xFE;}
        }
        else
        {
            if(kind==BoulderImpactKind.Ground && (defense&0x1F)!=0)
            {flags=flags&~1|0x4000;defense&=~0x1F;}
            if((defense&8)!=0){flags|=0x200;}
            if((defense&2)!=0){flags|=0x100;}
            if((defense&0x11)!=0){flags|=0x10;}
            if(defense!=0){flags&=~1;}
        }
        // Full ExecuteMissileDamage changes an unsupported KB mode to GH.
        // Health events happen before this mode stage, even when uninterruptible.
        if(!uninterruptible && (flags&8)!=0 && !knockbackMode){flags=flags&~8|4;}
        return new(flags,(flags&1)!=0,!uninterruptible && (flags&8)!=0,initial.Seed);
    }
    internal static BoulderPhasePlan Emerge(bool hasMissile,bool hasRow,int child,int x,int y,int aimX,int aimY,int rank)
    {
        if(!hasMissile || !hasRow || child<0){return new(1,false,[]);}
        return new(1,false,[new(33,child,x,y,aimX,aimY,false,0,0,rank)]);
    }
    internal static BoulderPhasePlan Trail(bool hasMissile,bool hasRow,bool hasOwner,bool hasPath,int child,int flags,
        int x,int y,int rank,int collisionResult)
    {
        if(!hasMissile || !hasOwner || !hasRow || child<0 || !hasPath){return new(2,false,[]);}
        return new(collisionResult,true,(flags&8)!=0?[new(33,child,x,y,x,y,false,0,0,rank)]:[]);
    }
    internal static DruidEmitterRequest[] Disc(int child,int range,int step,int x,int y,int rank)
    {
        if(child<0){return [];}
        int[] xs=[2,-2,0,0,-3,0,3,-1,1,-1,2,-4,-3,-1,0,1,3,4];
        int[] ys=[-2,-2,2,5,3,3,3,2,1,-1,-1,-2,-2,-3,-4,-3,-3,-2];
        var result=new List<DruidEmitterRequest>();step=Math.Max(step,1);
        for(int i=0;i<18;i+=step)
        {result.Add(new(range>0?0x8001:1,child,x+xs[i],y+ys[i],0,0,false,0,0,rank){RangeOverride=range>0?range:null});}
        return result.ToArray();
    }
    internal static BoulderContactPlan Contact(BoulderContactInput c)
    {
        if(c==null){throw new ArgumentNullException(nameof(c));}
        if(!c.HasMissile || !c.HasRow || !c.HasOwner){return new(1,false,null,[]);}
        if(c.TargetType!=1 || !c.HasSpecies || !c.Large){return new(2,false,null,[]);}
        // The source fills before looking up a fallback skill radius; retain
        // that service ordering even if the skill-row lookup subsequently fails.
        int radius=c.RowRadius;
        if(radius<=0)
        {
            if(!c.HasSkill){return new(1,true,null,[]);}
            radius=Math.Max(c.AuraRadius,1);
        }
        var area=new BoulderAreaPlan(radius,c.PacketHitFlags|c.RowHitFlags,c.PacketResultFlags|c.RowResultFlags);
        return new(c.AreaSuccess?1:3,true,area,Disc(c.Child,radius,c.Step,c.X,c.Y,c.Rank));
    }
    // Current rows use mode8, no ToHit/NextHit/Pierce/CollideFriend, and
    // LastCollide only on emergence. This is deliberately scoped to
    // those rows, rather than a replacement for the generic missile router.
    internal static BoulderRoutePlan Route(int missile,bool hasTarget,bool sameLastTarget,bool hasOwner,
        bool hostile,bool terminal,BoulderContactInput contact,int x,int y,int aimX,int aimY,int rank)
    {
        if(missile is not (452 or 453 or 455)){throw new NotSupportedException("Unmapped Boulder hit route");}
        bool last=missile==453;
        if(hasTarget && last && sameLastTarget){return new(1,false,false,false,false,null,[]);}
        bool write=hasTarget && last;
        // Source records LastCollide before checking allegiance. Retaining
        // this order prevents a friendly contact from being retried forever.
        if(hasTarget && hasOwner && !hostile){return new(1,write,false,false,false,null,[]);}
        if(missile==455 && !hasTarget && !terminal){return new(1,write,false,false,false,null,[]);}
        int hit=2;BoulderContactPlan packet=null;DruidEmitterRequest[] requests=[];
        if(missile==453)
        {
            var emerge=Emerge(true,true,452,x,y,aimX,aimY,rank);
            hit=emerge.Result;requests=emerge.Requests;
        }
        else if(missile==452)
        {
            if(contact==null || contact.HasOwner!=hasOwner || contact.TargetType.HasValue!=hasTarget)
            {throw new NotSupportedException("Inconsistent Boulder contact services");}
            packet=Contact(contact);hit=packet.Result;requests=packet.Requests;
        }
        bool remove=(hit&1)!=0 || terminal;
        return new(remove?2:1,write,true,hasTarget && (hit&2)!=0,remove,packet,requests);
    }
    internal static BoulderKnockbackPlan Knockback(bool hasMissile,int missileType,bool hasTarget,int targetType,
        bool hasRow,bool hasSpecies,bool small,bool large,int parameter,int basis,int resultFlags,ulong seed)
    {
        if(!hasMissile || missileType!=3 || !hasTarget || !hasRow){return new(resultFlags,seed);}
        uint chance;
        if(targetType==0)
        {if(parameter<1){return new(resultFlags,seed);}chance=unchecked((uint)basis);}
        else if(targetType==1)
        {
            if(!hasSpecies){return new(resultFlags,seed);}
            if(parameter<1){if(!small){return new(resultFlags,seed);}chance=unchecked((uint)basis);}
            else if(parameter==1)
            {if(small){chance=unchecked((uint)(2*basis));}else if(!large){chance=unchecked((uint)basis);}else{return new(resultFlags,seed);}}
            else{chance=unchecked((uint)(basis*(small?3:!large?2:1)));}
        }
        else{return new(resultFlags,seed);}
        if(chance>0 && SourceRandom.Roll(ref seed)%100<chance){resultFlags|=8;}
        return new(resultFlags,seed);
    }
    internal static BoulderAnimationPlan Animate(bool hasMissile,bool hasRow,int remaining,int animation256,ulong seed,
        int subStart,int subStop,int collisionResult)
    {
        if(!hasMissile || !hasRow){return new(collisionResult,true,false,animation256,seed);}
        int current=animation256>>8;
        if(current==subStart-1){animation256=(current+(int)SourceRandom.Limited(ref seed,subStop-subStart))<<8;}
        else if(remaining==subStart){animation256=(subStart-3)<<8;}
        else if(remaining<subStart){animation256=Math.Max(current-2,0)<<8;}
        return new(collisionResult,true,true,animation256,seed);
    }
}
