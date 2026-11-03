// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
namespace Sanctuary;

internal static class PaladinCastClock
{
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.HolyBolt or CombatAbility.BlessedHammer or CombatAbility.HolyShield or CombatAbility.FistOfTheHeavens;
    public static void Configure()
    {
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in BalanceCatalog.Read<SourceCastRow>("paladin-cast-clock"))
        {
            if(row.Frames!=16||row.Speed!=256||row.Action!=9||row.Flag!=2||!keys.Add(row.WeaponClass))
            {throw new InvalidDataException("Unsupported pinned Paladin SC animation");}
        }
        foreach(string key in new[]{"HTH","1HS","1HT","2HS","2HT","STF","BOW","XBW"})
        {if(!keys.Contains(key)){throw new InvalidDataException("Missing Paladin SC weapon class");}}
        if(keys.Count!=8){throw new InvalidDataException("Unexpected Paladin SC weapon class");}
        int count=0;
        foreach(var row in SkillMechanics.All)
        {
            if(!Handles(row.Ability)){continue;}
            if(row.Text("anim")!="SC"||row.Text("seqtrans")!="SC")
            {throw new InvalidDataException("Paladin skill no longer selects SC");}
            count++;
        }
        if(count!=4){throw new InvalidDataException("Unexpected Paladin SC routes");}
    }
    // Every owned human Paladin SC weapon record has the same clock.
    internal static ActionTiming Calculate(int fcr)=>SourceCastClock.Calculate(16,256,9,fcr);
}
