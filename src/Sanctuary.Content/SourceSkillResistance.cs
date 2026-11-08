// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;

// Numeric contract of current453660. Record0c is an explicit lookup result,
// not a guessed species flag. Raw reads must precede item resistance pierce.
internal static class SourceSkillResistance
{
    internal static int ImmunityDelta(int resistance,int value,bool eligible)
        =>eligible&&value<0&&resistance>=100?value/5:value;
    internal static int Scale(int stat,int value,bool present,int kind,bool recordExempts,bool definition,bool root,int resistance)
        =>stat is 36 or 37 or 39 or 41 or 43 or 45&&value<=0&&present&&kind!=0
            &&!(kind==1&&recordExempts)&&definition&&root?ImmunityDelta(resistance,value,true):value;
}
