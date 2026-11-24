// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed record MercenaryCareState(Serial Owner, int Level, bool Retired, long[] Healing, DateTime NextDrinkUtc)
{
    internal bool Parked { get; init; }
    internal bool Active => !Retired && !Parked;
}
public sealed class MercenaryServiceRequest
{
    internal bool Claimed;
    internal PlayerMobile Owner;
    internal BaseCreature Mercenary, Healer;
    internal MercenaryCareState State;
    internal NetState Connection;
    public bool Resurrection { get; internal init; }
    public int Gold { get; internal init; }
}

// Recruitment registers real companions here after their class/skill implementation exists.
public static class MercenaryCare
{
    private const int CompanionCombatRange = 12;
    internal static readonly Dictionary<Serial, MercenaryCareState> States = new();
    private static readonly HashSet<Serial> _paidResurrection = new();
    private static readonly List<Serial> _keys = new();
    public static MercenaryCareProfiles Profiles { get; private set; }
    public static void Configure()
    {
        Profiles = new();
        ClassCombatHooks.CompanionRangedSwing = (actor, target, weapon) => actor switch
        {
            Iolo { Recruited: true } iolo => iolo.BeginShot(target, weapon),
            Merydwen { Recruited: true } merydwen => merydwen.BeginShot(target, weapon),
            _ => null
        };
        ClassMercenaryHooks.RestoredOrder = (owner, pet) =>
            pet is IClassCompanion && !pet.IsDeadPet && IsCompanion(owner, pet) ? OrderType.Guard : null;
        EventSink.Connected += owner =>
        {
            if (owner is PlayerMobile player)
                Timer.StartTimer(TimeSpan.Zero, () => SyncOwner(player));
        };
        ClassMercenaryHooks.KeepEquipment = merc => States.ContainsKey(merc.Serial);
        ClassMercenaryHooks.ResurrectionAllowed = merc => !States.ContainsKey(merc.Serial) || _paidResurrection.Contains(merc.Serial);
        ClassMercenaryHooks.ContextEntries = AddEntries;
        ClassMercenaryHooks.ReleaseOrder = (mercenary, issuer) =>
        {
            if (!States.ContainsKey(mercenary.Serial)) { return false; }
            if (issuer is PlayerMobile owner && mercenary.ControlMaster == owner)
            {
                if (mercenary is IClassCompanion)
                    owner.SendMessage($"{mercenary.Name} has promised to travel with thee.");
                else
                    Retire(owner, mercenary);
            }
            return true;
        };
        ClassMercenaryHooks.TradeOpened = (npc, from) => { if (from is PlayerMobile owner) { HealFollowers(owner, npc); } };
        ClassMercenaryHooks.Conversation = (npc, from, speech) =>
        {
            if (from is PlayerMobile owner && speech != null &&
                (speech.Equals("hello", StringComparison.OrdinalIgnoreCase) || speech.Equals("hi", StringComparison.OrdinalIgnoreCase)
                    || speech.Equals("heal", StringComparison.OrdinalIgnoreCase) || speech.Equals("help", StringComparison.OrdinalIgnoreCase)
                    || speech.Equals("buy", StringComparison.OrdinalIgnoreCase) || speech.Equals("sell", StringComparison.OrdinalIgnoreCase)
                    || speech.StartsWith("vendor ", StringComparison.OrdinalIgnoreCase)
                    || npc.Name != null && speech.StartsWith(npc.Name + " ", StringComparison.OrdinalIgnoreCase)))
            { HealFollowers(owner, npc); }
        };
    }
    public static void Initialize()
    {
        // Native pet loading starts a deletion timer for uncontrolled former followers.
        foreach (var (serial, state) in States)
            if (state.Parked && World.FindMobile(serial) is BaseCreature companion)
                companion.StopDeleteTimer();

        Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () => Tick(TimedEffects.Now));
        Timer.StartTimer(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30), CatchUpMountedCompanions);
    }
    public static int ResurrectionPrice(int level) => level is >= 1 and <= 99 ? level * 100 : throw new ArgumentOutOfRangeException(nameof(level));
    internal static bool HasActiveCompanion(PlayerMobile owner)
    {
        foreach (var row in States.Values)
            if (row.Owner == owner.Serial && row.Active)
                return true;
        return false;
    }
    internal static bool HasContract<T>(PlayerMobile owner) where T : BaseCreature, IClassCompanion
    {
        foreach (var (serial, state) in States)
            if (state.Owner == owner.Serial && !state.Retired && World.FindMobile(serial) is T { Deleted: false } companion && companion.GetType() == typeof(T))
                return true;
        return false;
    }

    internal static IEnumerable<BaseCreature> ParkedCompanions(PlayerMobile owner)
    {
        foreach (var (serial, state) in States)
            if (state.Owner == owner.Serial && state.Parked
                && World.FindMobile(serial) is BaseCreature { Deleted: false, Controlled: false } companion
                && companion is IClassCompanion && companion.Map == Map.Internal)
                yield return companion;
    }

    internal static bool ParkAtInn(PlayerMobile owner, BaseCreature companion)
    {
        if (companion is not IClassCompanion || !Owned(owner, companion, out var state))
            return false;

        States[companion.Serial] = state with { Parked = true };
        MountTravel.RemoveCompanionHorse(companion);
        TimedEffects.Clear(companion);
        companion.Combatant = null;
        companion.FocusMob = null;
        companion.Warmode = false;
        companion.AIObject?.ResetApproachState();
        companion.SetControlMaster(null);
        companion.StopDeleteTimer();
        companion.Internalize();
        return true;
    }

    internal static bool CallFromInn(PlayerMobile owner, BaseCreature companion, Map map, Point3D landing,
        Point3D alongside, int fee)
    {
        if (companion is not IClassCompanion || companion.Deleted || companion.Controlled || companion.Map != Map.Internal
            || !States.TryGetValue(companion.Serial, out var state) || state.Owner != owner.Serial || !state.Parked
            || HasActiveCompanion(owner) || !companion.SetControlMaster(owner))
            return false;

        if (!SanctuaryBanking.TryPay(owner, fee))
        {
            companion.SetControlMaster(null);
            companion.StopDeleteTimer();
            return false;
        }

        state = state with { Parked = false };
        States[companion.Serial] = state;
        Sync(owner, companion, state);
        ClassCombat.Stop(owner);
        BaseCreature.TeleportPets(owner, landing, map);
        owner.MoveToWorld(landing, map);
        companion.MoveToWorld(alongside, map);
        companion.ControlTarget = owner;
        companion.ControlOrder = companion.IsDeadPet ? OrderType.Follow : OrderType.Guard;
        return true;
    }

    internal static bool Register(PlayerMobile owner, BaseCreature mercenary, int level, Func<bool> pay = null)
    {
        if (owner is not { Deleted: false } || mercenary is not { Deleted: false, Summoned: false, Controlled: true }
            || mercenary.ControlMaster != owner || level is < 1 or > 99 || States.ContainsKey(mercenary.Serial)) { return false; }
        if (HasActiveCompanion(owner)) { return false; }
        mercenary.IsBonded = true;
        mercenary.NoKillAwards = true;
        var state = new MercenaryCareState(owner.Serial, level, false, new long[5], TimedEffects.Now);
        States.Add(mercenary.Serial, state);
        bool accepted = false;
        try
        {
            Sync(owner, mercenary, state, joining: true);
            accepted = pay == null || pay();
            return accepted;
        }
        finally
        {
            if (!accepted)
                States.Remove(mercenary.Serial);
        }
    }
    internal static int? CompanionLevel(BaseCreature actor) =>
        actor is IClassCompanion && States.TryGetValue(actor.Serial, out var state) && state.Active ? state.Level : null;

    internal static BaseCreature ActiveStoryCompanion(PlayerMobile owner)
    {
        foreach (var (serial, state) in States)
            if (state.Owner == owner.Serial && state.Active && World.FindMobile(serial) is BaseCreature actor and IClassCompanion)
                return actor;
        return null;
    }

    internal static void RestoreCompanionForGhost(PlayerMobile owner)
    {
        // The native login leaves auto-stabled followers behind while their owner is dead.
        if (owner.Alive || owner.Deleted || owner.Map is not { } map || map == Map.Internal
            || owner.AutoStabled is not { Count: > 0 } stabled
            || ActiveStoryCompanion(owner) is not { Deleted: false, Controlled: false, IsStabled: true } companion
            || companion.StabledBy != owner || !stabled.Contains(companion) || !companion.SetControlMaster(owner))
            return;

        stabled.Remove(companion);
        owner.Stabled?.Remove(companion);
        companion.IsStabled = false;
        companion.StabledBy = null;
        companion.Loyalty = BaseCreature.MaxLoyalty;
        companion.ControlTarget = owner;
        companion.ControlOrder = companion.IsDeadPet ? OrderType.Follow : OrderType.Guard;
        companion.MoveToWorld(PersistentMinions.FindPosition(owner, out var nearby) ? nearby : owner.Location, map);
    }

    internal static BaseCreature SyncOwner(PlayerMobile owner)
    {
        foreach (var (serial, state) in States)
            if (state.Owner == owner.Serial && state.Active && World.FindMobile(serial) is BaseCreature actor
                && (Owned(owner, actor, out _) || actor.IsStabled && actor.StabledBy == owner))
            {
                Sync(owner, actor, state);
                return actor;
            }
        return null;
    }

    private static void Sync(PlayerMobile owner, BaseCreature actor, MercenaryCareState state, bool joining = false)
    {
        if (actor is not IClassCompanion build || !ClassCreation.Profiles.TryGet(owner.Serial, out var profile))
            return;
        if (actor is Wolfgang wolfgang)
            wolfgang.PrepareForDuty();
        CompanionBuild.Apply(actor, build.CombatClass, profile.Level, joining);
        if (state.Level != profile.Level)
            States[actor.Serial] = state with { Level = profile.Level };
    }
    public static long Stock(BaseCreature mercenary)
    {
        if (!States.TryGetValue(mercenary.Serial, out var state)) { return 0; }
        long total = 0;
        foreach (long amount in state.Healing)
            total += amount;
        if (mercenary is (Wolfgang or Merydwen) && mercenary.Backpack is { } pack)
            foreach (var item in pack.FindItems())
                if (PackHealingTier(item) > 0)
                    total = Math.Min(total, long.MaxValue - item.Amount) + item.Amount;
        return total;
    }
    private static int PackHealingTier(Item item) => item switch
    {
        RestorationPotion { Kind: RestorationKind.Healing, Tier: >= 1 and <= 5 } potion => potion.Tier,
        LesserHealPotion => 1,
        HealPotion => 2,
        GreaterHealPotion => 3,
        _ => 0
    };
    private static bool Owned(PlayerMobile owner, BaseCreature mercenary, out MercenaryCareState state)
    {
        state = null;
        return owner is { Deleted: false } && mercenary is { Deleted: false, Controlled: true, IsBonded: true }
            && mercenary.ControlMaster == owner && States.TryGetValue(mercenary.Serial, out state) && state.Owner == owner.Serial && state.Active;
    }
    internal static bool IsCompanion(PlayerMobile owner,BaseCreature pet)=>Owned(owner,pet,out _);

    internal static bool WithinCompanionLeash(PlayerMobile owner, BaseCreature companion, Mobile target) =>
        owner is { Alive: true, Mounted: false, Map: { } map } && map != Map.Internal && companion.Map == map
        && target?.Map == map && owner.InRange(companion, CompanionCombatRange) && owner.InRange(target, CompanionCombatRange)
        && Math.Abs(owner.Z - companion.Z) < 16 && Math.Abs(owner.Z - target.Z) < 16;

    internal static bool? GuardTarget(BaseCreature companion, Mobile target)
    {
        if (companion is not IClassCompanion || companion.ControlMaster is not PlayerMobile owner
            || !IsCompanion(owner, companion))
            return null;

        if (!WithinCompanionLeash(owner, companion, target) || !ClassCombat.IsHostile(owner, target)
            || ClassCombatHooks.TargetAllowed?.Invoke(companion, target) == false)
            return false;

        // Native Guard already covers enemies attacking the companion or owner. Also assist their other followers.
        if (OnOwnersSide(owner, target.Combatant))
            return true;
        foreach (var aggression in target.Aggressors)
            if (!aggression.Expired && OnOwnersSide(owner, aggression.Attacker))
                return true;

        if (companion is Wolfgang && owner.InRange(target, 8) && owner.InLOS(target))
            return true;

        return null;
    }

    private static bool OnOwnersSide(PlayerMobile owner, Mobile actor) =>
        actor == owner || actor is BaseCreature creature && creature.GetMaster() == owner;

    private static bool Near(Mobile first, Mobile second, int range = 3) => first.Map != Map.Internal && first.Map == second.Map
        && first.InRange(second, range) && Math.Abs(first.Z - second.Z) < 16 && first.InLOS(second);
    public static bool Deposit(PlayerMobile owner, BaseCreature mercenary, RestorationPotion potion)
    {
        if (!Owned(owner, mercenary, out var state) || !owner.Alive || !Near(owner, mercenary)
            || potion is not { Deleted: false, Movable: true, Amount: > 0, Kind: RestorationKind.Healing, Tier: >= 1 and <= 5 }
            || owner.Backpack == null || !(potion.IsChildOf(owner.Backpack) || owner.Holding == potion)
            || !potion.VerifyMove(owner) || potion.IsLockedDown || potion.IsSecure || Stock(mercenary) > long.MaxValue - potion.Amount) { return false; }
        var amounts = (long[])state.Healing.Clone(); amounts[potion.Tier - 1] += potion.Amount;
        States[mercenary.Serial] = state with { Healing = amounts }; potion.Consume(potion.Amount); return true;
    }
    internal static bool TryDrink(BaseCreature mercenary, DateTime now)
    {
        if (!States.TryGetValue(mercenary.Serial, out var state) || !state.Active || mercenary.Deleted || mercenary.IsDeadPet
            || !mercenary.Alive || mercenary.Map == Map.Internal || mercenary.ControlMaster?.Serial != state.Owner
            || mercenary.HitsMax <= 0 || now < state.NextDrinkUtc) { return false; }
        TimedEffects.Settle(mercenary, now);
        if (mercenary.IsDeadPet || !mercenary.Alive || (long)mercenary.Hits * 2 > mercenary.HitsMax) { return false; }
        int tier = Array.FindIndex(state.Healing, count => count > 0) + 1;
        Item packedDose = null;
        if (mercenary is (Wolfgang or Merydwen) && mercenary.Backpack is { } pack)
            foreach (var item in pack.FindItems())
            {
                int candidate = PackHealingTier(item);
                if (candidate > 0 && (tier == 0 || candidate < tier))
                {
                    tier = candidate;
                    packedDose = item;
                }
            }
        if (tier == 0) { return false; }
        var potion = PotionBeltCatalog.Potion(RestorationKind.Healing, tier);
        if (!TimedEffects.AddRestoration(mercenary, true, potion.Amount, potion.DurationMs, now)) { return false; }
        var amounts = state.Healing;
        if (packedDose != null)
            packedDose.Consume(1);
        else
        {
            amounts = (long[])amounts.Clone();
            amounts[tier - 1]--;
        }
        States[mercenary.Serial] = state with { Healing = amounts, NextDrinkUtc = now.AddSeconds(10) };
        long remaining = Stock(mercenary);
        if (remaining is 9 or 5 or 1 && World.FindMobile(state.Owner) is PlayerMobile owner)
        { owner.SendMessage($"Your mercenary has {remaining} healing potions left."); }
        mercenary.PlaySound(0x2D6); return true;
    }
    private static void Tick(DateTime now)
    {
        _keys.Clear(); foreach (var serial in States.Keys) { _keys.Add(serial); }
        foreach (var serial in _keys)
        {
            if (World.FindMobile(serial) is BaseCreature mercenary
                && mercenary.ControlMaster is PlayerMobile { NetState: not null } owner)
            {
                MountTravel.SyncCompanion(owner);
                UpdateCompanionRetreat(owner, mercenary);
                TryDrink(mercenary, now);
            }
        }
    }

    internal static void UpdateCompanionRetreat(PlayerMobile owner, BaseCreature companion)
    {
        if (companion is not IClassCompanion || !IsCompanion(owner, companion)
            || companion is not { Alive: true, IsDeadPet: false, IsStabled: false }
            || owner is not { Alive: true, Mounted: false, Map: { } map } || map == Map.Internal || companion.Map != map
            || !(companion.ControlOrder is OrderType.Guard or OrderType.Attack
                || companion.ControlOrder == OrderType.Follow && companion.ControlTarget == owner))
            return;

        var target = companion.ControlOrder == OrderType.Attack ? companion.ControlTarget : companion.Combatant;
        Point3D spot = default;
        bool catchUp = !Utility.InUpdateRange(owner.Location, companion.Location)
            && PersistentMinions.FindPosition(owner, out spot);
        if (!catchUp && (target == null || WithinCompanionLeash(owner, companion, target)))
            return;

        ClearCompanionCombat(companion);
        if (companion.ControlOrder == OrderType.Attack)
            companion.IssueOrder(OrderType.Guard, null, owner);
        if (catchUp)
            companion.MoveToWorld(spot, map);
    }

    private static void ClearCompanionCombat(BaseCreature companion)
    {
        companion.Combatant = null;
        companion.FocusMob = null;
        companion.Warmode = false;
        companion.AIObject?.ResetApproachState();
    }

    private static void CatchUpMountedCompanions()
    {
        foreach (var (serial, state) in States)
        {
            if (!state.Active || World.FindMobile(serial) is not BaseCreature { IsStabled: false, Map: { } companionMap } companion
                || companion is not IClassCompanion || companionMap == Map.Internal
                || !(companion.Alive || companion.IsDeadBondedPet)
                || companion.ControlMaster is not PlayerMobile { Alive: true, Mounted: true, Map: { } map, NetState.Running: true } owner
                || map == Map.Internal || !IsCompanion(owner, companion)
                || companionMap == map && Utility.InUpdateRange(owner.Location, companion.Location))
                continue;

            ClearCompanionCombat(companion);
            companion.MoveToWorld(owner.Location, map);
            companion.IssueOrder(OrderType.Follow, null, owner);
            MountTravel.SyncCompanion(owner);
        }
    }

    internal static bool Retire(PlayerMobile owner, BaseCreature mercenary, bool keepPotions = false)
    {
        if (!Owned(owner, mercenary, out var state)) { return false; }
        MountTravel.RemoveCompanionHorse(mercenary);
        TimedEffects.Clear(mercenary); mercenary.Combatant = null;
        States[mercenary.Serial] = state with { Retired = true, Healing = keepPotions ? state.Healing : new long[5] };
        mercenary.SetControlMaster(null);
        // Keep the real gear on its saved carrier until it can be returned; never delete it with the contract.
        mercenary.Internalize(); return true;
    }
    internal static bool RecoverRetiredGear(PlayerMobile owner, BaseCreature mercenary, Item item) =>
        States.TryGetValue(mercenary.Serial, out var state) && state.Owner == owner.Serial && state.Retired
        && owner is { Alive: true, Deleted: false, Backpack: not null } && owner.Holding == null
        && item is { Deleted: false, Movable: true } && item != mercenary.Backpack
        && (item.Parent == mercenary || mercenary.Backpack != null && item.IsChildOf(mercenary.Backpack))
        && item.Layer is not (Layer.Hair or Layer.FacialHair) && owner.Backpack.TryDropItem(owner, item, false);

    internal static void RecoverRetiredPotions(PlayerMobile owner, BaseCreature mercenary)
    {
        if (!States.TryGetValue(mercenary.Serial, out var state) || state.Owner != owner.Serial || !state.Retired
            || owner is not { Alive: true, Backpack: not null, Holding: null })
            return;
        for (int i = 0; i < state.Healing.Length; i++)
        {
            while (state.Healing[i] > 0)
            {
                var potion = new RestorationPotion(RestorationKind.Healing, i + 1)
                {
                    Amount = (int)Math.Min(60_000, state.Healing[i])
                };
                int amount = potion.Amount;
                if (!owner.Backpack.TryDropItem(owner, potion, false))
                {
                    potion.Delete();
                    return;
                }
                state.Healing[i] -= amount;
            }
        }
    }
    public static bool Provider(BaseCreature healer) => healer is BaseHealer or HealerGuildmaster;
    public static int HealFollowers(PlayerMobile owner, BaseCreature npc, bool includeParked = false)
    {
        if (!ClassProfessions.Affected(owner) || !ClassProfessions.Ready(owner) || npc is not { Deleted: false, Alive: true }
            || !(Provider(npc) || npc is BaseVendor { IsActiveVendor: true }) || !Near(owner, npc) || !Peaceful(owner) || !Peaceful(npc)
            || npc is BaseVendor vendor && !vendor.CheckVendorAccess(owner) || npc is BaseHealer healer && !healer.CheckResurrect(owner)) { return 0; }
        int healed = 0;using var pets=PooledRefList<BaseCreature>.Create();
        foreach(var follower in owner.AllFollowers??[]){if(follower is BaseCreature pet){pets.Add(pet);}}
        foreach (var pet in pets)
        {
            if (pet is not { Deleted: false, Alive: true, IsDeadPet: false, Controlled: true }
                || pet.ControlMaster != owner || (!Near(pet, npc, 12)
                    && !(includeParked && Provider(npc) && MinionSafeAreas.Contains(owner) && MinionSafeAreas.Parked(pet))) || !Peaceful(pet)) { continue; }
            TimedEffects.Settle(pet, TimedEffects.Now);
            if (pet.Deleted || !pet.Alive || pet.IsDeadPet || pet.Hits >= pet.HitsMax) { continue; }
            pet.Hits = pet.HitsMax; TimedEffects.ClearRestoration(pet); healed++;
        }
        if (healed > 0) { owner.SendMessage("Your living companions have been healed to full health."); } return healed;
    }
    private static bool Peaceful(Mobile mobile)
    {
        if (mobile.Combatant != null) { return false; }
        foreach (var info in mobile.Aggressors) { if (!info.Expired && Core.Now - info.LastCombatTime < TimeSpan.FromSeconds(10)) { return false; } }
        foreach (var info in mobile.Aggressed) { if (!info.Expired && Core.Now - info.LastCombatTime < TimeSpan.FromSeconds(10)) { return false; } }
        return true;
    }
    public static MercenaryServiceRequest Quote(PlayerMobile owner, BaseCreature mercenary, BaseCreature healer)
    {
        bool freeAtMerryn = healer is NewHavenHealer && mercenary is { IsDeadPet: true };
        if (!ClassProfessions.Ready(owner) || !Owned(owner, mercenary, out var state) || !Provider(healer)
            || healer is not { Deleted: false, Alive: true } || !Near(owner, healer, freeAtMerryn ? 4 : 3)
            || !(freeAtMerryn ? Near(owner, mercenary, 4) : Near(mercenary, healer))
            || !Peaceful(owner) || !Peaceful(mercenary) || !Peaceful(healer)
            || healer is BaseHealer native && !native.CheckResurrect(owner)
            || mercenary.IsDeadPet && !mercenary.Map.CanFit(mercenary.Location, 16, false, false)
            || !mercenary.IsDeadPet && mercenary.Hits >= mercenary.HitsMax) { return null; }
        return new() { Owner = owner, Mercenary = mercenary, Healer = healer, State = state, Connection = owner.NetState,
            Resurrection = mercenary.IsDeadPet, Gold = mercenary.IsDeadPet && !freeAtMerryn ? ResurrectionPrice(state.Level) : 0 };
    }
    public static bool Commit(MercenaryServiceRequest request)
    {
        if (request == null || request.Claimed) { return false; } request.Claimed = true;
        var fresh = Quote(request.Owner, request.Mercenary, request.Healer);
        if (fresh == null || fresh.State != request.State || fresh.Resurrection != request.Resurrection || fresh.Gold != request.Gold
            || request.Owner.NetState != request.Connection || request.Gold > 0 && !Banker.Withdraw(request.Owner, request.Gold)) { return false; }
        var mercenary = request.Mercenary;
        if (request.Resurrection) { RestoreLife(mercenary); }
        else { HealFollowers(request.Owner, request.Healer); }
        FinishTreatment(mercenary);
        return true;
    }
    internal static bool CanRaiseAtShrine(PlayerMobile owner, BaseCreature companion, Item shrine) =>
        owner.Alive && companion is { IsDeadPet: true } && Owned(owner, companion, out _)
        && ShrineResurrection.Near(owner, shrine, Ankhs.ResurrectRange) && ShrineResurrection.Near(companion, shrine, 4)
        && Peaceful(owner) && Peaceful(companion) && companion.Map.CanFit(companion.Location, 16, false, false);

    internal static bool RaiseAtShrine(PlayerMobile owner, BaseCreature companion, Item shrine)
    {
        if (!CanRaiseAtShrine(owner, companion, shrine))
            return false;

        RestoreLife(companion);
        if (companion.IsDeadPet)
            return false;
        FinishTreatment(companion);
        return true;
    }
    private static void RestoreLife(BaseCreature companion)
    {
        _paidResurrection.Add(companion.Serial);
        try { companion.ResurrectPet(); }
        finally { _paidResurrection.Remove(companion.Serial); }
    }
    private static void FinishTreatment(BaseCreature companion)
    {
        TimedEffects.ClearRestoration(companion);
        companion.PlaySound(0x1F2);
        companion.FixedEffect(0x376A, 9, 32);
    }
    private static void AddEntries(BaseCreature healer, Mobile from, ref PooledRefList<ContextMenuEntry> entries)
    {
        if (!Provider(healer) || from is not PlayerMobile owner) { return; }
        entries.Add(new HealEntry(healer));
        foreach (var (serial, state) in States)
        {
            if (state.Owner == owner.Serial && state.Active && World.FindMobile(serial) is BaseCreature { IsDeadPet: true } mercenary)
            { entries.Add(new CareEntry(healer, mercenary)); break; }
        }
    }
    private sealed class HealEntry(BaseCreature healer) : ContextMenuEntry(1044060 + (int)SkillName.Healing, 3)
    {
        public override void OnClick(Mobile from, IEntity target)
        { if (from is PlayerMobile owner) { HealFollowers(owner, healer, includeParked:true); } }
    }
    private sealed class CareEntry(BaseCreature healer, BaseCreature mercenary) : ContextMenuEntry(6195, 3)
    {
        public override void OnClick(Mobile from, IEntity target)
        { if (from is PlayerMobile owner) { MercenaryCareGump.DisplayTo(owner, mercenary, healer); } }
    }
}
