// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal sealed record SkillStartupRecord(bool TargetableOnly, bool TargetCorpse, bool TargetAlly,
    bool InTown, int LineOfSight, int Start, bool Periodic, bool UseManaOnDo);
internal sealed record SkillStartupTarget(int Type, int Mode, bool Targetable);
internal sealed record SkillStartupRoom(bool InTown);

// The host supplies live target/room/skill state and real nested start bodies.
// In particular, PlayerManaAllowed is the server's player-only admission
// check, not the common UI use-state check or an NPC mana requirement.
internal abstract class SkillStartupServices
{
    internal abstract bool HasGame { get; }
    internal abstract bool HasSource { get; }
    internal abstract int UnitType { get; }
    internal abstract ExecutionSkill UsedSkill();
    internal abstract SkillStartupRecord Record(int skill);
    internal abstract SkillStartupTarget Target();
    internal abstract bool EnemyAllowed(SkillStartupTarget target);
    internal abstract bool PetAllowed(SkillStartupTarget target, int skill);
    internal abstract bool AllyAllowed(SkillStartupTarget target, int skill);
    internal abstract void ClearTarget();
    internal abstract int Rank(ExecutionSkill skill);
    // Null means intrinsic ownership; item-owned skills replace the rank
    // with their base rank and require a nonzero charge count.
    internal abstract (int Rank, int Charges)? ItemInfo(ExecutionSkill skill);
    internal abstract SkillStartupRoom Room();
    internal abstract void ClearUsedSkill();
    internal abstract void TownNeutral();
    internal abstract bool PlayerManaAllowed();
    internal abstract (int X, int Y) TargetPoint(SkillStartupTarget target);
    internal abstract (int X, int Y) FirstPathPoint { get; }
    internal abstract bool ClearLine(int x, int y, uint collisionMask);
    internal abstract int StartCount { get; }
    internal abstract bool HasStart(int function);
    internal abstract int ExecuteStart(int function, int skill, int rank);
    internal abstract void ConsumeMana(int skill, int rank);
    internal abstract void DeletePeriodicEvent();
}

// Complete older selected-skill wrapper (sub_6FD12FD0) and start dispatcher
// (sub_6FD12950). This complements SkillExecution's do dispatcher. It does
// not supply an NPC controller, animation, target search or a missing effect.
internal static class SkillStartup
{
    internal static int Selected(SkillStartupServices s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var used = s.UsedSkill();
        if (used == null) { return 0; }
        int skill = used.Id;
        var row = s.Record(skill);
        var target = s.Target();
        if (target != null)
        {
            if (row == null)
            {
                NeutralPlayer(s);
                return 0;
            }
            if (row.TargetableOnly && !s.EnemyAllowed(target) && !s.PetAllowed(target, skill) && !s.AllyAllowed(target, skill))
            {
                NeutralPlayer(s);
                return 0;
            }
            if (!target.Targetable || (target.Type == 1 && target.Mode == 12) != row.TargetCorpse)
            { s.ClearTarget(); }
        }
        int rank = s.Rank(used);
        if (s.ItemInfo(used) is {} item)
        {
            rank = item.Rank;
            if (item.Charges == 0) { return 0; }
        }
        if (row == null) { return 0; }
        target = s.Target();
        if (target != null && !row.TargetAlly && s.AllyAllowed(target, skill)) { return 0; }
        if (!row.InTown && s.Room()?.InTown == true)
        {
            s.ClearUsedSkill();
            NeutralPlayer(s);
            return 0;
        }
        int result = Run(s, skill, rank, false);
        if (result == 0) { NeutralPlayer(s); }
        return result;
    }

    private static void NeutralPlayer(SkillStartupServices s)
    { if (s.HasSource && s.UnitType == 0) { s.TownNeutral(); } }

    internal static int Run(SkillStartupServices s, int skill, int rank, bool item)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!s.HasGame || !s.HasSource || s.Room() == null) { return 0; }
        var row = s.Record(skill);
        if (row == null || !item && !s.PlayerManaAllowed()) { return 0; }
        if (!row.InTown)
        {
            var room = s.Room();
            if (room == null || room.InTown)
            {
                s.ClearUsedSkill();
                NeutralPlayer(s);
                return 0;
            }
        }
        if (row.LineOfSight != 0)
        {
            var target = s.Target();
            var point = target == null ? s.FirstPathPoint : s.TargetPoint(target);
            if (point.X != 0 && point.Y != 0)
            {
                uint mask = row.LineOfSight switch
                {
                    1 => 0x0004,
                    2 => 0x1C09,
                    3 => 0x0180,
                    4 => 0x0804,
                    5 => 0x0805,
                    _ => 0
                };
                if (mask == 0 || !s.ClearLine(point.X, point.Y, mask)) { return 0; }
            }
        }
        if (row.Start < 0 || row.Start >= s.StartCount) { return 0; }
        if (!s.HasStart(row.Start))
        {
            if (row.Periodic) { s.DeletePeriodicEvent(); }
            return 1;
        }
        int result = s.ExecuteStart(row.Start, skill, rank);
        if (result != 0)
        {
            if (!row.UseManaOnDo && !item) { s.ConsumeMana(skill, rank); }
            if (row.Periodic) { s.DeletePeriodicEvent(); }
        }
        return result;
    }
}
