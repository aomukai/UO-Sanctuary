// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
using Server.Spells.Fifth;
using Server.Spells.Fourth;
using Server.Spells.Sixth;
namespace Sanctuary;

// Explicit native-world candidate,not recovered D2 room/height data. It is
// reachable only with its supplied size2/flying-mask path;no learned host is
// admitted here. Keep source movement masks separate from placement masks.
internal static class DefilerFlyingWorld
{
    private const TileFlag Barriers=TileFlag.Wall|TileFlag.Door|TileFlag.NoShoot|TileFlag.Roof;
    internal static bool Profile(SkeletonGuardian body,GenericPetCommitPath path)=>body is WarlockDefilerBody{Prepared:not null}
        &&path is{Size5c:2,Mask60:0x100,Mask64:0x1804};
    internal static GenericPetBirthPath BirthPath(SkeletonGuardian body)
    {
        if(body is not WarlockDefilerBody{Prepared:not null}||body.Map==null||body.Map==Map.Internal)
        {throw new NotSupportedException("Flying candidate birth requires its original world Defiler");}
        _=DefilerFlyingMovement.Species;
        var p=new GenericPetCommitPath{X=((uint)(body.X*2)<<16)|32768,Y=((uint)(body.Y*2)<<16)|32768,Flags=1,
            Room=GenericPetNativePlacement.RoomHandle(body.Map,body.X*2,body.Y*2),Size5c=2,Mask60=0x100,Mask64=0x1804,Actor=new(checked((int)body.Serial.Value),1)};
        return new(new(new(744,0),true,1,744,2,2,0),p,1,1,TimedEffects.Now);
    }
    internal static WarlockDefilerBody CreateCandidate(PlayerMobile owner,Point3D point,DateTime at)
        =>WarlockDefilerBody.CreatePrepared(owner,point,at,new(1,BirthPath));
    private static bool Bounds(Map map,int x,int y)=>map!=null&&map!=Map.Internal&&x>0&&y>0&&x<map.Width*2&&y<map.Height*2;
    private static bool Intersects(int bottom,int height,Point3D p)=>bottom<p.Z+16&&bottom+Math.Max(0,height)>p.Z;
    private static Point3D Position(WarlockDefilerBody body,int x,int y)
    {
        var p=new Point3D((x+1)/2,(y+1)/2,body.Z);var land=body.Map.Tiles.GetLandTile(p.X,p.Y);
        var flags=TileData.LandTable[land.ID&TileData.MaxLandValue].Flags;
        int z=body.Map.GetAverageZ(p.X,p.Y);
        // Follow nearby dry ground. Water/ignored terrain and larger gaps
        // retain altitude;an upward cliff still blocks the solid volume.
        return !land.Ignored&&(flags&TileFlag.Wet)==0&&Math.Abs(z-body.Z)<16?new(p.X,p.Y,z):p;
    }
    internal static int RawCollision(WarlockDefilerBody body,int x,int y,bool ignoreMobiles=false)
    {
        var map=body?.Map;if(!Bounds(map,x,y)||body.ControlMaster is not PlayerMobile owner||owner.Map!=map){return 4;}
        var p=Position(body,x,y);
        if(SpellHelper.IsTown(p,owner)){return 4;}
        var land=map.Tiles.GetLandTile(p.X,p.Y);var flags=TileData.LandTable[land.ID&TileData.MaxLandValue].Flags;
        // A land tile describes a surface,not an infinitely tall wall. Its
        // walk-only flags cannot block a flyer above that actual surface;
        // vertical static/multi/item barriers are checked separately below.
        if(!land.Ignored&&(flags&TileFlag.Wet)==0&&map.GetAverageZ(p.X,p.Y)>p.Z){return 4;}
        foreach(var tile in map.Tiles.GetStaticAndMultiTiles(p.X,p.Y))
        {
            var data=TileData.ItemTable[tile.ID&TileData.MaxItemValue];
            if((data.Flags&Barriers)!=0&&Intersects(tile.Z,data.CalcHeight,p)){return (data.Flags&TileFlag.Door)!=0?0x800:4;}
        }
        foreach(var item in map.GetItemsInRange<Item>(p,0))
        {
            if(item is BaseDoor{Open:true}){continue;}
            var data=item.ItemData;
            if(Intersects(item.Z,data.CalcHeight,p)&&(item is BaseDoor||(data.Flags&Barriers)!=0
                ||item.BlocksFit&&item is not(FireFieldItem or PoisonField or ParalyzeField)))
            {return item is BaseDoor||(data.Flags&TileFlag.Door)!=0?0x800:4;}
        }
        return !ignoreMobiles&&!Teleportation.MobileSpace(map,p,body,true)?0x1000:0;
    }
    internal static int Collision(WarlockDefilerBody body,int x,int y)
    {
        int mask=0;
        // Size2 uses the existing source size box (center plus one cell on
        // each side). Native tiles are sampled through those source cells.
        for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){mask|=RawCollision(body,x+dx,y+dy);}
        return mask;
    }
    internal static bool Point(WarlockDefilerBody body,Point2D cell,out Point3D point,bool placement=false)
    {
        point=default;if(!Bounds(body?.Map,cell.X,cell.Y)){return false;}
        if((placement?RawCollision(body,cell.X,cell.Y,true):Collision(body,cell.X,cell.Y))!=0){return false;}
        point=Position(body,cell.X,cell.Y);return true;
    }
}
