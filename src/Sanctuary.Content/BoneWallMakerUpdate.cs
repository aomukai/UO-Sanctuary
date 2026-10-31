// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record BoneWallMakerServices(Func<bool> Owner,Func<bool> Changed,Func<bool> Skill,Func<bool> Summon,
    Func<bool> Root,Func<bool> Spawn,Action<int> WriteBudget,Func<int> Update);

// Maker13 precedes the shared missile update. Allocation is exact; failure
// leaves the success budget intact and still allows movement/expiry/contact.
internal static class BoneWallMakerUpdate
{
    internal static int Run(int budget,BoneWallMakerServices s)
    {
        if(!s.Owner()||budget==0){return 2;}
        if(!s.Changed()){return s.Update();}
        if(!s.Owner()||!s.Skill()){return 2;}
        if(!s.Summon()){return 0;}
        if(s.Root()&&s.Spawn()){s.WriteBudget(budget-1);}
        return s.Update();
    }
}
