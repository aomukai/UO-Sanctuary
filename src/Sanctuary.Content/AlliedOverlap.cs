// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.CompilerServices;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class AlliedOverlap
{
    public const int RegroupMs = 5000;
    private sealed class State
    {
        internal Point3D Position;
        internal Map Map, GoalMap;
        internal long Since;
        internal bool Active, HasGoal, Retreating;
        internal Point3D Goal;
        internal int Range;
    }
    private sealed class Presence
    {
        internal Point3D Position;
        internal Map Map;
        internal ulong Order;
    }
    private static ulong _arrival;
    private static readonly ConditionalWeakTable<Mobile, Presence> _presence = new();
    private static readonly ConditionalWeakTable<BaseCreature, State> _states = new();
    public static void Configure()
    {
        ClassCombatHooks.ActionAllowed = Allowed;
        ClassCombatHooks.OccupancyChanged = Arrived;
        ClassCombatHooks.PassThrough = PassThrough;
        ClassCombatHooks.OverlapMovement = pet => pet is IPersistentMinion && !PersistentMinions.CanThink(pet) || UpdatePet(pet, Core.TickCount);
        ClassCombatHooks.MovementGoal = Remember;
        ClassCombatHooks.FreeDestination = FreeDestination;
    }
    public static bool PassThrough(Mobile a, Mobile b) => a != b && a is { Deleted: false, Alive: true }
        && b is { Deleted: false, Alive: true } && !a.IsDeadBondedPet && !b.IsDeadBondedPet && ClassCombat.Friendly(a, b);
    public static bool Blocked(Mobile actor)
    {
        if (actor is not { Deleted: false, Alive: true, Map: { } map } || map == Map.Internal || actor.IsDeadBondedPet || !ClassCombat.Friendly(actor, actor)) { return false; }
        foreach (var other in map.GetMobilesInRange<Mobile>(actor.Location, 0))
        {
            if (PassThrough(actor, other) && actor.Z + 16 > other.Z && other.Z + 16 > actor.Z
                && PresenceOf(other).Order < PresenceOf(actor).Order) { return true; }
        }
        return false;
    }
    private static Presence PresenceOf(Mobile actor)
    {
        if (!_presence.TryGetValue(actor, out var state))
        {
            // Saved occupants have no arrival event; serial order gives a consistent initial holder.
            state = new Presence { Position = actor.Location, Map = actor.Map, Order = actor.Serial.Value };
            _presence.Add(actor, state);
        }
        if (state.Position != actor.Location || state.Map != actor.Map) { Arrived(actor); }
        return state;
    }
    private static void Arrived(Mobile actor)
    {
        if (actor.Map == null || actor.Map == Map.Internal || !actor.Alive || actor.IsDeadBondedPet) { _presence.Remove(actor); return; }
        if (!ClassCombat.Friendly(actor, actor)) { return; }
        var state = _presence.GetValue(actor, static _ => new Presence());
        state.Position = actor.Location; state.Map = actor.Map; state.Order = (1UL << 32) + ++_arrival;
        if (Blocked(actor)) { actor.NetState?.CancelAllTrades(); }
    }
    public static bool Allowed(Mobile actor) => !Blocked(actor);
    internal static bool UpdatePet(BaseCreature pet, long now)
    {
        if (pet.GetMaster() is not PlayerMobile { Deleted: false, Alive: true } owner || !ClassCombat.Enabled(owner)) { return false; }
        var state = _states.GetValue(pet, static _ => new State());
        if (!Blocked(pet)) { state.Active = state.Retreating = false; return false; }
        if (!state.Active || state.Map != pet.Map || state.Position != pet.Location)
        {
            if (!state.Active || state.Map != pet.Map) { state.Retreating = false; }
            state.Active = true; state.Map = pet.Map; state.Position = pet.Location; state.Since = now;
            if (pet is SkeletonGuardian guardian) { guardian.InterruptForRecovery(); }
        }
        if (now - state.Since >= RegroupMs && owner.Map == pet.Map
            && PersistentMinions.FindPosition(owner, out var regroup))
        {
            pet.AIObject?.ResetApproachState();
            pet.MoveToWorld(regroup, owner.Map); state.Active = false; return true;
        }
        if (!pet.Frozen && !pet.Paralyzed && CreatureHitRecovery.Remaining(pet) == 0
            && pet.AIObject?.CanMoveNow(out _) == true)
        {
            bool goalKnown = state.HasGoal && state.GoalMap == pet.Map;
            if (!state.Retreating && goalKnown && !pet.InRange(state.Goal, state.Range))
            {
                var forward = pet.GetDirectionTo(state.Goal); var next = pet.Location;
                Server.Movement.Movement.Offset(forward, ref next);
                if (Server.Movement.Movement.CheckMovement(pet, pet.Map, pet.Location, forward, out int z))
                {
                    next = new Point3D(next.X, next.Y, z);
                    bool stopping = Math.Max(Math.Abs(next.X - state.Goal.X), Math.Abs(next.Y - state.Goal.Y)) <= state.Range;
                    if (!stopping || Teleportation.Space(pet.Map, next, pet))
                    { pet.AIObject.DoMove(forward); return true; }
                }
            }
            state.Retreating = true;
            pet.AIObject.ClearMoveIntent();
            if (FindRoute(pet, goalKnown ? state.Goal : pet.Location, goalKnown ? state.Range : 0,
                out _, out var direction, escape: true)) { pet.AIObject.DoMove(direction); }
        }
        return true;
    }
    internal static void DisperseFollowers(PlayerMobile owner)
    {
        foreach (var follower in owner.AllFollowers ?? [])
        {
            if (follower is BaseCreature { Deleted: false, Alive: true } pet && pet.GetMaster() == owner
                && pet.Map == owner.Map && pet.Location == owner.Location
                && PersistentMinions.FindPosition(owner, out var point))
            { pet.AIObject?.ResetApproachState(); pet.MoveToWorld(point, owner.Map); }
        }
    }
    private static bool Managed(BaseCreature pet) => pet.GetMaster() is PlayerMobile owner && ClassCombat.Enabled(owner);
    private static void Remember(BaseCreature pet, IPoint3D goal, int range)
    {
        if (!Managed(pet) || goal == null) { return; }
        var state = _states.GetValue(pet, static _ => new State());
        state.Goal = new Point3D(goal); state.GoalMap = pet.Map; state.Range = range; state.HasGoal = true;
    }
    private static Point3D? FreeDestination(BaseCreature pet, IPoint3D goal)
    {
        if (!Managed(pet) || goal == null || pet.Map == null || pet.Map == Map.Internal) { return null; }
        var point = new Point3D(goal);
        if (Teleportation.Space(pet.Map, point, pet)) { return null; }
        return FindRoute(pet, point, 0, out var free, out _) ? free : pet.Location;
    }
    // Bounded local routing can retreat through a chain of allies without crossing walls or enemies.
    internal static bool FindRoute(BaseCreature pet, Point3D goal, int range, out Point3D free, out Direction first, bool escape = false)
    {
        const int radius = CombatProtocol.SightRange, width = radius * 2 + 1, capacity = width * width;
        Span<Point3D> points = stackalloc Point3D[capacity];
        Span<Direction> starts = stackalloc Direction[capacity];
        Span<int> steps = stackalloc int[capacity];
        Span<bool> seen = stackalloc bool[capacity]; seen.Clear();
        var origin = pet.Location; points[0] = origin; steps[0] = 0; starts[0] = pet.Direction; seen[radius * width + radius] = true;
        int read = 0, count = 1, best = -1, bestDistance = int.MaxValue, bestSteps = int.MaxValue;
        while (read < count)
        {
            int index = read++; var point = points[index];
            if (escape && steps[index] > bestSteps) { break; }
            if (Teleportation.Space(pet.Map, point, pet))
            {
                int distance = Math.Max(0, Math.Max(Math.Abs(point.X - goal.X), Math.Abs(point.Y - goal.Y)) - range);
                if (escape ? steps[index] < bestSteps || steps[index] == bestSteps && distance < bestDistance
                    : distance < bestDistance || distance == bestDistance && steps[index] < bestSteps)
                { best = index; bestDistance = distance; bestSteps = steps[index]; }
            }
            for (int d = 0; d < 8; d++)
            {
                var next = point; Server.Movement.Movement.Offset((Direction)d, ref next);
                int dx = next.X - origin.X + radius, dy = next.Y - origin.Y + radius;
                if (dx < 0 || dx >= width || dy < 0 || dy >= width || seen[dy * width + dx]
                    || !Server.Movement.Movement.CheckMovement(pet, pet.Map, point, (Direction)d, out int z)) { continue; }
                seen[dy * width + dx] = true;
                points[count] = new Point3D(next.X, next.Y, z); steps[count] = steps[index] + 1;
                starts[count] = index == 0 ? (Direction)d : starts[index]; count++;
            }
        }
        free = best < 0 ? origin : points[best]; first = best < 0 ? pet.Direction : starts[best];
        return best > 0;
    }

}
