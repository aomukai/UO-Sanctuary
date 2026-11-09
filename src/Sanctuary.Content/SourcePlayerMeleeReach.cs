// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal readonly record struct PlayerReachSelection(bool Found,int? Kind,int? RangeAdder);
internal static class SourcePlayerMeleeReach
{
    // Current27c360 player branch:class7bonus is captured before inventory
    // selection. Selection failure returns0;null/nonitem success retains bonus.
    internal static int Read(int sourceClass,Func<PlayerReachSelection> select)
    {
        int bonus=sourceClass==7?2:0;var weapon=select();
        if(!weapon.Found){return 0;}if(weapon.Kind!=4){return bonus;}
        if(weapon.RangeAdder is not int range||range is <0 or >255)
        {throw new NotSupportedException("Current player reach requires its byte item row");}
        return bonus+range;
    }
}
