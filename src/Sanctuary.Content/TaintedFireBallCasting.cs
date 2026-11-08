// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record TaintedCastClock(int Skill, int Mode, int Frames, int Speed, int Action, int Flag);
internal sealed record TaintedFireBallAction(uint Actor, uint Owner, uint Target, int Rank, int Speed,
    DateTime StartedUtc, DateTime ActionUtc, DateTime EndUtc, bool Released = false, long Flight = 0, string Fault = null,
    bool LiveRank = false, int ReleasedRank = 0);

// NPC A2 action service. Selection and mode admission remain caller services.
// Registered casts use the shared live innate getter; explicit prepared casts
// retain supplied source inputs. No replacement targeting AI is invented.
// Unissued actions are transient, as with player pending actions. Issued
// missiles use their existing independent persistent host.
internal static class TaintedFireBallCasting
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(TaintedFireBallCasting));
    private static readonly Dictionary<uint, TaintedFireBallAction> Actions = new();
    private static readonly Lazy<TaintedCastClock> Clock = new(() =>
    {
        var rows = BalanceCatalog.Read<TaintedCastClock>("tainted-fireball-cast");
        if (rows.Length != 1 || rows[0] != new TaintedCastClock(422, 5, 10, 256, 6, 2))
        { throw new NotSupportedException("Unsupported Tainted A2 Fire Ball animation"); }
        return rows[0];
    });
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40), () => Tick(TimedEffects.Now));
    internal static TaintedFireBallAction State(WarlockTaintedBody body) => body == null ? null : Actions.GetValueOrDefault(body.Serial.Value);
    private static void Remove(uint actor,WarlockTaintedBody body)
    {if(Actions.Remove(actor)&&body!=null){DeathMarkWorld.FinishAction(body,5);}}
    internal static void Cancel(WarlockTaintedBody body) { if (body != null) { Remove(body.Serial.Value,body); } }
    private static bool Attached(WarlockTaintedBody body, PlayerMobile owner) =>
        body is { Alive: true, Deleted: false } && owner is { Alive: true, Deleted: false } && body.Prepared?.Owner == owner.Serial.Value
        && body.ControlMaster == owner && body.Map != null && body.Map != Map.Internal && owner.Map == body.Map
        && PersistentMinions.Active(owner, body) && body.Permitted(owner) && !SpellHelper.IsTown(owner.Location, owner);
    internal static (int Action, int End, int Speed) Timing(WarlockTaintedBody body)
    {
        var row = Clock.Value;
        if (body?.Prepared == null) { throw new NotSupportedException("Tainted A2 clock requires its captured NPC attack rate"); }
        // Captured summon/mastery attackrate plus this NPC's current haste and
        // slows. Owner weapon IAS, FCR and weapon-speed modifiers are absent.
        int speed = SourceAttackRate.Speed(row.Speed, 0, checked(SourceAttackRate.WolfRate(body) + body.Prepared.Snapshot.Birth.Suppliers.AttackSpeed));
        var flags = new int[144]; flags[row.Action] = row.Flag;
        var plan = SourceAnimationEvents.Initial(new(0, speed, row.Frames * 256, flags, []), 0);
        return (plan.Events.Single(e => e.Type == SourceAnimationEvents.ModeChange && e.Kind == 2).Frame,
            plan.Events.Single(e => e.Type == SourceAnimationEvents.EndAnimation).Frame, speed);
    }
    internal static bool BeginPrepared(WarlockTaintedBody body, Mobile target, int suppliedRank, DateTime at)
    {
        if (at.Kind != DateTimeKind.Utc || at > DateTime.MaxValue.AddMinutes(-1)) { throw new ArgumentException("Tainted A2 start requires UTC"); }
        var owner = body?.ControlMaster as PlayerMobile;
        if (!Attached(body, owner) || suppliedRank is < 1 or > 255 || State(body) != null
            || target is not { Alive: true, Deleted: false } || target.Map != body.Map || !ClassCombat.IsHostile(owner, target)
            || !body.CanBeHarmful(target, false)) { return false; }
        var clock = Timing(body);
        var state = new TaintedFireBallAction(body.Serial.Value, owner.Serial.Value, target.Serial.Value, suppliedRank, clock.Speed,
            at, at.AddMilliseconds(clock.Action * 40), at.AddMilliseconds(clock.End * 40));
        DeathMarkWorld.ChangeMode(body,Clock.Value.Mode);
        Actions.Add(body.Serial.Value, state); body.Direction = body.GetDirectionTo(target); return true;
    }
    internal static bool BeginRegistered(WarlockTaintedBody body, Mobile target, DateTime at)
    {
        int rank = TaintedNpcRanks.Level(body, 422);
        if (!BeginPrepared(body, target, rank, at)) { return false; }
        Actions[body.Serial.Value] = State(body) with { LiveRank = true }; return true;
    }
    // Bind the supported targeted NPC422 path into the existing captured
    // GenericPet action wrapper. Other skills keep the supplied outer service.
    // The caller still owns source mode admission and effective skill lookup.
    internal static GenericPetActionServices Bind(WarlockTaintedBody body, GenericPetActionServices outer, Func<int> rank,
        Func<Mobile, int, bool> admit, Func<DateTime> now)
    {
        ArgumentNullException.ThrowIfNull(outer); ArgumentNullException.ThrowIfNull(rank); ArgumentNullException.ThrowIfNull(admit); ArgumentNullException.ThrowIfNull(now);
        return outer with { ExecuteSkill = (mode, skill, target, x, y, flag) =>
        {
            if (skill != 422) { return outer.ExecuteSkill(mode, skill, target, x, y, flag); }
            if (mode != Clock.Value.Mode || !flag || x != 0 || y != 0 || target == 0)
            { throw new NotSupportedException("Tainted Fire Ball ground override or non-A2 request needs its source mode adapter"); }
            var unit = World.FindMobile((Serial)unchecked((uint)target)); int level = rank();
            return admit(unit, level) && BeginPrepared(body, unit, level, now());
        } };
    }
    internal static GenericPetActionServices BindRegistered(WarlockTaintedBody body, GenericPetActionServices outer,
        Func<Mobile, int, bool> admit, Func<DateTime> now)
    {
        // Reuse the same mode/target guard and caller-owned admission contract.
        var bound = Bind(body, outer, () => TaintedNpcRanks.Level(body, 422), admit, now);
        return bound with { ExecuteSkill = (mode, skill, target, x, y, flag) =>
        {
            bool accepted = bound.ExecuteSkill(mode, skill, target, x, y, flag);
            if (accepted && skill == 422) { Actions[body.Serial.Value] = State(body) with { LiveRank = true }; }
            return accepted;
        } };
    }
    internal static void Tick(DateTime at, bool? allowActions = null)
    {
        if (at.Kind != DateTimeKind.Utc) { throw new ArgumentException("Tainted A2 clock must be UTC"); }
        foreach (var state in Actions.Values.OrderBy(s => s.Released ? s.EndUtc : s.ActionUtc).ThenBy(s => s.Actor).ToArray())
        {
            if ((state.Released ? state.EndUtc : state.ActionUtc) > at) { continue; }
            var body = World.FindMobile((Serial)state.Actor) as WarlockTaintedBody;
            var owner = World.FindMobile((Serial)state.Owner) as PlayerMobile;
            if (!Attached(body, owner) || !(allowActions ?? owner.NetState != null)) { Remove(state.Actor,body); continue; }
            var next = state;
            if (!state.Released)
            {
                // Mark the event before attempting allocation: a failed or
                // partially observed release is never retried for this action.
                next = state with { Released = true }; Actions[state.Actor] = next;
                try
                {
                    if (World.FindMobile((Serial)state.Target) is not { Alive: true, Deleted: false } target
                        || target.Map != body.Map || !ClassCombat.IsHostile(owner, target) || !body.CanBeHarmful(target, false))
                    { throw new NotSupportedException("Lost Tainted A2 target; current source path-point fallback is unverified"); }
                    int rank = state.LiveRank ? TaintedNpcRanks.Level(body, 422) : state.Rank;
                    var suppliers = body.Prepared.Snapshot.Birth.Suppliers with { FireBallRank = rank };
                    long id = TaintedFireBallMissiles.LaunchPrepared(body, target.Location, rank, TaintedNpcSkills.FireBall(suppliers), state.ActionUtc);
                    next = next with { Flight = id, ReleasedRank = rank }; Actions[state.Actor] = next;
                }
                catch (Exception e)
                { next = next with { Fault = e.Message }; Actions[state.Actor] = next; Log.Error(e, "Tainted A2 release suspended: {Actor}", state.Actor); }
            }
            if (next.EndUtc <= at) { Remove(state.Actor,body); }
        }
    }
}
