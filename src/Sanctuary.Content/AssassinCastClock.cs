// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
namespace Sanctuary;

internal static class AssassinCastClock
{
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.PsychicHammer or CombatAbility.BurstOfSpeed or CombatAbility.CloakOfShadows or CombatAbility.Fade or CombatAbility.MindBlast or CombatAbility.Venom or CombatAbility.BladeShield;
    public static void Configure()
    {
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in BalanceCatalog.Read<SourceCastRow>("assassin-cast-clock"))
        {
            if(row.Frames!=17||row.Speed!=256||row.Action!=9||row.Flag!=1||!keys.Add(row.WeaponClass))
            {throw new InvalidDataException("Unsupported pinned Assassin SC animation");}
        }
        foreach(string key in new[]{"HTH","1HS","1HT","2HS","2HT","STF","BOW","XBW","HT1","HT2"})
        {if(!keys.Contains(key)){throw new InvalidDataException("Missing Assassin SC weapon class");}}
        if(keys.Count!=10){throw new InvalidDataException("Unexpected Assassin SC weapon class");}
        int count=0;
        foreach(var row in SkillMechanics.All)
        {
            if(!Handles(row.Ability)){continue;}
            if(row.Text("anim")!="SC"||row.Text("seqtrans")!="SC")
            {throw new InvalidDataException("Assassin skill no longer selects SC");}
            count++;
        }
        if(count!=7){throw new InvalidDataException("Unexpected Assassin SC routes");}
    }
    // Every owned human Assassin SC weapon record has the same clock.
    internal static ActionTiming Calculate(int fcr)=>SourceCastClock.Calculate(17,256,9,fcr);
}
