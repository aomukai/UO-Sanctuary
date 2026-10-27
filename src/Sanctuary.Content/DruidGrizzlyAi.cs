// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record GrizzlyAiServices(Action<SpiritWolfAiRequest> Request,
    Func<int,bool,int,int,bool> PetMove,Func<int,int> Percentage);

// Complete source AI112 outer policy. Shared context/request shapes retain
// supplied target/visibility/full-size distance services. Movement, sequence
// execution, world scheduling and persistence are separate consumers.
internal static class DruidGrizzlyAi
{
    internal static int Think(SpiritWolfAiContext c,GrizzlyAiServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(s);
        if(c.RememberedOwner<0||c.OwnerId<0||c.Primary<0||c.Potential<0||c.PotentialDistance<0||c.OwnerDistance<0
            ||c.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
            ||s.Request==null||s.PetMove==null||s.Percentage==null)
        {throw new ArgumentException("Unsupported Grizzly AI context/services");}
        void A(int kind,int a=0,int b=0,int d=0,int e=0)=>s.Request(new(kind,a,b,d,e));
        int P(int index)=>NecromancerSummons.Source("druidbear","aip"+index,c.Difficulty);
        bool Chance(int index)
        {
            int roll=s.Percentage(index-1);
            if(roll is <0 or >99){throw new NotSupportedException("Grizzly chance supplier must return0..99");}
            return roll<P(index);
        }
        if(!c.HasOwner)
        {
            if(c.RememberedOwner!=0&&c.LookupOwner){s.PetMove(3,false,0,0);}
            else{A(0,10);}
            return c.RememberedOwner;
        }
        if(c.OwnerDistance>50){s.PetMove(3,false,0,0);return c.OwnerId;}
        int velocity=c.Velocity<=0?100:Math.Min(checked(100*c.Run)/c.Velocity-100,100);
        if(c.OwnerDistance>28){s.PetMove(0,false,100,0);return c.OwnerId;}
        if(c.OwnerDistance>18)
        {
            if(c.OwnerMode is 2 or 6&&s.PetMove(0,false,0,0)){return c.OwnerId;}
            if(c.OwnerMode==3&&s.PetMove(0,false,100,0)){return c.OwnerId;}
        }
        int target=c.Primary!=0&&c.PrimaryVisible?c.Primary
            :c.Potential!=0&&c.PotentialDistance<28&&c.PotentialVisible?c.Potential:0;
        if(c.Combat&&target!=0)
        {
            if(Chance(3)){A(15,326,target);}
            else{A(6,4,target);A(4,P(1));}
        }
        else if(target==0)
        {
            if(c.OwnerDistance<=16){A(0,15);}
            else{s.PetMove(0,false,0,0);}
        }
        else
        {
            if(Chance(2)){A(8,0,velocity,40);}
            // Source WalkToTargetUnit, distinct from the wolves' Run request.
            A(14,target);
        }
        return c.OwnerId;
    }
}
