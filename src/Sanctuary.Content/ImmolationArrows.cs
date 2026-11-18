// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

// Immediate hit09 consumer; existing bow flights own the separate direct hit,
// while FireGround owns source-cell lifetimes and persistence.
public static class ImmolationArrows
{
    internal static Func<int,int> SoftHitRoll=Utility.Random;
    private static readonly HashSet<string> Unsupported=[];
    private static void Report(string reason)
    {if(Unsupported.Add(reason)){Server.Logging.LogFactory.GetLogger(typeof(ImmolationArrows)).Warning("Immolation unsupported context: {Reason}",reason);}}
    internal static void FireFrame(PlayerMobile owner,Mobile target,FirePatch patch,int damage,bool soft)
    {
        if(target is BaseCreature creature)
        {if(!PlayerWeaponMissileContact.TryFireFrame(owner,creature,damage,patch.DamageRate,soft,patch.NextUtc,out string reason)){Report(reason);}}
        else{SkillCombat.FireFrame(owner,target,damage,patch.DamageRate);}
    }
    internal static bool Touches(int size,int dx,int dy)
        =>CombatHitboxes.SourceTouches(1,size,dx,dy);
    internal static int FieldDamage256(SkillValues values,bool maximum)
        =>FireGround.MissileDamage256("immolationfire",values.Rank,NecromancerSummons.Formula("immolationfire","EDmgSymPerCalc",values),maximum);
    internal static Mobile Recipient(PlayerMobile owner,FirePatch patch,uint lastCollision=0)=>Recipient(owner,owner,patch,lastCollision);
    internal static Mobile Recipient(Mobile caster,PlayerMobile owner,FirePatch patch,uint lastCollision=0)
    {
        var point=new Point3D((int)Math.Round(patch.X),(int)Math.Round(patch.Y),patch.Z);
        // Source returns the first eligible collider. Native sector enumeration
        // projects source room-list order; do not turn this into per-cell AoE.
        foreach(var target in caster.Map.GetMobilesInRange<Mobile>(point,2))
        {
            int size=target is BaseCreature c?CreatureProfiles.For(c).Traits.Size??2:2;
            int dx=(int)(patch.X*2)-target.X*2,dy=(int)(patch.Y*2)-target.Y*2;
            if(target.Serial.Value!=lastCollision && Math.Abs(target.Z-patch.Z)<16 && CombatHitboxes.SourceTouches(patch.Size,size,dx,dy) && ClassCombat.IsHostile(owner,target)
                && !SpellHelper.IsTown(target.Location,owner)){return target;}
        }
        return null;
    }
    internal static void Contact(PlayerMobile owner,WeaponFlight parent,DateTime at)
    {
        if(parent.Ability!=CombatAbility.ImmolationArrow || parent.SkillRank<1 || owner.Map==null || owner.Map==Map.Internal){return;}
        int x=(int)Math.Floor(parent.X*2),y=(int)Math.Floor(parent.Y*2);
        var values=SkillMechanics.Values(owner,CombatAbility.ImmolationArrow,parent.SkillRank);
        int radius=(int)values.Eval("calc1"),frames=NecromancerSummons.Source("immolationarrow","SHitCalc1");
        // Current field has no source weapon damage or ApplyMastery flag.
        int minimum=FieldDamage256(values,false),maximum=FieldDamage256(values,true);
        for(int dx=-radius;dx<=radius;dx++)for(int dy=-radius;dy<=radius;dy++)
        {
            if(dx*dx+dy*dy>radius*radius){continue;}
            double px=(x+dx)/2.0,py=(y+dy)/2.0;
            if(px<0 || py<0 || px>=owner.Map.Width || py>=owner.Map.Height){continue;}
            var from=new Point3D((int)Math.Round(px),(int)Math.Round(py),parent.Z+12);
            var to=new Point3D((int)Math.Round((x+2*dx)/2.0),(int)Math.Round((y+2*dy)/2.0),parent.Z+12);
            if(!owner.Map.LineOfSight(from,to) || SpellHelper.IsTown(new Point3D(from.X,from.Y,parent.Z),owner)){continue;}
            FireGround.AddImmolation(owner,parent.SkillRank,px,py,parent.Z,minimum,maximum,frames,at);
        }
        int fire=WeaponMissileDamage.RollValue(parent.Damage.Elements.FireMin,parent.Damage.Elements.FireMax,0,WeaponMissiles.DamageRoll);
        int area=(int)values.Eval("calc2");var center=new Point3D((int)Math.Round(x/2.0),(int)Math.Round(y/2.0),parent.Z);
        AmazonBowEffects.Burst(owner,CombatAbility.ImmolationArrow,center);
        var targets=new List<Mobile>();
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(center,(int)Math.Ceiling(SkillGeometry.Tiles(area)+1)))
        {
            int dx=target.X*2-x,dy=target.Y*2-y;
            if(dx*dx+dy*dy<=area*area && Math.Abs(target.Z-parent.Z)<16 && ClassCombat.IsHostile(owner,target)
                && !SpellHelper.IsTown(target.Location,owner)){targets.Add(target);}
        }
        // HP/death can mutate sector membership. Keep the existing projected
        // room order but finish collecting before releasing copied damage.
        foreach(var target in targets)
        {
            if(target is BaseCreature creature)
            {if(!PlayerWeaponMissileContact.TryElemental(owner,creature,fire,false,0,at,out string reason)){Report(reason);}}
            else{SkillCombat.FireImpact(owner,target,fire);}
        }
    }
}
