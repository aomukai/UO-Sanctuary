// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Cast adapter is not registered in AssassinCombat yet. Its normal shared
// Begin path therefore stays unavailable until the lifecycle/input gate opens.
internal static class BladeSentinelCasts
{
    internal static ActionTiming Timing(PlayerMobile owner)=>LobProjectiles.Timing(owner);
    internal static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!LobProjectiles.Aim(owner,selected,ref point)){return false;}
        return SkillCombat.Begin(owner,CombatAbility.BladeSentinel,selected,point,now,
            (p,_,_,at)=>LobProjectiles.Valid(p,at),timing:Timing(owner));
    }
    internal static bool Release(PlayerMobile owner,Serial selected)
    {
        var point=ClassCombat.For(owner).PendingPoint;
        if(owner is not{Alive:true,Deleted:false} || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile)
            || profile.Class!=DiabloClass.Assassin || SkillMechanics.Rank(owner,CombatAbility.BladeSentinel) is <1 or >255
            || !FireGround.Ready(owner,CombatAbility.BladeSentinel) || !LobProjectiles.Aim(owner,selected,ref point))
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
