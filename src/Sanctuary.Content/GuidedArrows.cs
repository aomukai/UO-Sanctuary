// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Spells;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record GuidedFlight(int Mode,uint Target,int Remaining,int OriginalX,int OriginalY,int VelocityFp8);

// Saved source mode1/2/5/6 controller. Shared weapon flights still own damage,
// geometry, item events, source block, owner lifecycle and persistence.
public static class GuidedArrows
{
    internal static int InitialFrames(int dx,int dy,int velocity)
    {
        int distance=Math.Max(1,SkillGeometry.SourceDistance(dx,dy));
        return velocity>0?(distance<<16)/(16*velocity):0;
    }
    internal static bool Turn(int mode,int remaining,int cadence,bool valid,int distance)
        =>(mode&1)!=0 && valid && remaining%(cadence>0?cadence:5)==0 && distance>3 && distance<25;
    internal static int Hit(int mode,int remaining,int victim,int pathFlags=0)
    {
        if(victim==0 && ((mode&4)!=0 || (pathFlags&4)!=0)){return 1;}
        if((mode&1)!=0){return victim==2?4:3;}
        if((mode&2)==0){return 3;}
        if(remaining>0){return 4-((mode&4)!=0?1:0);}
        return (mode&4)!=0?1:4;
    }
    internal static bool Valid(GuidedFlight g)=>g!=null && g.Mode is 1 or 2 or 5 or 6 && g.Remaining is >=0 and <=128
        && g.VelocityFp8 is >0 and <=16384 && g.OriginalX is >-100 and <100 && g.OriginalY is >-100 and <100
        && (g.OriginalX!=0 || g.OriginalY!=0) && ((g.Mode&1)!=0?((Serial)g.Target).IsMobile && g.Target!=0:g.Target==0);
    internal static GuidedFlight Capture(PlayerMobile owner,Mobile target,Point3D point,int dx,int dy)
    {
        int velocity=(NecromancerSummons.Source("guidedarrow","Vel")<<8)*75/100;
        return new(target!=null?1:2,target?.Serial.Value??0,target!=null?NecromancerSummons.Source("guidedarrow","Range"):InitialFrames(dx,dy,velocity),dx,dy,velocity);
    }
    internal static int Distance(WeaponFlight flight,Mobile target,int size)
    {
        int dx=Math.Abs((int)Math.Floor(flight.X*SkillGeometry.SubtilesPerTile)-(int)(target.X*SkillGeometry.SubtilesPerTile));
        int dy=Math.Abs((int)Math.Floor(flight.Y*SkillGeometry.SubtilesPerTile)-(int)(target.Y*SkillGeometry.SubtilesPerTile));
        int offset=1/-2-size/2;return SkillGeometry.SourceDistance(Math.Max(0,dx+offset),Math.Max(0,dy+offset));
    }
    private static (double X,double Y) Velocity(double x,double y,double targetX,double targetY,int velocity)
    {
        int dx=(int)Math.Round((targetX-x)*SkillGeometry.SubtilesPerTile*65536),dy=(int)Math.Round((targetY-y)*SkillGeometry.SubtilesPerTile*65536);
        return dx==0 && dy==0?(0,0):MissileVectors.VelocityFp8(dx,dy,velocity);
    }
    internal static WeaponFlight Steer(PlayerMobile owner,WeaponFlight flight)
    {
        if(flight.Guided is not{} g){return flight;}
        var target=World.FindMobile((Serial)g.Target);
        bool valid=ClassCombat.IsHostile(owner,target);int size=target is BaseCreature c?CreatureProfiles.For(c).Traits.Size??2:2;
        if(!Turn(g.Mode,g.Remaining,NecromancerSummons.Source("guidedarrow","Param1"),valid,valid?Distance(flight,target,size):0)){return flight;}
        var v=Velocity(flight.X,flight.Y,target.X,target.Y,g.VelocityFp8);return flight with{Vx=v.X,Vy=v.Y};
    }
    internal static bool Accepts(WeaponFlight flight,Mobile target)
        =>Hit(flight.Guided.Mode,flight.Guided.Remaining,target.Serial.Value==flight.Guided.Target?1:2)==3;
    internal static WeaponFlight Extend(PlayerMobile owner,WeaponFlight flight)
    {
        var g=flight.Guided;if(g.Mode!=2 || g.Remaining>0){return null;}
        // Source range is centered on the integer missile subtile, while
        // the filter's LOS is from owner to candidate, not from that center.
        int sourceX=(int)Math.Floor(flight.X*SkillGeometry.SubtilesPerTile),sourceY=(int)Math.Floor(flight.Y*SkillGeometry.SubtilesPerTile);
        int radius=NecromancerSummons.Source("guidedarrow","Param2");
        var center=new Point3D((int)Math.Round(flight.X),(int)Math.Round(flight.Y),flight.Z);Mobile target=null;
        foreach(var candidate in owner.Map.GetMobilesInRange<Mobile>(center,(int)Math.Ceiling(SkillGeometry.Tiles(radius)+1)))
        {
            int dx=(int)(candidate.X*SkillGeometry.SubtilesPerTile)-sourceX,dy=(int)(candidate.Y*SkillGeometry.SubtilesPerTile)-sourceY;
            if(dx*dx+dy*dy>radius*radius || Math.Abs(candidate.Z-flight.Z)>=16 || !ClassCombat.IsHostile(owner,candidate)
                || !owner.InLOS(candidate) || SpellHelper.IsTown(candidate.Location,owner)){continue;}
            // previous=-1 makes the complete source selector choose lowest ID.
            if(target==null || candidate.Serial.Value<target.Serial.Value){target=candidate;}
        }
        int range=NecromancerSummons.Source("guidedarrow","Range");
        var next=g with{Mode=target!=null?5:6,Target=target?.Serial.Value??0,Remaining=range};
        var v=target!=null?Velocity(flight.X,flight.Y,target.X,target.Y,g.VelocityFp8):MissileVectors.VelocityFp8(g.OriginalX,g.OriginalY,g.VelocityFp8);
        return flight with{Guided=next,Frames=flight.Frame+range,Vx=v.X,Vy=v.Y};
    }
}
