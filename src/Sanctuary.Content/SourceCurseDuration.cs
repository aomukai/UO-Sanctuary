// SPDX-License-Identifier: GPL-3.0-or-later
using Server;

namespace Sanctuary;

public static class SourceCurseDuration
{
    // sub_6FD10EC0 gates only curse-mask states. It subtracts the truncated
    // percentage, preserving a one-frame curse below100 resistance.
    internal static bool Reduce(int frames,bool curse,int resistance,out int result)
    {
        result=frames;
        if(!curse){return true;}
        if(resistance>=100){return false;}
        if(resistance!=0){result=checked(frames-SourcePercentage.Calculate(frames,resistance,100));}
        return true;
    }
    internal static bool Incoming(Mobile target,string state,int frames,out int result)
        =>Reduce(frames,PaladinCleansing.IsCurse(state),AssassinBuffs.CurseResistance(target),out result);
}
