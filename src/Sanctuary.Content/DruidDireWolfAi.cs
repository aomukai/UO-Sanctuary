// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record DireWolfAiContext(SpiritWolfAiContext Common,bool CanRage,bool Raging,int PursuingCorpse=0,int CorpseGuid=0);
internal sealed record DireWolfCorpse(int Guid,int Distance,bool InMelee);
internal readonly record struct DireWolfAiMemory(int Owner,int PursuingCorpse,int CorpseGuid);
internal sealed record DireWolfAiServices(SpiritWolfAiServices Common,Func<DireWolfCorpse> FindCorpse);

// Complete Fenris outer policy. Reuse common service shapes, not Spirit Wolf's
// different visibility, follow, teleport or combat decisions. World placement,
// corpse eligibility/consumption and skill events remain service consumers.
internal static class DruidDireWolfAi
{
    internal static DireWolfAiMemory Think(DireWolfAiContext context,DireWolfAiServices services)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(services);
        var c=context.Common;var s=services.Common;
        if(c==null || s==null || c.RememberedOwner<0 || c.OwnerId<0 || c.Primary<0 || c.Potential<0
            || c.PotentialDistance<0 || c.OwnerDistance<0 || c.OwnerTargetDistance<0
            || c.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
            || context.PursuingCorpse is <0 or >1 || context.CorpseGuid<0
            || s.Request==null || s.CommonMove==null || s.PetMove==null || s.Percentage==null || services.FindCorpse==null)
        {throw new ArgumentException("Unsupported Dire Wolf AI context/services");}
        int owner=c.RememberedOwner,pursuit=context.PursuingCorpse,corpseGuid=context.CorpseGuid;
        void A(int kind,int a=0,int b=0,int d=0,int e=0)=>s.Request(new(kind,a,b,d,e));
        int P(int index)=>NecromancerSummons.Source("fenris","aip"+index,c.Difficulty);
        bool Chance(int parameter)
        {
            int roll=s.Percentage();if(roll is <0 or >99){throw new NotSupportedException("Dire Wolf chance supplier must return0..99");}
            return roll<P(parameter);
        }
        DireWolfAiMemory Memory()=>new(owner,pursuit,corpseGuid);
        if(!c.HasOwner)
        {
            if(owner!=0 && c.LookupOwner && c.Teleport)
            {A(2,8,292,c.OwnerX,c.OwnerY);A(3,6);A(4,8);}
            else{A(0,10);}
            return Memory();
        }
        owner=c.OwnerId;
        if(c.Town)
        {if(!s.CommonMove(0,false,0,6)){A(0,33);}return Memory();}
        int target=c.Primary!=0 && c.PrimaryVisible?c.Primary
            :c.Potential!=0 && c.PotentialDistance<P(4) && c.PotentialVisible?c.Potential:0;
        if(target!=0 && c.OwnerDistance>P(4) && c.OwnerTargetDistance>P(4)){target=0;}
        int velocity=c.Velocity<=0?100:Math.Min(checked(100*c.Run)/c.Velocity-100,100);
        if(c.Teleport && c.OwnerDistance>50)
        {A(2,8,292,c.OwnerX,c.OwnerY);A(3,2);A(4,10);return Memory();}
        if(c.OwnerDistance>P(5))
        {s.PetMove(0,true,100,0);return Memory();}
        if(c.OwnerDistance>P(4))
        {
            if(c.OwnerMode==3){s.PetMove(0,true,100,0);return Memory();}
            if(c.OwnerMode is 2 or 6){s.PetMove(0,false,0,0);return Memory();}
        }
        if(s.CommonMove(target,c.Combat,1,6)){return Memory();}
        // Preserve source evaluation order: the roll occurs before the
        // no-target/pursuit alternatives, even when either is already true.
        if(context.CanRage && !context.Raging && (Chance(3) || target==0 || pursuit!=0))
        {
            var corpse=services.FindCorpse();
            if(corpse!=null)
            {
                if(corpse.Guid<=0 || corpse.Distance<0){throw new NotSupportedException("Invalid Dire Wolf corpse supplier");}
                if(corpse.Distance<P(4)/2)
                {
                    if(corpse.InMelee)
                    {pursuit=0;A(11,4,314,corpse.Guid);return Memory();}
                    if(!c.Combat)
                    {A(9,corpse.Guid);pursuit=1;corpseGuid=corpse.Guid;return Memory();}
                    A(6,4,target);A(4,P(1));return Memory();
                }
            }
            // Failed search/range does not clear a remembered pursuit.
        }
        else{pursuit=0;}
        if(!c.Combat)
        {
            if(target!=0){A(8,0,velocity,0);A(9,target);}
            else if(Chance(2)){A(10,10);}
            else{A(0,15);}
        }
        else{A(6,4,target);A(4,P(1));}
        return Memory();
    }
}
