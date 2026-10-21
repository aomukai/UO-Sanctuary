// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record EscrowValueOverride(string Type, int Value);
public static class EscrowValuation
{
    private static readonly List<IShopSellInfo> _sales = new();
    private static readonly Dictionary<(Type, int), int> _purchases = new();
    private static readonly Dictionary<string, int> _overrides = new(StringComparer.Ordinal);
    public static void Initialize()
    {
        _sales.Clear(); _purchases.Clear(); _overrides.Clear();
        foreach (var row in BalanceCatalog.Read<EscrowValueOverride>("escrow-values"))
        {
            if (EquipmentCrafting.Resolve(row.Type) == null || row.Value is < 1 or > 100000000
                || !_overrides.TryAdd(row.Type, row.Value))
            { throw new System.IO.InvalidDataException("Invalid escrow valuation override."); }
        }
        // Stock definitions provide undiscounted reference prices without creating merchant stock or display items.
        foreach (var type in typeof(SBInfo).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(SBInfo).IsAssignableFrom(t)
            && t.GetConstructor(Type.EmptyTypes) != null).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var stock = (SBInfo)Activator.CreateInstance(type);
            if (stock.SellInfo != null) { _sales.Add(stock.SellInfo); }
            foreach (var offer in stock.BuyInfo)
            {
                if (offer.Type != null && offer.Price > 0)
                {
                    var key = (offer.Type, offer.ItemID);
                    _purchases[key] = Math.Min(_purchases.GetValueOrDefault(key, int.MaxValue), offer.Price);
                }
            }
        }
    }
    public static long Unit(Item item)
    {
        if (item is Gold) { return 1; }
        if (_overrides.TryGetValue(item.GetType().Name, out int value)) { return value; }
        if (ClassGear.Converted(item) || item is Runebark or SocketGem) { return TradePrices.Quote(item); }
        var supply = TownMarkets.Supplies.FirstOrDefault(s => s.Type == item.GetType().Name
            && (s.Kind == null || item is RestorationPotion p && p.Kind == s.Kind && p.Tier == s.Tier));
        if (supply?.SellPrice > 0) { return supply.SellPrice; }
        int sale = int.MaxValue;
        foreach (var info in _sales)
        {
            if (info.Types.Contains(item.GetType()) && info.GetSellPriceFor(item) is > 0 and var price)
            { sale = Math.Min(sale, price); }
        }
        if (sale != int.MaxValue) { return sale; }
        if (supply != null) { return supply.Price; }
        if (_purchases.TryGetValue((item.GetType(), item.ItemID), out int purchase)) { return purchase; }
        return item.Stackable ? 10 : 100;
    }
}
