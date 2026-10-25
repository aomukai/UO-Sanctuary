// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

// Prepared finisher component. The learned Fists action stays gated until its
// separate claw sequence, selected-hand timing and input have native acceptance.
internal sealed record FistsFirePacket(int Count,int Rank,int ConversionPercent,int Inline256,int Min256,int Max256,
    int FieldMin256,int FieldMax256,int Radius,int ScatterRadius);
public static class FistsFireRelease
{
    internal static Func<int,int> DamageRoll=Utility.Random;
    // Owner seed acquisition is a native RNG adapter; the child scatter itself
    // uses the source LCG, including its asymmetric range and rejected attempts.
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    internal static int Rank(MartialCharge state,int live)=>state is{Ability:CombatAbility.FistsOfFire,Count:>=1 and <=3}
        ?Math.Max(state.InitialRank,live):0;
    internal static FistsFirePacket Capture(PlayerMobile owner,MartialCharge state)
    {
        int rank=Rank(state,SkillMechanics.Rank(owner,CombatAbility.FistsOfFire));
        if(rank is <1 or >255){return null;}
        return Capture(SkillMechanics.Values(owner,CombatAbility.FistsOfFire,rank),state.Count,NamedElements.MasteryPercent(owner,"fire"),DamageRoll);
    }
    internal static FistsFirePacket Capture(SkillValues values,int count,int mastery,Func<int,int> random)
    {
        if(values.Ability!=CombatAbility.FistsOfFire || values.Rank is <1 or >255 || count is <1 or >3)
        {throw new NotSupportedException("Unmapped Fists release context");}
        int Enhance(int damage)=>checked((int)((long)damage*Math.Max(0,100+mastery)/100));
        int minimum=Enhance(values.Damage256(false)),maximum=Enhance(values.Damage256(true));
        int synergy=NecromancerSummons.Formula("fistsoffirefirewall","EDmgSymPerCalc",values);
        return new(count,values.Rank,Math.Clamp((int)values.Eval("calc1"),0,100),WeaponMissileDamage.RollValue(minimum,maximum,0,random),minimum,maximum,
            Enhance(FireGround.MissileDamage256("fistsoffirefirewall",values.Rank,synergy,false)),
            Enhance(FireGround.MissileDamage256("fistsoffirefirewall",values.Rank,synergy,true)),
            (int)values.Eval("prgcalc2"),(int)values.Eval("prgcalc3"));
    }
    internal static Point2D[] Scatter(int radius,int lowSeed)
    {
        if(radius is <1 or >64){throw new NotSupportedException("Unmapped progressive scatter radius");}
        ulong seed=SourceRandom.InitLow(lowSeed);int area=radius*radius;var points=new List<Point2D>();
        for(int i=0;i<area;i++)
        {
            int x=radius-(int)SourceRandom.Limited(ref seed,2*radius),y=radius-(int)SourceRandom.Limited(ref seed,2*radius);
            if(x*x+y*y<=area){points.Add(new(x,y));} // Duplicates are separate source missiles.
        }
        return points.ToArray();
    }
    internal static bool InRadius(Point3D center,Mobile target,int radius)
    {
        long dx=2L*(target.X-center.X),dy=2L*(target.Y-center.Y);
        return dx*dx+dy*dy<=(long)radius*radius && Math.Abs(target.Z-center.Z)<16;
    }
    internal static void AfterParent(PlayerMobile owner,Point3D center,Map map,FistsFirePacket packet,DateTime at)
    {
        if(packet==null || packet.Count<2 || owner is not{Alive:true,Deleted:false} || owner.Map!=map || map==null || map==Map.Internal){return;}
        // Parent weapon damage, durability, drains and procs have committed. A
        // distinct skill roll is shared by the area's individually reduced hits.
        int area=WeaponMissileDamage.RollValue(packet.Min256,packet.Max256,0,DamageRoll);
        Effects.SendLocationEffect(center,map,0x36BD,3); // Identified Fists explosion placeholder.
        foreach(var target in map.GetMobilesInRange<Mobile>(center,(packet.Radius+1)/2))
        {
            // Default filter0x583 has no optional LOS or NOAURA check. Native
            // hostility/town/height are the established Sanctuary projection.
            if(InRadius(center,target,packet.Radius) && ClassCombat.IsHostile(owner,target)
                && !SpellHelper.IsTown(target.Location,owner)){SkillCombat.FireImpact(owner,target,area);}
        }
        if(packet.Count<3){return;}
        foreach(var offset in Scatter(packet.ScatterRadius,SeedLow()))
        {
            double x=center.X+offset.X/2.0,y=center.Y+offset.Y/2.0;
            if(x<0 || y<0 || x>=map.Width || y>=map.Height){continue;}
            var point=new Point3D((int)Math.Round(x),(int)Math.Round(y),center.Z);
            if(SpellHelper.IsTown(point,owner)){continue;}
            FireGround.AddFists(owner,packet.Rank,x,y,center.Z,packet.FieldMin256,packet.FieldMax256,at);
        }
    }
}
