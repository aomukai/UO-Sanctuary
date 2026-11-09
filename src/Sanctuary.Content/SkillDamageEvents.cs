// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
namespace Sanctuary;

public enum SkillDamageKind { Direct, Periodic, Return, SelfCost }
internal readonly record struct SkillDamageContact(Mobile Source,Mobile Target,int Damage256);

// Post-mitigation, pre-life-commit notification. Keep it separate from hit
// recovery: fractional contacts and lethal hits need the original FP256 value.
// Subscribers must explicitly suppress their own secondary packets when those
// packets must not dispatch another damage event (source ResultFlags32).
internal static class SkillDamageEvents
{
    internal static event Action<SkillDamageContact> BeforeDirectDamage;
    internal static bool Notify(Mobile source,Mobile target,int damage256,SkillDamageKind kind)
    {
        var notify=BeforeDirectDamage;
        if(notify!=null&&kind==SkillDamageKind.Direct&&!SkillHealth.IsSelfCost(source,target)
            &&CombatItemEvents.Allowed(source,target))
        {notify(new(source,target,damage256));return true;}
        return false;
    }
}
