// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

// Native accepted-step adapter used by Charge's separate phase/cadence processor.
internal static class ChargeMotion
{
    private static PlayerMobile _stepping;
    internal static bool Active(Mobile actor)=>actor!=null && ReferenceEquals(actor,_stepping);

    internal static bool TryStep(PlayerMobile owner,Direction direction,Func<Point3D,bool> destinationAllowed=null,bool ignoreHostiles=false)
    {
        if(owner is not{Alive:true,Deleted:false} || !ClassCombat.Enabled(owner) || owner.Map==null || owner.Map==Map.Internal
            || owner.Frozen || owner.Paralyzed || HitRecovery.Active(owner,Core.TickCount)
            || PaladinZeal.Locked(owner,Core.TickCount)){return false;}
        direction&=Direction.Mask;
        var origin=owner.Location;var map=owner.Map;
        int x=origin.X,y=origin.Y;
        switch(direction)
        {
            case Direction.North:y--;break;case Direction.Right:x++;y--;break;
            case Direction.East:x++;break;case Direction.Down:x++;y++;break;
            case Direction.South:y++;break;case Direction.Left:x--;y++;break;
            case Direction.West:x--;break;case Direction.Up:x--;y--;break;
        }
        if(x<0 || y<0 || x>=map.Width || y>=map.Height || !owner.CheckMovement(direction,out int z)
            || destinationAllowed?.Invoke(new(x,y,z))==false){return false;}
        foreach(var actor in map.GetMobilesInRange<Mobile>(new Point3D(x,y,z),0))
        {
            // Native walking can push a hostile at full stamina. A charge must stop at
            // contact; retain Sanctuary's approved pass-through for cooperative allies.
            if(actor!=owner && actor is{Alive:true,Deleted:false} && actor.Z<z+16 && actor.Z+16>z
                && !ClassCombat.Friendly(owner,actor) && !ignoreHostiles){return false;}
        }

        var previous=_stepping;var network=owner.NetState;
        _stepping=owner;
        try
        {
            // A server-owned step is not a predicted client request. Clear stale input before
            // moving, then clear Mobile.Move's normal walking debt and reconcile the owner.
            network?.ResetMovementState();
            owner.Direction=direction;
            bool moved=owner.Move(direction) && owner.Map==map && owner.X==x && owner.Y==y;
            network?.ResetMovementState();
            if(network!=null && !owner.Deleted && ReferenceEquals(owner.NetState,network)){network.SendMobileUpdate(owner);}
            return moved;
        }
        finally{_stepping=previous;}
    }
}
