// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace Sanctuary;

public sealed record SkillStateRule(string State,bool Curse,bool Curable);
public static class PaladinCleansing
{
    private static readonly Dictionary<string,SkillStateRule> _states=new(StringComparer.OrdinalIgnoreCase);
    public static void Load()
    {
        _states.Clear();foreach(var row in BalanceCatalog.Read<SkillStateRule>("class-states")) { _states.Add(row.State,row); }
    }
    public static bool Curable(string state)=>_states.TryGetValue(state,out var rule) && rule.Curse && rule.Curable;
    public static DateTime Shorten(DateTime until,DateTime now,int remainingPercent)
    {
        long frames=Math.Max(0,(until-now).Ticks/TimeSpan.TicksPerMillisecond/40);
        return now.AddMilliseconds(frames*Math.Clamp(remainingPercent,0,100)/100*40);
    }
    public static bool Apply(Mobile target,int remainingPercent,DateTime now)
    {
        bool changed=TimedEffects.CleansePoison(target,remainingPercent,now);
        if (NecromancerEffects.Curse(target) is {} curse
            && Curable(SkillMechanics.For(curse.Ability).Text("auratargetstate")))
        {
            var state=NecromancerEffects.Profiles.Find(target);
            NecromancerEffects.Profiles.Store(target,state with { Curse=curse with { UntilUtc=Shorten(curse.UntilUtc,now,remainingPercent) } });
            changed=true;
        }
        if (Curable("amplifydamage") && EliteAuras.Curses.Expires(target.Serial)>now)
        {
            EliteAuras.Curses.Apply(target.Serial,Shorten(EliteAuras.Curses.Expires(target.Serial),now,remainingPercent));
            EliteAuras.Refresh(target);changed=true;
        }
        return changed;
    }
}
