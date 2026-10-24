// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class PsychicHammers
{
    // User observed approximately2 UO tiles (2–3 yards) in3.3.93847.
    // This native projection is explicit; the exact source client boundary is open.
    internal const int NearClickTiles=2;
    internal static Func<int,int> Roll=Utility.Random;
    internal static bool Valid(PlayerMobile owner,Serial selected)
    {
        var target=World.FindMobile(selected);
        return owner is{Alive:true,Deleted:false} && owner.Map!=null && owner.Map!=Map.Internal
            && target is BaseCreature{Alive:true,Deleted:false} && ClassCombat.IsHostile(owner,target)
            && owner.InRange(target,CombatProtocol.TeethRange) && Math.Abs(owner.Z-target.Z)<16 && owner.InLOS(target)
            && !SpellHelper.IsTown(owner.Location,owner) && !SpellHelper.IsTown(target.Location,target);
    }
    internal static Mobile Acquire(PlayerMobile owner,Point3D cursor)
    {
        if(owner.Map==null || owner.Map==Map.Internal || !owner.InRange(cursor,CombatProtocol.TeethRange) || !owner.InLOS(cursor)){return null;}
        Mobile closest=null;long distance=long.MaxValue;bool tied=false;
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(cursor,NearClickTiles))
        {
            long dx=target.X-cursor.X,dy=target.Y-cursor.Y,squared=dx*dx+dy*dy;
            if(squared>NearClickTiles*NearClickTiles || !Valid(owner,target.Serial)){continue;}
            if(squared<distance){distance=squared;closest=target;tied=false;}else if(squared==distance){tied=true;}
        }
        // Exact equidistant tie and edge geometry remain unverified.
        return tied?null:closest;
    }
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(selected==Serial.Zero){if(Acquire(owner,point) is not{} target){return false;}selected=target.Serial;}
        var actor=World.FindMobile(selected);
        return SkillCombat.Begin(owner,CombatAbility.PsychicHammer,selected,actor?.Location??point,now,(p,_,target,_)=>Valid(p,target));
    }
    internal static int Chance(SkillValues values,bool playerOrHireling,bool boss,bool unique)
        =>(int)values.Eval(playerOrHireling?"calc4":boss?"calc3":unique?"calc2":"calc1");
    internal static int Chance(PlayerMobile owner,BaseCreature target,SkillValues values)
    {
        // Source monstats Boss is independent of encounter grade. The native
        // fallback retains existing traits only for unmapped non-encounter actors.
        bool boss=PaladinConversion.SourceRule(target)?.Boss
            ?? (target is not EncounterCreature && CreatureProfiles.For(target).Traits.Boss==true);
        bool unique=target is EncounterCreature{Grade:EncounterGrade.Unique or EncounterGrade.SuperUnique};
        return Chance(values,false,boss,unique); // Learned PvP/hireling adapters remain unsupported.
    }
    internal static GearDamage Damage(SkillValues values,int magicMastery,Func<int,int> random)
    {
        int physical=WeaponMissileDamage.RollValue(values.PhysicalDamage256(false),values.PhysicalDamage256(true),0,random);
        int magic=WeaponMissileDamage.RollValue(values.Damage256(false),values.Damage256(true),magicMastery,random);
        return new(physical/256.0,0,0,0,0,Magic:magic/256.0);
    }
    internal static GearDamage Potion(PlayerMobile owner,GearDamage raw)
    {
        // Approved T29 adds each elemental channel from original spell damage;
        // original physical/magic endpoints are already rolled and enhanced once.
        double added=raw.Total*CombatPotions.Bonus(owner,"magical-penetration")/100;
        return added<=0?raw:raw with{Fire=NamedElements.Enhance(owner,added,"fire"),Cold=NamedElements.Enhance(owner,added,"cold"),Lightning=NamedElements.Enhance(owner,added,"ltng"),Poison=NamedElements.Enhance(owner,added,"pois")};
    }
    public static bool Release(PlayerMobile owner,Serial selected)
    {
        if(!AssassinCombat.Available(owner,CombatAbility.PsychicHammer) || !Valid(owner,selected)){return false;}
        var target=(BaseCreature)World.FindMobile(selected);var values=SkillMechanics.Values(owner,CombatAbility.PsychicHammer);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        var raw=Damage(values,NamedElements.MasteryPercent(owner,"mag"),Roll);
        owner.DoHarmful(target,true);var parts=SkillCombat.MitigatePrepared(owner,target,Potion(owner,raw));
        int loss=SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total));
        // Source events commit damage before do33 rolls the chance. Roll even
        // when that damage killed the target; no new knockback may start on a corpse.
        int chance=Chance(owner,target,values);bool knockback=chance>0 && Roll(100)<chance;
        if(knockback && target is{Alive:true,Deleted:false})
        {
            if(!SourceKnockbacks.Begin(owner,target) && SourceKnockbacks.Rule(target) is{Knockback:false,GetHit:true})
            {CreatureHitRecovery.Apply(target,owner,loss);}
        }
        owner.PlaySound(0x1E5);target.FixedEffect(0x374A,10,15,0x481,0);target.PlaySound(0x1F1); // Identified Psychic Hammer cast/impact/audio placeholders.
        return true;
    }
}
