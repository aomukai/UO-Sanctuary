// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public partial class HavenEncounter
{
    private const int CorpseSearchRange = 8;

    private TimerExecutionToken _reveal, _rockWindup;
    private Corpse _feedingTarget;
    private DateTime _nextCorpseCheck, _reachCorpseBy, _finishEatingAt;

    protected override BaseAI ForcedAI => new HavenEncounterAI(this);
    protected override bool Caster => Dungeon.Id == GraniteElemental || base.Caster;
    protected override int EncounterDamageRegen => Dungeon.Id == Ghoul ? 0 : base.EncounterDamageRegen;
    protected override bool IsRefuge(Point3D point) => HavenCrypts.IsArrivalRoom(Map, point) || base.IsRefuge(point);
    protected override bool BusyWithSpecial => Hidden && _thiefHiddenUntil != default || _feedingTarget != null;

    public override bool BeginFirebolt(Mobile target) => Dungeon.Id == GraniteElemental
        ? BeginRockThrow(target, ref _rockWindup) : base.BeginFirebolt(target);

    internal override void StopCombatActions()
    {
        _rockWindup.Cancel();
        base.StopCombatActions();
    }

    private void RestoreThiefVisibility()
    {
        if (_thiefHiddenUntil == default)
            return;

        var remaining = _thiefHiddenUntil - Core.Now;
        if (remaining > TimeSpan.Zero && Hidden)
            ScheduleThiefReveal(remaining);
        else
            EndThiefVanish();
    }

    internal bool ThinkSpecial(LeashedMeleeAI ai)
    {
        if (Dungeon.Id == Thief && Hidden && _thiefHiddenUntil != default)
        {
            ai.ClearMoveIntent();
            return true;
        }

        if (Controlled || Summoned || ReturningHome || !EliteCombat.CanAct(this)
            || !NamedControl.SpecialAllowed(this) || BardPacified || BardProvoked)
        {
            StopFeeding(ai);
            return false;
        }

        if (_feedingTarget != null)
            return ContinueFeeding(ai);

        if (Dungeon.Id is not (Thief or Ghoul) || !EliteCombat.Enemy(this, Combatant, RangePerception))
            return false;

        return Dungeon.Id == Thief ? TryVanish(ai) : TryFindMeal(ai);
    }

    private bool TryVanish(LeashedMeleeAI ai)
    {
        if (_thiefRolled || Hidden || 2L * SkillHealth.Balance256(this) >= SkillHealth.Maximum256(this))
            return false;

        ThiefRolled = true;
        if (Utility.Random(100) >= 30)
            return false;

        StopCombatActions();
        ai.ClearMoveIntent();
        var duration = TimeSpan.FromSeconds(3);
        ThiefHiddenUntil = Core.Now + duration;
        Hidden = true;
        ScheduleThiefReveal(duration);
        return true;
    }

    private void ScheduleThiefReveal(TimeSpan duration)
    {
        // The native melee timer reveals its attacker before the custom swing runs.
        NextCombatTime = Math.Max(NextCombatTime, Core.TickCount + (long)Math.Ceiling(duration.TotalMilliseconds));
        Timer.StartTimer(duration, EndThiefVanish, out _reveal);
    }

    private void EndThiefVanish()
    {
        ThiefHiddenUntil = default;
        if (!Deleted && Hidden)
            RevealingAction();
    }

    public override void OnHiddenChanged()
    {
        base.OnHiddenChanged();
        if (!Hidden && _thiefHiddenUntil != default)
        {
            _reveal.Cancel();
            ThiefHiddenUntil = default;
        }
    }

    private bool TryFindMeal(LeashedMeleeAI ai)
    {
        var now = Core.Now;
        if (now < _devourReadyAt || now < _nextCorpseCheck || !InRange(Home, HomeLeashRadius)
            || 4L * SkillHealth.Balance256(this) > 3L * SkillHealth.Maximum256(this))
            return false;

        _nextCorpseCheck = now.AddSeconds(5);
        if (Utility.Random(100) >= 30)
            return false;

        Corpse nearest = null;
        int distance = int.MaxValue;
        foreach (var corpse in Map.GetItemsInRange<Corpse>(Location, CorpseSearchRange))
        {
            if (!CanFeedOn(corpse) || !InLOS(corpse) || !CanSee(corpse)
                || !Utility.InRange(Home, corpse.Location, HomeLeashRadius - 1))
                continue;

            int dx = corpse.X - X, dy = corpse.Y - Y;
            int candidate = dx * dx + dy * dy;
            if (nearest == null || candidate < distance || candidate == distance && corpse.Serial.Value < nearest.Serial.Value)
            {
                nearest = corpse;
                distance = candidate;
            }
        }

        if (nearest == null)
            return false;

        StopCombatActions();
        ai.ClearMoveIntent();
        _feedingTarget = nearest;
        _reachCorpseBy = now.AddSeconds(6);
        return ContinueFeeding(ai);
    }

    private bool CanFeedOn(Corpse corpse) => CorpseSkillRules.BodyAvailable(corpse) && corpse.Map == Map
        && Math.Abs(corpse.Z - Z) < 16 && corpse.Owner is not BaseCreature { Controlled: true }
        && CorpseSkillRules.Profiles.Find(corpse) is not ({ Owned: true } or { Shattered: true } or { NoSelect: true })
        && !RepelledByRefuge(corpse.Location);

    private bool ContinueFeeding(LeashedMeleeAI ai)
    {
        var corpse = _feedingTarget;
        var now = Core.Now;
        if (!CanFeedOn(corpse) || SkillHealth.Full(this))
        {
            StopFeeding(ai);
            return false;
        }

        if (!InRange(corpse, 1) || !InLOS(corpse))
        {
            if (now >= _reachCorpseBy || _finishEatingAt != default)
            {
                StopFeeding(ai);
                return false;
            }
            ai.MoveToPoint(corpse, InLOS(corpse) ? 1 : 0);
            return true;
        }

        ai.ClearMoveIntent();
        if (_finishEatingAt == default)
        {
            DevourReadyAt = now.AddMinutes(1);
            _finishEatingAt = now.AddSeconds(1);
            Direction = GetDirectionTo(corpse);
            PublicOverheadMessage(MessageType.Emote, 0x3B2, false, "*starts to devour the corpse*");
            return true;
        }

        if (now < _finishEatingAt)
            return true;

        SkillHealth.Heal(this, SkillHealth.Maximum256(this) / 4);
        StopFeeding(ai);
        return true;
    }

    private void StopFeeding(LeashedMeleeAI ai)
    {
        if (_feedingTarget == null)
            return;
        _feedingTarget = null;
        _finishEatingAt = default;
        ai.ClearMoveIntent();
    }

    public override void OnDelete()
    {
        _reveal.Cancel();
        base.OnDelete();
    }
}

internal sealed class HavenEncounterAI(HavenEncounter creature) : LeashedMeleeAI(creature)
{
    public override bool Think() => creature.ThinkSpecial(this) || base.Think();
}
