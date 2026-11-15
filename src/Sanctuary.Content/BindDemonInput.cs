// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Shared wire layout,bindings,lease and original-body controller. Supplied
// preparation skips only learned admission;it still checks the actual saved
// binding,class,hard training,source and ordinary player-action conditions.
internal static class BindDemonInput
{
    internal static bool BoundPrepared(PlayerMobile owner,int source)
    {
        if(source is<1 or>=CombatProtocol.ChannelSources||!ClassProfessions.Ready(owner)||owner.Target!=null
            ||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Warlock
            ||ClassCreation.Profiles.GetRank(owner.Serial,"warlock.binddemon")<1){return false;}
        return (source==1?SkillBindings.Instance.Get(owner).Right:SkillBindings.Instance.GetQuick(owner.Serial,DiabloClass.Warlock,source-2))==CombatAbility.BindDemon;
    }
    internal static void Pose(PlayerMobile owner)
    {
        // Identified existing human cast pose,not D2R SQ6 animation parity.
        ClassCombat.SendAnimation(owner,17,1000);owner.Animate(17,7,1,true,false,0);
    }
    internal static bool Apply(PlayerMobile owner,CombatRequest action,int source,Serial target,Point3D point,DateTime at)
        =>WarlockCombat.Implemented(CombatAbility.BindDemon)&&WarlockCombat.Available(owner,CombatAbility.BindDemon)
            &&ApplyPrepared(owner,action,source,target,point,at);
    internal static bool ApplyPrepared(PlayerMobile owner,CombatRequest action,int source,Serial target,Point3D point,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||!BoundPrepared(owner,source)||!PlayerInfernoInput.PointAllowed(owner,point))
        {BindDemonChannel.EndHeld(owner,source);return false;}
        if(action==CombatRequest.ChannelAim){return BindDemonChannel.RenewPrepared(owner,source,at);}
        if(action!=CombatRequest.ChannelStart||BindDemonChannel.State(owner)is{InputBound:true} active&&active.Source==source){return false;}
        var session=ClassCombat.For(owner);
        if(Core.TickCount<session.NextAction||session.PendingAbility!=CombatAbility.None){return false;}
        if(World.FindMobile(target)is not BaseCreature body){return false;}
        ClassCombat.Stop(owner);
        return BindDemonChannel.BeginPrepared(owner,body,at,out _,source,inputBound:true);
    }
}
