// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
namespace Sanctuary;

internal static class BarbarianCastClock
{
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.Howl or CombatAbility.FindPotion or CombatAbility.Shout or CombatAbility.Taunt or CombatAbility.BattleCry or CombatAbility.FindItem or CombatAbility.BattleOrders or CombatAbility.WarCry or CombatAbility.BattleCommand;
    public static void Configure()
    {
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in BalanceCatalog.Read<SourceCastRow>("barbarian-cast-clock"))
        {
            if(row.Frames!=14||row.Speed!=256||row.Action!=9||row.Flag!=1||!keys.Add(row.WeaponClass))
            {throw new InvalidDataException("Unsupported pinned Barbarian SC animation");}
        }
        foreach(string key in new[]{"HTH","1HS","1HT","2HS","2HT","STF","BOW","XBW","1JS","1JT","1SS","1ST"})
        {if(!keys.Contains(key)){throw new InvalidDataException("Missing Barbarian SC weapon class");}}
        if(keys.Count!=12){throw new InvalidDataException("Unexpected Barbarian SC weapon class");}
        int count=0;
        foreach(var row in SkillMechanics.All)
        {
            if(!Handles(row.Ability)){continue;}
            if(row.Text("anim")!="SC"||row.Text("seqtrans")!="SC")
            {throw new InvalidDataException("Barbarian skill no longer selects SC");}
            count++;
        }
        if(count!=9){throw new InvalidDataException("Unexpected Barbarian SC routes");}
    }
    // Every owned human Barbarian SC weapon record has the same clock.
    internal static ActionTiming Calculate(int fcr)=>SourceCastClock.Calculate(14,256,9,fcr);
}
