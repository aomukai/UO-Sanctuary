// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal readonly record struct TotemTarget(int Id,bool Combat);
internal sealed record TotemContext(bool HasOwner,int OwnerMode,MonsterDifficulty Difficulty=MonsterDifficulty.Normal,string Source="oaksage");
internal sealed record TotemServices(Action<int> Idle,Func<int,TotemTarget> Target,Func<int,int> Percentage,
    Func<int,int,bool,bool> Escape,Func<int> OwnerDistance,Func<bool> Teleport,
    Func<int,bool,int,int,bool> PetMove,Func<int,bool,int,int,bool> CommonMove);

// Complete outer Totem policy. Target/vision, RNG, room teleport and shared
// movement remain explicit services; no ordinary native follower AI is used.
internal static class DruidTotemAi
{
    internal static void Think(TotemContext c,TotemServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(s);
        if(c.OwnerMode is <0 or >19 || c.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
            || c.Source is not("oaksage" or "heartofwolverine") || s.Idle==null || s.Target==null || s.Percentage==null || s.Escape==null || s.OwnerDistance==null
            || s.Teleport==null || s.PetMove==null || s.CommonMove==null)
        {throw new ArgumentException("Unsupported Totem AI context/services");}
        int P(int index)=>NecromancerSummons.Source(c.Source,"aip"+index,c.Difficulty);
        bool Chance(int index)
        {
            int roll=s.Percentage(index-1);if(roll is <0 or >99){throw new ArgumentException("Totem percentage outside0..99");}
            return roll<P(index);
        }
        if(!c.HasOwner){s.Idle(10);return;}
        var target=s.Target(24);if(target.Id<0){throw new ArgumentException("Invalid Totem target");}
        if(target.Combat&&target.Id!=0&&Chance(1)&&s.Escape(target.Id,6,true)){return;}
        if(Chance(2)){target=new(0,false);}
        int distance=s.OwnerDistance();if(distance<0){throw new ArgumentException("Negative Totem owner distance");}
        if(distance>P(3)&&s.Teleport()){s.Idle(25);return;}
        if(distance>P(4))
        {
            if((c.OwnerMode is 2 or 6)&&s.PetMove(0,false,0,0)
                || c.OwnerMode==3&&s.PetMove(0,false,60,0)){return;}
        }
        if(s.CommonMove(target.Id,target.Combat,0,6)){return;}
        s.Idle(25);
    }
}
