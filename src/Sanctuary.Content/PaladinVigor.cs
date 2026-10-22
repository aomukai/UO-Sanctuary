// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class PaladinVigor
{
    public static int RecoveryPercent(Mobile actor)=>PaladinAuras.Effect(actor,CombatAbility.Vigor)?.Percent ?? 0;
    public static int StaminaPercent(Mobile actor)=>PaladinAuras.Effect(actor,CombatAbility.Vigor)?.StaminaPercent ?? 0;
    public static int VelocityPercent(Mobile actor)=>PaladinAuras.Effect(actor,CombatAbility.Vigor)?.VelocityPercent ?? 0;
    public static void Configure()
    {
        var previous=ClassGearHooks.StaminaMaximum;
        ClassGearHooks.StaminaMaximum=(actor,maximum)=>
        {
            if(World.Loading){return 65000;}
            int? prior=previous?.Invoke(actor,maximum);int bonus=StaminaPercent(actor)+BattleOrders.Percent(actor);
            return bonus==0 ? prior : (int)Math.Clamp((long)(prior ?? maximum)*(100+bonus)/100,0,65000);
        };
    }
    public static double MovementDelay(Mobile actor,double seconds)
    {
        if(actor is not BaseCreature){return seconds;}
        int existing=actor is NecromancerMinion minion?minion.VelocityPercent:0;
        return seconds*(100+existing)/Math.Max(1,100+existing+VelocityPercent(actor));
    }
    public static void Refresh(Mobile actor)
    {
        if(actor is not {Deleted:false}){return;}
        actor.Stam=Math.Min(actor.Stam,actor.StamMax);actor.CheckStatTimers();actor.Delta(MobileDelta.Stam);
    }
}
