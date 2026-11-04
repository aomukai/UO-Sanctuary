// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal static class BoneBarrierCasts
{
    internal static bool Handles(CombatAbility ability)=>ability is CombatAbility.BoneWall or CombatAbility.BonePrison;
    // Approved Sanctuary aid, not a measured D2R client boundary. Reuse the
    // near-click scan with a two-tile circle and lowest-serial exact ties.
    internal const int NearClickTiles=2;
    internal static Mobile AcquirePrison(PlayerMobile owner,Point3D cursor)
        =>NearClickTargets.Acquire(owner,cursor,NearClickTiles,target=>target is BaseCreature{Alive:true,Deleted:false,Blessed:false}
            &&MobileTarget(owner,target)&&ClassCombat.IsHostile(owner,target)&&Math.Abs(owner.Z-target.Z)<16
            &&Valid(owner,CombatAbility.BonePrison,target.Serial,target.Location),stableTies:true);
    internal static bool MobileTarget(PlayerMobile owner,Mobile target)=>target is{Alive:true,Deleted:false}&&target.Map==owner.Map;
    internal static bool Point(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D cached,out Point3D point)
    {
        point=cached;
        if(selected!=Serial.Zero)
        {
            var target=World.FindMobile(selected);if(!MobileTarget(owner,target)){return false;}point=target.Location;
        }
        return ability!=CombatAbility.BonePrison||selected!=Serial.Zero;
    }
    internal static bool Valid(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D cached)
        =>owner.Map is{} map&&map!=Map.Internal&&Point(owner,ability,selected,cached,out var p)
            &&p.X>0&&p.Y>0&&p.X<map.Width&&p.Y<map.Height&&owner.InRange(p,CombatProtocol.TeethRange)&&owner.InLOS(p)
            &&SpellHelper.CheckTown(owner.Location,owner)&&SpellHelper.CheckTown(p,owner);
    internal static bool Release(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D cached,SkillValues values)
    {
        if(!Point(owner,ability,selected,cached,out var point)){return false;}
        // Sanctuary's shared action framework settles once at release. Legacy
        // Prison start19 settles earlier; phase/cancel parity remains separate.
        if(!BoneBarriers.Create(owner,ability,point,values)){return false;}
        return NecromancerEffects.Pay(owner,values.Mana256);
    }
}
