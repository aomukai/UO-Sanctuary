// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
namespace Sanctuary;

internal static class NecromancerCastClock
{
    internal static bool Handles(CombatAbility ability)=>ClassAbilities.Find(ability) is{Class:DiabloClass.Necromancer,Passive:false}
        &&ability!=CombatAbility.PoisonDagger;
    public static void Configure()
    {
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in BalanceCatalog.Read<SourceCastRow>("necromancer-cast-clock"))
        {
            if(row.Frames!=16||row.Speed!=256||row.Action!=8||row.Flag!=1||!keys.Add(row.WeaponClass))
            {throw new InvalidDataException("Unsupported pinned Necromancer SC animation");}
        }
        foreach(string key in new[]{"HTH","1HS","1HT","2HS","2HT","STF","BOW","XBW"})
        {if(!keys.Contains(key)){throw new InvalidDataException("Missing Necromancer SC weapon class");}}
        if(keys.Count!=8){throw new InvalidDataException("Unexpected Necromancer SC weapon class");}
        int count=0;
        foreach(var row in SkillMechanics.All)
        {
            if(!Handles(row.Ability)){continue;}
            if(row.Text("anim")!="SC"||row.Text("seqtrans")!="SC")
            {throw new InvalidDataException("Necromancer skill no longer selects SC");}
            count++;
        }
        if(count!=26){throw new InvalidDataException("Unexpected Necromancer SC routes");}
    }
    // Every owned human Necromancer SC weapon record has the same clock.
    internal static ActionTiming Calculate(int fcr)=>SourceCastClock.Calculate(16,256,8,fcr);
}
