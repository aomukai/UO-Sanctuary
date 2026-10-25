// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;
internal sealed record SentryCorpseCandidate(int Guid,int X,int Y,int Type=1,int Mode=12,bool Masked=false,bool Model=true,bool Selectable=true);
// Ordered-room/linked-list projection is a caller service. Do not sort by
// distance or apply AI104's later remembered-GUID/explosion-distance gate here.
internal static class AssassinSentryCorpses
{
    internal static SentryCorpseCandidate Select(bool caster,bool target,int rank,bool room,
        int enemyX,int enemyY,IEnumerable<SentryCorpseCandidate> ordered)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        if(!caster||!target||rank<=0||!room){return null;}
        foreach(var corpse in ordered)
        {
            if(corpse.Type!=1||corpse.Mode!=12||corpse.Masked||!corpse.Model||!corpse.Selectable){continue;}
            long dx=(long)corpse.X-enemyX,dy=(long)corpse.Y-enemyY;
            if(dx*dx+dy*dy<=100){return corpse;}
        }
        return null;
    }
}
