// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;
internal sealed record GenericPetRatingRoutingServices(Func<byte,int,byte?> SkillFlags39,
    Func<GenericPetCombatCandidate,long,long,GenericPetCombatCandidate> RateTarget);
internal static class GenericPetRatingRouting
{
    // This ports the complete routing wrapper. Its per-target scoring callee is
    // mandatory and still separate; no generic replacement score is supplied.
    internal static GenericPetCombatCandidate Rate(byte selector,GenericPetParameters parameters,int frame100,
        GenericPetCombatSelection selection,GenericPetCombatCandidate row,GenericPetRatingRoutingServices s)
    {
        ArgumentNullException.ThrowIfNull(parameters);ArgumentNullException.ThrowIfNull(selection);ArgumentNullException.ThrowIfNull(s);
        GenericPetCombatCandidate Score(GenericPetCombatCandidate value)=>s.RateTarget(value,selection.Word40,selection.Word48);
        if(row.BaseModeFlag==0&&s.SkillFlags39(selector,row.SkillId) is{} flags&&(flags&1)!=0&&selection.Targets[3]!=0)
        {
            row=Score(row with{Target=selection.Targets[3]});if(row.Rating!=int.MinValue){return row;}
        }
        GenericPetCombatCandidate Selected(int target)=>target!=0?Score(row with{Target=target}):row;
        switch(row.Byte36)
        {
            case 1:case 2:return row.Target!=0?row with{Rating=int.MaxValue}:row;
            case 3:case 11:return Selected(selection.Targets[4]);
            case 4:case 12:return Selected(selection.Targets[0]);
            case 5:return Selected(selection.Targets[1]);
            case 6:case 8:case 9:case 10:return Score(row);
            case 7:
            {
                bool meleeFirst=parameters.MeleeBonus>parameters.RangedBonus;
                int first=selection.Targets[meleeFirst?0:1],second=selection.Targets[meleeFirst?1:0];
                if(first!=0)
                {var copy=Score(row with{Target=first});if(copy.Rating>int.MinValue){return copy;}}
                if(second!=0)
                {var copy=Score(row with{Target=second});if(copy.Rating>int.MinValue){return copy;}}
                return row;
            }
            case 13:
            {
                uint low=unchecked((uint)selection.Word40),high=unchecked((uint)((ulong)selection.Word40>>32));
                if(!(frame100>=50&&high<=3)&&selection.Targets[2]!=0&&low<4)
                {
                    var copy=Score(row with{Target=selection.Targets[2]});
                    if(copy.Rating>int.MinValue){return copy with{Rating=unchecked(copy.Rating+20)};}
                }
                return Selected(selection.Targets[0]);
            }
            default:return row;
        }
    }
}
