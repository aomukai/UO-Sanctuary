// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal static class DruidFormCastClock
{
    internal static ActionTiming Calculate(CombatAbility form,int fcr)
    {
        if(fcr<0){throw new ArgumentOutOfRangeException(nameof(fcr));}
        var row=form switch
        {
            CombatAbility.None=>(Frames:15,Speed:208,Action:8),
            CombatAbility.Werewolf=>(Frames:11,Speed:168,Action:5),
            CombatAbility.Werebear=>(Frames:10,Speed:152,Action:5),
            _=>throw new ArgumentOutOfRangeException(nameof(form))
        };
        long effective=120L*fcr/(120L+fcr);
        int advance=checked((int)(row.Speed*Math.Min(175L,100+effective)/100));
        return new((row.Frames*256+advance-1)/advance,(row.Action*256+advance-1)/advance);
    }
}
