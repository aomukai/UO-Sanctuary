// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class ApocalypseArea
{
    internal static int Radius(int rank)
    {
        if(rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(rank));}
        var row=SkillMechanics.For(CombatAbility.Apocalypse);
        // Current3a6090 requires both immediate and aura flags to gate an
        // area pulse. This non-aura row bypasses it;localdelay is separate.
        if(row.Number("aurafilter")!=0x8783||row.Number("aura")!=0){throw new NotSupportedException("Apocalypse area filter/aura binding changed");}
        return checked((int)new SkillValues(row,k=>k==row.SourceKey?rank:0,_=>0,1).Eval("aurarangecalc"));
    }
    internal static Point3D Center(PlayerMobile owner,Point3D center)=>center.X==0||center.Y==0?owner.Location:center;
    internal static bool Recipient(PlayerMobile owner,BaseCreature target,Map map,Point3D center,int rank)
        =>SourceDamageAreas.Recipient(owner,target,map.MapID,center.X*2,center.Y*2,center.Z,Radius(rank),true,center);
    internal static int ReleasePrepared(PlayerMobile owner,Map map,Point3D center,ApocalypsePacket packet,int? pierceOverride=null)
    {
        ApocalypsePackets.Validate(packet);
        if(owner is not{Alive:true,Deleted:false}||owner.Serial.Value!=packet.Owner||map==null||map==Map.Internal||owner.Map!=map){return 0;}
        center=Center(owner,center);int radius=Radius(packet.Rank);var targets=new List<BaseCreature>();
        foreach(var target in map.GetMobilesInRange<BaseCreature>(center,(int)Math.Ceiling(SkillGeometry.Tiles(radius))))
        {
            if(!Recipient(owner,target,map,center,packet.Rank)){continue;}
            // Reuse existing recipient admission before any damage is applied.
            // The native map iterator is the established world projection;
            // D2 room-list ordering is not asserted for Britannia.
            TaintedFireBallMissiles.RequireOrdinaryRecipient(owner,target);targets.Add(target);
        }
        int applied=0;
        foreach(var target in targets)
        {
            if(Recipient(owner,target,map,center,packet.Rank)
                &&ApocalypsePackets.ReleasePrepared(owner,target,packet,pierceOverride:pierceOverride).Applied){applied++;}
        }
        return applied;
    }
}
