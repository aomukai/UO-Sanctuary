// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class BarbarianMelee
{
    internal sealed record Prepared(CombatAbility Ability,Serial Target,bool Hit,PreparedWeaponImpact Impact,double ElementRoll,int StunFrames,long EffectAt);
    internal static Func<double> HitRoll=Utility.RandomDouble;
    internal static Func<double> ElementRoll=Utility.RandomDouble;
    internal static Func<int,int> PhysicalRoll=Utility.Random;
    internal static Random Effects=Random.Shared;
    public static bool Handles(CombatAbility ability)=>ability is CombatAbility.Bash or CombatAbility.Stun or CombatAbility.Concentrate;
    public static bool ValidTarget(PlayerMobile owner,CombatAbility ability,Serial selected)
    {
        if(!Handles(ability) || !PaladinVengeance.ValidTarget(owner,selected) || World.FindMobile(selected) is not BaseCreature target){return false;}
        if(ability==CombatAbility.Concentrate){return true;}
        // Damage/control stay available for source-mapped species; do not invent
        // unknown native stun metadata or the special sand-leaper knockback path.
        if(ability==CombatAbility.Stun){return CombatStuns.Rules(target,out _,out _);}
        return SourceKnockbacks.Rule(target) is{} rule && (!rule.Knockback || !rule.GetHit || rule.Distance==5);
    }
    public static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)
    {
        if(!Handles(ability) || !SkillCombat.Begin(owner,ability,selected,point,now,(_,skill,id,_)=>ValidTarget(owner,skill,id))){return false;}
        var session=ClassCombat.For(owner);var target=World.FindMobile(selected);var values=SkillMechanics.Values(owner,ability);
        bool hit=ClassStats.CheckHit(owner,target,(int)(SkillMechanics.For(ability).Text("ToHitCalc").Length>0?values.Eval("ToHitCalc"):values.Formula("toht")),HitRoll)==true;
        double roll=0;PreparedWeaponImpact packet=default;
        if(hit)
        {
            roll=ElementRoll();var damage=GearEffects.Damage(owner,roll,target,(int)values.Eval("calc1"),PhysicalRoll);
            packet=GearEffects.PrepareWeaponImpact(owner,target,roll,Effects,damage,physicalToMagicPercent:ability==CombatAbility.Concentrate?(int)values.Eval("calc4"):0);
            // Start32 adds this after allocation's ordinary critical/mitigation.
            // ExecuteEvents later commits the already-stored total, so the legacy
            // flat amount remains a physical-event field without increasing life damage.
            // Modern D2R equivalence for that stale-total behavior remains unverified.
            packet=packet with{LifeDamage256=NamedElements.To256(packet.Parts.Total),Parts=packet.Parts with{Physical=packet.Parts.Physical+values.Eval("calc2")}};
        }
        if(!NecromancerEffects.Pay(owner,values.Mana256)){session.PendingAbility=CombatAbility.None;return false;}
        if(ability==CombatAbility.Concentrate){BarbarianConcentrate.Begin(owner,values);}
        session.BarbarianAttack=new(ability,selected,hit,packet,roll,ability==CombatAbility.Stun?values.DurationFrames:0,session.EffectAt);
        return true;
    }
    public static bool Release(PlayerMobile owner,CombatAbility ability,Serial selected,long now)
    {
        var session=ClassCombat.For(owner);var prepared=session.BarbarianAttack;
        if(prepared==null || now<prepared.EffectAt){return false;}
        session.BarbarianAttack=null;
        if(prepared.Ability!=ability || prepared.Target!=selected || !BarbarianCombat.Available(owner,ability) || !ValidTarget(owner,ability,selected)){return false;}
        if(ability==CombatAbility.Concentrate){BarbarianConcentrate.Impact(owner,now);}
        var target=(BaseCreature)World.FindMobile(selected);owner.DoHarmful(target,true);owner.PlaySound(0x238); // Existing weapon sound/animation placeholders.
        using var attempt=PaladinThorns.Attempt(owner,target,false);
        if(!prepared.Hit){return true;}
        GearEffects.Strike(owner,target,prepared.ElementRoll,Effects,skillKnockback:ability==CombatAbility.Bash,preparedImpact:prepared.Impact);
        if(target is{Alive:true,Deleted:false})
        {
            if(ability==CombatAbility.Bash){SourceKnockbacks.Begin(owner,target);}
            else if(ability==CombatAbility.Stun){CombatStuns.Apply(target,prepared.StunFrames);}
        }
        return true;
    }
}
