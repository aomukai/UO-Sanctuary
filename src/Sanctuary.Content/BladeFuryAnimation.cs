// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal static class BladeFuryAnimation
{
    internal const string Profile="source-sq23-19-action12-v1";
    internal static SourceAnimationState Create(int epoch256,int speed)
    {
        if(SkillMechanics.For(CombatAbility.BladeFury).Number("seqnum")!=23)
        {throw new NotSupportedException("Blade Fury sequence differs from pinned SQ23");}
        var points=new int[19];points[12]=1;
        return new(epoch256,speed,19*256,points,[]);
    }
}
