// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Source frame-based cold state for the saved weapon-missile consumer. Other
// chill producers keep their existing callers until individually migrated.
public static class ColdStates
{
    internal static int StateFrames(int mitigatedFrames,bool monster,int effect,int divisor)
    {
        if(mitigatedFrames<=0 || effect==0){return 0;}
        if(monster && effect<0 && divisor!=0){mitigatedFrames/=divisor;}
        return Math.Max(1,mitigatedFrames);
    }
    internal static bool ShatterRoll(bool monster,bool deadCollision,int roll)=>roll>=20 && monster && deadCollision;
    internal static int LengthFrames(int frames,int resistance,bool cannotFreeze,bool half)
    {
        if(frames<=0 || cannotFreeze || resistance>=100){return 0;}
        if(half){frames/=2;}
        return Math.Max(0,SourcePercentage.Calculate(frames,Math.Clamp(100-resistance,0,200),100));
    }
    internal static void Expire(Mobile target,TimedActor actor)=>actor.Cold=null;
    public static int OtherRate(Mobile target)=>TimedEffects.TryActor(target,out var actor) && actor.Cold!=null && actor.ChillUntil>TimedEffects.Now?-actor.ChillPercent:0;
    internal static bool Commit(Mobile source,Mobile target,int mitigatedFrames,bool monster,int effect,int divisor,long tick)
    {
        if(target is not{Alive:true,Deleted:false} || target.IsDeadBondedPet || DruidSpiritWolfBody.ObservedChillImmune(target)){return false;}
        int frames=StateFrames(mitigatedFrames,monster,effect,divisor);if(frames==0){return false;}
        var now=TimedEffects.TimeAt(tick);
        bool existing=TimedEffects.TryActor(target,out var old) && old.ChillUntil>now && old.ChillPercent!=0;
        uint owner=existing?(old.Cold?.Source ?? 0):(source?.Serial.Value ?? 0);
        TimedEffects.Chill(target,-effect,checked(frames*40),now);
        if(!TimedEffects.TryActor(target,out var actor) || !target.Alive){return false;}
        actor.Cold=new(owner);
        // The older raw SHATTER/deadCol branch does not establish current
        // corpse destruction. Its compiled helper is component evidence only;
        // no guessed destruction chance or corpse eligibility is enabled here.
        return true;
    }
    internal static int Effect(Mobile target)
    {
        var rule=PaladinHolyFreeze.SourceRule(target);
        return rule==null?-50:MonsterVariants.Difficulty((BaseCreature)target) switch
        {MonsterDifficulty.Nightmare=>rule.Nightmare,MonsterDifficulty.Hell=>rule.Hell,_=>rule.Normal};
    }
    public static bool Apply(Mobile source,Mobile target,int frames,long tick,int? pierceOverride=null)
    {
        if(target is not{Alive:true,Deleted:false} || target.IsDeadBondedPet){return false;}
        bool monster=target is BaseCreature;int effect=Effect(target);
        int resistance=NamedElements.Resistance(source,target,"cold",target.ColdResistance,pierceOverride);
        bool cannot=ItemDurationDefenses.CannotFreeze(target),half=ItemDurationDefenses.HalfFreeze(target);
        frames=LengthFrames(frames,resistance,cannot,half);
        if(!Commit(source,target,frames,monster,effect,NecromancerEffects.DifficultyDivisor(target),tick)){return false;}
        target.FixedEffect(0x374A,10,15,0x480,0); // Identified native cold-state marker.
        return true;
    }
}
