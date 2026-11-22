// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class OrcFortScout : OrcFortEncounter
{
    private static readonly Dictionary<string, IPoint3D[]> _approaches = new(StringComparer.Ordinal)
    {
        ["north-east"] = [new Point3D(651, 1458, 0), new Point3D(653, 1480, 0), new Point3D(650, 1500, 0)],
        ["north-west"] = [new Point3D(614, 1471, 0), new Point3D(614, 1496, 0), new Point3D(627, 1499, 0)],
        ["east"] = [new Point3D(657, 1480, 0), new Point3D(650, 1500, 0)],
        ["south-east"] = [new Point3D(647, 1504, 0)],
        ["south-west"] = [new Point3D(621, 1505, 0)],
        ["west"] = [new Point3D(609, 1495, 0), new Point3D(627, 1499, 0)]
    };

    [SerializableField(0)]
    private string _approach;

    internal IPoint3D[] ApproachWaypoints => _approaches[_approach];
    internal bool FightingNormally => Controlled || Summoned || PaladinConversion.Active(this);

    private bool AwayOnReport => AIObject is OrcFortScoutAI { AwayOnReport: true } && !FightingNormally;
    public override bool PlayerRangeSensitive => !AwayOnReport && base.PlayerRangeSensitive;
    public override bool ReturnsToHome => !AwayOnReport && base.ReturnsToHome;

    [Constructible]
    public OrcFortScout(string approach)
    {
        if (!_approaches.ContainsKey(approach))
            throw new ArgumentException("Unknown fort scout approach.", nameof(approach));
        _approach = approach;
        RangePerception = 10;
    }

    protected override BaseAI ForcedAI => new OrcFortScoutAI(this);
    protected override int EncounterMoveInterval(int interval) => 180;

    public override void OnThink()
    {
        if (FightingNormally)
            base.OnThink();
        else
            RecoverLife(Core.TickCount);
    }

    public override bool BeginFirebolt(Mobile target) =>
        (FightingNormally || AIObject is OrcFortScoutAI { Shooting: true }) && base.BeginFirebolt(target);
}

internal sealed class OrcFortScoutAI(OrcFortScout scout) : MeleeAI(scout)
{
    internal static readonly IPoint3D GateApproach = new Point3D(634, 1500, 0);
    internal static readonly IPoint3D GatePassage = new Point3D(634, 1494, 0);
    internal static readonly IPoint3D RallyPoint = new Point3D(634, 1487, 0);
    private static readonly string[] _spotted = ["Humie?!", "Gah! Humie!", "Ug! Intruda!"];
    private static readonly string[] _warning = ["Da fort! Gotta warn 'em!", "Me tell da guards!", "Da shaman gotta know!"];
    private enum Phase { Watching, Hesitating, Running, Rallying, Shooting, Returning }

    private Phase _phase;
    private Mobile _target;
    private IPoint3D[] _route;
    private int _leg;
    private long _phaseAt, _lastContact;

    internal bool Shooting => _phase == Phase.Shooting;
    internal bool AwayOnReport => _phase != Phase.Watching;

    public override bool OnAggressiveAction(Mobile aggressor)
    {
        if (scout.FightingNormally || Shooting)
            return base.OnAggressiveAction(aggressor);
        if (_phase == Phase.Watching && aggressor is PlayerMobile player)
            Spot(player);
        return false;
    }

    public override bool Think()
    {
        if (scout.Deleted || !scout.Alive || scout.Hits <= 0)
        {
            ClearMoveIntent();
            return false;
        }
        if (scout.FightingNormally || scout.Map != Map.Trammel)
            return base.Think();
        if (!EliteCombat.CanAct(scout))
        {
            ClearMoveIntent();
            return true;
        }

        long now = Core.TickCount;
        if (!Shooting && scout.Combatant != null)
            scout.Combatant = null;

        switch (_phase)
        {
            case Phase.Watching:
                Watch();
                break;
            case Phase.Hesitating when now - _phaseAt >= 1000:
                scout.Bark(Utility.RandomList(_warning));
                Enter(Phase.Running, now);
                FollowRoute(1, now);
                break;
            case Phase.Running:
                if (FollowRoute(1, now))
                {
                    Enter(Phase.Rallying, now);
                    DeliverWarning();
                }
                else if (Stuck(now))
                    Return(now);
                break;
            case Phase.Rallying:
                if (EnemyNearby(18) && (DefenderEngaged() || CallDefenders()))
                {
                    scout.Combatant = _target;
                    _lastContact = now;
                    Enter(Phase.Shooting, now);
                }
                else if (now - _phaseAt >= 8000)
                    Return(now);
                break;
            case Phase.Shooting:
                if (EnemyNearby(18) && DefenderEngaged())
                {
                    _lastContact = now;
                    scout.BeginFirebolt(_target);
                }
                else if (now - _lastContact >= 8000)
                    Return(now);
                break;
            case Phase.Returning:
                if (FollowRoute(-1, now) || Stuck(now))
                    Enter(Phase.Watching, now);
                break;
        }
        return true;
    }

    private void Watch()
    {
        PlayerMobile nearest = null;
        double distance = double.MaxValue;
        foreach (var player in scout.Map.GetMobilesInRange<PlayerMobile>(scout.Location, scout.RangePerception))
        {
            double candidate = scout.GetDistanceToSqrt(player);
            if (candidate < distance && EliteCombat.Enemy(scout, player, scout.RangePerception))
            {
                nearest = player;
                distance = candidate;
            }
        }
        if (nearest != null)
            Spot(nearest);
    }

    private void Spot(PlayerMobile player)
    {
        if (!EliteCombat.CanAct(scout) || !EliteCombat.Enemy(scout, player, scout.RangePerception))
            return;

        _target = player;
        _route = [scout.Home, .. scout.ApproachWaypoints, GateApproach, GatePassage, RallyPoint];
        _leg = 1;
        scout.Direction = scout.GetDirectionTo(player);
        scout.Bark(Utility.RandomList(_spotted));
        Enter(Phase.Hesitating, Core.TickCount);
        AITimer.Prod();
    }

    private void Enter(Phase phase, long now)
    {
        _phase = phase;
        _phaseAt = now;
        Path = null;
        ClearMoveIntent();
        scout.Warmode = phase is Phase.Running or Phase.Returning or Phase.Shooting;
        scout.CurrentSpeed = phase == Phase.Watching ? scout.PassiveSpeed : scout.ActiveSpeed;
    }

    private bool FollowRoute(int direction, long now)
    {
        var goal = _route[_leg];
        if (PathFollower.Check(scout.Location, new Point3D(goal), 1))
        {
            if (_leg + direction < 0 || _leg + direction >= _route.Length)
                return true;
            _leg += direction;
            _phaseAt = now;
            goal = _route[_leg];
        }
        MoveToPoint(goal, 1);
        return false;
    }

    private bool Stuck(long now) => now - Math.Max(_phaseAt, scout.LastMoveTime) >= 10000;

    private void Return(long now)
    {
        scout.Combatant = null;
        scout.StopCombatActions();
        _target = null;
        _leg = Math.Max(0, _leg - 1);
        Enter(Phase.Returning, now);
    }

    private bool EnemyNearby(int range) =>
        _target is { Alive: true, Deleted: false, Blessed: false } target && target.Map == scout.Map
        && scout.InRange(target, range) && Math.Abs(target.Z - scout.Z) < 16 && scout.IsEnemy(target) && scout.CanSee(target);

    private void DeliverWarning()
    {
        scout.Bark("Intruda! Watch da gate!");
        foreach (var ally in scout.Map.GetMobilesInRange<OrcFortEncounter>(scout.Location, 18))
            if (AvailableDefender(ally) && ally.AIObject is OrcFortAI guard)
                guard.ReceiveWarning();
    }

    private bool CallDefenders()
    {
        if (!EliteCombat.Enemy(scout, _target, 18))
            return false;

        foreach (var ally in scout.Map.GetMobilesInRange<OrcFortEncounter>(scout.Location, 18))
        {
            if (!AvailableDefender(ally) || ally.Combatant != null || !ally.IsEnemy(_target))
                continue;
            ally.Combatant = _target;
            if (ally.Combatant == _target)
            {
                ally.FocusMob = _target;
                ally.AIObject?.AITimer.Prod();
            }
        }
        return DefenderEngaged();
    }

    private bool AvailableDefender(OrcFortEncounter ally) =>
        ally is { FortDefender: true, Alive: true, Deleted: false, Controlled: false, Summoned: false, ReturningHome: false }
        && !PaladinConversion.Active(ally) && scout.IsFriend(ally);

    private bool DefenderEngaged()
    {
        foreach (var ally in scout.Map.GetMobilesInRange<OrcFortEncounter>(scout.Location, 18))
            if (AvailableDefender(ally) && ally.IsEnemy(_target) && (ally.Combatant == _target
                || ally.Combatant is BaseCreature { Alive: true } follower && follower.GetMaster() == _target))
                return true;
        return false;
    }
}
