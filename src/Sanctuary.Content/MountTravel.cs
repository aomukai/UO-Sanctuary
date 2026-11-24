// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ModernUO.Serialization;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class MountTravel
{
    internal const int HorsePrice = 5000;
    internal const int RetrievePrice = 100;
    private const int StableLimit = 5;
    private static readonly TimeSpan _abandonDelay = TimeSpan.FromHours(24);
    private static readonly ConditionalWeakTable<PlayerMobile, WarningTime> _warnings = new();
    private sealed class WarningTime { public long Combat; public long Travel; }
    internal static MountProfiles Profiles { get; private set; }

    public static void Configure()
    {
        Profiles = new();
        ClassMountHooks.Managed = Managed;
        ClassMountHooks.BlockTravel = BlocksTravel;
        ClassMountHooks.CanRide = CanRide;
        ClassMountHooks.RiderChanged = RiderChanged;
        ClassMountHooks.BeforeDeath = DropCargo;
        ClassMountHooks.ContextEntries = ContextEntries;
        ClassMountHooks.Feed = MountTaming.Feed;
        ClassMountHooks.CanUseTaming = MountTaming.CanUse;
        ClassMountHooks.TamingChanged = MountTaming.CheckUnlock;
        ClassMountHooks.CanTame = MountTaming.CanTame;
        ClassMountHooks.TameSkill = (_, creature) => MountTaming.RequiredSkill(creature);
        ClassMountHooks.Tamed = MountTaming.Completed;
        ClassMountHooks.CorpseInteraction = MountTaming.OfferRaise;
        ClassMountHooks.StableInteraction = StableInteraction;
        EventSink.Connected += mobile =>
        {
            if (mobile is PlayerMobile owner)
                Timer.StartTimer(TimeSpan.Zero, () =>
                {
                    MountTaming.CheckUnlock(owner);
                    SyncCompanion(owner);
                });
        };
        EventSink.Disconnected += mobile =>
        {
            if (mobile is PlayerMobile owner)
                RemoveCompanionHorse(MercenaryCare.ActiveStoryCompanion(owner));
        };
    }

    public static void Initialize()
    {
        Sweep();
        Timer.StartTimer(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), Sweep);
    }

    internal static bool Managed(BaseCreature pet) =>
        pet is CompanionTravelHorse || Profiles?.Mounts.ContainsKey(pet.Serial) == true;
    internal static MountRecord Find(BaseCreature pet) => Profiles.Mounts.GetValueOrDefault(pet.Serial);
    internal static bool IsActive(MountRecord record) => record is { Stable: null, Price: 0 };
    internal static BaseMount Active(PlayerMobile owner) =>
        Owned(owner).FirstOrDefault(entry => IsActive(entry.Record)).Mount;

    internal static IEnumerable<(BaseMount Mount, MountRecord Record)> Owned(PlayerMobile owner)
    {
        foreach (var (id, record) in Profiles.Mounts)
            if (record.Owner == owner.Serial && World.FindMobile(id) is BaseMount { Deleted: false } mount)
                yield return (mount, record);
    }

    internal static bool Adopt(PlayerMobile owner, BaseMount mount)
    {
        if (Active(owner) != null || mount.Deleted || mount.Controlled)
            return false;
        mount.ControlSlots = 0;
        if (!mount.SetControlMaster(owner))
            return false;
        CreatureProfiles.RestoreTamedMountLife(mount);
        if (!mount.Owners.Contains(owner))
            mount.Owners.Add(owner);
        mount.IsStabled = false;
        mount.IsBonded = false;
        mount.Tamable = false;
        mount.NoKillAwards = true;
        mount.FightMode = FightMode.None;
        mount.Combatant = null;
        mount.FocusMob = null;
        mount.ControlTarget = null;
        mount.ControlOrder = OrderType.Stay;
        Profiles.Mounts[mount.Serial] = new(owner.Serial, null, 0, Core.Now + _abandonDelay, DateTime.MinValue);
        return true;
    }

    internal static void BuyHorse(PlayerMobile owner, Mobile seller)
    {
        if (Active(owner) != null)
        {
            owner.SendMessage("Stable or list your active mount first.");
            return;
        }
        if (!FindLanding(owner, out var spot))
            return;

        var horse = new Horse();
        if (!Adopt(owner, horse) || !SanctuaryBanking.TryPay(owner, HorsePrice))
        {
            Forget(horse);
            owner.SendMessage($"A horse costs {HorsePrice:N0} gold.");
            return;
        }
        horse.MoveToWorld(spot, owner.Map);
        owner.SendMessage($"You bought a horse from {seller.Name}.");
    }

    internal static bool FindLanding(PlayerMobile owner, out Point3D spot)
    {
        if (owner.Map is { } map && map != Map.Internal
            && PersistentMinions.FindPosition(map, owner.Location,
                point => point != owner.Location && Utility.InRange(owner.Location, point, 2)
                    && Math.Abs(point.Z - owner.Z) <= 4 && map.LineOfSight(owner, point), out spot))
            return true;
        spot = default;
        owner.SendMessage("There is no room for a mount here.");
        return false;
    }

    private static bool CanRide(Mobile from, BaseMount mount)
    {
        if (mount is CompanionTravelHorse)
            return false;
        if (from is not PlayerMobile owner || Find(mount) is not { } record || record.Owner != owner.Serial)
            return true;
        if (mount is HellSteed && !MountTaming.IsNecromancer(owner))
        {
            owner.SendMessage("Only a necromancer can ride a hellsteed.");
            return false;
        }
        return IsActive(record) && !Expired(mount, record);
    }

    internal static bool RefuseCombat(PlayerMobile owner)
    {
        if (!owner.Mounted)
            return false;
        var warning = _warnings.GetOrCreateValue(owner);
        if (warning.Combat <= Core.TickCount)
        {
            warning.Combat = Core.TickCount + 1500;
            owner.SendMessage("You must dismount before you can fight.");
        }
        return true;
    }

    internal static bool BlocksTravel(Mobile from)
    {
        if (from is not PlayerMobile { Mounted: true } owner)
            return false;
        var warning = _warnings.GetOrCreateValue(owner);
        if (warning.Travel <= Core.TickCount)
        {
            warning.Travel = Core.TickCount + 1500;
            owner.SendMessage("Your mount refuses to go there.");
        }
        return true;
    }

    private static void RiderChanged(BaseMount mount, Mobile previous, Mobile current)
    {
        if (mount is CompanionTravelHorse)
            return;
        if (Find(mount) is { } record && IsActive(record))
            Profiles.Mounts[mount.Serial] = record with
            {
                AbandonAt = current == null ? Core.Now + _abandonDelay : DateTime.MinValue
            };
        if ((current ?? previous) is not PlayerMobile owner)
            return;
        ClassCombat.Stop(owner);
        owner.Combatant = null;
        PersistentMinions.Resume(owner);
        SyncCompanion(owner);
    }

    internal static void SyncCompanion(PlayerMobile owner)
    {
        var companion = MercenaryCare.ActiveStoryCompanion(owner);
        if (companion == null)
            return;
        if (!owner.Mounted || !owner.Alive || owner.NetState?.Running != true || companion.IsDeadPet || !companion.Alive
            || companion.Map != owner.Map || owner.Map == Map.Internal)
        {
            RemoveCompanionHorse(companion);
            return;
        }
        if (!companion.Mounted)
        {
            var horse = new CompanionTravelHorse { Direction = companion.Direction };
            horse.Rider = companion;
            companion.Combatant = null;
            companion.Warmode = false;
            companion.IssueOrder(OrderType.Follow, null, owner);
        }
    }

    internal static void RemoveCompanionHorse(BaseCreature companion)
    {
        if (companion?.Mount is CompanionTravelHorse horse)
        {
            horse.Rider = null;
            horse.Delete();
            if (companion.ControlMaster is PlayerMobile owner && companion.Alive && !companion.IsDeadPet)
                companion.IssueOrder(OrderType.Guard, null, owner);
        }
    }

    internal static void ClassChanged(PlayerMobile owner)
    {
        if (owner.Mount is HellSteed steed && !MountTaming.IsNecromancer(owner))
            steed.Rider = null;
        MountTaming.CheckUnlock(owner);
    }

    internal static bool Nearby(PlayerMobile owner, Mobile actor, int distance = 3) =>
        owner.Alive && actor is { Deleted: false } && owner.Map == actor.Map && owner.Map != Map.Internal
        && owner.InRange(actor, distance) && owner.CanSee(actor) && owner.InLOS(actor);

    internal static bool StableInteraction(Mobile from, BaseCreature stablehand)
    {
        if (from is not PlayerMobile owner)
            return false;
        if (!Nearby(owner, stablehand))
            return true;

        var home = stablehand.Spawner is Item marker ? marker.Location : stablehand.Home;
        if (home == Point3D.Zero)
            home = stablehand.Location;
        string prefix = $"{stablehand.Map.MapID}:";
        string key = $"{prefix}{home.X}:{home.Y}";
        var boarded = Owned(owner).Where(entry => entry.Record.Stable != null).ToArray();
        var existing = boarded.FirstOrDefault(entry => SameStable(entry.Record.Stable, prefix, home));
        var mount = Active(owner);
        if (mount != null && (mount.Rider == owner || Nearby(owner, mount)))
        {
            if (existing.Mount != null || boarded.Length >= StableLimit)
            {
                owner.SendMessage(existing.Mount != null ? "You already have a mount at this stable." : "You have five mounts in stables.");
                stablehand.Say(existing.Mount != null ? "My stall is full. Try another stable." : "Thou hast already filled five stalls.");
                return true;
            }
            Store(mount, Find(mount) with { Stable = key });
            owner.SendMessage($"{mount.Name} is now in this stable's care.");
        }
        else if (existing.Mount != null)
        {
            if (mount != null)
                owner.SendMessage("Stable your current mount before taking another.");
            else if (existing.Mount is HellSteed && !MountTaming.IsNecromancer(owner))
                owner.SendMessage("Return as a necromancer to collect your hellsteed.");
            else if (FindLanding(owner, out var spot))
            {
                if (!SanctuaryBanking.TryPay(owner, RetrievePrice))
                    owner.SendMessage($"It costs {RetrievePrice} gold to collect your mount.");
                else
                    Retrieve(owner, existing.Mount, existing.Record with { Stable = null }, spot);
            }
        }
        else
            owner.SendMessage(mount == null ? "You have no mount at this stable." : "Bring your mount to the stablemaster.");
        return true;
    }

    private static bool SameStable(string saved, string mapPrefix, Point3D home)
    {
        if (!saved.StartsWith(mapPrefix, StringComparison.Ordinal))
            return false;
        var coordinates = saved.AsSpan(mapPrefix.Length);
        int separator = coordinates.IndexOf(':');
        return separator > 0 && int.TryParse(coordinates[..separator], out int x)
            && int.TryParse(coordinates[(separator + 1)..], out int y)
            && Math.Abs(x - home.X) <= 10 && Math.Abs(y - home.Y) <= 10;
    }

    internal static void Store(BaseMount mount, MountRecord record)
    {
        mount.Rider = null;
        mount.IsStabled = true;
        mount.Internalize();
        Profiles.Mounts[mount.Serial] = record with { AbandonAt = DateTime.MinValue };
    }

    internal static void Retrieve(PlayerMobile owner, BaseMount mount, MountRecord record, Point3D spot)
    {
        Profiles.Mounts[mount.Serial] = record with { AbandonAt = Core.Now + _abandonDelay };
        mount.IsStabled = false;
        mount.ControlOrder = OrderType.Stay;
        mount.MoveToWorld(spot, owner.Map);
        owner.SendMessage($"{mount.Name} is waiting for you.");
    }

    private static bool ContextEntries(BaseCreature pet, Mobile from, ref PooledRefList<ContextMenuEntry> entries)
    {
        if (Find(pet) is not { } record)
            return pet is CompanionTravelHorse;
        if (from is PlayerMobile owner && record.Owner == owner.Serial && IsActive(record) && !Expired((BaseMount)pet, record)
            && (pet is not HellSteed || MountTaming.IsNecromancer(owner)))
            entries.Add(new DismissEntry());
        return true;
    }

    private sealed class DismissEntry() : ContextMenuEntry(6118, 3)
    {
        public override void OnClick(Mobile from, IEntity target)
        {
            if (from is PlayerMobile owner && target is BaseMount mount && Nearby(owner, mount)
                && Find(mount) is { } record && record.Owner == owner.Serial && IsActive(record))
                owner.SendGump(new MountConfirmation(owner, $"Dismiss {mount.Name} and relinquish ownership?", "Dismiss", () =>
                {
                    if (Nearby(owner, mount) && Find(mount) is { } now && now.Owner == owner.Serial && IsActive(now)
                        && mount.Rider == null)
                        Forget(mount);
                }));
        }
    }

    private static void DropCargo(BaseMount mount)
    {
        if (!Managed(mount) || mount is not (Beetle or FireBeetle) || !mount.IsBonded || mount.Map == Map.Internal
            || mount.Backpack == null)
            return;
        foreach (var item in mount.Backpack.Items.ToArray())
            item.MoveToWorld(mount.Location, mount.Map);
    }

    internal static bool ResurrectNearHealer(PlayerMobile owner, BaseCreature healer)
    {
        if (!Nearby(owner, healer) || healer is BaseHealer native && !native.CheckResurrect(owner)
            || Active(owner) is not { IsDeadPet: true, IsBonded: true } mount || !Nearby(owner, mount, 12))
            return false;
        mount.ResurrectPet();
        if (mount.IsDeadPet)
            return false;
        owner.SendMessage("Your mount has been restored to life.");
        return true;
    }

    private static bool Expired(BaseMount mount, MountRecord record) =>
        IsActive(record) && mount.Rider == null && record.AbandonAt != DateTime.MinValue && Core.Now >= record.AbandonAt;

    private static void Sweep()
    {
        foreach (var (id, record) in Profiles.Mounts.ToArray())
        {
            if (World.FindMobile(id) is not BaseMount { Deleted: false } mount)
                Profiles.Mounts.Remove(id);
            else if (World.FindMobile(record.Owner)?.Deleted != false || Expired(mount, record))
                Forget(mount);
        }
        foreach (var mount in World.Mobiles.Values.OfType<CompanionTravelHorse>().ToArray())
            if (mount.Rider is not BaseCreature { Alive: true, Deleted: false, IsDeadPet: false, ControlMaster: PlayerMobile { Mounted: true } })
                mount.Delete();
    }

    private static void Forget(BaseMount mount)
    {
        mount.Rider = null;
        Profiles.Mounts.Remove(mount.Serial);
        mount.Delete();
    }
}

[SerializationGenerator(0)]
public partial class CompanionTravelHorse : Horse
{
    public CompanionTravelHorse()
    {
        Tamable = false;
        FightMode = FightMode.None;
        Blessed = true;
    }
}
