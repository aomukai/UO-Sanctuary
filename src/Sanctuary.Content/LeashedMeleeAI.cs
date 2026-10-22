// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Wild project mobs keep their spawner home; guardians retain their own pet AI.
public enum MonsterApproachPhase { Idle, Hesitating, Wandering, Pursuing, Melee, Returning }

public sealed class LeashedMeleeAI : MeleeAI
{
    public const int HomeRadius = 24;
    public const int ChaseTimeoutMs = 12000;
    private long _lastContact, _returnStarted, _homeReached;
    private bool _engaged;
    private Serial _approachTarget;
    private long _approachUntil = Core.TickCount, _scanAfter = Core.TickCount;
    public MonsterApproachPhase ApproachPhase { get; private set; }
    internal int ApproachRolls { get; private set; }
    internal int FocusScans { get; private set; }
    internal long ApproachUntil => _approachUntil;
    private bool UsesSpecies => Mobile is GraveyardUndead { NormalStats: not null, Controlled: false, Summoned: false };
    private IPoint3D _homeGoal;
    public bool ReturningHome { get; private set; }
    public override double FleeChance => Mobile is GraveyardUndead { NormalStats: not null } ? 0 : base.FleeChance;
    public LeashedMeleeAI(GraveyardUndead mobile) : base(mobile) { }

    internal bool UpdateLeash(long now)
    {
        // Forced source fear owns movement until expiry; losing the combatant
        // during that control state is not voluntary chase abandonment.
        if(SourceTerror.State(Mobile)!=null){return false;}
        if (Mobile.Deleted || !Mobile.Alive || Mobile.Controlled || Mobile.Summoned
            || Mobile.Map == null || Mobile.Map == Map.Internal)
        {
            return false;
        }
        if (Mobile.Home == Point3D.Zero)
        {
            Mobile.Home = Mobile.Location;
            Mobile.HomeMap = Mobile.Map;
        }
        if (!ReturningHome)
        {
            var target = Mobile.Combatant;
            bool valid = target is { Deleted: false, Alive: true } && target.Map == Mobile.Map;
            if (valid && !_engaged)
            {
                _engaged = true;
                _lastContact = now;
            }
            if (valid && Mobile.InRange(target, Mobile.RangeFight) && Math.Abs(Mobile.Z - target.Z) < 16
                && Mobile.CanSee(target) && Mobile.InLOS(target))
            {
                _lastContact = now;
            }
            if (!Mobile.InRange(Mobile.Home, HomeRadius) || Mobile.Map != Mobile.HomeMap
                || _engaged && (!valid || now - _lastContact >= ChaseTimeoutMs))
            {
                ReturningHome = true;
                ResetSpecies(now);
                ApproachPhase = MonsterApproachPhase.Returning;
                _returnStarted = now;
                _homeReached = 0;
                _homeGoal = Mobile.Home;
                ((GraveyardUndead)Mobile).StopCombatActions();
                ResetApproachState();
                Path = null;
                Action = ActionType.Wander;
            }
        }
        if (ReturningHome)
        {
            Mobile.Combatant = null;
            Mobile.FocusMob = null;
            Mobile.Warmode = false;
            if (Mobile.Map == Mobile.HomeMap && Mobile.InRange(Mobile.Home, 1))
            {
                _homeReached = _homeReached == 0 ? now : _homeReached;
                if (now - _homeReached >= 3000)
                {
                    ReturningHome = false;
                    ResetSpecies(now);
                    _engaged = false;
                    ResetApproachState();
                    Path = null;
                    Mobile.ForceReacquire();
                }
            }
        }
        return ReturningHome;
    }

    private void ResetSpecies(long now)
    {
        _approachTarget = Serial.Zero;
        _approachUntil = _scanAfter = now;
        ApproachPhase = MonsterApproachPhase.Idle;
        ResetApproachState();
    }

    private void ObserveTarget(Mobile target, long now)
    {
        if (_approachTarget != target.Serial)
        {
            ResetSpecies(now);
            _approachTarget = target.Serial;
        }
    }

    internal long MeleeWait(Mobile target, long now)
    {
        if (!UsesSpecies)
        {
            return 0;
        }
        ObserveTarget(target, now);
        long remaining = _approachUntil - now;
        if (remaining > 0)
        {
            return remaining;
        }
        ApproachPhase = MonsterApproachPhase.Melee;
        return 0;
    }

    public override bool AcquireFocusMob(int range, FightMode mode, bool playerOnly, bool friend, bool foe)
    {
        if (!UsesSpecies || Mobile.BardProvoked || Mobile.BardPacified)
        {
            return base.AcquireFocusMob(range, mode, playerOnly, friend, foe);
        }
        long now = Core.TickCount;
        if (now - _scanAfter < 0)
        {
            Mobile.FocusMob = null;
            return false;
        }
        if (now - Mobile.NextReacquireTime >= 0)
        {
            _scanAfter = now + 200;
            FocusScans++;
        }
        return base.AcquireFocusMob(range, mode, playerOnly, friend, foe);
    }

    public override bool DoActionWander()
    {
        if (!UsesSpecies)
        {
            return base.DoActionWander();
        }
        if (AcquireFocusMob(Mobile.RangePerception, Mobile.FightMode, false, false, true))
        {
            Mobile.Combatant = Mobile.FocusMob;
            Action = ActionType.Combat;
            return DoActionCombat();
        }
        return base.DoActionWander();
    }

    public override bool DoActionGuard() => UsesSpecies ? DoActionWander() : base.DoActionGuard();

    public override bool DoActionCombat()
    {
        var target = Mobile.Combatant;
        if (!UsesSpecies || !IsValidCombatant(target))
        {
            return base.DoActionCombat();
        }
        long now = Core.TickCount;
        ObserveTarget(target, now);
        if (now - _approachUntil < 0)
        {
            ClearMoveIntent();
            return true;
        }
        if (Mobile.Frozen || Mobile.Paralyzed)
        {
            ClearMoveIntent();
            return true;
        }
        if (Mobile.InRange(target, Mobile.RangeFight) && Mobile.InLOS(target) && Math.Abs(Mobile.Z - target.Z) < 16)
        {
            ApproachPhase = MonsterApproachPhase.Melee;
            return base.DoActionCombat();
        }
        if (ApproachPhase != MonsterApproachPhase.Pursuing)
        {
            var profile = CreatureProfiles.For(Mobile).Reaction;
            ApproachRolls++;
            bool aware = profile.AwarenessRange == 0 || Mobile.InRange(target, profile.AwarenessRange.Value - 1);
            if (aware && Utility.RandomDouble() * 100 < profile.ApproachChance)
            {
                ApproachPhase = MonsterApproachPhase.Pursuing;
            }
            else
            {
                ResetApproachState();
                _approachUntil = now + profile.RetryMs.Value;
                ApproachPhase = profile.Wander == true ? MonsterApproachPhase.Wandering : MonsterApproachPhase.Hesitating;
                if (profile.Wander == true && !Mobile.DisallowAllMoves)
                {
                    // One collision-checked UO step keeps local wandering at this map's scale.
                    DoMove((Direction)Utility.Random(8), true);
                }
                return true;
            }
        }
        // Range alone can declare arrival on the other side of a wall. A full route
        // keeps following until an actual attack position is visible, using native collision.
        if (!Mobile.InLOS(target) || Math.Abs(Mobile.Z - target.Z) >= 16)
        {
            MoveTo(target, 0);
            return true;
        }
        return base.DoActionCombat();
    }

    public override bool OnAggressiveAction(Mobile aggressor)
    {
        bool accepted = !ReturningHome && base.OnAggressiveAction(aggressor);
        if (accepted && UsesSpecies)
        {
            AITimer.Prod();
        }
        return accepted;
    }
    public override bool Think()
    {
        long now = Core.TickCount;
        if (!UpdateLeash(now))
        {
            if (Mobile is GraveyardUndead { NormalStats: not null } undead && now - undead.NextMeleeDecision < 0)
            {
                ClearMoveIntent();
                return true;
            }
            return base.Think();
        }
        Mobile.CurrentSpeed = Mobile.ActiveSpeed;
        // A blocked route must not strand the mob indefinitely. Never heal on reset.
        if (now - _returnStarted >= 60000 && !Mobile.DisallowAllMoves
            && Mobile.HomeMap?.CanSpawnMobile(Mobile.Home) == true)
        {
            Mobile.MoveToWorld(Mobile.Home, Mobile.HomeMap);
            UpdateLeash(now);
        }
        else if (Mobile.Map == Mobile.HomeMap)
        {
            MoveToPoint(_homeGoal, 1);
        }
        return true;
    }
}
