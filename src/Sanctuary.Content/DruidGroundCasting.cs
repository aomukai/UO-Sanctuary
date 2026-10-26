// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

// Shared native ground/target input adapter for the Druid's travelling fire
// spells. Source-specific missile factories and payment remain with the skill.
internal static class DruidGroundCasting
{
    internal static bool Ground(PlayerMobile owner,Point3D point)=>owner?.Map!=null && owner.Map!=Map.Internal
        && point.X>0 && point.Y>0 && point.X<owner.Map.Width && point.Y<owner.Map.Height
        && owner.InRange(point,CombatProtocol.TeethRange) && owner.InLOS(point)
        && !SpellHelper.IsTown(owner.Location,owner) && !SpellHelper.IsTown(point,owner);
    internal static bool Aim(PlayerMobile owner,Serial selected,ref Point3D point,bool release)
    {
        if(selected!=Serial.Zero)
        {
            var target=World.FindMobile(selected);
            if(target is{Deleted:false})
            {
                if(target.Map!=owner.Map || !release && !ClassCombat.IsHostile(owner,target)){return false;}
                // A registered dead target retains its location; a deleted
                // target falls back to the cached ground point at release.
                point=target.Location;
            }
            else if(!release){return false;}
        }
        return Ground(owner,point);
    }
}
