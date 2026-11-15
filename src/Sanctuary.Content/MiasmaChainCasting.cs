// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Approved normal-SC/FCR adaptation;the current startup remains unverified.
// The legacy CAST mode caller prioritizes FCR despite UseAttackRate;current
// Warlock startup has not been recovered;the shared action owns release.
internal static class MiasmaChainCasting
{
    internal sealed class Pending
    {internal bool Released,Cancelled;internal long Chain;internal int ManaPaid256;internal string Fault;}
    private static bool Actor(PlayerMobile p)=>p is{Map:not null}&&MiasmaWorld.Fighting(p,p.Map.MapID)
        &&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock&&SkillMechanics.Rank(p,CombatAbility.MiasmaChain)>0;
    internal static bool BeginLearned(PlayerMobile p,Serial selected,Point3D point,long now)
    {
        if(!MiasmaChainWorld.Approved||!WarlockCombat.Available(p,CombatAbility.MiasmaChain)){return false;}
        try{return BeginPrepared(p,selected,point,now)!=null;}
        catch(NotSupportedException ex){p.SendMessage(0x22,"Miasma Chain: "+ex.Message);return false;}
    }
    internal static Pending BeginPrepared(PlayerMobile p,Serial selected,Point3D point,long now)
    {
        if(!Actor(p)||!MiasmaChainWorld.Aim(p,selected,ref point)
            ||!SkillCombat.BeginPrepared(p,CombatAbility.MiasmaChain,selected,point,now,
                (who,_,id,at)=>MiasmaChainWorld.Aim(who,id,ref at),(who,_)=>Actor(who),WarlockCastClock.Calculate(CombatTiming.Fcr(p))))
        {return null;}
        var a=new Pending();ClassCombat.For(p).MiasmaChain=a;return a;
    }
    internal static void Cancel(PlayerMobile p)
    {
        var s=ClassCombat.Existing(p);if(s?.MiasmaChain is not{} a){return;}a.Cancelled=true;s.MiasmaChain=null;
        if(s.PendingAbility==CombatAbility.MiasmaChain){s.PendingAbility=CombatAbility.None;}
    }
    internal static bool ResolvePrepared(PlayerMobile p,long now)
    {
        var s=ClassCombat.For(p);if(s.MiasmaChain is not{} a||a.Released||a.Cancelled){return false;}
        if(!Actor(p)||!ShapeRestrictions.Allowed(p,CombatAbility.MiasmaChain)||p.Frozen||p.Paralyzed||p.Spell!=null||!AlliedOverlap.Allowed(p)
            ||s.PendingAbility!=CombatAbility.MiasmaChain||p.Map!=s.ActionMap||p.Location!=s.ActionOrigin
            ||(CombatTiming.Equipped(p)?.Serial??Serial.Zero)!=s.PendingWeapon)
        {Cancel(p);return false;}
        if(now<s.EffectAt){return false;}a.Released=true;s.MiasmaChain=null;s.PendingAbility=CombatAbility.None;
        var selected=s.PendingTarget;var point=s.PendingPoint;
        if(World.FindMobile(selected)is not BaseCreature t||!MiasmaWorld.Enemy(p,t,p.Z)){selected=Serial.Zero;}
        try
        {
            int cost=SkillMechanics.Values(p,CombatAbility.MiasmaChain).Mana256;
            a.Chain=MiasmaChainWorld.ReleasePrepared(p,selected,point,TimedEffects.Now);if(a.Chain!=0){a.ManaPaid256=cost;}return a.Chain!=0;
        }
        catch(NotSupportedException ex){a.Fault=ex.Message;p.SendMessage(0x22,"Miasma Chain: "+ex.Message);return false;}
    }
}
