// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class MindBlasts
{
    internal static Func<int,int> Roll=Utility.Random;
    internal static bool Valid(PlayerMobile owner,Point3D point)=>owner is{Alive:true,Deleted:false}
        && owner.Map!=null && owner.Map!=Map.Internal && point.X>=0 && point.Y>=0
        && point.X<owner.Map.Width && point.Y<owner.Map.Height && owner.InRange(point,CombatProtocol.TeethRange)
        && Math.Abs(owner.Z-point.Z)<16 && owner.InLOS(point) && !SpellHelper.IsTown(owner.Location,owner)
        && !SpellHelper.IsTown(point,owner);
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        PaladinConversion.Prune();
        if(selected!=Serial.Zero)
        {
            if(World.FindMobile(selected) is not BaseCreature target || !ClassCombat.IsHostile(owner,target)){return false;}
            point=target.Location;
        }
        // Ground casting remains available without a victim. SearchEnemyXY's
        // precise modern client assistance is unverified; no Hammer radius is invented.
        return SkillCombat.Begin(owner,CombatAbility.MindBlast,selected,point,now,(p,_,_,at)=>Valid(p,at));
    }
    internal static bool Target(PlayerMobile owner,BaseCreature target,Point3D point,int radius)
    {
        // Filter8383: living attackable enemies, no town, caster-to-victim LOS.
        // do51 passes bCheckMonAuraFlag=0: NOAURA is not an exclusion here.
        bool attackable=target is EncounterCreature?target.CanBeDamaged():SourceAuraTargets.Rule(CreatureProfiles.Definition(target).Reference)?.Attackable==true;
        return attackable && ClassCombat.IsHostile(owner,target) && AmazonInnerSight.InRadius(target.X-point.X,target.Y-point.Y,radius)
            && Math.Abs(target.Z-point.Z)<16 && owner.InLOS(target) && !SpellHelper.IsTown(target.Location,target);
    }
    internal static int Chance(SkillValues values)=>values.Diminishing(5,6);
    internal static GearDamage Damage(SkillValues values,Func<int,int> random)
        =>new(WeaponMissileDamage.RollValue(values.PhysicalDamage256(false),values.PhysicalDamage256(true),0,random)/256.0,0,0,0,0);
    internal static bool Impact(PlayerMobile owner,BaseCreature target,SkillValues values,GearDamage raw)
    {
        if(target is not{Alive:true,Deleted:false} || !ClassCombat.IsHostile(owner,target)){return false;}
        bool convert=PaladinConversion.MindBlastEligible(owner,target) && Roll(100)<=Chance(values);
        int frames=convert?values.Param(3)+Roll(values.Param(4)):0;
        owner.DoHarmful(target,true);
        var parts=SkillCombat.MitigatePrepared(owner,target,PsychicHammers.Potion(owner,raw));
        // Current3.3.93847 observation supersedes the older success/no-damage
        // branch. Commit while hostile, then transfer allegiance; never bypass
        // global friendly protection. Modern damage/scaling order is still open.
        int loss=SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total));
        if(convert)
        {
            if(target is{Alive:true,Deleted:false}){PaladinConversion.ApplyMindBlast(owner,target,frames);}
            // User confirmed: successful conversion has no stun/knockback.
        }
        else if(target is{Alive:true,Deleted:false})
        {
            CombatStuns.Apply(target,values.DurationFrames);
            if(!SourceKnockbacks.Begin(owner,target) && SourceKnockbacks.Rule(target) is{Knockback:false,GetHit:true})
            {CreatureHitRecovery.Apply(target,owner,loss);}
        }
        target.FixedEffect(0x374A,10,15,0x481,0); // Identified mindblast-hit/overlay placeholder.
        return true;
    }
    public static bool Release(PlayerMobile owner,Serial selected)
    {
        PaladinConversion.Prune();
        var session=ClassCombat.For(owner);Point3D point=session.PendingPoint;
        if(selected!=Serial.Zero)
        {
            if(World.FindMobile(selected) is not BaseCreature target || !ClassCombat.IsHostile(owner,target)){return false;}
            point=target.Location;
        }
        if(!AssassinCombat.Available(owner,CombatAbility.MindBlast) || !Valid(owner,point)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.MindBlast);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        GearDamage raw=Damage(values,Roll);int radius=(int)values.Eval("aurarangecalc");
        var victims=new List<BaseCreature>();
        foreach(var target in owner.Map.GetMobilesInRange<BaseCreature>(point,(int)Math.Ceiling(SkillGeometry.Tiles(radius))))
        {if(Target(owner,target,point,radius)){victims.Add(target);}}
        victims.Sort((a,b)=>a.Serial.CompareTo(b.Serial));
        foreach(var target in victims){Impact(owner,target,values,raw);}
        owner.PlaySound(0x1E5);Effects.SendLocationEffect(point,owner.Map,0x373A,15,10,0x481,0); // Identified cast/center/audio placeholders.
        return true;
    }
}
