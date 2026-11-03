// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

// Shared no-equipment NPC skill getter. The historical Oak context pins the
// common current experience maximum; it is not an Oak-only ceiling.
internal static class InnateNpcSkills
{
    internal static int Level(int assigned, int allSkills, bool bonuses = true, bool itemOwned = false)
    {
        if (assigned is < 1 or > 255 || allSkills is < 0 or > 1)
        { throw new NotSupportedException("Unsupported innate NPC skill-rank context"); }
        int maximum = NecromancerSummons.Source("oaksage-skill-context", "MaximumRank");
        if (maximum != 99) { throw new NotSupportedException("Unsupported current NPC skill ceiling"); }
        return Math.Clamp(checked(assigned + (bonuses && !itemOwned ? allSkills : 0)), 0, maximum);
    }
}

internal static class TaintedNpcRanks
{
    internal static int Assigned(TaintedSuppliers captured, int sourceSkill)
    {
        ArgumentNullException.ThrowIfNull(captured);
        return sourceSkill switch
        {
            420 => captured.ResistFireRank,
            422 => captured.FireBallRank,
            380 => captured.BloodBoilRank,
            _ => throw new NotSupportedException("Tainted skill is not in its captured innate roster")
        };
    }
    internal static int Level(WarlockTaintedBody body, int sourceSkill, bool bonuses = true)
    {
        if (body is not { Alive: true, Deleted: false } || body.Prepared == null)
        { throw new NotSupportedException("Tainted rank getter requires its original living captured NPC"); }
        int assigned = Assigned(body.Prepared.Snapshot.Birth.Suppliers, sourceSkill);
        // An unassigned Blood Boil is absent; allskills cannot create it.
        return assigned == 0 ? 0 : InnateNpcSkills.Level(assigned, BattleCommand.Bonus(body), bonuses);
    }
}
