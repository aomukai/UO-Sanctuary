// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Commands;
using Server.Engines.Insurance;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record InsurancePriceRow(string Rarity, int Normal, int Exceptional, int Elite);
public sealed record InsuranceQuote(Item Item, EquipmentPolicy Policy, int Cost, bool Insured, bool Paid);

public static class EquipmentInsurance
{
    private sealed class DeathCover
    {
        public readonly HashSet<Serial> Protected = new();
        public readonly HashSet<Serial> Settled = new();
        public bool MissedRenewal;
    }
    private static readonly Dictionary<Serial, DeathCover> _deaths = new();
    private static readonly int[,] _prices = new int[3, 3];
    public static InsuranceProfiles Profiles { get; private set; }

    public static void Configure()
    {
        Profiles = new InsuranceProfiles();
        var rows = JsonSerializer.Deserialize<InsurancePriceRow[]>(File.ReadAllText(
            Path.Join(Core.BaseDirectory, "Data", "Sanctuary", "Balance", "insurance.json")));
        var seen = new HashSet<GearRarity>();
        if (rows?.Length != 3) { throw new InvalidDataException("Insurance needs exactly the three supported rarities."); }
        foreach (var row in rows)
        {
            if (row == null || !Enum.TryParse<GearRarity>(row.Rarity, out var rarity) || !Enum.IsDefined(rarity)
                || !seen.Add(rarity) || row.Normal is < 1 or > 1000000 || row.Exceptional is < 1 or > 1000000
                || row.Elite is < 1 or > 1000000)
            { throw new InvalidDataException("Invalid insurance price row."); }
            _prices[(int)rarity, 0] = row.Normal; _prices[(int)rarity, 1] = row.Exceptional; _prices[(int)rarity, 2] = row.Elite;
        }
        InsuranceContentHooks.Cost = (_, item) => Cost(item);
        InsuranceContentHooks.Eligible = (from, item) => from is PlayerMobile p && Owned(p, item) && Cost(item) > 0;
        InsuranceContentHooks.Toggle = Toggle;
        InsuranceContentHooks.OpenMenu = from => { if (from is PlayerMobile p) { InsuranceGump.DisplayTo(p); } };
        InsuranceContentHooks.BeforeDeath = BeforeDeath;
        InsuranceContentHooks.DeathItem = Protect;
        InsuranceContentHooks.AfterDeath = AfterDeath;
        InsuranceContentHooks.Received = Received;
        InsuranceContentHooks.Deleted = Deleted;
        EventSink.Connected += from => { if (from is PlayerMobile p) { AdoptCarried(p); } };
        EventSink.Disconnected += from => { _deaths.Remove(from.Serial); InsuranceGump.Forget(from.Serial); };
        CommandSystem.Register("Insurance", AccessLevel.Player, e => { if (e.Mobile is PlayerMobile p) { InsuranceGump.DisplayTo(p); } });
    }

    [CallPriority(300)]
    public static void Initialize()
    {
        ServerFeatureFlags.InsuranceEnabled = true;
        ItemStateHooks.Copied += (_, copy) => Profiles.Remove(copy);
    }

    public static void Deleted(PlayerMobile player)
    { _deaths.Remove(player.Serial); InsuranceGump.Forget(player.Serial); }

    public static int Cost(Item item)
    {
        if (item is not (BaseWeapon or BaseArmor or BaseClothing or BaseJewel) || item is Container || item.Deleted
            || !item.Movable || item.Stackable || !ClassGear.Converted(item) || item.BlessedFor != null
            || item.LootType is LootType.Blessed or LootType.Newbied or LootType.Cursed
            || ClassGear.State(item) is { Identified: false }) { return 0; }
        var state = ClassGear.State(item);
        var rarity = state?.Roll.Rarity ?? GearRarity.Normal;
        var tier = state?.Tier ?? GearTier.Normal;
        return Enum.IsDefined(rarity) && Enum.IsDefined(tier) ? _prices[(int)rarity, (int)tier] : 0;
    }
    public static bool Owned(PlayerMobile p, Item item) => item is { Deleted: false }
        && (item.Parent == p || p.Backpack != null && item.IsChildOf(p.Backpack));
    public static List<Item> Carried(PlayerMobile p)
    {
        var items = new List<Item>();
        foreach (var item in p.Items) { if (item is not Container) { items.Add(item); } }
        if (p.Backpack != null) { foreach (var item in p.Backpack.FindItems()) { items.Add(item); } }
        return items;
    }
    public static void AdoptCarried(PlayerMobile p)
    {
        foreach (var item in Carried(p))
        {
            var policy = Profiles.Find(item.Serial);
            if (policy != null && policy.Owner != p.Serial) { Profiles.Remove(item); }
            else if (policy == null && item.PaidInsurance && Cost(item) > 0)
            { Profiles.Store(item, new(p.Serial, true, item.Insured)); }
            else if (policy != null && Cost(item) == 0) { Profiles.Remove(item); }
            else if (policy == null && (item.Insured || item.PaidInsurance)) { Profiles.Remove(item); }
        }
    }
    private static void Received(Mobile from, Item item)
    {
        if (Profiles == null) { return; }
        void Check(Item candidate)
        {
            var policy = Profiles.Find(candidate.Serial);
            if (policy != null ? policy.Owner != from.Serial : candidate.Insured || candidate.PaidInsurance)
            { Profiles.Remove(candidate); }
        }
        Check(item);
        if (item is Container container) { foreach (var child in container.FindItems()) { Check(child); } }
    }
    public static InsuranceQuote Quote(Item item) => new(item, Profiles.Find(item.Serial), Cost(item), item.Insured, item.PaidInsurance);
    public static bool Current(PlayerMobile p, InsuranceQuote quote) => Owned(p, quote.Item) && quote.Cost > 0
        && (quote.Policy == null || quote.Policy.Owner == p.Serial)
        && Cost(quote.Item) == quote.Cost && ReferenceEquals(Profiles.Find(quote.Item.Serial), quote.Policy)
        && quote.Item.Insured == quote.Insured && quote.Item.PaidInsurance == quote.Paid;

    public static bool Apply(PlayerMobile p, InsuranceQuote[] quotes, bool[] selected)
    {
        if (p is not { Deleted: false, Alive: true } || quotes.Length != selected.Length || quotes.Length > 1024
            || quotes.Select(q => q.Item.Serial).Distinct().Count() != quotes.Length || quotes.Any(q => !Current(p, q)))
        { p?.SendMessage("Your equipment or insurance changed. Review a fresh quote."); return false; }
        int charge = 0;
        for (int i = 0; i < quotes.Length; i++)
        { if (selected[i] && quotes[i].Policy?.Paid != true) { charge = checked(charge + quotes[i].Cost); } }
        if (charge > 0 && !Banker.Withdraw(p, charge))
        { p.SendMessage("Your bank cannot cover the full insurance purchase. No policies changed."); return false; }
        for (int i = 0; i < quotes.Length; i++)
        {
            var policy = quotes[i].Policy;
            if (selected[i] != (policy?.Active == true))
            { Profiles.Store(quotes[i].Item, new(p.Serial, selected[i] || policy?.Paid == true, selected[i])); }
        }
        p.SendMessage($"Insurance updated. {charge} gold paid from your bank; coverage has no time expiry.");
        return true;
    }
    private static void Toggle(Mobile from, Item item, bool repeat)
    {
        if (from is not PlayerMobile p || !p.CheckAlive()) { return; }
        AdoptCarried(p);
        if (!Owned(p, item) || Cost(item) == 0)
        { p.SendMessage("Select identified, supported equipment you are wearing or carrying. Containers and consumables cannot be insured."); }
        else { Apply(p, [Quote(item)], [Profiles.Find(item.Serial)?.Active != true]); }
        if (repeat) { Server.Engines.Insurance.Insurance.ToggleItemInsurance(p); }
    }
    private static void BeforeDeath(Mobile from)
    {
        if (from is not PlayerMobile p) { return; }
        AdoptCarried(p);
        var context = new DeathCover(); _deaths[p.Serial] = context; InsuranceGump.Forget(p.Serial);
        foreach (var item in Carried(p))
        {
            if (Profiles.Find(item.Serial) is { Paid: true, Active: true } policy && policy.Owner == p.Serial && Cost(item) > 0)
            { context.Protected.Add(item.Serial); item.Insured = item.PaidInsurance = true; }
        }
    }
    private static bool Protect(PlayerMobile p, Item item)
    {
        if (!_deaths.TryGetValue(p.Serial, out var context) || !context.Protected.Contains(item.Serial)) { return false; }
        if (!context.Settled.Add(item.Serial)) { return true; }
        bool renew = p.AutoRenewInsurance && Cost(item) is > 0 and var cost && Banker.Withdraw(p, cost);
        if (p.AutoRenewInsurance && !renew) { context.MissedRenewal = true; }
        Profiles.Store(item, new(p.Serial, renew, renew));
        return true;
    }
    private static void AfterDeath(Mobile from)
    {
        if (from is PlayerMobile p && _deaths.Remove(p.Serial, out var context) && context.MissedRenewal)
        { p.SendMessage("Some insurance could not renew because your bank lacked funds. This death was covered; review protection before your next death."); }
    }
}
