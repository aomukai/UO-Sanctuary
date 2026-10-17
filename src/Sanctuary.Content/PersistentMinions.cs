// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Collections;
using Server.Mobiles;

namespace Sanctuary;

// Class minions opt in without changing their existing serialized inheritance chain.
public interface IPersistentMinion
{
    void SuspendActions();
}

public static class PersistentMinions
{
    public static void Configure()
    {
        ClassCombatHooks.PersistentMinion = pet => pet is IPersistentMinion;
        ClassCombatHooks.SuspendMinions = Suspend;
        ClassCombatHooks.ResumeMinions = Resume;
        EventSink.Disconnected += OnDisconnected;
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
        foreach (var follower in followers)
        {
            if (follower is BaseCreature { Deleted: false, Alive: true } pet and IPersistentMinion
                && pet.ControlMaster == owner && pet.Map == Map.Internal && FindPosition(owner, out var point))
            {
                pet.MoveToWorld(point, owner.Map);
                pet.IssueOrder(OrderType.Guard, null);
            }
        }
    }

    private static bool FindPosition(PlayerMobile owner, out Point3D point)
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

    public static void Dismiss(PlayerMobile owner)
    {
        using var pets = PooledRefList<BaseCreature>.Create();
        foreach (var follower in owner.AllFollowers ?? [])
        {
            if (follower is BaseCreature pet and IPersistentMinion && pet.ControlMaster == owner)
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
