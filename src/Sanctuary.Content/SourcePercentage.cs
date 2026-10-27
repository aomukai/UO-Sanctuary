// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;

// MONSTERUNIQUE_CalculatePercentage is shared by summon life and missile
// conversion. Its large-value early division is observable in FP256 damage.
internal static class SourcePercentage
{
    internal static int Calculate(int value,int percent,int divisor)
    {
        if(divisor==0){return 0;}
        if(value<=1048576)
        {
            if(percent<=65536){return unchecked(percent*value)/divisor;}
            if(divisor<=percent>>4){return unchecked(value*(percent/divisor));}
        }
        else if(divisor<=value>>4){return unchecked(percent*(value/divisor));}
        return (int)((long)percent*value/divisor);
    }

    // Nonnegative damage factors are0..200. Preserve the source's division
    // ordering,with the existing native wide intermediate/ceiling retained
    // outside the legacy signed-int result domain.
    internal static long Damage(long value,int percent)
    {
        if(value<0||percent is <0 or >200){throw new System.ArgumentOutOfRangeException();}
        return value>1048576?checked(value/100*percent):checked(value*percent)/100;
    }
}
