// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Explicit input edges are distinct from transport lease renewals. No held
// intent is saved. A failed/expired press requires a new physical press.
internal static class PlayerInfernoInput
{
    internal static bool Bound(PlayerMobile owner,int source,CombatAbility ability=CombatAbility.Inferno)
    {
        if(source is <0 or >=CombatProtocol.ChannelSources||!ClassProfessions.Ready(owner)||owner.Target!=null
            ||ability is not(CombatAbility.Inferno or CombatAbility.ArcticBlast or CombatAbility.BladeFury)
            ||!(ability==CombatAbility.Inferno?SorceressCombat.Available(owner,ability):ability==CombatAbility.ArcticBlast?DruidCombat.Available(owner,ability):AssassinCombat.Available(owner,ability))){return false;}
        var pair=SkillBindings.Instance.Get(owner);
        return (source==0?pair.Left:source==1?pair.Right:SkillBindings.Instance.GetQuick(owner.Serial,SkillMechanics.For(ability).CharacterClass,source-2))==ability;
    }
    internal static void EndHeld(PlayerMobile owner,int source)
    {PlayerInfernoModes.EndHeld(owner,source);BladeFuryModes.EndHeld(owner,source);}
    internal static bool Apply(PlayerMobile owner,CombatRequest action,int source,CombatAbility ability,Serial target,Point3D point,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||!Bound(owner,source,ability)
            ||owner.Map==null||point.X<=0||point.Y<=0||point.X>=owner.Map.Width||point.Y>=owner.Map.Height
            ||Math.Abs((long)point.X-owner.X)>=50||Math.Abs((long)point.Y-owner.Y)>=50
            ||AmazonSlowMissiles.Capture(owner,ability!=CombatAbility.ArcticBlast).DamagePercent!=100)
        {EndHeld(owner,source);return false;}
        if(action==CombatRequest.ChannelAim){return ability==CombatAbility.BladeFury?BladeFuryModes.RenewHeld(owner,source,target,point,at):PlayerInfernoModes.RenewHeld(owner,source,target,point,at);}
        if(action!=CombatRequest.ChannelStart){return false;}
        // Repeated start delivery for an active gesture cannot charge twice.
        if(PlayerInfernoModes.State(owner) is{HeldSource:>=0} active&&active.HeldSource==source){return false;}
        if(BladeFuryModes.State(owner) is{HeldSource:>=0} fury&&fury.HeldSource==source){return false;}
        var session=ClassCombat.For(owner);
        if(Core.TickCount<session.NextAction||session.PendingAbility!=CombatAbility.None){return false;}
        ClassCombat.Stop(owner);
        return ability==CombatAbility.BladeFury?BladeFuryModes.BeginHeld(owner,source,target,point,at):PlayerInfernoModes.BeginHeld(owner,source,target,point,at,ability);
    }
}
