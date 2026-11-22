// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public partial class Merydwen
{
    private const int BowRange = 7;
    private TimerExecutionToken _shot;
    private long _nextGuided, _nextSight, _nextStrafe;

    public override void OnThink()
    {
        if (Recruited)
            RangeFight = Weapon is BaseRanged bow && !ClassGear.Broken(bow) ? Math.Min(BowRange, bow.MaxRange) : 1;
        base.OnThink();
        if (Recruited && this is not Nightwind && !Mounted)
            ObserveBarks();
    }

    private static SkillValues Values(CombatAbility skill, int level)
    {
        var row = SkillMechanics.For(skill);
        int Rank(string key) => key == row.SourceKey ? Math.Min(20, 1 + (level - 1) / 4) : 0;
        return new(row, Rank, Rank, level);
    }

    protected PlayerMobile ArcherOwner(BaseRanged bow) =>
        Alive && !Deleted && !IsDeadPet && !Mounted && !Frozen && !Paralyzed && CreatureHitRecovery.Remaining(this) == 0
        && ClassCombatHooks.ActionAllowed?.Invoke(this) != false && Weapon == bow && !ClassGear.Broken(bow)
        && ControlMaster is PlayerMobile { Alive: true } owner && owner.Map == Map && Map != Map.Internal
        && MercenaryCare.IsCompanion(owner, this) ? owner : null;

    private bool CanAim(PlayerMobile owner, Mobile target, BaseRanged bow) =>
        target is { Deleted: false, Alive: true } && target.Map == Map && InRange(target, Math.Min(BowRange, bow.MaxRange))
        && Math.Abs(Z - target.Z) < 16 && CanSee(target) && InLOS(target) && ClassCombat.IsHostile(owner, target);

    internal TimeSpan BeginShot(Mobile target, BaseRanged bow)
    {
        if (_shot.Running || ArcherOwner(bow) is not { } owner || Combatant != target || !CanAim(owner, target, bow))
            return TimeSpan.FromMilliseconds(100);

        long now = Core.TickCount;
        int level = MercenaryCare.CompanionLevel(this) ?? 1;
        Direction = GetDirectionTo(target);
        if (this is not Nightwind && now >= _nextSight && AmazonInnerSight.State(target) == null)
        {
            _nextSight = now + 12000;
            int castSpeed = CompanionBuild.Bonus(this, "cast1") + CompanionBuild.Bonus(this, "cast2")
                + CompanionBuild.Bonus(this, "cast3");
            var timing = AmazonCastClock.Calculate("BOW", Math.Clamp(castSpeed, 0, 10000));
            Animate(16, 5, 1, true, false, 0);
            Timer.StartTimer(TimeSpan.FromMilliseconds(timing.EffectMs), () => MarkEnemies(target, bow, level), out _shot);
            return TimeSpan.FromMilliseconds(timing.DurationMs);
        }

        if (now >= _nextStrafe)
        {
            var targets = Targets(owner, target, bow);
            if (targets.Count > 1)
            {
                _nextStrafe = now + 7000;
                return this is Nightwind ? BeginMultipleShot(targets, bow, level) : BeginStrafe(targets, bow, level);
            }
        }

        var ability = now >= _nextGuided ? CombatAbility.GuidedArrow : CombatAbility.BasicAttack;
        if (ability == CombatAbility.GuidedArrow)
            _nextGuided = now + 3200;
        var shot = ability == CombatAbility.GuidedArrow
            ? AmazonBow.Timing(AttackFamily.Bow, SequenceAdvance(bow))
            : CompanionBuild.Timing(this, CombatClass, ability, bow);
        bow.PlaySwingAnimation(this);
        Timer.StartTimer(TimeSpan.FromMilliseconds(shot.EffectMs), () =>
        {
            if (ArcherOwner(bow) is not { } currentOwner || Combatant != target || !CanAim(currentOwner, target, bow))
                return;
            if (ability == CombatAbility.GuidedArrow && this is not Nightwind)
                SayBark(currentOwner, _guided, Core.TickCount);
            Shoot(target, bow, ability, level, ability == CombatAbility.GuidedArrow ? Values(ability, level) : null);
        }, out _shot);
        return TimeSpan.FromMilliseconds(shot.DurationMs);
    }

    private int SequenceAdvance(BaseRanged bow) =>
        CombatTiming.FrameAdvance(CombatTiming.Weapon(bow), CompanionBuild.AttackSpeedBonus(this),
            GearEffects.AttackSlowTotal(this), CombatTiming.SkillAttackRate(this));

    private List<Mobile> Targets(PlayerMobile owner, Mobile primary, BaseRanged bow)
    {
        var targets = new List<Mobile>();
        if (CanAim(owner, primary, bow))
            targets.Add(primary);
        foreach (var foe in Map.GetMobilesInRange<BaseCreature>(Location, Math.Min(BowRange, bow.MaxRange)))
            if (foe != primary && CanAim(owner, foe, bow))
                targets.Add(foe);
        return targets;
    }

    private void MarkEnemies(Mobile primary, BaseRanged bow, int level)
    {
        if (ArcherOwner(bow) is not { } owner || !CanAim(owner, primary, bow))
            return;
        var values = Values(CombatAbility.InnerSight, level);
        int radius = (int)values.Eval("aurarangecalc");
        var at = TimedEffects.Now;
        foreach (var foe in Targets(owner, primary, bow))
            if (foe is BaseCreature creature && AmazonInnerSight.InRadius(foe.X - X, foe.Y - Y, radius)
                && SourceAuraTargets.Eligible(creature))
                AmazonInnerSight.Apply(this, creature, values, at);
        AmazonUtilityEffects.Burst(this, SpellEffectGraphics.InnerSightCast, SpellEffectSounds.InnerSightCast);
        SayBark(owner, _sight, Core.TickCount);
    }

    private TimeSpan BeginMultipleShot(List<Mobile> targets, BaseRanged bow, int level)
    {
        var timing = AmazonBow.Timing(AttackFamily.Bow, SequenceAdvance(bow));
        var ability = CombatAbility.MultipleShot;
        var values = Values(ability, level);
        int arrows = 1 + values.Rank;
        bow.PlaySwingAnimation(this);
        Timer.StartTimer(TimeSpan.FromMilliseconds(timing.EffectMs), () =>
        {
            if (ArcherOwner(bow) is not { } owner)
                return;
            foreach (var target in targets)
            {
                if (!CanAim(owner, target, bow))
                    continue;
                Shoot(target, bow, ability, level, values);
                if (--arrows == 0)
                    break;
            }
        }, out _shot);
        return TimeSpan.FromMilliseconds(timing.DurationMs);
    }

    private TimeSpan BeginStrafe(List<Mobile> targets, BaseRanged bow, int level)
    {
        var values = Values(CombatAbility.Strafe, level);
        var (events, end) = AmazonStrafe.Schedule(AttackFamily.Bow, SequenceAdvance(bow), AmazonStrafe.Count(values, targets.Count, false));
        var origin = Location;
        var map = Map;
        bow.PlaySwingAnimation(this);
        Schedule(0, 0);
        return TimeSpan.FromMilliseconds(end * ActionTiming.FrameMs);

        void Schedule(int index, int previous)
        {
            Timer.StartTimer(TimeSpan.FromMilliseconds((events[index] - previous) * ActionTiming.FrameMs), () =>
            {
                if (Location != origin || Map != map || ArcherOwner(bow) is not { } owner)
                    return;
                for (int offset = 0; offset < targets.Count; offset++)
                {
                    var target = targets[(index + offset) % targets.Count];
                    if (!CanAim(owner, target, bow))
                        continue;
                    Direction = GetDirectionTo(target);
                    if (index == 0)
                        SayBark(owner, _strafe, Core.TickCount);
                    Shoot(target, bow, CombatAbility.Strafe, level, values);
                    break;
                }
                if (index + 1 < events.Length)
                {
                    bow.PlaySwingAnimation(this);
                    Schedule(index + 1, events[index]);
                }
            }, out _shot);
        }
    }

    private void Shoot(Mobile target, BaseRanged bow, CombatAbility skill, int level, SkillValues values)
    {
        DoHarmful(target);
        PlaySound(skill == CombatAbility.GuidedArrow ? SpellEffectSounds.GuidedArrowRelease : SpellEffectSounds.AmazonBowRelease);
        MovingParticles(target, bow.EffectID, 18, 1, false, false, 0, 0, 0, 0, 0, EffectLayer.RightHand, 0);
        bool guided = skill == CombatAbility.GuidedArrow;
        int attackPercent = CompanionBuild.Bonus(this, "att%") + (int)(values?.Formula("toht") ?? 0);
        if (ClassStats.CheckHit(this, target, attackPercent, projectile: true, guaranteed: guided) != true)
            return;

        int skillPercent = skill switch
        {
            CombatAbility.GuidedArrow => (int)values.Eval("calc1"),
            CombatAbility.Strafe => (int)values.Eval("calc2"),
            _ => 0
        };
        double physical = CompanionBuild.BowPhysicalDamage(this, bow, level, skillPercent);
        if (skill == CombatAbility.MultipleShot)
            physical *= 0.75;
        var parts = DefensiveArmors.Absorb(target, new GearDamage(physical, 0, 0, 0, 0), this);
        parts = parts with { Physical = GemCombat.Component(target, parts.Physical, target.PhysicalResistance, false) };
        var settled = DirectDamageParts.From(parts);
        SkillHealth.TypedDamage(this, target, checked((int)settled.Total), settled);
        AmazonBowEffects.Contact(this, target, skill);
    }

    public override void OnAfterDelete()
    {
        _shot.Cancel();
        base.OnAfterDelete();
    }
}
