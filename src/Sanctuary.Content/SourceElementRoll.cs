// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// SUNITDMG_RollDamageValueInRange; fixed-point inputs and an exclusive
// upper bound. Shared by original equipment and existing monster packets.
internal static class SourceElementRoll
{
    internal static int Roll256(int minimum,int maximum,int minimumPercent,int maximumPercent,int initial,Func<int,int> limited)
    {
        ArgumentNullException.ThrowIfNull(limited);
        int value=initial;
        if(maximum>0)
        {
            minimum=checked(minimum+SourcePercentage.Calculate(minimum,minimumPercent,100));
            maximum=checked(maximum+SourcePercentage.Calculate(maximum,maximumPercent,100));
            value=checked(value+minimum);
            if(maximum>minimum)
            {
                int bound=checked(maximum-minimum),roll=limited(bound);
                if(roll<0||roll>=bound){throw new InvalidOperationException("Element roll outside source range");}
                value=checked(value+roll);
            }
        }
        return Math.Max(0,value);
    }
}
