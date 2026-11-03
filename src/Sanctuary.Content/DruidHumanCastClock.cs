// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
namespace Sanctuary;

internal static class DruidHumanCastClock
{
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.Firestorm or CombatAbility.MoltenBoulder or CombatAbility.Fissure or CombatAbility.CycloneArmor or CombatAbility.Twister or CombatAbility.Volcano or CombatAbility.Tornado or CombatAbility.Hurricane or CombatAbility.Armageddon or CombatAbility.Werewolf or CombatAbility.Werebear or CombatAbility.Raven or CombatAbility.OakSage or CombatAbility.SummonSpiritWolf or CombatAbility.HeartOfWolverine or CombatAbility.SummonDireWolf or CombatAbility.SpiritOfBarbs or CombatAbility.SummonGrizzly;
    public static void Configure()
    {
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in BalanceCatalog.Read<SourceCastRow>("druid-human-cast-clock"))
        {
            if(row.Frames!=15||row.Speed!=208||row.Action!=8||row.Flag!=1||!keys.Add(row.WeaponClass))
            {throw new InvalidDataException("Unsupported pinned DruidHuman SC animation");}
        }
        foreach(string key in new[]{"HTH","1HS","1HT","2HS","2HT","STF","BOW","XBW"})
        {if(!keys.Contains(key)){throw new InvalidDataException("Missing DruidHuman SC weapon class");}}
        if(keys.Count!=8){throw new InvalidDataException("Unexpected DruidHuman SC weapon class");}
        int count=0;
        foreach(var row in SkillMechanics.All)
        {
            if(!Handles(row.Ability)){continue;}
            if(row.Text("anim")!="SC"||row.Text("seqtrans")!="SC")
            {throw new InvalidDataException("DruidHuman skill no longer selects SC");}
            count++;
        }
        if(count!=18){throw new InvalidDataException("Unexpected DruidHuman SC routes");}
    }
    // Every owned human Druid SC weapon record has the same clock.
    internal static ActionTiming Calculate(int fcr)=>DruidFormCastClock.Calculate(CombatAbility.None,fcr);
}
