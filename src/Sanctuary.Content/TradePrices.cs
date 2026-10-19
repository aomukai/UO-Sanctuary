// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;

namespace Sanctuary;

public static class TradePrices
{
    public static (int Min, int Max) Band(GearTier tier) => tier switch
    {
        GearTier.Exceptional => (TownMarkets.Settings.ExceptionalMin, TownMarkets.Settings.ExceptionalMax),
        GearTier.Elite => (TownMarkets.Settings.EliteMin, TownMarkets.Settings.EliteMax),
        _ => (TownMarkets.Settings.NormalMin, TownMarkets.Settings.NormalMax)
    };
    public static int Valuation(GearTier tier, Random random)
    {
        var (min, max) = Band(tier);
        return random.Next(min, max + 1);
    }
    public static bool Starter(Item item) => ClassGear.State(item) is { StarterTeeth: true } || item.LootType == LootType.Newbied;
    public static bool CanSell(Item item) => item is { Deleted: false, Movable: true, Nontransferable: false, QuestItem: false, Insured: false }
        && !item.IsLockedDown && !item.IsSecure && (item.IsStandardLoot() || ClassGear.Converted(item) && Starter(item))
        && (item is not Container container || container.Items.Count == 0);
    public static int Quote(Item item, int nativePrice = 0)
    {
        if (!ClassGear.Converted(item)) { return nativePrice; }
        if (Starter(item)) { return 1; }
        var state = ClassGear.Profiles.Ensure(item);
        if (item.Origin == ItemOrigin.Vendor)
        {
            return state.VendorPurchasePrice is int paid ? paid * TownMarkets.Settings.VendorResalePercent / 100 : 1;
        }
        if (state.IdentifiedSaleOffer == null)
        {
            state = state with { IdentifiedSaleOffer = Valuation(state.Tier, Random.Shared) };
            ClassGear.Profiles.Store(item, state);
        }
        return state.Identified ? state.IdentifiedSaleOffer.Value : state.IdentifiedSaleOffer.Value / 10;
    }
}
