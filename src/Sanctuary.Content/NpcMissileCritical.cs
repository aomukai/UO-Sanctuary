// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal static class NpcMissileCritical
{
    // User-approved unsigned NPC weapon-missile policy. The historical
    // proposal entry remains for its retained differential source controls.
    internal static bool ProposedUnsigned(int passive,int deadly,int mastery,Func<uint> raw)=>Unsigned(passive,deadly,mastery,raw);
    internal static bool Unsigned(int passive,int deadly,int mastery,Func<uint> raw)
    {
        if(passive<0||deadly<0||mastery<0){throw new NotSupportedException("Negative NPC missile critical chances need a source policy");}
        ArgumentNullException.ThrowIfNull(raw);
        if(raw()%100<(uint)passive){return true;}
        if(deadly!=0&&raw()%100<(uint)deadly){return true;}
        return mastery!=0&&raw()%100<(uint)mastery;
    }
}
