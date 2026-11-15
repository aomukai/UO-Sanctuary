// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Approved ordinary-SC action;shared action owns interruption and one release.
internal static class AbyssCasting
{
    internal sealed class Pending
    {internal bool Released,Cancelled;internal long Field;internal int ManaPaid256;internal string Fault;}
    private static bool Actor(PlayerMobile p)=>p is{Map:not null}&&MiasmaWorld.Fighting(p,p.Map.MapID)
        &&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock&&SkillMechanics.Rank(p,CombatAbility.Abyss)>0;
    internal static bool BeginLearned(PlayerMobile p,Serial selected,Point3D point,long now)
    {
        if(!AbyssWorld.Approved||!WarlockCombat.Available(p,CombatAbility.Abyss)){return false;}
        try{return BeginPrepared(p,selected,point,now)!=null;}
        catch(NotSupportedException ex){p.SendMessage(0x22,"Abyss: "+ex.Message);return false;}
    }
    internal static Pending BeginPrepared(PlayerMobile p,Serial selected,Point3D point,long now)
    {
        if(!Actor(p)||!AbyssWorld.Aim(p,selected,ref point)
            ||!SkillCombat.BeginPrepared(p,CombatAbility.Abyss,selected,point,now,
                (who,_,id,at)=>AbyssWorld.Aim(who,id,ref at),(who,_)=>Actor(who),WarlockCastClock.Calculate(CombatTiming.Fcr(p))))
        {return null;}
        var a=new Pending();ClassCombat.For(p).Abyss=a;return a;
    }
    internal static void Cancel(PlayerMobile p)
    {
        var s=ClassCombat.Existing(p);if(s?.Abyss is not{} a){return;}a.Cancelled=true;s.Abyss=null;
        if(s.PendingAbility==CombatAbility.Abyss){s.PendingAbility=CombatAbility.None;}
    }
    internal static bool ResolvePrepared(PlayerMobile p,long now)
    {
        var s=ClassCombat.For(p);if(s.Abyss is not{} a||a.Released||a.Cancelled){return false;}
        if(!Actor(p)||!ShapeRestrictions.Allowed(p,CombatAbility.Abyss)||p.Frozen||p.Paralyzed||p.Spell!=null||!AlliedOverlap.Allowed(p)
            ||s.PendingAbility!=CombatAbility.Abyss||p.Map!=s.ActionMap||p.Location!=s.ActionOrigin
            ||(CombatTiming.Equipped(p)?.Serial??Serial.Zero)!=s.PendingWeapon)
        {Cancel(p);return false;}
        if(now<s.EffectAt){return false;}a.Released=true;s.Abyss=null;s.PendingAbility=CombatAbility.None;
        var selected=s.PendingTarget;var point=s.PendingPoint;
        if(World.FindMobile(selected)is not BaseCreature t||!MiasmaWorld.Enemy(p,t,p.Z)){selected=Serial.Zero;}
        try
        {
            if(!AbyssWorld.Aim(p,selected,ref point)){return false;}
            int cost=SkillMechanics.Values(p,CombatAbility.Abyss).Mana256;
            a.Field=AbyssWorld.ReleasePrepared(p,point,TimedEffects.Now);if(a.Field!=0){a.ManaPaid256=cost;}return a.Field!=0;
        }
        catch(NotSupportedException ex){a.Fault=ex.Message;p.SendMessage(0x22,"Abyss: "+ex.Message);return false;}
    }
}
