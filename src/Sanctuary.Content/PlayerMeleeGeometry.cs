// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record PlayerWeaponReach(string Code,int Range);
internal static class PlayerMeleeGeometry
{
    private static readonly Lazy<Dictionary<string,int>> Ranges=new(()=>
    {
        var rows=BalanceCatalog.Read<PlayerWeaponReach>("player-melee-ranges");
        if(rows.Length!=306||rows.Any(x=>string.IsNullOrWhiteSpace(x.Code)||x.Range is <0 or >255)||rows.Select(x=>x.Code).Distinct().Count()!=306)
        {throw new InvalidDataException("Invalid pinned player weapon reach");}
        return rows.ToDictionary(x=>x.Code,x=>x.Range,StringComparer.Ordinal);
    });
    internal static int ForCode(string code)=>code!=null&&Ranges.Value.TryGetValue(code,out int reach)?reach:throw new NotSupportedException("No pinned melee reach for equipped weapon");
    internal static int Reach(PlayerMobile owner)
    {
        var weapon=CombatTiming.Equipped(owner);
        if(weapon==null||ClassGear.Broken(weapon)){return 0;}
        var code=ClassGear.Base(weapon)?.Code;
        return ForCode(code);
    }
    // Coarse native input/search bound only. Exact attack/progressive/contact
    // predicates use the source footprint/distance/collision routine below.
    internal static int SearchRange(PlayerMobile owner)=>Math.Min(CombatProtocol.SightRange,(Reach(owner)+5)/2);
    internal static bool Within(PlayerMobile owner,BaseCreature target,int bonus)
    {
        if(owner.Map==null||owner.Map==Map.Internal||owner.Map!=target.Map||Math.Abs(owner.Z-target.Z)>=16){return false;}
        int reach=Reach(owner);
        if(PlayerWeaponContact.Metadata(target,true)==null)
        {
            // Provisional unclassified UO bridge: weapon reach projected to
            // native tiles/LOS. Exact source footprints are not asserted.
            // It receives no invented D2 base ID, footprint or tentacle flag.
            return owner.InRange(target,Math.Max(1,(reach+3)/2))&&owner.InLOS(target);
        }
        var defender=ReviveMeleeGeometry.Unit(target,requireRange:false);
        // The predicate uses the canonical base ID; the shared row profile
        // retains the actor's own hcIdx for identity validation.
        if(CreatureProfiles.For(target).SourceStats is{}stats&&stats.TryGetValue("BaseId",out string root)&&!string.IsNullOrEmpty(root))
        {defender=defender with{BaseId=ReviveMeleeGeometry.Profile(root).BaseId};}
        var attacker=new MeleeGeometryUnit(PetOwnerRoutes.Cell(owner),2,0,-1,reach);
        return SourceMeleeGeometry.Within(attacker,defender,bonus,(a,b,mask)=>
        {
            if(mask!=SourceMeleeGeometry.FlyingCollisionMask){throw new NotSupportedException("Unsupported player collision mask");}
            return !owner.Map.LineOfSight(new Point3D((a.X+1)/2,(a.Y+1)/2,owner.Z+14),new Point3D((b.X+1)/2,(b.Y+1)/2,target.Z+14));
        });
    }
}
