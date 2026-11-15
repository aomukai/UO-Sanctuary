// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Prepared action reuses the real SC/FCR session. Network availability stays
// gated while the full Echoing action/world proposal awaits approval.
internal static class EchoingStrikeCasting
{
    internal sealed class Pending
    {
        internal bool Released,Cancelled;
        internal long[] Flights=[];
        internal string Fault;
    }
    private static bool Actor(PlayerMobile p)=>EchoingStrikeWorld.Actor(p)&&p.NetState is{Running:true}&&SkillMechanics.Rank(p,CombatAbility.EchoingStrike)>0&&PlayerMeleeGeometry.WarlockMelee(p);
    internal static Pending BeginPrepared(PlayerMobile p,Serial selected,Point3D point,long now)
    {
        if(!Actor(p)||!EchoingStrikeWorld.Aim(p,selected,ref point)
            ||!SkillCombat.BeginPrepared(p,CombatAbility.EchoingStrike,Serial.Zero,point,now,
                (who,_,id,at)=>EchoingStrikeWorld.Aim(who,Serial.Zero,ref at),(who,_)=>Actor(who),CombatTiming.For(p,CombatAbility.EchoingStrike)))
        {return null;}
        var action=new Pending();ClassCombat.For(p).Echoing=action;return action;
    }
    internal static void Cancel(PlayerMobile p)
    {
        var s=ClassCombat.Existing(p);if(s?.Echoing is not{} action){return;}
        action.Cancelled=true;s.Echoing=null;if(s.PendingAbility==CombatAbility.EchoingStrike){s.PendingAbility=CombatAbility.None;}
    }
    internal static bool ResolvePrepared(PlayerMobile p,long now)
    {
        var s=ClassCombat.For(p);if(s.Echoing is not{} action||action.Released||action.Cancelled){return false;}
        if(!Actor(p)||!ShapeRestrictions.Allowed(p,CombatAbility.EchoingStrike)||p.Frozen||p.Paralyzed||p.Spell!=null||!AlliedOverlap.Allowed(p)
            ||s.PendingAbility!=CombatAbility.EchoingStrike||p.Map!=s.ActionMap||p.Location!=s.ActionOrigin
            ||(CombatTiming.Equipped(p)?.Serial??Serial.Zero)!=s.PendingWeapon)
        {Cancel(p);return false;}
        if(now<s.EffectAt){return false;}
        action.Released=true;s.Echoing=null;s.PendingAbility=CombatAbility.None;
        try{action.Flights=EchoingStrikeWorld.ReleasePrepared(p,s.PendingPoint,TimedEffects.Now);return action.Flights.Length!=0;}
        catch(NotSupportedException ex){action.Fault=ex.Message;p.SendMessage(0x22,"Echoing Strike: "+ex.Message);return false;}
    }
}
