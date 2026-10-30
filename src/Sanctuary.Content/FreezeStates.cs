// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record SourceFreezeState(uint Source,DateTime AiUntilUtc,bool Removed=false);

// Source-frame freeze application shared by skill callers. Item freeze keeps
// its existing chance/length provider; remaining producer ordering is separate.
public static class FreezeStates
{
    // 0 rejects, 1 delegates to ordinary cold, 2 applies a freeze stat list.
    internal static int Mode(int type,bool uninterruptable,bool boss,bool unique,bool hireling,int? effect)
    {
        if(type==0){return 1;}
        if(type==1)
        {
            if(uninterruptable){return 0;}
            if(boss || unique || hireling){return 1;}
            if(effect>=0){return 0;}
        }
        return 2;
    }
    internal static int RawFrames(int mitigatedFrames,bool monster,int divisor)=>monster?mitigatedFrames/divisor:mitigatedFrames;
    internal static int RemovalDelay(int normal,int current,bool network)=>Math.Max(1,(network?current:normal)>0?(network?current:normal):15);
    private static int RemovalDelay(Mobile target)
    {
        if(target is not BaseCreature creature || PaladinHolyFreeze.SourceRule(target)==null){return 0;}
        var stats=CreatureProfiles.For(creature).SourceStats;
        int Read(string key)=>stats.TryGetValue(key,out string value) && int.TryParse(value,out int n)?n:0;
        string field=MonsterVariants.Difficulty(creature) switch{MonsterDifficulty.Nightmare=>"aidel(N)",MonsterDifficulty.Hell=>"aidel(H)",_=>"aidel"};
        // Sanctuary is a network co-op host; current-difficulty AI delay is the
        // source network-game branch. Known rows with no positive delay use its15-frame fallback.
        return RemovalDelay(Read("aidel"),Read(field),true);
    }
    internal static bool Commit(Mobile source,Mobile target,int mitigatedFrames,bool monster,int divisor,long tick)
    {
        if(mitigatedFrames<=0 || target is not{Alive:true,Deleted:false} || target.IsDeadBondedPet){return false;}
        int frames=RawFrames(mitigatedFrames,monster,divisor);var now=TimedEffects.TimeAt(tick);
        var actor=TimedEffects.Actor(target,now);var old=actor.Control??new(default,default,default,0,0,0);
        bool existing=old.FreezeUntil>now;uint owner=existing?(old.Freeze?.Source??0):(source?.Serial.Value??0);
        // The raw freeze routine permits zero after division. The event setter
        // moves an expiry at the current frame to the following frame.
        var until=now.AddMilliseconds(Math.Max(1,frames)*40L);
        if(existing && old.FreezeUntil>until){until=old.FreezeUntil;}
        actor.Control=old with{FreezeUntil=until,Freeze=new(owner,now.AddMilliseconds((frames+1)*40L))};
        if(target is BaseCreature creature){NamedControl.Interrupt(creature);}
        return true;
    }
    internal static void Settle(Mobile target,TimedActor actor,DateTime now)
    {
        if(actor.Control is not{Freeze:{} freeze} control){return;}
        if(!freeze.Removed && control.FreezeUntil<=now)
        {
            freeze=freeze with{Removed=true,AiUntilUtc=control.FreezeUntil.AddMilliseconds(RemovalDelay(target)*40L)};
            control=control with{FreezeUntil=default,Freeze=freeze};
        }
        if(freeze.Removed && freeze.AiUntilUtc<=now){control=control with{Freeze=null};}
        actor.Control=control;
    }
    internal static bool Waiting(Mobile target)
    {
        if(!TimedEffects.TryActor(target,out var actor)){return false;}Settle(target,actor,TimedEffects.Now);
        return actor.Control is{Freeze:{Removed:true} freeze} && freeze.AiUntilUtc>TimedEffects.Now;
    }
    internal static bool Shattered(Mobile target)=>target!=null && TimedEffects.TryActor(target,out var actor)
        && actor.Control is{Freeze:{Removed:false}} state && state.FreezeUntil>TimedEffects.Now;
    public static bool Apply(Mobile source,Mobile target,int frames,long tick,int? pierceOverride=null)
    {
        if(frames<=0 || target is not{Alive:true,Deleted:false} || target.IsDeadBondedPet){return false;}
        bool monster=target is BaseCreature;var rule=PaladinHolyFreeze.SourceRule(target);
        int? effect=rule==null?null:ColdStates.Effect(target);
        // Native mapped boss/encounter grades project source boss/unique-bit8.
        // D2-only uninterruptable/hireling modes have no native hostile adapter;
        // their distinct source branches remain explicit component coverage.
        int mode=Mode(target is PlayerMobile?0:monster?1:2,false,monster && CreatureProfiles.For((BaseCreature)target).Traits.Boss==true,
            monster && NamedCombat.Elite(target),false,effect);
        if(mode==0){return false;}if(mode==1){return ColdStates.Apply(source,target,frames,tick,pierceOverride);}
        int resistance=NamedElements.Resistance(source,target,"cold",target.ColdResistance,pierceOverride);
        bool cannot=ItemDurationDefenses.CannotFreeze(target),half=ItemDurationDefenses.HalfFreeze(target);
        frames=ColdStates.LengthFrames(frames,resistance,cannot,half);
        if(!Commit(source,target,frames,monster,NecromancerEffects.DifficultyDivisor(target),tick)){return false;}
        target.FixedEffect(0x374A,10,15,0x480,0); // Identified native freeze marker.
        return true;
    }
}
