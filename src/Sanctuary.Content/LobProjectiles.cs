// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

// Shared S2 action and source-coordinate lob adapter. Persistent skill records
// stay with their controllers; Fire Blast's saved V0 shape is unchanged.
internal static class LobProjectiles
{
    internal static int Velocity=>(NecromancerSummons.Source("bomb in air","Vel")<<8)*75/100;
    internal static ActionTiming Timing(int advance)
    {
        int frames=Math.Max(1,(8*256+advance-1)/advance-1);
        return new(frames,Math.Clamp((4*256+advance-1)/advance,1,frames));
    }
    internal static ActionTiming Timing(PlayerMobile owner)
    {
        var weapon=CombatTiming.Weapon(CombatTiming.Equipped(owner));
        if(WeaponHands.Pair(owner) is{} pair){weapon=weapon with{Modifier=(CombatTiming.Weapon(pair.Primary).Modifier+CombatTiming.Weapon(pair.Secondary).Modifier)/2};}
        return Timing(CombatTiming.FrameAdvance(weapon,CombatTiming.Ias(owner),GearEffects.AttackSlow(owner),
            CombatTiming.SkillAttackRate(owner),baseSpeed:128));
    }
    internal static bool Valid(PlayerMobile owner,Point3D point)=>owner is{Alive:true,Deleted:false}
        && owner.Map!=null && owner.Map!=Map.Internal && point.X>0 && point.Y>0 && point.X<owner.Map.Width && point.Y<owner.Map.Height
        && owner.InRange(point,CombatProtocol.TeethRange) && Math.Abs(owner.Z-point.Z)<16
        && !SpellHelper.IsTown(owner.Location,owner) && !SpellHelper.IsTown(point,owner);
    internal static bool Aim(PlayerMobile owner,Serial selected,ref Point3D point)
    {
        if(selected!=Serial.Zero)
        {if(World.FindMobile(selected) is not BaseCreature target || !ClassCombat.IsHostile(owner,target)){return false;}point=target.Location;}
        return Valid(owner,point);
    }
    internal static int Lifetime(int dx,int dy)=>Lifetime(dx,dy,Velocity);
    internal static int Lifetime(int dx,int dy,int velocity)
    {
        if(velocity<=0){throw new ArgumentOutOfRangeException(nameof(velocity));}
        int x=Math.Abs(dx),y=Math.Abs(dy),distance=Math.Max(1,Math.Max(x,y)+Math.Min(x,y)/2);
        return distance*65536/(16*velocity);
    }
    internal static bool Barrier(Map map,Point3D point)
    {
        // UO tile metadata projects the source WALL/MISSILE_BARRIER mask.
        // Adjacent LOS alone permits a segment ending inside a wall tile.
        bool Blocks(int id,int z)
        {
            var data=TileData.ItemTable[id&TileData.MaxItemValue];
            return (data.Flags&(TileFlag.Wall|TileFlag.NoShoot))!=0 && z<point.Z+16 && z+Math.Max(1,(int)data.Height)>point.Z+8;
        }
        foreach(var tile in map.Tiles.GetStaticAndMultiTiles(point.X,point.Y)){if(Blocks(tile.ID,tile.Z)){return true;}}
        foreach(var item in map.GetItemsInRange<Item>(point,0)){if(item.Visible && Blocks(item.ItemID,item.Z)){return true;}}
        return false;
    }
    internal static (double X,double Y,bool Landed) Advance(Map map,double startX,double startY,int z,double vx,double vy,int remaining)
    {
        double x=startX+vx,y=startY+vy;bool barrier=false;var previous=new Point3D((int)Math.Round(startX),(int)Math.Round(startY),z);
        var trace=SourceMissileSteps.Advance(startX,startY,vx,vy,null);
        foreach(var cell in trace?.Cells??[])
        {
            var point=new Point3D((int)Math.Round(cell.X/2.0),(int)Math.Round(cell.Y/2.0),z);
            if(point.X<0 || point.Y<0 || point.X>=map.Width || point.Y>=map.Height
                || Barrier(map,point) || !map.LineOfSight(new Point3D(previous.X,previous.Y,z+12),new Point3D(point.X,point.Y,z+12)))
            {barrier=true;break;}
            previous=point;
        }
        return barrier?(previous.X,previous.Y,true):(x,y,remaining<=1);
    }
    internal static bool CanLand(PlayerMobile owner,double x,double y)
    {
        int dx=Math.Abs(owner.X*2-(int)Math.Floor(x*2+.5)),dy=Math.Abs(owner.Y*2-(int)Math.Floor(y*2+.5));
        return Math.Max(dx,dy)+Math.Min(dx,dy)/2<=100;
    }
}
