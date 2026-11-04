// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class ReviveMeleeGeometry
{
    private static readonly Lazy<Dictionary<string,NpcMeleeGeometryProfile>> Profiles=new(()=>
    {
        var rows=BalanceCatalog.Read<NpcMeleeGeometryProfile>("revive-melee-geometry");
        if(rows.Length!=751||rows.Select(r=>r.Key).Distinct(StringComparer.Ordinal).Count()!=751||rows.Select(r=>r.BaseId).Distinct().Count()!=751
            ||rows.Any(r=>string.IsNullOrWhiteSpace(r.Key)||r.BaseId<0||r.Size is <0 or >255||r.RawRange is <0 or >255))
        {throw new InvalidDataException("Invalid pinned Revive melee geometry");}
        return rows.ToDictionary(r=>r.Key,StringComparer.Ordinal);
    });
    internal static NpcMeleeGeometryProfile Profile(string source)=>source!=null&&Profiles.Value.TryGetValue(source,out var row)
        ?row:throw new NotSupportedException("No pinned source melee geometry: "+source);
    internal static MeleeGeometryUnit Unit(BaseCreature actor)
    {
        if(actor is not{Deleted:false}){throw new NotSupportedException("No live melee geometry actor");}
        string source=ReviveNativeActivation.State(actor)?.Setup.LifePlan?.Source;
        if(source==null&&actor is SkeletonGuardian guardian){source=guardian.SourceMonsterId;}
        if(source==null&&actor is HydraBody hydra){source=hydra.SourceMonsterId;}
        if(source==null&&actor is EncounterCreature)
        {
            if(CreatureProfiles.For(actor).Traits.Size is not int size||size is <0 or >255)
            {throw new NotSupportedException("Native encounter has no explicit footprint");}
            return new(PetOwnerRoutes.Cell(actor),size,1,-1,HasRoom:actor.Map!=null&&actor.Map!=Map.Internal);
        }
        if(source==null)
        {
            var definition=CreatureProfiles.Definition(actor);source=definition.Reference;
            var profile=CreatureProfiles.For(actor);var mapped=Profile(source);
            if(!profile.SourceStats.TryGetValue("hcIdx",out string index)||!int.TryParse(index,out int id)||id!=mapped.BaseId)
            {throw new NotSupportedException("Native melee actor source identity is not corroborated");}
        }
        var row=Profile(source);
        if(row.RawRange==255){throw new NotSupportedException("Original composite weapon-class range is not captured");}
        return new(PetOwnerRoutes.Cell(actor),row.Size,1,row.BaseId,row.RawRange,HasRoom:actor.Map!=null&&actor.Map!=Map.Internal);
    }
    internal static bool Within(BaseCreature source,BaseCreature target,int bonus)
    {
        // These are explicit Britannia facet/height/ray services, not D2 room grids.
        if(source.Map==null||source.Map==Map.Internal||source.Map!=target.Map||Math.Abs(source.Z-target.Z)>=16){return false;}
        return SourceMeleeGeometry.Within(Unit(source),Unit(target),bonus,(a,b,mask)=>
        {
            if(mask!=SourceMeleeGeometry.FlyingCollisionMask){throw new NotSupportedException("Unsupported source collision mask");}
            return !source.Map.LineOfSight(new Point3D((a.X+1)/2,(a.Y+1)/2,source.Z+14),new Point3D((b.X+1)/2,(b.Y+1)/2,target.Z+14));
        });
    }
    internal static int Result(BaseCreature source,PlayerMobile owner,BaseCreature target)
    {
        if(!ReviveEquipmentEvents.Handles(source)||source.ControlMaster!=owner||owner is not{Alive:true,Deleted:false})
        {throw new NotSupportedException("Original melee result needs admitted equipment and a living owner");}
        _=Unit(source);_=Unit(target);
        return NpcMeleeResult.Resolve(source,target,bonus=>Within(source,target,bonus),()=>ClassCombat.IsHostile(owner,target),n=>NpcEquipmentRandom.Limited(source,n));
    }
    internal static IronGolemContact.Prepared Prepare(BaseCreature source,PlayerMobile owner,BaseCreature target)
    {
        _=Unit(source);_=Unit(target);
        var rules=IronGolemContact.Require(source,owner,target);
        return IronGolemContact.Prepare(source,target,rules,()=>Result(source,owner,target));
    }
    internal static MonsterContactResult Release(BaseCreature source,PlayerMobile owner,BaseCreature target,IronGolemContact.Prepared prepared,DateTime at)
        =>IronGolemContact.Release(source,owner,target,prepared,bonus=>Within(source,target,bonus),at);
}
