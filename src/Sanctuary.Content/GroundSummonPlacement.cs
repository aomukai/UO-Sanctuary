// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

// Shared projection of the source's failed-exact/spawn-parameter4 retry.
// The two-native-tile perimeter and height lookup are native adapters, not
// recovered D2 room, footprint, random-start or candidate-order semantics.
internal static class GroundSummonPlacement
{
    internal static bool Find(Map map,Point3D aim,Func<Point3D,bool> legal,out Point3D point)
    {
        if(legal(aim)){point=aim;return true;}
        for(int radius=1;radius<=2;radius++)
        for(int x=-radius;x<=radius;x++)
        for(int y=-radius;y<=radius;y++)
        {
            if(Math.Abs(x)!=radius&&Math.Abs(y)!=radius){continue;}
            var trial=new Point3D(aim.X+x,aim.Y+y,aim.Z);
            if(trial.X<=0||trial.Y<=0||trial.X>=map.Width||trial.Y>=map.Height){continue;}
            if(legal(trial)){point=trial;return true;}
            int z=map.GetAverageZ(trial.X,trial.Y);
            if(Math.Abs(z-aim.Z)<16&&legal(new(trial.X,trial.Y,z))){point=new(trial.X,trial.Y,z);return true;}
        }
        point=default;return false;
    }
}
