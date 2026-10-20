// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static partial class TownMarkets
{
    private sealed record PurchaseQuote(Serial Vendor, TimedBuff City, Server.Network.NetState Connection);
    private static readonly Dictionary<Serial, PurchaseQuote> _purchaseQuotes = new();
    private static void OpenPurchase(BaseVendor vendor, Mobile buyer)
    { if (Role(vendor) != null) { _purchaseQuotes[buyer.Serial] = new(vendor.Serial, MapSurveys.City(buyer), buyer.NetState); } }
    private static int ShownPurchasePrice(BaseVendor vendor, Mobile buyer, int price) => Role(vendor) != null
        && _purchaseQuotes.GetValueOrDefault(buyer.Serial) is { } quote && quote.Vendor == vendor.Serial
        ? MapSurveys.Price(price, quote.City != null) : price;
    private static bool Trading(BaseVendor vendor, Mobile from) => vendor is { Deleted: false, Alive: true }
        && from is { Deleted: false, Alive: true, Backpack: not null, Holding: null }
        && AlliedOverlap.Allowed(from) && from.Map == vendor.Map && from.InRange(vendor, 3) && from.InLOS(vendor) && vendor.CheckVendorAccess(from);
    private static bool? Buy(BaseVendor vendor, Mobile buyer, List<BuyItemResponse> list)
    {
        if (Role(vendor) == null) { return null; }
        if (!Trading(vendor, buyer) || list is not { Count: > 0 and <= 249 }) { return false; }
        var city = MapSurveys.City(buyer);
        if (_purchaseQuotes.TryGetValue(buyer.Serial, out var quote)
            ? quote.Vendor != vendor.Serial || quote.City != city || !ReferenceEquals(quote.Connection, buyer.NetState)
            : city != null || buyer.NetState != null)
        { buyer.SendMessage("Your map discount or shop changed. Reopen the shop before purchasing. Nothing was charged."); return false; }
        var market = Market(vendor);
        var selected = new List<(MarketOffer Offer, int Amount)>();
        var seen = new HashSet<Serial>();
        long total = 0;
        foreach (var request in list)
        {
            var offer = market.Offers.Find(o => o.Item == request.Serial);
            if (!seen.Add(request.Serial) || offer == null || request.Amount < 1 || request.Amount > (offer.Equipment ? 1 : 999)
                || World.FindItem(offer.Item) is not { Deleted: false } item || item.Parent != World.FindItem(market.Shelf))
            { buyer.SendMessage("That shop offer has changed. Please reopen the shop."); return false; }
            total += (long)MapSurveys.Price(offer.Price, city != null) * request.Amount;
            if (total > int.MaxValue) { return false; }
            selected.Add((offer, request.Amount));
        }
        int cost = buyer.AccessLevel >= AccessLevel.GameMaster ? 0 : (int)total;
        var consumes = new List<(Item Item, int Amount)>();
        bool bank = false;
        int remaining = cost;
        foreach (var item in buyer.Backpack.FindItemsByType(typeof(Gold), true))
        {
            int take = Math.Min(remaining, item.Amount);
            if (take > 0) { consumes.Add((item, take)); remaining -= take; }
        }
        if (remaining > 0)
        {
            if (cost < 2000 || Banker.GetBalance(buyer) < cost)
            { buyer.SendMessage("You cannot afford that purchase."); return false; }
            consumes.Clear(); bank = true;
        }
        var output = new List<Item>();
        var created = new List<Item>();
        bool committed = false;
        try
        {
            foreach (var (offer, amount) in selected)
            {
                if (offer.Equipment) { output.Add(World.FindItem(offer.Item)); continue; }
                var row = _supplies[offer.Supply];
                var display = World.FindItem(offer.Item);
                for (int i = 0; i < amount; i++)
                {
                    var item = Supply(row);
                    if (item is BaseTool tool && display is BaseTool template)
                    {
                        tool.UsesRemaining = template.UsesRemaining;
                        if (CraftTransactions.Profiles.Find(template) is { } state) { CraftTransactions.Profiles.Store(tool, state); }
                    }
                    output.Add(item); created.Add(item);
                    if (item.Stackable) { item.Amount = amount; break; }
                }
            }
            var delivery = MarketDelivery.Create(buyer, consumes, output);
            if (delivery == null) { buyer.SendMessage("Your backpack cannot hold that purchase. Nothing was charged."); return false; }
            if (bank && !Banker.Withdraw(buyer, cost)) { return false; }
            int index = 0;
            foreach (var (offer, amount) in selected)
            {
                int count = offer.Equipment || output[index].Stackable ? 1 : amount;
                for (int i = 0; i < count; i++)
                {
                    var item = output[index++];
                    if (ClassGear.Converted(item))
                    {
                        ClassGear.Profiles.Store(item, ClassGear.Profiles.Ensure(item) with
                        { VendorPurchasePrice = cost == 0 ? 0 : MapSurveys.Price(offer.Price, city != null) });
                    }
                }
            }
            delivery.Commit(); committed = true;
            foreach (var (offer, _) in selected)
            {
                if (offer.Equipment) { market.Offers.Remove(offer); }
            }
            buyer.PlaySound(0x32);
            buyer.SendMessage($"Purchase complete: {cost} gold.");
            return true;
        }
        finally
        {
            if (!committed) { foreach (var item in created) { item.Delete(); } }
        }
    }
    private static bool? Sell(BaseVendor vendor, Mobile seller, List<SellItemResponse> list)
    {
        if (Role(vendor) == null) { return null; }
        if (!Trading(vendor, seller) || list is not { Count: > 0 and <= 500 }) { return false; }
        var infos = vendor.GetSellInfo();
        var consumes = new List<(Item Item, int Amount)>();
        var seen = new HashSet<Serial>();
        long total = 0;
        foreach (var request in list)
        {
            var item = request.Item;
            if (item == null || !seen.Add(item.Serial) || !TradePrices.CanSell(item) || !item.IsChildOf(seller.Backpack) || !item.VerifyMove(seller) || !item.IsAccessibleTo(seller)
                || request.Amount < 1 || request.Amount > item.Amount)
            { seller.SendMessage("The sale has changed. Please reopen the shop."); return false; }
            var info = infos.FirstOrDefault(i => i.IsSellable(item));
            int price = info == null ? 0 : BaseVendor.GetSellPriceFor(item, info);
            if (price <= 0) { return false; }
            total += (long)price * request.Amount;
            if (total > int.MaxValue) { return false; }
            consumes.Add((item, request.Amount));
        }
        var gold = new List<Item>();
        bool committed = false;
        try
        {
            long remaining = total;
            while (remaining > 0)
            {
                int amount = (int)Math.Min(60000, remaining); remaining -= amount;
                gold.Add(new Gold(amount));
            }
            var delivery = MarketDelivery.Create(seller, consumes, gold);
            if (delivery == null) { seller.SendMessage("You cannot carry the sale proceeds. Nothing was sold."); return false; }
            delivery.Commit(); committed = true;
            seller.PlaySound(0x32);
            seller.SendMessage($"Sale complete: {total} gold. Sold goods are not available for buyback.");
            return true;
        }
        finally
        {
            if (!committed) { foreach (var item in gold) { item.Delete(); } }
        }
    }
}

// Preflight the complete exchange before consuming inputs. Commits use ordinary
// item moves, without rerunning a mutable UI quote or partially charging a batch.
internal sealed class MarketDelivery
{
    private readonly Mobile _owner;
    private readonly List<(Item Item, int Amount)> _consumes;
    private readonly List<(Item Target, Item Output)> _merges;
    private readonly List<Item> _outputs;
    private bool _committed;
    private MarketDelivery(Mobile owner, List<(Item, int)> consumes, List<(Item, Item)> merges, List<Item> outputs)
    { _owner = owner; _consumes = consumes; _merges = merges; _outputs = outputs; }
    public static MarketDelivery Create(Mobile owner, List<(Item Item, int Amount)> consumes, List<Item> outputs, int additionalWeight = 0)
    {
        var amounts = new Dictionary<Item, int>();
        int slots = 0, weight = additionalWeight;
        foreach (var (item, amount) in consumes)
        {
            if (item.Deleted || !item.IsChildOf(owner.Backpack) || amount < 1 || amount > item.Amount || !amounts.TryAdd(item, item.Amount - amount))
            { return null; }
            if (amount == item.Amount) { slots--; }
            weight += (int)Math.Ceiling(item.Weight * (item.Amount - amount)) - item.PileWeight;
        }
        var targets = new List<Item>(owner.Backpack.Items);
        var merges = new List<(Item, Item)>();
        var stacks = new List<Item>();
        foreach (var output in outputs)
        {
            Item target = null;
            if (output.Stackable)
            {
                foreach (var candidate in targets)
                {
                    int amount = amounts.GetValueOrDefault(candidate, candidate.Amount);
                    if (candidate.Stackable && amount > 0 && candidate.CanStackWith(output) && amount <= 60000 - output.Amount)
                    { target = candidate; break; }
                }
            }
            if (target != null)
            {
                int amount = amounts.GetValueOrDefault(target, target.Amount);
                weight += (int)Math.Ceiling(target.Weight * (amount + output.Amount)) - (int)Math.Ceiling(target.Weight * amount);
                amounts[target] = amount + output.Amount; merges.Add((target, output));
            }
            else
            {
                weight += output.PileWeight + output.TotalWeight; slots += 1 + output.TotalItems;
                targets.Add(output); amounts[output] = output.Amount; stacks.Add(output);
            }
        }
        if (slots > 0 && owner.Backpack.MaxItems != 0 && owner.Backpack.TotalItems + slots > owner.Backpack.MaxItems
            || weight > 0 && (owner.TotalWeight + weight > owner.MaxWeight
                || owner.Backpack.MaxWeight != 0 && owner.Backpack.TotalWeight + weight > owner.Backpack.MaxWeight))
        { return null; }
        return new(owner, consumes, merges, stacks);
    }
    public void Commit()
    {
        if (_committed) { throw new InvalidOperationException("Market exchange already committed."); }
        _committed = true;
        foreach (var (item, amount) in _consumes) { item.Consume(amount); }
        foreach (var (target, output) in _merges)
        {
            if (!target.StackWith(_owner, output)) { throw new InvalidOperationException("Market delivery changed during commit."); }
        }
        foreach (var output in _outputs)
        {
            if (!output.Deleted) { _owner.Backpack.DropItem(output); }
        }
    }
}
