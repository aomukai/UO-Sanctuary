// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Approved ordinary SC/FCR input;the transient action exclusively owns release.
internal static class MiasmaBoltCasting
{
    internal sealed class Pending
    {
        internal bool Released,Cancelled;
        internal long Flight;
        internal int ManaPaid256;
        internal string Fault;
    }
    private static bool Actor(PlayerMobile p)=>p is{Map:not null}&&MiasmaWorld.Fighting(p,p.Map.MapID)
        &&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock&&SkillMechanics.Rank(p,CombatAbility.MiasmaBolt)>0;
    internal static bool BeginLearned(PlayerMobile p,Serial selected,Point3D point,long now)
    {
        if(!MiasmaWorld.Approved||!WarlockCombat.Available(p,CombatAbility.MiasmaBolt)){return false;}
        try{return BeginPrepared(p,selected,point,now)!=null;}
        catch(NotSupportedException ex){p.SendMessage(0x22,"Miasma Bolt: "+ex.Message);return false;}
    }
    internal static Pending BeginPrepared(PlayerMobile p,Serial selected,Point3D point,long now)
    {
        if(!Actor(p)||!MiasmaWorld.Aim(p,selected,ref point)
            ||!SkillCombat.BeginPrepared(p,CombatAbility.MiasmaBolt,selected,point,now,
                (who,_,_,at)=>MiasmaWorld.Aim(who,Serial.Zero,ref at),(who,_)=>Actor(who),CombatTiming.For(p,CombatAbility.MiasmaBolt)))
        {return null;}
        var action=new Pending();ClassCombat.For(p).Miasma=action;return action;
    }
    internal static void Cancel(PlayerMobile p)
    {
        var s=ClassCombat.Existing(p);if(s?.Miasma is not{} a){return;}a.Cancelled=true;s.Miasma=null;
        if(s.PendingAbility==CombatAbility.MiasmaBolt){s.PendingAbility=CombatAbility.None;}
    }
    internal static bool ResolvePrepared(PlayerMobile p,long now)
    {
        var s=ClassCombat.For(p);if(s.Miasma is not{} a||a.Released||a.Cancelled){return false;}
        if(!Actor(p)||!ShapeRestrictions.Allowed(p,CombatAbility.MiasmaBolt)||p.Frozen||p.Paralyzed||p.Spell!=null||!AlliedOverlap.Allowed(p)
            ||s.PendingAbility!=CombatAbility.MiasmaBolt||p.Map!=s.ActionMap||p.Location!=s.ActionOrigin
            ||(CombatTiming.Equipped(p)?.Serial??Serial.Zero)!=s.PendingWeapon)
        {Cancel(p);return false;}
        if(now<s.EffectAt){return false;}a.Released=true;s.Miasma=null;s.PendingAbility=CombatAbility.None;
        var point=s.PendingPoint;
        if(World.FindMobile(s.PendingTarget)is BaseCreature{Alive:true,Deleted:false} target&&target.Map==p.Map&&ClassCombat.IsHostile(p,target))
        {point=target.Location;}
        try
        {
            int cost=SkillMechanics.Values(p,CombatAbility.MiasmaBolt).Mana256;
            a.Flight=MiasmaWorld.ReleasePrepared(p,point,TimedEffects.Now);if(a.Flight!=0){a.ManaPaid256=cost;}return a.Flight!=0;
        }
        catch(NotSupportedException ex){a.Fault=ex.Message;p.SendMessage(0x22,"Miasma Bolt: "+ex.Message);return false;}
    }
}
