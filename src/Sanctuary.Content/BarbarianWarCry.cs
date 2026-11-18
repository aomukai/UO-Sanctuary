// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class BarbarianWarCry
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BarbarianWarCry));
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
        if(ray.MinDamage256 is not{} min||ray.MaxDamage256 is not{} max){return false;}
        int potion=CombatPotions.BonusAt(owner,"magical-penetration",ray.NextUtc);
        if(ray.Modifiers==null&&potion!=0)
        {Log.Warning("Legacy War Cry ray {Id} lacks captured mastery for active Magical Penetration;contact refused",ray.Id);return false;}
        var modifiers=ray.Modifiers is{} captured?captured with{Potion=potion}:null;
        var point = target?.Location ?? default;
        var map = target?.Map;
        bool contact = PhysicalHit(owner,target,min,max,Roll,creature=>
            CombatStuns.Apply(creature,(int)SkillMechanics.Values(owner,CombatAbility.WarCry,ray.Rank).Eval("calc4"),ray.NextUtc),modifiers);
        if (contact) { BarbarianCryVisuals.Contact(owner, CombatAbility.WarCry, point, map, ray.NextUtc); }
        return contact;
    }
    // Shared weapon-independent physical spell path; optional control occurs
    // after block/absorption and before health,retaining War Cry's event order.
    internal static bool PhysicalHit(PlayerMobile owner,Mobile target,int min,int max,Func<int,int> roll,Action<BaseCreature> beforeHealth=null,FirestormModifiers modifiers=null)
    {
        // PvP, unmapped monsters and nonzero passive dodge/weapon-block adapters
        // remain acceptance work; do not infer them from native UO armor.
        if(target is not BaseCreature creature || !ClassCombat.IsHostile(owner,target) || !owner.InLOS(target)
            || !SpellHelper.CheckTown(target.Location,owner)
            || !(BarbarianBattleCry.Supported(target) || target is EncounterCreature)
            || !SourceBlocking.TryRate(creature,out _,out _)){return false;}
        int damage=DamageRoll(min,max,max>min?roll(max-min):0);
        owner.DoHarmful(target,true);
        if(!SourceBlocking.TryBlock(creature,damage!=0,out bool blocked) || blocked){return false;}
        // Reuse prepared mitigation/FP health; SrcDam0 supplies no weapon/ED,
        // critical, crushing, leech, enchantment or on-strike proc component.
        // DAMAGE_VS_MONTYPE layers are a separately recorded unsupported branch.
        var packet=new GearDamage(damage/256.0,0,0,0,0);
        if(modifiers!=null){packet=DruidMoltenBoulder.Potion(modifiers,packet);}
        var parts=SkillCombat.MitigatePrepared(owner,target,packet);
        // ExecuteEvents reaches stun even when physical resistance removes all
        // damage. A blocked hit never enters that event path.
        beforeHealth?.Invoke(creature);
        int loss=SkillHealth.TypedDamage(owner,target,NamedElements.To256(parts.Total),DirectDamageParts.From(parts));
        CreatureHitRecovery.Apply(creature,owner,loss);
        return true;
    }
}
