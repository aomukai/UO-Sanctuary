// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using Server;

namespace Sanctuary;

// A hidden mobile supplies the engine's existing player-cell collision and
// CanFit occupancy without changing terrain, missiles or Server map code.
public sealed class SkillLandingReservation:Mobile
{
    internal static readonly HashSet<SkillLandingReservation> Live=new();
    public uint Owner{get;private set;}
    public SkillLandingReservation(uint owner)
    {
        Owner=owner;Name="[internal skill landing reservation]";Body=0;Hidden=true;Blessed=true;CantWalk=true;Live.Add(this);
    }
    public SkillLandingReservation(Serial serial):base(serial){Live.Add(this);}
    public override bool CanBeDamaged()=>false;
    public override bool CanRegenHits=>false;
    public override bool CanRegenMana=>false;
    public override bool CanRegenStam=>false;
    public override bool OnMoveOver(Mobile mobile)=>mobile?.Serial.Value==Owner;
    public override void OnDelete(){Live.Remove(this);base.OnDelete();}
    public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);writer.Write(Owner);}
    public override void Deserialize(IGenericReader reader)
    {
        base.Deserialize(reader);if(reader.ReadInt()!=0){throw new System.IO.InvalidDataException("Unknown landing reservation version");}
        Owner=reader.ReadUInt();Hidden=true;Blessed=true;CantWalk=true;
    }
}

public static class LandingReservations
{
    internal static bool OwnedBy(Mobile obstacle,Mobile traveller)=>obstacle is SkillLandingReservation reservation
        && traveller!=null && reservation.Owner==traveller.Serial.Value;
    internal static bool PassThrough(Mobile first,Mobile second)=>OwnedBy(first,second) || OwnedBy(second,first);
    internal static SkillLandingReservation Create(Mobile owner,Map map,Point3D point)
    {var marker=new SkillLandingReservation(owner.Serial.Value);marker.MoveToWorld(point,map);return marker;}
    internal static void Prune(System.Func<SkillLandingReservation,bool> active)
    {foreach(var marker in SkillLandingReservation.Live.ToArray()){if(!active(marker)){marker.Delete();}}}
}
