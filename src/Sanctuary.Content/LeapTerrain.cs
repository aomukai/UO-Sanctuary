// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Regions;
using Server.Engines.ConPVP;
using Server.Spells;

namespace Sanctuary;

internal static class LeapTerrain
{
    internal static bool Allowed(PlayerMobile owner,Map map,Point3D point)=>map!=null && map!=Map.Internal
        && point.X>=0 && point.Y>=0 && point.X<map.Width && point.Y<map.Height && point.Z is >=-128 and <=127
        && !Town(owner,map,point) && !SpellHelper.CheckMulti(point,map);
    private static bool Town(PlayerMobile owner,Map map,Point3D point)
    {
        if(owner.Map==map){return SpellHelper.IsTown(point,owner);}
        // Offline load internalizes the body; inspect the saved world map rather
        // than accidentally accepting a newly guarded/safe landing on Internal.
        var region=Region.Find(point,map);
        return region.GetRegion<SafeZone>()!=null || region.GetRegion<GuardedRegion>()?.IsDisabled()==false;
    }
    internal static bool Clear(PlayerMobile owner,Map map,Point3D point)=>Allowed(owner,map,point) && Teleportation.Space(map,point,owner);
    internal static bool Flying(Map map,Point3D origin,Point3D destination)
    {
        // Native NoShoot/closed-door/Z geometry is an explicit port adapter for
        // source door/missile barriers; it does not use walking/hostile contact.
        return map.LineOfSight(new Point3D(origin.X,origin.Y,origin.Z+12),new Point3D(destination.X,destination.Y,destination.Z+12));
    }
    internal static bool Ground(PlayerMobile owner,Map map,int x,int y,int preferredZ,out Point3D point)
    {
        point=new(x,y,preferredZ);if(Clear(owner,map,point)){return true;}
        if(map==null || map==Map.Internal || x<0 || y<0 || x>=map.Width || y>=map.Height){return false;}
        int z=map.GetAverageZ(x,y);point=new(x,y,z);if(z!=preferredZ && Clear(owner,map,point)){return true;}
        foreach(var tile in map.Tiles.GetStaticAndMultiTiles(x,y))
        {
            var data=TileData.ItemTable[tile.ID & TileData.MaxItemValue];
            if(data.Surface && !data.Impassable)
            {point=new(x,y,tile.Z+data.CalcHeight);if(Clear(owner,map,point)){return true;}}
        }
        return false;
    }
    internal static bool Destination(PlayerMobile owner,Point3D requested,int range,out Point3D destination)
    {
        var map=owner.Map;destination=default;
        if(!Allowed(owner,map,owner.Location) || !Allowed(owner,map,requested)){return false;}
        var origin=owner.Location;var points=new Dictionary<LeapCell,Point3D?>();
        Point3D? Resolve(LeapCell cell)
        {
            if(!points.TryGetValue(cell,out var value))
            {value=Ground(owner,map,cell.X/2,cell.Y/2,requested.Z,out var point)?point:null;points.Add(cell,value);}
            return value;
        }
        LeapCell? Free(LeapCell cell)=>LeapGeometry.Free(cell,c=>Resolve(c)!=null);
        if(!LeapGeometry.Find(new(origin.X*2,origin.Y*2),new(requested.X*2,requested.Y*2),range,Free,
            c=>Resolve(c)!=null,c=>Resolve(c) is{} point && Flying(map,origin,point),out var result)
            || Resolve(result) is not{} landing){return false;}
        destination=landing;return true;
    }
}
