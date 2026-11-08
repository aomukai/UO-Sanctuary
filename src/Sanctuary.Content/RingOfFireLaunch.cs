// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Numerics;
namespace Sanctuary;

internal sealed record RingOfFireLaunchContext(uint Caster,int Kind,bool HasPath,int X,int Y,int Skill,int Rank,int Selector,int MissileCount,int Context12c);
internal sealed record RingOfFireLaunchRequest(int Flags,uint Caster,int Missile,int X,int Y,int TargetX,int TargetY,int Velocity,int Skill,int Rank,int Context12c);
internal sealed record RingOfFireLaunchServices(Action MarkServerDo,Func<bool> SkillRecord,Func<int> ProgressiveMissile,
    Func<int,int> BaseVelocity,Func<int,int> Formula,Func<RingOfFireLaunchRequest,bool> Create);

// Current captured do154 composition only. Getter/formula/factory services
// are explicit;no protected helper,world missile or damage17 is invented.
internal static class RingOfFireLaunch
{
    private static int Count(int value)
    {
        if((value&unchecked(value-1))!=0)
        {value=1<<unchecked(BitOperations.Log2((uint)(value|1))+1);}
        return Math.Clamp(value,1,64); // Preserve current signed clamp/shift.
    }
    internal static int Run(RingOfFireLaunchContext c,RingOfFireLaunchServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(s);
        if(c.Caster!=0){s.MarkServerDo();}
        if(!s.SkillRecord()){return 0;}
        int missile=s.ProgressiveMissile();if(missile<0){return 0;}
        if(c.Selector is<1 or>3){throw new NotSupportedException("Current Ring launch requires source table selector1..3");}
        if(missile>=c.MissileCount){return 0;}
        int velocity=unchecked(s.BaseVelocity(missile)+s.Formula(0x198));
        int count=Count(s.Formula(0x194));
        if(c.Caster==0||c.Kind is 2 or 4 or 5&&!c.HasPath)
        {throw new NotSupportedException("Ring launch has no qualified source/path context");}
        bool stationary=c.Kind is 2 or 4 or 5;
        int x=stationary?c.X:c.HasPath?unchecked((ushort)c.X):0;
        int y=stationary?c.Y:c.HasPath?unchecked((ushort)c.Y):0;
        for(int direction=0,step=64/count;direction<64;direction+=step)
        {
            var vector=FrozenOrbs.Direction(direction);
            _=s.Create(new(velocity==0?3:7,c.Caster,missile,x,y,vector.X,vector.Y,velocity,c.Skill,c.Rank,c.Context12c));
        }
        return 1; // Each allocation result is deliberately independent.
    }
}
