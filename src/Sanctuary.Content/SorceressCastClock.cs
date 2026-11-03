// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
namespace Sanctuary;

internal static class SorceressCastClock
{
    internal static bool Handles(CombatAbility ability)=>ClassAbilities.Find(ability) is{Class:DiabloClass.Sorceress,Passive:false}
        &&ability is not (CombatAbility.Lightning or CombatAbility.ChainLightning or CombatAbility.Inferno);
    public static void Configure()
    {
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in BalanceCatalog.Read<SourceCastRow>("sorceress-cast-clock"))
        {
            if(row.Frames!=14||row.Speed!=256||row.Action!=7||row.Flag!=1||!keys.Add(row.WeaponClass))
            {throw new InvalidDataException("Unsupported pinned Sorceress SC animation");}
        }
        foreach(string key in new[]{"HTH","1HS","1HT","2HS","2HT","STF","BOW","XBW"})
        {if(!keys.Contains(key)){throw new InvalidDataException("Missing Sorceress SC weapon class");}}
        if(keys.Count!=8){throw new InvalidDataException("Unexpected Sorceress SC weapon class");}
        int count=0;
        foreach(var row in SkillMechanics.All)
        {
            if(!Handles(row.Ability)){continue;}
            if(row.Text("anim")!="SC"||row.Text("seqtrans")!="SC")
            {throw new InvalidDataException("Sorceress skill no longer selects SC");}
            count++;
        }
        if(count!=23){throw new InvalidDataException("Unexpected Sorceress SC routes");}
    }
    // Every owned human Sorceress SC weapon record has the same clock.
    internal static ActionTiming Calculate(int fcr)=>SourceCastClock.Calculate(14,256,7,fcr);
}
