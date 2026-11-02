// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Learned S2 adapter reuses the saved actor, controller and shared trap roster.
internal static class BladeSentinelCasts
{
    internal static ActionTiming Timing(PlayerMobile owner)=>LobProjectiles.Timing(owner);
    internal static bool Supported(PlayerMobile owner)
    {
        if(!AssassinCombat.Available(owner,CombatAbility.BladeSentinel)){return false;}
        try
        {
            // These collectors do not draw a spawn or combat seed. Actual
            // weapon/critical capture still occurs at the source first-arm event.
            _=BladeSentinelSummons.Capture(owner);
            return ClassGear.Bonus(owner,"dmg-ac")<=0;
        }
        catch(System.NotSupportedException){return false;}
    }
    internal static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!LobProjectiles.Aim(owner,selected,ref point) || !Supported(owner)){return false;}
        return SkillCombat.Begin(owner,CombatAbility.BladeSentinel,selected,point,now,
            (p,_,_,at)=>LobProjectiles.Valid(p,at),timing:Timing(owner));
    }
    internal static bool Release(PlayerMobile owner,Serial selected)
    {
        var point=ClassCombat.For(owner).PendingPoint;
        if(owner is not{Alive:true,Deleted:false} || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile)
            || profile.Class!=DiabloClass.Assassin || SkillMechanics.Rank(owner,CombatAbility.BladeSentinel) is <1 or >255
            || !Supported(owner) || !FireGround.Ready(owner,CombatAbility.BladeSentinel) || !LobProjectiles.Aim(owner,selected,ref point))
        {return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.BladeSentinel);
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        var body=BladeSentinelSummons.Create(owner,point,TimedEffects.Now);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){body.Delete();return false;}
        FireGround.Delay(owner,CombatAbility.BladeSentinel);
        owner.PlaySound(0x23B); // Identified S2 cast-audio placeholder.
        return true;
    }
}
