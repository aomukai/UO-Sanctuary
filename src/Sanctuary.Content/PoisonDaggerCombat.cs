// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal static class PoisonDaggerCombat
{
    internal sealed record Prepared(Serial Target,int Flags,bool BlockAnimation,PreparedWeaponImpact Impact,
        PlayerWeaponDrain Drain,double ElementRoll,long EffectAt);
    internal static Random Effects=Random.Shared;
    public static void Initialize()=>ClassMapHooks.LocationChanged+=LocationChanged;
    private static void LocationChanged(Mobile mobile)
    {
        if(mobile is not PlayerMobile owner){return;}
        var session=ClassCombat.Existing(owner);
        if(session!=null&&(session.PendingAbility==CombatAbility.PoisonDagger||session.PoisonDaggerAttack!=null)
            &&(owner.Location!=session.ActionOrigin||owner.Map!=session.ActionMap)){Cancel(owner);}
    }

    internal static bool ValidTarget(PlayerMobile owner,Serial selected)
    {
        var weapon=CombatTiming.Equipped(owner);
        return weapon!=null&&ClassGear.Base(weapon) is{} basis&&EquipmentBalance.IsType(basis.Type,"knif")
            &&World.FindMobile(selected) is BaseCreature target&&ClassCombat.IsHostile(owner,target)
            &&owner.Map==target.Map&&owner.InRange(target,CombatTiming.Range(owner,CombatAbility.BasicAttack))
            &&owner.InLOS(target)&&!SpellHelper.IsTown(owner.Location,owner)&&!SpellHelper.IsTown(target.Location,owner);
    }
    private sealed class Result(PlayerMobile owner,BaseCreature target):SourceMeleeResultServices
    {
        internal bool BlockAnimation;
        internal override int? AttackerKind=>0;
        internal override int? DefenderKind=>1;
        internal override bool Hostile()=>ClassCombat.IsHostile(owner,target);
        internal override bool DefenderRunning()=>throw new InvalidOperationException("NPC is not a running player");
        internal override int MeleeReach()=>throw new NotSupportedException("Poison Dagger uses zero range offset");
        internal override bool InRange(int bonus)
        {
            if(bonus!=1){throw new NotSupportedException("Unmapped player melee range bonus");}
            // The existing native weapon reach/LOS adapter is retained. Exact
            // source subtile geometry remains separate from result decisions.
            return owner.InRange(target,CombatTiming.Range(owner,CombatAbility.BasicAttack))&&owner.InLOS(target);
        }
        internal override bool HitSuccessful(int percent,bool missile)
        {
            if(missile){throw new NotSupportedException("Poison Dagger is not a missile attack");}
            return ClassStats.CheckHit(owner,target,percent,PoisonDaggerPackets.HitRoll,projectile:false)
                ??throw new NotSupportedException("Missing player attack supplier");
        }
        internal override int BlockOrDodge(bool avoid,bool block)
        {
            if(avoid||!block||!SourceBlocking.TryRoll(target,true,out bool blocked,out BlockAnimation))
            {throw new NotSupportedException("Poison Dagger recipient shield rule is unavailable");}
            return blocked?1:0;
        }
        internal override bool Uninterruptible()=>MonsterUninterruptibles.Active(target);
    }
    internal static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        const CombatAbility ability=CombatAbility.PoisonDagger;
        if(!NecromancerCombat.Available(owner,ability)||!ValidTarget(owner,selected)){return false;}
        var values=SkillMechanics.Values(owner,ability);
        if(!NecromancerEffects.CanPay(owner,values.Mana256))
        {return ClassCombat.Perform(owner,CombatAbility.BasicAttack,World.FindMobile(selected),point,now);}
        var target=(BaseCreature)World.FindMobile(selected);
        if(!SourceBlocking.TryRate(target,out _,out _))
        {owner.SendMessage(0x22,"This monster's shield rule is not available for Poison Dagger yet.");return false;}
        try{BowLaunch.RawPoison(owner,CombatTiming.Equipped(owner));}
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
        if(!SkillCombat.Begin(owner,ability,selected,target.Location,now,(p,_,id,_)=>ValidTarget(p,id))){return false;}
        var session=ClassCombat.For(owner);var services=new Result(owner,target);
        int flags=SourceMeleeResult.Resolve(services,(int)values.Formula("toht"));
        double roll=0;PreparedWeaponImpact impact=default;
        if((flags&SourceMeleeResult.Successful)!=0)
        {
            var damage=PoisonDaggerPackets.Damage(owner,target,values,Utility.RandomDouble,out roll);
            impact=GearEffects.PrepareWeaponImpact(owner,target,roll,Effects,damage);
            var poison=PoisonJavelins.PreparePoison(owner,target,damage.SourcePoison.Rate256,damage.SourcePoison.Frames);
            // CalculateTotalDamage includes the mitigated poison rate once in
            // immediate damage, independently of the subsequent poison clock.
            // This is the pinned older-source bridge; modern parity is open.
            impact=impact with{LifeDamage256=checked(NamedElements.To256(impact.Parts.Total)+poison.Rate256),AllocatedPoison=poison};
        }
        var drain=WeaponLeech.Capture(owner);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){session.PendingAbility=CombatAbility.None;return false;}
        session.PoisonDaggerAttack=new(selected,flags,services.BlockAnimation,impact,drain,roll,session.EffectAt);
        return true;
    }
    internal static bool Release(PlayerMobile owner,Serial selected,long now)
    {
        var session=ClassCombat.For(owner);var prepared=session.PoisonDaggerAttack;
        if(prepared==null||now<prepared.EffectAt){return false;}
        session.PoisonDaggerAttack=null;
        if(prepared.Target!=selected||!NecromancerCombat.Available(owner,CombatAbility.PoisonDagger)
            ||!ValidTarget(owner,selected)){return false;}
        var target=(BaseCreature)World.FindMobile(selected);owner.DoHarmful(target,true);owner.PlaySound(0x238); // Native weapon placeholder.
        using var attempt=PaladinThorns.Attempt(owner,target,false);
        if((prepared.Flags&SourceMeleeResult.Block)!=0){SourceBlocking.Present(target,prepared.BlockAnimation);}
        if((prepared.Flags&SourceMeleeResult.Successful)==0){return true;}
        GearEffects.Strike(owner,target,prepared.ElementRoll,Effects,preparedImpact:prepared.Impact,sourceDrain:prepared.Drain,
            softHit:(prepared.Flags&SourceMeleeResult.GetHit)==0
                ||prepared.Impact.Parts.Total==0&&prepared.Impact.AllocatedPoison is{Rate256:>0});
        return true;
    }
    internal static void Cancel(PlayerMobile owner)
    {var session=ClassCombat.Existing(owner);if(session==null){return;}session.PoisonDaggerAttack=null;if(session.PendingAbility==CombatAbility.PoisonDagger){session.PendingAbility=CombatAbility.None;}}
    internal static void EquipmentChanged(PlayerMobile owner)
    {var session=ClassCombat.Existing(owner);if(session?.PoisonDaggerAttack!=null&&(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=session.PendingWeapon){Cancel(owner);}}
}
