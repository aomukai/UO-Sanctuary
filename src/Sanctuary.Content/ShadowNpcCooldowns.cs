// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Explicit approved Sanctuary rule:intrinsic original Shadow Shock Web and
// Blade Sentinel add no cooldown beyond their own action and AI cadence.
// Current localdelay fields are retained and verified,not silently discarded.
internal static class ShadowNpcCooldowns
{
    internal static bool Approved(int skill)=>skill is 256 or 257;
    internal static SkillExecutionRecord Record(int skill)
    {
        if(!Approved(skill)){throw new NotSupportedException("No approved intrinsic Shadow localdelay policy");}
        var r=ItemSkillExecution.Skill(skill);
        if(r.Number("localdelay")!=(skill==256?15:25)||r.HasDelay("delay")||r.HasDelay("globaldelay")||r.HasDelay("perdelay"))
        {throw new NotSupportedException("Approved Shadow localdelay row changed");}
        return r.LegacyRecord(false,1);
    }
}
