// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
namespace Sanctuary;

// Owned ordinary human WKSC records share the existing Necromancer clock.
// SC alone is insufficient:Chain's UseAttackRate requires a separate caller.
internal static class WarlockCastClock
{
    internal static bool Handles(CombatAbility ability)=>ClassAbilities.Find(ability)is{Class:DiabloClass.Warlock,Passive:false}
        &&SkillMechanics.For(ability).Text("anim")=="SC"&&SkillMechanics.For(ability).Number("UseAttackRate")==0;
    public static void Configure()
    {
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in BalanceCatalog.Read<SourceCastRow>("warlock-cast-clock"))
        {
            if(row.Frames!=16||row.Speed!=256||row.Action!=8||row.Flag!=1||!keys.Add(row.WeaponClass))
            {throw new InvalidDataException("Unsupported pinned Warlock SC animation");}
        }
        foreach(string key in new[]{"HTH","1HS","1HT","2HS","2HT","STF","BOW","XBW"})
        {if(!keys.Contains(key)){throw new InvalidDataException("Missing Warlock SC weapon class");}}
        if(keys.Count!=8){throw new InvalidDataException("Unexpected Warlock SC weapon classes");}
        int count=0;foreach(var row in SkillMechanics.All)
        {
            if(!Handles(row.Ability)){continue;}
            if(row.Text("seqtrans")!="SC"){throw new InvalidDataException("Warlock ordinary SC transition changed");}count++;
        }
        if(count!=22){throw new InvalidDataException("Unexpected Warlock ordinary SC routes");}
    }
    internal static ActionTiming Calculate(int fcr)=>SourceCastClock.Calculate(16,256,8,fcr);
}
