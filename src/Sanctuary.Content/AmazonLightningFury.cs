// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class AmazonLightningFury
{
    internal static bool IsChild(WeaponFlight flight)=>flight.Ability==CombatAbility.LightningFury && flight.Missile=="furylightning";
    internal static WeaponMissileDamage Child(PlayerMobile owner,int rank)
    {
        var values=SkillMechanics.Values(owner,CombatAbility.LightningFury,rank);int mastery=NamedElements.MasteryPercent(owner,"ltng");
        return new(0,0,0,0,0,false,new(LightningMin:AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery),LightningMax:AmazonMelee.EnhanceEndpoint(values.Damage256(true),mastery)));
    }
    internal static bool ValidChild(WeaponMissileDamage damage)=>damage?.Elements is{} e && e.LightningMin>=0 && e.LightningMax>=e.LightningMin
        && e==new MissileSourceStats(LightningMin:e.LightningMin,LightningMax:e.LightningMax) && damage==new WeaponMissileDamage(0,0,0,0,0,false,e);
    internal static Mobile[] Targets(PlayerMobile owner,WeaponFlight parent)
    {
        var values=SkillMechanics.Values(owner,CombatAbility.LightningFury,parent.SkillRank);int radius=(int)values.Eval("aurarangecalc"),limit=Math.Max(1,(int)values.Eval("calc1"));
        int x=(int)Math.Floor(parent.X*2+.5),y=(int)Math.Floor(parent.Y*2+.5);var result=new List<Mobile>();
        var center=new Point3D((int)Math.Round(x/2.0),(int)Math.Round(y/2.0),parent.Z);
        // Full filter0xA583 has no optional owner-LOS bit. Native sector order
        // projects source adjacent-room/list order; no nearest sorting/exclusion
        // of the primary collider is introduced by hit20.
        foreach(var candidate in owner.Map.GetMobilesInRange<Mobile>(center,(int)Math.Ceiling(SkillGeometry.Tiles(radius)) + 1))
        {
            int dx=candidate.X*2-x,dy=candidate.Y*2-y;
            if((long)dx*dx+(long)dy*dy>(long)radius*radius || !ClassCombat.IsHostile(owner,candidate) || candidate is not BaseCreature creature
                || !SourceAuraTargets.Eligible(creature) || SpellHelper.IsTown(candidate.Location,owner)){continue;}
            result.Add(candidate);if(result.Count==limit){break;}
        }
        return result.ToArray();
    }
    internal static void Burst(PlayerMobile owner,WeaponFlight parent,DateTime at)
    {
        if(parent.Ability!=CombatAbility.LightningFury || IsChild(parent) || owner.Map==null || owner.Map==Map.Internal || parent.X<0 || parent.Y<0 || parent.X>=owner.Map.Width || parent.Y>=owner.Map.Height){return;}
        foreach(var target in Targets(owner,parent)){WeaponMissiles.LaunchFuryChild(owner,parent,target,at);}
    }
}
