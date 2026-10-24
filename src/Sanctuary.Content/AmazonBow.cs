// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class AmazonBow
{
    internal static Random LaunchRandom=Random.Shared;
    public static bool Handles(CombatAbility ability)=>ability==CombatAbility.MagicArrow;
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
        if(selected!=Serial.Zero)
        {
            var target=World.FindMobile(selected);if(!ClassCombat.IsHostile(owner,target)){return false;}point=target.Location;
        }
        if(!SkillCombat.Begin(owner,ability,selected,point,now,(p,_,_,aim)=>Aim(p,aim))){return false;}
        var session=ClassCombat.For(owner);session.PendingLeft=session.HeldAbility==ability && session.HeldAttack;return true;
    }
    public static bool Release(PlayerMobile owner,CombatAbility ability)
    {
        if(!Handles(ability) || !AmazonCombat.Available(owner,ability)){return false;}
        var session=ClassCombat.For(owner);var target=World.FindMobile(session.PendingTarget);
        var point=ClassCombat.IsHostile(owner,target)?target.Location:session.PendingPoint;
        if(!Aim(owner,point)){return false;}
        var values=SkillMechanics.Values(owner,ability);if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        // One independently created missile owns its critical result and pierce
        // budget. No quiver gate/debit: pinned Magic Arrow has noammo1/decquant0.
        bool critical=NamedCombat.ProjectileCritical(owner,LaunchRandom);
        var damage=BowLaunch.Capture(owner,ability,critical);
        int pierces=NamedProjectiles.PierceBudget(WeaponMissiles.CurrentPierce(owner),LaunchRandom);
        var ray=new FanRay((int)((point.X-owner.X)*SkillGeometry.SubtilesPerTile),(int)((point.Y-owner.Y)*SkillGeometry.SubtilesPerTile),33,NecromancerSummons.Source("magicarrow","Activate"));
        long id=WeaponMissiles.Launch(owner,ability,ray,damage,WeaponMissiles.NewCast(),pierces,NecromancerSummons.Source("magicarrow","Vel"),NecromancerSummons.Source("magicarrow","Range"),0);
        if(id==0){return false;}
        if(!NecromancerEffects.Pay(owner,values.Mana256)){throw new InvalidOperationException("Bow release lost validated mana within one event");}
        owner.PlaySound(0x238); // Identified ordinary native bow sound placeholder.
        return true;
    }
}
