// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Explicit native synchronous item frame. It has no selected ordinary D2
// skill-list entry; never infer one from a PlayerMobile or the pet AI target.
// The complete common executor is shared; eight existing do30 curse bodies
// are supplied, with full current metadata instead of a copied Life Tap row.
internal static class NpcItemSkillExecution
{
    private sealed class Curse(SkeletonGuardian source, BaseCreature target, DateTime at) : SkillExecutionServices
    {
        private static NotSupportedException Missing() => new("NPC item skill requires an additional admitted execution service");
        internal override bool HasGame => source.Map != null && source.Map != Map.Internal;
        internal override bool HasSource => source != null;
        internal override int UnitType => 1;
        internal override SkillExecutionRecord Record(int skill)
        {
            return ItemSkillExecution.RequireCurse(skill).LegacyRecord(false, UnitType);
        }
        internal override ExecutionSkill UsedSkill() => null;
        internal override bool HasRoom => throw Missing();
        internal override bool RoomInTown => throw Missing();
        internal override void ClearUsedSkill() => throw Missing();
        internal override void TownNeutral() => throw Missing();
        internal override ExecutionSkill HighestSkill(int skill) => throw Missing();
        internal override int Rank(ExecutionSkill skill, bool bonuses) => throw Missing();
        // Reviewed older table length; this adapter admits do30 only, not
        // current extensions or a claim that all191 entries are implemented.
        internal override int DoCount => 191;
        internal override bool ManaAllowed() => throw Missing();
        internal override bool HasDo(int function) => function == 30 ? true : throw Missing();
        internal override int Execute(int function, int skill, int level)
        {
            if (function != 30) { throw Missing(); }
            return NecromancerCurseCasts.NpcDo30(source, target, ItemSkillExecution.CurseAbility(skill), level, at) ? 1 : 0;
        }
        internal override int MissileCount => throw Missing();
        internal override void SetServerDo() => throw Missing();
        internal override (int X, int Y)? TargetPoint => throw Missing();
        internal override (int X, int Y) FirstPathPoint => throw Missing();
        internal override (int X, int Y) SourcePoint => throw Missing();
        internal override void Launch(bool lob, int missile, int skill, int level, int x, int y, int targetX, int targetY, int flags) => throw Missing();
        internal override bool HasStart(int function) => throw Missing();
        internal override void ConsumeMana(int skill, int level) => throw Missing();
        internal override void ConsumeQuantity() => throw Missing();
        internal override int Delay(int formula, int skill, int level) => throw Missing();
        internal override uint SequenceFrame => throw Missing();
        internal override void SetDelay(int frames) => throw Missing();
    }
    internal static int Run(SkeletonGuardian source, BaseCreature target, int skill, int level, int finish, int item, int outgoing, DateTime at)
    {
        if (source == null || target == null) { return 0; }
        if (finish != 0 || item != 1 || outgoing is < 0 or > 1 || !NpcItemSpellCasts.KeepsCombatTarget(source))
        { throw new NotSupportedException("NPC item execution needs its synchronous protected item frame"); }
        return SkillExecution.Run(new Curse(source, target, at), skill, level, false, true, outgoing != 0);
    }
}
