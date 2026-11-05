// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal sealed record ExecutionSkill(int Id, uint Flags);
internal sealed record SkillExecutionRecord(int Do, int ItemEffect, int Missile, int Start,
    bool InTown, bool UseManaOnDo, bool Lob, bool DecrementQuantity, int Animation, int DelayFormula);

internal abstract class SkillExecutionServices
{
    internal abstract bool HasGame { get; }
    internal abstract bool HasSource { get; }
    internal abstract int UnitType { get; }
    internal abstract SkillExecutionRecord Record(int skill);
    internal abstract ExecutionSkill UsedSkill();
    internal abstract bool HasRoom { get; }
    internal abstract bool RoomInTown { get; }
    internal abstract void ClearUsedSkill();
    internal abstract void TownNeutral();
    internal abstract ExecutionSkill HighestSkill(int skill);
    internal abstract int Rank(ExecutionSkill skill, bool bonuses);
    internal abstract int DoCount { get; }
    internal abstract bool ManaAllowed();
    internal abstract bool HasDo(int function);
    internal abstract int Execute(int function, int skill, int level);
    internal abstract int MissileCount { get; }
    internal abstract void SetServerDo();
    internal abstract (int X, int Y)? TargetPoint { get; }
    internal abstract (int X, int Y) FirstPathPoint { get; }
    internal abstract (int X, int Y) SourcePoint { get; }
    internal abstract void Launch(bool lob, int missile, int skill, int level, int x, int y, int targetX, int targetY, int flags);
    internal abstract bool HasStart(int function);
    internal abstract void ConsumeMana(int skill, int level);
    internal abstract void ConsumeQuantity();
    internal abstract int Delay(int formula, int skill, int level);
    internal abstract uint SequenceFrame { get; }
    internal abstract void SetDelay(int frames);
}

// Complete older common executor. Ordinary, item and outgoing-item flags stay
// independent; nested do/missile/resource services must actually be supplied.
internal static class SkillExecution
{
    internal static int Run(SkillExecutionServices s, int skill, int level, bool finish, bool item, bool outgoing)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!s.HasGame || !s.HasSource) { return 0; }
        var record = s.Record(skill);
        if (record == null) { return 0; }
        bool living = s.UnitType is 0 or 1;
        if (living)
        {
            var used = s.UsedSkill();
            if (used != null && (used.Flags & 1) == 0 && !record.InTown && (!s.HasRoom || s.RoomInTown))
            {
                s.ClearUsedSkill();
                if (s.UnitType == 0) { s.TownNeutral(); }
                return 0;
            }
            if (!item && (used == null || used.Id != skill || s.Rank(used, true) <= 0))
            {
                var known = s.HighestSkill(skill);
                if (known == null || s.Rank(known, true) <= 0) { return 0; }
            }
        }
        int function = record.Do;
        if (function < 0 || function >= s.DoCount || living && record.UseManaOnDo && !s.ManaAllowed()) { return 0; }
        if (outgoing && item && record.ItemEffect > 1) { function = record.ItemEffect; }
        // The C++ table access is undefined for a corrupt override. Never
        // convert an unmapped/current extension into a successful no-op.
        if (function < 0 || function >= s.DoCount) { throw new NotSupportedException("Unmapped item-effect do-function override"); }
        int result = s.HasDo(function) ? s.Execute(function, skill, level) : 0;
        if (record.Missile >= 0 && record.Missile < s.MissileCount)
        {
            s.SetServerDo();
            int x = 0, y = 0, targetX = 0, targetY = 0;
            if (item && outgoing)
            {
                var point = s.TargetPoint ?? s.FirstPathPoint;
                x = point.X; y = point.Y;
                if (x != 0 && y != 0)
                {
                    var source = s.SourcePoint;
                    targetX = x; targetY = y;
                    x = unchecked(x - source.X); y = unchecked(y - source.Y);
                    targetX = unchecked(targetX + x); targetY = unchecked(targetY + y);
                }
            }
            s.Launch(record.Lob, record.Missile, skill, level, x, y, targetX, targetY, 0);
            result = 1; // The source ignores the nested factory's return value.
        }
        if (result == 0) { return 0; }
        if (finish)
        {
            if (living)
            {
                if (!s.HasStart(record.Start) || record.UseManaOnDo) { s.ConsumeMana(skill, level); }
                if (record.DecrementQuantity) { s.ConsumeQuantity(); }
            }
            int delay = s.Delay(record.DelayFormula, skill, level);
            if (delay > 0 && s.UnitType == 0 && (record.Animation != 18 || (s.SequenceFrame & 0xFFFFFF00u) == 0))
            { s.SetDelay(delay); }
        }
        return result;
    }
}
