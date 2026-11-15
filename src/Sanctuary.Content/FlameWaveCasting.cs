// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Approved normal SC/FCR action; shared action owns entry, interruption and one release.
internal static class FlameWaveCasting
{
    internal sealed class Pending
    {internal bool Released,Cancelled;internal long Wave;internal int ManaPaid256;internal string Fault;}
    private static bool Actor(PlayerMobile p)=>p is{Map:not null}&&MiasmaWorld.Fighting(p,p.Map.MapID)
        &&ClassCreation.Profiles.TryGet(p.Serial,out var c)&&c.Class==DiabloClass.Warlock&&SkillMechanics.Rank(p,CombatAbility.FlameWave)>0;
    internal static bool BeginLearned(PlayerMobile p,Serial selected,Point3D point,long now)
    {
        if(!FlameWaveWorld.Approved||!WarlockCombat.Available(p,CombatAbility.FlameWave)){return false;}
        try{return BeginPrepared(p,selected,point,now)!=null;}
        catch(NotSupportedException ex){p.SendMessage(0x22,"Flame Wave: "+ex.Message);return false;}
    }
    internal static Pending BeginPrepared(PlayerMobile p,Serial selected,Point3D point,long now)
    {
        if(!Actor(p)||!FlameWaveWorld.Aim(p,selected,ref point)
            ||!SkillCombat.BeginPrepared(p,CombatAbility.FlameWave,selected,point,now,
                (who,_,id,at)=>FlameWaveWorld.Aim(who,id,ref at),(who,_)=>Actor(who),WarlockCastClock.Calculate(CombatTiming.Fcr(p))))
        {return null;}
        var a=new Pending();ClassCombat.For(p).FlameWave=a;return a;
    }
    internal static void Cancel(PlayerMobile p)
    {
        var s=ClassCombat.Existing(p);if(s?.FlameWave is not{} a){return;}a.Cancelled=true;s.FlameWave=null;
        if(s.PendingAbility==CombatAbility.FlameWave){s.PendingAbility=CombatAbility.None;}
    }
    internal static bool ResolvePrepared(PlayerMobile p,long now)
    {
        var s=ClassCombat.For(p);if(s.FlameWave is not{} a||a.Released||a.Cancelled){return false;}
        if(!Actor(p)||!ShapeRestrictions.Allowed(p,CombatAbility.FlameWave)||p.Frozen||p.Paralyzed||p.Spell!=null||!AlliedOverlap.Allowed(p)
            ||s.PendingAbility!=CombatAbility.FlameWave||p.Map!=s.ActionMap||p.Location!=s.ActionOrigin
            ||(CombatTiming.Equipped(p)?.Serial??Serial.Zero)!=s.PendingWeapon)
        {Cancel(p);return false;}
        if(now<s.EffectAt){return false;}a.Released=true;s.FlameWave=null;s.PendingAbility=CombatAbility.None;
        var selected=s.PendingTarget;var point=s.PendingPoint;
        if(World.FindMobile(selected)is not BaseCreature t||!MiasmaWorld.Enemy(p,t,p.Z)){selected=Serial.Zero;}
        try
        {
            if(!FlameWaveWorld.Aim(p,selected,ref point)){return false;}
            int cost=SkillMechanics.Values(p,CombatAbility.FlameWave).Mana256;
            a.Wave=FlameWaveWorld.ReleasePrepared(p,point,TimedEffects.Now);if(a.Wave!=0){a.ManaPaid256=cost;}return a.Wave!=0;
        }
        catch(NotSupportedException ex){a.Fault=ex.Message;p.SendMessage(0x22,"Flame Wave: "+ex.Message);return false;}
    }
}
