// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal readonly record struct SpiritWolfAiRequest(int Kind,int A=0,int B=0,int C=0,int D=0);
internal sealed record SpiritWolfAiContext(bool HasOwner,int RememberedOwner,bool LookupOwner,int OwnerId,int OwnerX,int OwnerY,
    bool Town,int Primary,int Potential,bool PrimaryVisible,bool PotentialVisible,int PotentialDistance,int OwnerDistance,
    int OwnerMode,bool Teleport,bool Combat,MonsterDifficulty Difficulty=MonsterDifficulty.Normal,
    int OwnerTargetDistance=0,int Velocity=5,int Run=10);
internal sealed record SpiritWolfAiServices(Action<SpiritWolfAiRequest> Request,Func<int,bool,int,int,bool> CommonMove,
    Func<int,bool,int,int,bool> PetMove,Func<int> Percentage);

// Legacy SpiritWolf policy with a valid monster/AI record. Selection, LOS,
// distances and the actual tactics/event execution remain explicit inputs and
// services. Existing individual target is retained; no whole-flock operation.
internal static class DruidSpiritWolfAi
{
    internal static int LegacyThink(SpiritWolfAiContext c,SpiritWolfAiServices s)
        =>Think(c,s,false);
    // Supplementary correction pinned at D2MOO7f6d773: the combat flag owns
    // the attack/chase branch. Current D2R execution still has separate limits.
    internal static int CorrectedThink(SpiritWolfAiContext c,SpiritWolfAiServices s)
        =>Think(c,s,true);
    private static int Think(SpiritWolfAiContext c,SpiritWolfAiServices s,bool corrected)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(s);
        if(c.RememberedOwner<0 || c.OwnerId<0 || c.Primary<0 || c.Potential<0 || c.PotentialDistance<0 || c.OwnerDistance<0 || c.OwnerTargetDistance<0
            || c.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
            || s.Request==null || s.CommonMove==null || s.PetMove==null || s.Percentage==null)
        {throw new ArgumentException("Unsupported Spirit Wolf AI context/services");}
        void A(int kind,int a=0,int b=0,int d=0,int e=0)=>s.Request(new(kind,a,b,d,e));
        int P(int index)=>NecromancerSummons.Source("spiritwolf","aip"+index,c.Difficulty);
        if(!c.HasOwner)
        {
            if(c.RememberedOwner!=0 && c.LookupOwner && c.Teleport)
            {A(2,8,292,c.OwnerX,c.OwnerY);A(3,6);A(4,8);}
            else{A(0,10);}
            return c.RememberedOwner;
        }
        if(c.Town)
        {
            if(!s.CommonMove(0,false,0,6)){A(0,33);}
            return c.OwnerId;
        }
        int target=c.Primary!=0 && c.PrimaryVisible?c.Primary
            :c.Potential!=0 && c.PotentialDistance<P(4) && !c.PotentialVisible?c.Potential:0;
        // Preserve the literal negated potential-visibility branch. Modern
        // fallback equivalence is uncertain; it is not silently inverted here.
        if(c.Teleport && c.OwnerDistance>50)
        {A(2,8,292,c.OwnerX,c.OwnerY);A(3,4);A(4,10);return c.OwnerId;}
        if(c.OwnerDistance>P(5) && s.PetMove(0,true,100,0)){return c.OwnerId;}
        if(c.OwnerDistance>P(3))
        {
            if(c.OwnerMode is 2 or 6 && s.PetMove(0,false,0,0)){return c.OwnerId;}
            if(c.OwnerMode==3 && s.PetMove(0,true,100,0)){return c.OwnerId;}
        }
        if(!s.CommonMove(target,c.Combat,1,6))
        {
            // The reconstruction tests the nonnull monstats pointer here,
            // not bCombat. With an out-of-melee target it still asks for A1;
            // the otherwise-present chase branch cannot run. This captures
            // that defect for comparison, not an approved live wolf policy.
            if(target!=0)
            {
                if(!corrected || c.Combat){A(6,4,target);A(4,P(1));}
                else if(c.OwnerTargetDistance>=P(4))
                {if(c.OwnerDistance<=10){A(0,15);}else{A(7,8,6);}}
                else
                {int velocity=c.Velocity<=0?100:Math.Min(checked(100*c.Run)/c.Velocity-100,100);A(8,0,velocity,0);A(9,target);}
            }
            else if(s.Percentage()<P(2)){A(10,10);}
            else{A(0,15);}
        }
        return c.OwnerId;
    }
}
