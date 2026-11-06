// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Monster branch of the older bonus getter and complete outer level clamp.
// NPC class/tree bonuses are deliberately absent in that source branch.
// Row validity, item ownership, shrine and maximum are explicit services.
internal static class NpcInnateSkillRank
{
    internal static int Read(int basis,int levelBonus,int all,int oskill,int single,int elementBonus=0,
        bool elemental=false,bool owned=false,bool bonuses=true,bool shrine=false,int maximum=99,
        bool unit=true,bool skill=true,bool row=true,bool valid=true)
    {
        if(maximum<0){throw new ArgumentOutOfRangeException(nameof(maximum));}
        if(!unit||!skill){return 0;}
        int value=basis;
        if(bonuses&&!owned&&row&&valid)
        {
            int extra=checked(levelBonus+(shrine?2:0)+all);
            extra=checked(extra+(basis<=0?oskill:Math.Min(oskill,3)));
            if(elemental){extra=checked(extra+elementBonus);}
            value=checked(value+extra+single);
        }
        return Math.Clamp(value,0,maximum);
    }
}
