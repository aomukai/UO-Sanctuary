// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary;

internal static class ClayGolemSlow
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ClayGolemSlow));
    private static readonly HashSet<string> Reported=new();
    // Complete callback27 type/cap policy. Presence and successful-melee
    // classification belong to its actual event producer,not damage magnitude.
    internal static int? Amount(int kind,int slow,int monsterFlags)
    {
        if(slow<0){throw new NotSupportedException("Negative incoming Clay slow is not a skill output");}
        if(slow==0||kind is not(0 or 1)){return null;}
        return Math.Min(slow,kind==0||(monsterFlags&12)!=0?50:90);
    }
    private static bool Flags(Mobile attacker,out int flags)
    {
        flags=0;
        if(attacker is PlayerMobile){return true;}
        if(attacker is not BaseCreature creature){return false;}
        if(creature is EncounterCreature encounter)
        {
            flags=encounter.Grade switch{EncounterGrade.Champion=>4,EncounterGrade.Unique or EncounterGrade.SuperUnique=>8,_=>0};return !creature.IsParagon;
        }
        // Plain native creatures and pets use the ordinary native mapping.
        // A boss marker alone does not establish D2's unique/champion bits.
        if(!creature.IsParagon&&CreatureProfiles.For(creature).Traits.Boss!=true){return true;}
        return false;
    }
    internal static void Incoming(Mobile attacker,Mobile defender,bool ranged,DateTime at)
    {
        if(ranged||defender is not ClayGolemBody{Alive:true,Deleted:false} golem||attacker is not{Alive:true,Deleted:false}
            ||attacker==golem||attacker.Map!=golem.Map||golem.Map==null||golem.Map==Map.Internal||ClassCombat.Friendly(attacker,golem)){return;}
        if(!Flags(attacker,out int flags))
        {
            string key=attacker.GetType().FullName;
            if(Reported.Add(key)){Log.Warning("Clay incoming slow needs reviewed monster flags for {Type};other combat continues",key);}return;
        }
        if(Amount(attacker is PlayerMobile?0:1,golem.ReactiveSlowPercent,flags) is{} amount)
        {TimedEffects.SlowReaction(attacker,amount,at);}
    }
}
