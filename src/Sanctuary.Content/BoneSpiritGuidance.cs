// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// The do10/do7/hit10 state machine is shared with Guided Arrow. Only source-zero
// spell packets enter this controller; weapon AR/Pierce/events stay separate.
internal static class BoneSpiritGuidance
{
    internal static GuidedFlight Capture(PlayerMobile owner,Mobile target,Point3D point,int velocity)
    {
        int dx=(point.X-owner.X)*2,dy=(point.Y-owner.Y)*2;
        return new(target!=null?1:2,target?.Serial.Value??0,target!=null?128:GuidedArrows.InitialFrames(dx,dy,velocity),
            (short)dx,(short)dy,velocity);
    }
    private static (double X,double Y) Vector(double x,double y,double tx,double ty,int velocity)
    {
        int dx=(int)Math.Round((tx-x)*2*65536),dy=(int)Math.Round((ty-y)*2*65536);
        return dx==0&&dy==0?(0,0):MissileVectors.VelocityFp8(dx,dy,velocity);
    }
    internal static int Distance(NecromancerSpellFlight s,Mobile target)
    {
        int size=target is BaseCreature c?CreatureProfiles.For(c).Traits.Size??2:2;
        int dx=Math.Abs((int)Math.Floor(s.X*2)-target.X*2),dy=Math.Abs((int)Math.Floor(s.Y*2)-target.Y*2);
        return SkillGeometry.SourceDistance(Math.Max(0,dx-size/2),Math.Max(0,dy-size/2));
    }
    internal static NecromancerSpellFlight Steer(PlayerMobile owner,NecromancerSpellFlight s)
    {
        var g=s.Guided;var target=World.FindMobile((Serial)g.Target);
        bool valid=target is{Alive:true,Deleted:false}&&target.Map==owner.Map&&ClassCombat.IsHostile(owner,target);
        if(!GuidedArrows.Turn(g.Mode,g.Remaining,5,valid,valid?Distance(s,target):0)){return s;}
        var v=Vector(s.X,s.Y,target.X,target.Y,g.VelocityFp8);return s with{Vx=v.X,Vy=v.Y};
    }
    internal static NecromancerSpellFlight Acquire(PlayerMobile owner,NecromancerSpellFlight s)
    {
        var g=s.Guided;if(g.Mode!=2||g.Remaining>0){throw new InvalidOperationException("Spirit acquisition outside first ground expiry");}
        int x=(int)Math.Floor(s.X*2),y=(int)Math.Floor(s.Y*2);Mobile selected=null;
        var center=new Point3D((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z);
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(center,9))
        {
            int dx=target.X*2-x,dy=target.Y*2-y;
            if(dx*dx+dy*dy>225||Math.Abs(target.Z-s.Z)>=16||!ClassCombat.IsHostile(owner,target)||!owner.CanBeHarmful(target,false)
                ||!owner.InLOS(target)||SpellHelper.IsTown(target.Location,owner)){continue;}
            // The complete source selector with previous=-1 chooses lowest ID,
            // independently of candidate enumeration or distance ordering.
            if(selected==null||target.Serial.Value<selected.Serial.Value){selected=target;}
        }
        var next=g with{Mode=selected!=null?5:6,Target=selected?.Serial.Value??0,Remaining=128};
        var v=(s.Vx,s.Vy);
        if(selected!=null)
        {if(Distance(s,selected)<25){v=Vector(s.X,s.Y,selected.X,selected.Y,g.VelocityFp8);}}
        else if(SkillGeometry.SourceDistance(g.OriginalX,g.OriginalY)<25)
        {v=Vector(s.X,s.Y,s.X+g.OriginalX/2.0,s.Y+g.OriginalY/2.0,g.VelocityFp8);}
        return s with{Guided=next,Frames=checked(s.Frame+128),Vx=v.Item1,Vy=v.Item2};
    }
    internal static bool Valid(NecromancerSpellFlight s)
    {
        var g=s.Guided;
        return g!=null&&g.Mode is 1 or 2 or 5 or 6&&g.Remaining>=0&&s.Frames==s.Frame+g.Remaining
            &&s.Frames>=0&&g.VelocityFp8==s.VelocityFp8&&g.VelocityFp8 is >=0 and <=2304
            &&g.OriginalX is >=short.MinValue and <=short.MaxValue&&g.OriginalY is >=short.MinValue and <=short.MaxValue
            &&((g.Mode&1)!=0?g.Target!=0&&((Serial)g.Target).IsMobile:g.Target==0)
            &&s.Flags==((g.Mode==1)?0x20u:0x420u)&&s.Cast==0&&s.Activation==0&&s.LastCollider==0;
    }
}
