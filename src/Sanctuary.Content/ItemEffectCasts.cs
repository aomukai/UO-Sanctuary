// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal sealed record ItemEffectSkill(int Effect, int Target, bool CheckStart);
internal readonly record struct ItemEffectResult(int UnitType, int UnitId, int X, int Y);

// The skill executor is mandatory. Success here is the executor's result,
// unlike the outer proc callback's accepted-attempt result.
internal abstract class ItemEffectServices<T> where T : class
{
    internal abstract T Source { get; }
    internal abstract bool HasGame { get; }
    internal abstract bool Uninterruptible { get; }
    internal abstract T CurrentTarget { get; }
    internal abstract (int X, int Y) FirstPathPoint { get; }
    internal abstract bool ServerDo { get; set; }
    internal abstract ItemEffectSkill Skill(int id);
    internal abstract (int Type, int Id) Identity(T unit);
    internal abstract void SetTarget(T target);
    internal abstract void SetPosition(int x, int y);
    internal abstract bool FindPosition(int skill, int level);
    internal abstract T FindCorpse(T target, int skill, int level);
    internal abstract T LastAttacker();
    internal abstract int Start(int skill, int level, int item);
    internal abstract int Execute(int skill, int level, int mode, int item, int flag);
    internal abstract void NotifyTarget(int skill, int level, int type, int id, int flag);
    internal abstract void NotifyPosition(int skill, int level, int x, int y, int flag);
}

internal static class ItemEffectCasts
{
    internal static int Handle<T>(ItemEffectServices<T> s, int skill, int level, T target,
        int x, int y, int flag, out ItemEffectResult output) where T : class
    {
        output = default;
        var record = s.Skill(skill);
        if (record == null || record.Effect == 0) { return 0; }
        var original = s.CurrentTarget;
        var point = s.FirstPathPoint;
        bool serverDo = s.ServerDo;
        output = new(6, -1, 0, 0);
        try
        {
            if (target != null) { s.SetTarget(target); }
            else { s.SetPosition(x, y); }
            switch (record.Target)
            {
                case 1:
                    s.SetTarget(s.Source);
                    var caster = s.Identity(s.Source);
                    output = output with { UnitType = caster.Type, UnitId = caster.Id };
                    break;
                case 2:
                    if (!s.FindPosition(skill, level)) { return 0; }
                    var position = s.FirstPathPoint;
                    output = output with { X = position.X, Y = position.Y };
                    break;
                case 3:
                    var corpse = s.FindCorpse(s.CurrentTarget, skill, level);
                    if (corpse == null) { return 0; }
                    s.SetTarget(corpse);
                    var identity = s.Identity(corpse);
                    output = output with { UnitType = identity.Type, UnitId = identity.Id };
                    break;
                case 4:
                    if (s.CurrentTarget == null)
                    {
                        var attacker = s.LastAttacker();
                        s.SetTarget(attacker);
                        if (attacker != null)
                        {
                            var last = s.Identity(attacker);
                            output = output with { UnitType = last.Type, UnitId = last.Id };
                        }
                    }
                    break;
            }
            if (record.CheckStart && s.Start(skill, level, 1) == 0) { return 0; }
            return s.Execute(skill, level, 0, 1, flag);
        }
        finally
        {
            if (original != null) { s.SetTarget(original); }
            else { s.SetPosition(point.X, point.Y); }
            s.ServerDo = serverDo;
        }
    }

    internal static int Target<T>(ItemEffectServices<T> s, int skill, int level, T target, int flag) where T : class
    {
        ArgumentNullException.ThrowIfNull(s);
        if (s.Source == null || target == null || !s.HasGame || s.Uninterruptible) { return 0; }
        if (Handle(s, skill, level, target, 0, 0, flag, out var output) == 0) { return 0; }
        var identity = output.UnitType == 6 ? s.Identity(target) : (output.UnitType, output.UnitId);
        s.NotifyTarget(skill, level, identity.Item1, identity.Item2, flag);
        return 1;
    }

    internal static int Position<T>(ItemEffectServices<T> s, int skill, int level, int x, int y, int flag) where T : class
    {
        ArgumentNullException.ThrowIfNull(s);
        if (s.Source == null || !s.HasGame || s.Uninterruptible) { return 0; }
        if (Handle(s, skill, level, null, x, y, flag, out var output) == 0) { return 0; }
        s.NotifyPosition(skill, level, output.X != 0 ? output.X : x, output.X != 0 ? output.Y : y, flag);
        return 1;
    }
}
