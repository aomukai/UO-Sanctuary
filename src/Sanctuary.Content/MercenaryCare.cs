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

public sealed record MercenaryCareState(Serial Owner, int Level, bool Retired, long[] Healing, DateTime NextDrinkUtc);
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
    internal static readonly Dictionary<Serial, MercenaryCareState> States = new();
    private static readonly HashSet<Serial> _paidResurrection = new();
    private static readonly List<Serial> _keys = new();
    public static MercenaryCareProfiles Profiles { get; private set; }
    public static void Configure()
    {
        Profiles = new();
        ClassMercenaryHooks.KeepEquipment = merc => States.ContainsKey(merc.Serial);
        ClassMercenaryHooks.ResurrectionAllowed = merc => !States.ContainsKey(merc.Serial) || _paidResurrection.Contains(merc.Serial);
        ClassMercenaryHooks.ContextEntries = AddEntries;
        ClassMercenaryHooks.ReleaseOrder = (mercenary, issuer) =>
        {
            if (!States.ContainsKey(mercenary.Serial)) { return false; }
            if (issuer is PlayerMobile owner && mercenary.ControlMaster == owner) { Retire(owner, mercenary); }
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
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () => Tick(TimedEffects.Now));
    public static int ResurrectionPrice(int level) => level is >= 1 and <= 99 ? level * 1000 : throw new ArgumentOutOfRangeException(nameof(level));
    internal static bool Register(PlayerMobile owner, BaseCreature mercenary, int level)
    {
        if (owner is not { Deleted: false } || mercenary is not { Deleted: false, Summoned: false, Controlled: true }
            || mercenary.ControlMaster != owner || level is < 1 or > 99 || States.ContainsKey(mercenary.Serial)) { return false; }
        foreach (var row in States.Values) { if (row.Owner == owner.Serial && !row.Retired) { return false; } }
        mercenary.IsBonded = true;
        mercenary.NoKillAwards = true;
        States.Add(mercenary.Serial, new(owner.Serial, level, false, new long[5], TimedEffects.Now)); return true;
    }
    public static long Stock(BaseCreature mercenary)
    {
        if (!States.TryGetValue(mercenary.Serial, out var state)) { return 0; }
        long total = 0; foreach (long amount in state.Healing) { total += amount; } return total;
    }
    private static bool Owned(PlayerMobile owner, BaseCreature mercenary, out MercenaryCareState state)
    {
        state = null;
        return owner is { Deleted: false } && mercenary is { Deleted: false, Controlled: true, IsBonded: true }
            && mercenary.ControlMaster == owner && States.TryGetValue(mercenary.Serial, out state) && state.Owner == owner.Serial && !state.Retired;
    }
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
        if (!States.TryGetValue(mercenary.Serial, out var state) || state.Retired || mercenary.Deleted || mercenary.IsDeadPet
            || !mercenary.Alive || mercenary.Map == Map.Internal || mercenary.ControlMaster?.Serial != state.Owner
            || mercenary.HitsMax <= 0 || now < state.NextDrinkUtc) { return false; }
        TimedEffects.Settle(mercenary, now);
        if (mercenary.IsDeadPet || !mercenary.Alive || (long)mercenary.Hits * 2 > mercenary.HitsMax) { return false; }
        int tier = 0; while (tier < 5 && state.Healing[tier] == 0) { tier++; }
        if (tier == 5) { return false; }
        var potion = PotionBeltCatalog.Potion(RestorationKind.Healing, tier + 1);
        if (!TimedEffects.AddRestoration(mercenary, true, potion.Amount, potion.DurationMs, now)) { return false; }
        var amounts = (long[])state.Healing.Clone(); amounts[tier]--;
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
            if (World.FindMobile(serial) is BaseCreature mercenary && mercenary.ControlMaster?.NetState != null)
            { TryDrink(mercenary, now); }
        }
    }
    internal static bool Retire(PlayerMobile owner, BaseCreature mercenary)
    {
        if (!Owned(owner, mercenary, out var state)) { return false; }
        TimedEffects.Clear(mercenary); mercenary.Combatant = null;
        States[mercenary.Serial] = state with { Retired = true, Healing = new long[5] };
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
    public static bool Provider(BaseCreature healer) => healer is BaseHealer or HealerGuildmaster;
    public static int HealFollowers(PlayerMobile owner, BaseCreature npc)
    {
        if (!ClassProfessions.Affected(owner) || !ClassProfessions.Ready(owner) || npc is not { Deleted: false, Alive: true }
            || !(Provider(npc) || npc is BaseVendor { IsActiveVendor: true }) || !Near(owner, npc) || !Peaceful(owner) || !Peaceful(npc)
            || npc is BaseVendor vendor && !vendor.CheckVendorAccess(owner) || npc is BaseHealer healer && !healer.CheckResurrect(owner)) { return 0; }
        int healed = 0;
        foreach (var follower in owner.AllFollowers ?? [])
        {
            if (follower is not BaseCreature { Deleted: false, Alive: true, IsDeadPet: false, Controlled: true } pet
                || pet.ControlMaster != owner || !Near(pet, npc, 12) || !Peaceful(pet)) { continue; }
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
        if (!ClassProfessions.Ready(owner) || !Owned(owner, mercenary, out var state) || !Provider(healer)
            || healer is not { Deleted: false, Alive: true } || !Near(owner, healer) || !Near(mercenary, healer)
            || !Peaceful(owner) || !Peaceful(mercenary) || !Peaceful(healer)
            || healer is BaseHealer native && !native.CheckResurrect(owner)
            || mercenary.IsDeadPet && !mercenary.Map.CanFit(mercenary.Location, 16, false, false)
            || !mercenary.IsDeadPet && mercenary.Hits >= mercenary.HitsMax) { return null; }
        return new() { Owner = owner, Mercenary = mercenary, Healer = healer, State = state, Connection = owner.NetState,
            Resurrection = mercenary.IsDeadPet, Gold = mercenary.IsDeadPet ? ResurrectionPrice(state.Level) : 0 };
    }
    public static bool Commit(MercenaryServiceRequest request)
    {
        if (request == null || request.Claimed) { return false; } request.Claimed = true;
        var fresh = Quote(request.Owner, request.Mercenary, request.Healer);
        if (fresh == null || fresh.State != request.State || fresh.Resurrection != request.Resurrection || fresh.Gold != request.Gold
            || request.Owner.NetState != request.Connection || request.Gold > 0 && !Banker.Withdraw(request.Owner, request.Gold)) { return false; }
        var mercenary = request.Mercenary;
        if (request.Resurrection)
        {
            _paidResurrection.Add(mercenary.Serial);
            try { mercenary.ResurrectPet(); }
            finally { _paidResurrection.Remove(mercenary.Serial); }
        }
        else { HealFollowers(request.Owner, request.Healer); }
        TimedEffects.ClearRestoration(mercenary); mercenary.PlaySound(0x1F2); mercenary.FixedEffect(0x376A, 9, 32); return true;
    }
    private static void AddEntries(BaseCreature healer, Mobile from, ref PooledRefList<ContextMenuEntry> entries)
    {
        if (!Provider(healer) || from is not PlayerMobile owner) { return; }
        entries.Add(new HealEntry(healer));
        foreach (var (serial, state) in States)
        {
            if (state.Owner == owner.Serial && !state.Retired && World.FindMobile(serial) is BaseCreature { IsDeadPet: true } mercenary)
            { entries.Add(new CareEntry(healer, mercenary)); break; }
        }
    }
    private sealed class HealEntry(BaseCreature healer) : ContextMenuEntry(1044060 + (int)SkillName.Healing, 3)
    {
        public override void OnClick(Mobile from, IEntity target)
        { if (from is PlayerMobile owner) { HealFollowers(owner, healer); } }
    }
    private sealed class CareEntry(BaseCreature healer, BaseCreature mercenary) : ContextMenuEntry(6195, 3)
    {
        public override void OnClick(Mobile from, IEntity target)
        { if (from is PlayerMobile owner) { MercenaryCareGump.DisplayTo(owner, mercenary, healer); } }
    }
}
