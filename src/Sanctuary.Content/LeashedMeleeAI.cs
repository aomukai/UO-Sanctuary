// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Mobiles;

namespace Sanctuary;

// Wild project mobs keep their spawner home; guardians retain their own pet AI.
public sealed class LeashedMeleeAI : MeleeAI
{
    public const int HomeRadius = 24;
    public const int ChaseTimeoutMs = 12000;
    private long _lastContact, _returnStarted, _homeReached;
    private bool _engaged;
    private IPoint3D _homeGoal;
    public bool ReturningHome { get; private set; }
    public override double FleeChance => Mobile is GraveyardUndead { NormalStats: not null } ? 0 : base.FleeChance;
    public LeashedMeleeAI(GraveyardUndead mobile) : base(mobile) { }

    internal bool UpdateLeash(long now)
    {
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
            if (valid && Mobile.InRange(target, Mobile.RangeFight) && Mobile.InLOS(target))
            {
                _lastContact = now;
            }
            if (!Mobile.InRange(Mobile.Home, HomeRadius) || Mobile.Map != Mobile.HomeMap
                || _engaged && (!valid || now - _lastContact >= ChaseTimeoutMs))
            {
                ReturningHome = true;
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
                    _engaged = false;
                    ResetApproachState();
                    Path = null;
                    Mobile.ForceReacquire();
                }
            }
        }
        return ReturningHome;
    }

    public override bool OnAggressiveAction(Mobile aggressor) => !ReturningHome && base.OnAggressiveAction(aggressor);
    public override bool Think()
    {
        long now = Core.TickCount;
        if (!UpdateLeash(now))
        {
            if (Mobile is GraveyardUndead { NormalStats: not null } undead && now < undead.NextMeleeDecision)
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
