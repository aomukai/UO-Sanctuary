// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class BarbarianWarCry
{
    internal static Func<int,int> Roll=Utility.Random;
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)=>SkillCombat.Begin(owner,CombatAbility.WarCry,selected,point,now,(p,_,_,_)=>BarbarianShout.ValidCast(p));
    public static bool Release(PlayerMobile owner)
    {
        if(!BarbarianCombat.Available(owner,CombatAbility.WarCry) || !BarbarianShout.ValidCast(owner)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.WarCry);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        BarbarianShout.Cast(owner,values,TimedEffects.Now,CombatAbility.WarCry);return true;
    }
    internal static int DamageRoll(int minimum,int maximum,int draw)
    {
        if(minimum<=0 || maximum<=0){return 0;}
        if(minimum>maximum){(minimum,maximum)=(maximum,minimum);}
        return minimum+(maximum>minimum?Math.Clamp(draw,0,maximum-minimum-1):0);
    }
    internal static bool Hit(PlayerMobile owner,Mobile target,ShoutRay ray)
    {
        // PvP, unmapped monsters and nonzero passive dodge/weapon-block adapters
        // remain acceptance work; do not infer them from native UO armor.
        if(target is not BaseCreature creature || !ClassCombat.IsHostile(owner,target) || !owner.InLOS(target)
            || !SpellHelper.CheckTown(target.Location,owner) || ray.MinDamage256 is not{} min || ray.MaxDamage256 is not{} max
            || !(BarbarianBattleCry.Supported(target) || target is EncounterCreature)
            || !SourceBlocking.TryRate(creature,out _,out _)){return false;}
        int damage=DamageRoll(min,max,max>min?Roll(max-min):0);
        owner.DoHarmful(target,true);
        if(!SourceBlocking.TryBlock(creature,damage!=0,out bool blocked) || blocked){return false;}
        // Reuse prepared mitigation/FP health; SrcDam0 supplies no weapon/ED,
        // critical, crushing, leech, enchantment or on-strike proc component.
        // DAMAGE_VS_MONTYPE layers are a separately recorded unsupported branch.
        var parts=SkillCombat.MitigatePrepared(owner,target,new(damage/256.0,0,0,0,0));
        // ExecuteEvents reaches stun even when physical resistance removes all
        // damage. A blocked hit never enters that event path.
        CombatStuns.Apply(creature,(int)SkillMechanics.Values(owner,CombatAbility.WarCry,ray.Rank).Eval("calc4"),ray.NextUtc);
        int loss=SkillHealth.Damage(owner,target,NamedElements.To256(parts.Total));
        CreatureHitRecovery.Apply(creature,owner,loss);
        return true;
    }
}
