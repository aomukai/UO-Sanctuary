// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

internal sealed record SigilLaunchContext(uint Caster,int Skill,int Rank,int Selector,int MissileCount,int Context12c,int State,int Frame);
internal sealed record SigilLaunchRequest(uint Caster,GroundMissileRequest Body,int Context12c);
internal sealed record SigilCreatedMissile(uint Id,int Kind,bool HasData,float Data14);
internal sealed record SigilTrackingRequest(uint Missile,int Limit,int State,int Start,int End,int RemovalCallbackRva);
internal sealed record SigilLaunchServices(Func<bool> SkillRecord,Func<int> ProgressiveMissile,Action MarkServerDo,Func<int,bool> Placement,
    Func<Point2D> Coordinates,Func<Point2D,int> Distance,Func<SigilLaunchRequest,SigilCreatedMissile> Create,Func<int,int> Formula,
    Func<SigilTrackingRequest,bool> Track,Action<int> ScheduleEvent12);

// Complete current do157 composition for all three Sigils. World allocation,
// tracking-list replacement/cleanup and recipient effects remain explicit
// services until connected;this component alone enables no learned cast.
internal static class SigilLaunch
{
    internal static int Run(SigilLaunchContext c,SigilLaunchServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(s);
        if(!s.SkillRecord()){return 0;}
        int missile=s.ProgressiveMissile();if(missile<0){return 0;}
        if(c.Selector is <1 or >3){throw new NotSupportedException("Current Sigil requires table selector1..3");}
        if(missile>=c.MissileCount){return 0;}
        if(c.Caster!=0){s.MarkServerDo();}
        if(!s.Placement(missile)||c.Caster==0){return 0;}
        var point=s.Coordinates();if(point.X==0&&point.Y==0){return 0;}
        if(unchecked((uint)s.Distance(point))>100){return 0;}
        var actor=s.Create(new(c.Caster,new(c.Skill,c.Rank,missile,point),c.Context12c));if(actor==null){return 0;}
        int limit=s.Formula(0x198),duration=0;
        if(actor.Kind==3&&actor.HasData)
        {
            float rounded=MathF.Ceiling(actor.Data14/40f);
            // The actual current finite-input rounding helper is captured.
            // Keep x64 CVTTSS2SI's out-of-range result explicit as well.
            duration=!float.IsFinite(rounded)||rounded>=2147483648f||rounded< -2147483648f?int.MinValue:unchecked((int)rounded);
        }
        int end=unchecked(c.Frame+duration);
        if(s.Track(new(actor.Id,limit,c.State,c.Frame,end,0x3a7f30))){s.ScheduleEvent12(end);}
        return 1; // Tracking refusal does not undo successful creation.
    }
}
