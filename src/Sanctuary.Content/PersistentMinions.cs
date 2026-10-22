// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Collections;
using Server.Mobiles;

namespace Sanctuary;

// Class minions opt in without changing their existing serialized inheritance chain.
public interface IPersistentMinion
{
    DiabloClass SummonClass { get; }
    DateTime? ExpiresUtc => null;
    void SuspendActions();
}

public static class PersistentMinions
{
    public static MinionWardrobeProfiles Profiles { get; private set; }
    public static void Configure()
    {
        Profiles = new();
        ClassCombatHooks.PersistentMinion = pet => pet is IPersistentMinion;
        ClassCombatHooks.SuspendMinions = Suspend;
        ClassCombatHooks.ResumeMinions = Resume;
        EventSink.Disconnected += OnDisconnected;
    }

    public static void Initialize() => Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () => Profiles.Sweep(DateTime.UtcNow));
    internal static bool Active(PlayerMobile owner, BaseCreature pet) => pet is IPersistentMinion minion
        && ClassCreation.Profiles.TryGet(owner.Serial, out var profile) && profile.Class == minion.SummonClass;
    internal static bool Permitted(PlayerMobile owner, BaseCreature pet, DiabloClass characterClass)
    {
        if (pet is not IPersistentMinion minion || minion.SummonClass != characterClass) { return false; }
        if (pet is NecromancerMinion summon) { return summon.Permitted(owner); }
        if (pet is not SkeletonGuardian) { return true; }
        foreach (var history in ClassCreation.Profiles.Histories(owner.Serial))
        {
            if (history.Profile.Class == characterClass)
            { return history.Ranks.TryGetValue("necromancer.raiseskeleton", out var rank) && rank > 0; }
        }
        return false;
    }
    internal static void ParkClass(PlayerMobile owner, DiabloClass characterClass)
    {
        foreach (var follower in owner.AllFollowers ?? [])
        {
            if (follower is BaseCreature { Deleted: false } pet and IPersistentMinion minion && pet.ControlMaster == owner && minion.SummonClass == characterClass)
            { Park(pet); }
        }
    }
    private static void Park(BaseCreature pet)
    {
        var state = Profiles.Track(pet);
        if (state == null) { return; }
        if (!state.Parked)
        {
            pet.ControlMaster.Followers -= Math.Min(pet.ControlSlots, pet.ControlMaster.Followers);
            pet.ControlSlots = 0; Profiles.Store(pet, state with { Parked = true });
        }
        Suspend(pet);
    }
    internal static bool CanActivate(PlayerMobile owner, DiabloClass target)
    {
        int released = 0, returning = 0, skeletons = 0, limit = 0;
        foreach (var h in ClassCreation.Profiles.Histories(owner.Serial))
        { if (h.Profile.Class == target && h.Ranks.TryGetValue("necromancer.raiseskeleton", out var rank)) { limit = SkeletonPets.Limit(rank); } }
        foreach (var follower in owner.AllFollowers ?? [])
        {
            if (follower is not BaseCreature { Deleted: false, Alive: true } pet || pet is not IPersistentMinion minion || pet.ControlMaster != owner) { continue; }
            var state = Profiles.Track(pet); if (state == null) { return false; }
            if (state.ExpiresUtc is { } expires && DateTime.UtcNow >= expires) { continue; }
            if (Active(owner, pet) && !state.Parked) { released += pet.ControlSlots; }
            if (minion.SummonClass != target) { continue; }
            if (!Permitted(owner, pet, target)) { return false; }
            if (pet.GetType() == typeof(SkeletonGuardian)) { skeletons++; }
            returning += state.Slots;
        }
        return skeletons <= limit && owner.Followers - released + returning <= owner.FollowersMax;
    }
    private static void OnDisconnected(Mobile mobile)
    {
        if (mobile is PlayerMobile owner)
        {
            Suspend(owner);
        }
    }

    public static void Suspend(PlayerMobile owner)
    {
        foreach (var follower in owner.AllFollowers ?? [])
        {
            if (follower is BaseCreature { Deleted: false } pet and IPersistentMinion && pet.ControlMaster == owner)
            {
                Suspend(pet);
            }
        }
    }

    private static void Suspend(BaseCreature pet)
    {
        ((IPersistentMinion)pet).SuspendActions();
        pet.Combatant = null;
        pet.FocusMob = null;
        pet.ControlTarget = null;
        pet.ControlOrder = OrderType.Stay;
        pet.Warmode = false;
        // Retain the native control master/follower relationship so ownership is saved.
        if (pet.Map != Map.Internal)
        {
            pet.Internalize();
        }
    }

    public static bool CanThink(BaseCreature pet)
    {
        if (pet.ControlMaster is not PlayerMobile { Deleted: false, Alive: true } owner)
        {
            pet.Delete();
            return false;
        }
        var state = Profiles.Track(pet);
        if (state == null || state.ExpiresUtc is { } expires && DateTime.UtcNow >= expires) { pet.Delete(); return false; }
        if (!Active(owner, pet) || !Permitted(owner, pet, ((IPersistentMinion)pet).SummonClass)) { Park(pet); return false; }
        if (owner.NetState == null || owner.Map == null || owner.Map == Map.Internal)
        {
            Suspend(pet);
            return false;
        }
        return pet.Map != Map.Internal;
    }

    // Runs after all mobile references have loaded; orphaned minions cannot be claimed.
    public static void Restore(BaseCreature pet)
    {
        if (pet.Deleted)
        {
            return;
        }
        if (pet.ControlMaster is not PlayerMobile { Deleted: false, Alive: true } owner)
        {
            pet.Delete();
            return;
        }
        var state = Profiles.Track(pet);
        if (state == null) { pet.Delete(); return; }
        if (!Active(owner, pet) || !Permitted(owner, pet, ((IPersistentMinion)pet).SummonClass)) { Park(pet); return; }
        Suspend(pet);
        if (owner.NetState != null)
        {
            Resume(owner);
        }
    }

    public static void Resume(PlayerMobile owner)
    {
        if (owner.Deleted || !owner.Alive || owner.Map == null || owner.Map == Map.Internal
            || owner.AllFollowers is not { Count: > 0 } followers)
        {
            return;
        }
        using var snapshot = PooledRefList<BaseCreature>.Create();
        foreach (var follower in followers) { if (follower is BaseCreature pet) { snapshot.Add(pet); } }
        foreach (var follower in snapshot)
        {
            if (follower is not BaseCreature { Deleted: false, Alive: true } pet || pet is not IPersistentMinion minion || pet.ControlMaster != owner) { continue; }
            var state = Profiles.Track(pet); if (state == null) { continue; }
            if (state.ExpiresUtc is { } expires && DateTime.UtcNow >= expires) { pet.Delete(); continue; }
            if (!Active(owner, pet) || !Permitted(owner, pet, minion.SummonClass)) { Park(pet); continue; }
            if (state.Parked)
            {
                if (owner.Followers + state.Slots > owner.FollowersMax) { continue; }
                pet.ControlSlots = state.Slots; owner.Followers += state.Slots;
                Profiles.Store(pet, state with { Parked = false });
            }
            if (pet.Map == Map.Internal && FindPosition(owner, out var point))
            { pet.MoveToWorld(point, owner.Map); pet.IssueOrder(OrderType.Guard, null); }
        }
    }

    internal static bool FindPosition(PlayerMobile owner, out Point3D point)
    {
        // A blocked login spot leaves the minion parked; accepted movement retries.
        for (int radius = 1; radius <= 4; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                    {
                        continue;
                    }
                    int x = owner.X + dx, y = owner.Y + dy;
                    point = new Point3D(x, y, owner.Z);
                    if (owner.Map.CanSpawnMobile(point) && owner.InLOS(point))
                    {
                        return true;
                    }
                    int z = owner.Map.GetAverageZ(x, y);
                    point = new Point3D(x, y, z);
                    if (Math.Abs(z - owner.Z) < 16 && owner.Map.CanSpawnMobile(point) && owner.InLOS(point))
                    {
                        return true;
                    }
                }
            }
        }
        point = default;
        return false;
    }

    public static void Dismiss(PlayerMobile owner, bool activeOnly = false)
    {
        using var pets = PooledRefList<BaseCreature>.Create();
        foreach (var follower in owner.AllFollowers ?? [])
        {
            if (follower is BaseCreature pet and IPersistentMinion && pet.ControlMaster == owner && (!activeOnly || Active(owner, pet)))
            {
                pets.Add(pet);
            }
        }
        foreach (var pet in pets)
        {
            pet.Delete();
        }
    }
}
