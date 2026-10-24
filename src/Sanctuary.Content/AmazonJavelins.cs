// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class AmazonJavelins
{
    public static bool Handles(CombatAbility ability)=>ability is CombatAbility.PoisonJavelin or CombatAbility.LightningBolt;
    internal static bool WeaponAllowed(PlayerMobile owner)=>CombatTiming.Equipped(owner) is{} weapon
        && ClassGear.State(weapon) is{Identified:true} && ClassGear.Base(weapon) is{} basis
        && ClassItemTypes.Is(basis.Type,"jave") && !ClassGear.Broken(weapon);
    internal static ActionTiming Timing(int advance)
    {
        advance=Math.Max(1,advance);
        // Cached AMTH1HT:16 frames, missile event9, no TH starting bonus.
        // Retain the ordinary modern repeat-cycle minus-one adapter.
        return new(Math.Max(1,(16*256+advance-1)/advance-1),Math.Max(1,(9*256+advance-1)/advance));
    }
    private static bool Aim(PlayerMobile owner,Point3D point)=>owner.Map!=null && owner.Map!=Map.Internal
        && point.X>=0 && point.Y>=0 && point.X<owner.Map.Width && point.Y<owner.Map.Height
        && owner.InRange(point,CombatProtocol.SightRange)
        && Math.Abs(point.Z-owner.Z)<16 && owner.InLOS(new Point3D(point.X,point.Y,point.Z+12))
        && !SpellHelper.IsTown(owner.Location,owner) && !SpellHelper.IsTown(point,owner);
    public static bool Begin(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)
    {
        if(!Handles(ability) || !AmazonCombat.Available(owner,ability) || !NamedAmmunition.CanAttack(CombatTiming.Equipped(owner))){return false;}
        if(selected!=Serial.Zero)
        {var target=World.FindMobile(selected);if(!ClassCombat.IsHostile(owner,target)){return false;}point=target.Location;}
        if(!SkillCombat.Begin(owner,ability,selected,point,now,(p,_,_,aim)=>Aim(p,aim))){return false;}
        var session=ClassCombat.For(owner);session.PendingLeft=session.HeldAbility==ability && session.HeldAttack;
        // Source start4 succeeds before the generic wrapper consumes mana.
        if(!NecromancerEffects.Pay(owner,SkillMechanics.Values(owner,ability).Mana256))
        {session.PendingAbility=CombatAbility.None;return false;}
        return true;
    }
    public static bool Release(PlayerMobile owner,CombatAbility ability)
    {
        // The do wrapper does not repeat affordability/start checks. A last
        // javelin can have been removed after the paid start; emission still
        // follows the same-weapon/native action guard and consumes afterward.
        if(!Handles(ability) || !AmazonCombat.Available(owner,ability) || !FireGround.Ready(owner,ability)){return false;}
        var session=ClassCombat.For(owner);var target=World.FindMobile(session.PendingTarget);
        var point=ClassCombat.IsHostile(owner,target)?target.Location:session.PendingPoint;
        if(!Aim(owner,point)){return false;}
        var values=SkillMechanics.Values(owner,ability);
        var packet=BowLaunch.Capture(owner,ability,NamedCombat.ProjectileCritical(owner,AmazonBow.LaunchRandom),preparedValues:values);
        string missile=ability==CombatAbility.LightningBolt?"lightningjavelin":"poisonjav";
        int dx=(point.X-owner.X)*2,dy=(point.Y-owner.Y)*2;
        long id=WeaponMissiles.Launch(owner,ability,new(dx,dy,0x21,0),packet,WeaponMissiles.NewCast(),
            NamedProjectiles.PierceBudget(WeaponMissiles.CurrentPierce(owner),AmazonBow.LaunchRandom),
            NecromancerSummons.Source(missile,"Vel"),NecromancerSummons.Source(missile,"Range"),0,missile,values.Rank);
        if(id<=0){return false;}
        NamedAmmunition.Consume(CombatTiming.Equipped(owner),TimedEffects.Now);FireGround.Delay(owner,ability);
        owner.PlaySound(0x238); // Identified ordinary throw sound placeholder.
        return true;
    }
    internal static void EquipmentChanged(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is{} session && Handles(session.PendingAbility)
            && (!WeaponAllowed(owner) || CombatTiming.Equipped(owner)?.Serial!=session.PendingWeapon))
        {session.PendingAbility=CombatAbility.None;}
    }
    public static void Cancel(Mobile owner)=>WeaponMissiles.CancelJavelins(owner);
}
