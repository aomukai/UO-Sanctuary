// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;

namespace Sanctuary;

public static class ChargedBolts
{
    private static readonly Point2D[] _directions = [new(1, 0), new(1, 1), new(0, 1), new(-1, 1), new(-1, 0), new(-1, -1), new(0, -1), new(1, -1)];
    public static int Frames => Math.Min(77, NecromancerSummons.Source("chargedbolt", "Range"));
    // D2COMMON_10175 is named FirstPointX in the reconstruction but returns
    // tTargetCoord.X. The initializer seeds from the aim, not the origin.
    internal static uint SeedSubtile(int aimX,int ordinal)=>unchecked((uint)(aimX+ordinal));
    public static uint Seed(Point3D aim,int ordinal)=>SeedSubtile(aim.X*2,ordinal);
    public static int Heading(int dx, int dy)
    {
        if (dx == 0 && dy == 0) { return 6; }
        if (Math.Abs(dx) >= 2 * Math.Abs(dy)) { return dx >= 0 ? 0 : 4; }
        if (Math.Abs(dy) >= 2 * Math.Abs(dx)) { return dy >= 0 ? 2 : 6; }
        return dx > 0 ? dy > 0 ? 1 : 7 : dy > 0 ? 3 : 5;
    }
    public static Point2D[] Path(uint seed, int direction, int frames)
    {
        // Each source waypoint is two subtiles apart: one tile in the staged geometry mapping.
        var result = new Point2D[(frames >> 1) + 1];
        ulong state = ((ulong)666 << 32) | seed; int x = 0, y = 0;
        for (int i = 1; i < result.Length; i++)
        {
            state = 0x6AC690C5UL * (state & uint.MaxValue) + (state >> 32);
            int sample = (int)(state & 31);
            int offset = sample == 31 ? 1 : sample % 3 - 1; // The source's 32-entry table deliberately omits the final zero.
            var step = _directions[(direction + offset) & 7]; x += step.X; y += step.Y;
            result[i] = new(x, y);
        }
        return result;
    }
    public static void Cast(PlayerMobile owner, Point3D aim, SkillValues values)
    {
        int direction = Heading(aim.X - owner.X, aim.Y - owner.Y);
        for (int i = 0; i < values.Eval("calc1"); i++)
        { new Bolt(owner, values, Path(Seed(aim,i), direction, Frames)).Start(); }
    }
    internal static (double X,double Y,int Next) Advance(Point2D[] path,int originX,int originY,double x,double y,int next,double remaining)
    {
        while(remaining>0 && next<path.Length)
        {
            double dx=originX+path[next].X-x,dy=originY+path[next].Y-y,distance=Math.Sqrt(dx*dx+dy*dy);
            if(distance<=remaining){x+=dx;y+=dy;remaining-=distance;next++;}
            else{x+=dx*remaining/distance;y+=dy*remaining/distance;remaining=0;}
        }
        return(x,y,next);
    }
    private sealed class Bolt(PlayerMobile owner, SkillValues values, Point2D[] path)
        : SkillField(owner, Frames, DiabloClass.Sorceress)
    {
        private double _x = owner.X, _y = owner.Y;
        private readonly int _originX = owner.X, _originY = owner.Y, _z = owner.Z;
        private int _next = 1;
        protected override void Step()
        {
            var before = new Point3D((int)Math.Round(_x), (int)Math.Round(_y), _z);
            (_x,_y,_next)=Advance(path,_originX,_originY,_x,_y,_next,SkillGeometry.Velocity(NecromancerSummons.Source("chargedbolt", "Vel")));
            var after = new Point3D((int)Math.Round(_x), (int)Math.Round(_y), _z);
            if (_next >= path.Length || after.X < 0 || after.Y < 0 || after.X >= Map.Width || after.Y >= Map.Height
                || !Map.LineOfSight(new Point3D(before.X, before.Y, _z + 12), new Point3D(after.X, after.Y, _z + 12))) { Stop(); return; }
            Mobile first = null; double nearest = double.MaxValue;
            foreach (var candidate in SkillCombat.Targets(Owner, after, 2))
            {
                if (CombatHitboxes.Intersect(before, after, candidate.Location, out double entry) && entry < nearest)
                { first = candidate; nearest = entry; }
            }
            Effects.SendLocationEffect(after, Map, 0x3818, 3, 10, 0x482, 0); // Placeholder charged bolt.
            if (first != null)
            {
                SorceressEffects.MissileHit(Owner, first, "chargedbolt");
                SorceressCombat.Impact(Owner, first, CombatAbility.ChargedBolt, values); Stop();
            }
        }
    }
}
