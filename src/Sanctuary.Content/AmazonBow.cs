// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class AmazonBow
{
    internal static Random LaunchRandom=Random.Shared;
    public static bool Handles(CombatAbility ability)=>ability is CombatAbility.MagicArrow or CombatAbility.MultipleShot or CombatAbility.FireArrow or CombatAbility.ColdArrow or CombatAbility.ExplodingArrow or CombatAbility.IceArrow;
    internal static ActionTiming Timing(AttackFamily family,int advance)
    {
        advance=Math.Max(1,advance);
        // Ordinary Amazon A1 has no bow/xbow starting-frame bonus. Cached
        // legacy AnimData is supplementary; current animation acceptance is open.
        // The original modern IAS calculator uses the ordinary repeat-cycle
        // minus-one; the old isolated scheduler ENDANIM event is a later clock.
        int frames=family==AttackFamily.Crossbow?20:14,action=family==AttackFamily.Crossbow?9:6;
        return new(Math.Max(1,(frames*256+advance-1)/advance-1),(action*256+advance-1)/advance);
    }
    internal static ActionTiming Timing(PlayerMobile owner)=>Timing(CombatTiming.Weapon(CombatTiming.Equipped(owner)).Family,CombatTiming.WeaponSequenceAdvance(owner));
    internal static void Cancel(PlayerMobile owner)
    {if(ClassCombat.Existing(owner) is{} session && Handles(session.PendingAbility)){session.PendingAbility=CombatAbility.None;}}
    internal static void EquipmentChanged(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is{} session && Handles(session.PendingAbility)
            && (!BowLaunch.WeaponAllowed(owner) || CombatTiming.Equipped(owner)?.Serial!=session.PendingWeapon)){Cancel(owner);}
    }
    private static bool Aim(PlayerMobile owner,Point3D point)=>owner.Map!=null && owner.Map!=Map.Internal
        && point.X>=0 && point.Y>=0 && point.X<owner.Map.Width && point.Y<owner.Map.Height
        && (point.X!=owner.X || point.Y!=owner.Y) && owner.InRange(point,CombatProtocol.SightRange)
        && Math.Abs(point.Z-owner.Z)<16 && owner.InLOS(new Point3D(point.X,point.Y,point.Z+12))
        && !SpellHelper.IsTown(owner.Location,owner) && !SpellHelper.IsTown(point,owner);
    public static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)
    {
        if(!Handles(ability) || !AmazonCombat.Available(owner,ability)){return false;}
        if(ability!=CombatAbility.MagicArrow && !BowAmmunition.CanRelease(owner,CombatTiming.Equipped(owner))){return false;}
        if(selected!=Serial.Zero)
        {
            var target=World.FindMobile(selected);if(!ClassCombat.IsHostile(owner,target)){return false;}point=target.Location;
        }
        if(!SkillCombat.Begin(owner,ability,selected,point,now,(p,_,_,aim)=>Aim(p,aim))){return false;}
        var session=ClassCombat.For(owner);session.PendingLeft=session.HeldAbility==ability && session.HeldAttack;
        // start4 has a successful server start function, so its ordinary
        // wrapper pays here. Cancellation retains this paid cost.
        if(ability!=CombatAbility.MagicArrow && !NecromancerEffects.Pay(owner,SkillMechanics.Values(owner,ability).Mana256))
        {session.PendingAbility=CombatAbility.None;return false;}
        return true;
    }
    public static bool Release(PlayerMobile owner,CombatAbility ability)
    {
        if(!Handles(ability) || !AmazonCombat.Available(owner,ability)){return false;}
        var session=ClassCombat.For(owner);var target=World.FindMobile(session.PendingTarget);
        var point=ClassCombat.IsHostile(owner,target)?target.Location:session.PendingPoint;
        if(!Aim(owner,point)){return false;}
        var values=SkillMechanics.Values(owner,ability);bool multiple=ability==CombatAbility.MultipleShot;
        if(ability==CombatAbility.MagicArrow && !NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        int dx=(int)((point.X-owner.X)*SkillGeometry.SubtilesPerTile),dy=(int)((point.Y-owner.Y)*SkillGeometry.SubtilesPerTile);
        string missile=multiple?(BowAmmunition.AmmoType(owner,CombatTiming.Equipped(owner))==typeof(Arrow)?"multipleshotarrow":"multipleshotbolt"):ability==CombatAbility.ExplodingArrow?"explodingarrow":ability==CombatAbility.IceArrow?"icearrow":ability==CombatAbility.FireArrow?"firearrow":ability==CombatAbility.ColdArrow?"coldarrow":"magicarrow";
        var rays=multiple?FanGeometry.Create(dx,dy,(int)values.Eval("calc1"),(int)values.Eval("calc3"),(int)values.Eval("calc2"))
            :new[]{new FanRay(dx,dy,33,NecromancerSummons.Source(missile,"Activate"))};
        long cast=WeaponMissiles.NewCast();int created=0;
        foreach(var ray in rays)
        {
            // Keep critical selection per created missile. The existing bounded
            // pierce helper is a port adapter; exact source seed correlation
            // across the fan remains explicitly unverified.
            bool critical=NamedCombat.ProjectileCritical(owner,LaunchRandom);
            var damage=BowLaunch.Capture(owner,ability,critical,preparedValues:values);
            int pierces=NamedProjectiles.PierceBudget(WeaponMissiles.CurrentPierce(owner),LaunchRandom);
            if(WeaponMissiles.Launch(owner,ability,ray,damage,cast,pierces,NecromancerSummons.Source(missile,"Vel"),NecromancerSummons.Source(missile,"Range"),
                NecromancerSummons.Source(missile,"NextHit")!=0?NecromancerSummons.Source(missile,"NextDelay"):0,missile,values.Rank)>0){created++;}
        }
        if(created==0){return false;}
        if(ability!=CombatAbility.MagicArrow)
        {
            // Legacy do8/generic missile emission has no second quiver gate; its wrapper decrements
            // once afterward, ignoring an absent/stale stack. Modern inventory
            // transition equivalence remains recorded separately.
            BowAmmunition.Consume(owner,CombatTiming.Equipped(owner));
        }
        else if(!NecromancerEffects.Pay(owner,values.Mana256)){throw new InvalidOperationException("Bow release lost validated mana within one event");}
        owner.PlaySound(0x238); // Identified ordinary native bow sound placeholder.
        return true;
    }
}
