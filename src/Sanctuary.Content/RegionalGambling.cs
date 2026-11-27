// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal enum BountyGearCategory { Armour, Weapons, Accessories }
internal enum BountyPayment { Trophies, Gold }

internal sealed record RegionalGambleShop(string Title, Type TrophyType, string Trophies,
    int GoldPerTrophy, int MinimumLevel, int MaximumLevel, Func<PlayerMobile, long> TurnIns)
{
    internal bool Unlocked(PlayerMobile player) => TurnIns(player) >= RegionalGambling.UnlockCount;
    internal int GoldPrice(int trophies) => checked(trophies * GoldPerTrophy);

    internal (int Minimum, int Maximum) ItemLevels(int level)
    {
        int centre = Math.Clamp(level, MinimumLevel, MaximumLevel);
        return (Math.Max(MinimumLevel, centre - 5), Math.Min(MaximumLevel, centre + 4));
    }
}

internal static class RegionalGambling
{
    internal const int UnlockCount = 50;
    internal const int CoveGoldPerEar = 50;
    internal static readonly RegionalGambleShop Cove = new("Cove bounty exchange", typeof(OrcEar), "ears",
        CoveGoldPerEar, 5, 30, player => Gambling.Profiles.CoveTurnIns(player));
    internal static readonly RegionalGambleShop Destard = new("Destard bounty exchange", typeof(DaemonHorn), "horns",
        DestardBounty.GoldPerHorn, 30, 60, player => DestardBounty.For(player)?.Horns ?? 0);

    internal static readonly ScriptedDialogue.Reply ExchangeItems =
        new("Exchange for Items", 0, (player, speaker) => Open(player, (ConversationNpc)speaker),
            TextId: "bounty.exchange_items");

    internal static RegionalGambleShop For(ConversationNpc npc) => npc switch
    {
        MayorJaana => Cove,
        DestardQueen => Destard,
        _ => null
    };

    internal static bool CanUse(PlayerMobile player, ConversationNpc npc, RegionalGambleShop shop) =>
        shop != null && For(npc) == shop && shop.Unlocked(player) && ClassProfessions.Ready(player)
        && Gambling.Level(player) is >= 1 and <= 99 && npc is { Alive: true, Deleted: false }
        && player.Map == npc.Map && player.InRange(npc, PortraitProtocol.NpcInteractionRange)
        && npc.CanTalkTo(player) && player.CanSee(npc) && player.InLOS(npc);

    internal static string LockedMessage(PlayerMobile player, RegionalGambleShop shop) =>
        $"The gear exchange opens after {UnlockCount} {shop.Trophies} have been turned in. So far, thou hast brought me {shop.TurnIns(player):N0}.";

    private static void Open(PlayerMobile player, ConversationNpc npc)
    {
        var shop = For(npc);
        if (shop == null || !ScriptedDialogue.CanTalk(player, npc))
            return;
        if (!shop.Unlocked(player))
        {
            ScriptedDialogue.Show(player, npc, [new(LockedMessage(player, shop), [])]);
            return;
        }
        RegionalGamblingGump.DisplayTo(player, npc, shop);
    }
}

public static partial class Gambling
{
    internal const int RegionalStockSize = 7;

    internal static GambleOffer[] RegionalStock(RegionalGambleShop shop, BountyGearCategory category, int level, Random random)
    {
        var (minimum, maximum) = shop.ItemLevels(level);
        var pool = _bases.Values.Where(b => FitsRegionalCap(EquipmentBalance.Base(b.Code), maximum))
            .Select(b => new GambleBase(b.Code, b.Variants.Where(v => InCategory(v, category)).ToArray()))
            .Where(b => b.Variants.Length != 0).ToArray();
        var stock = new GambleOffer[Math.Min(pool.Length, RegionalStockSize)];
        for (int i = 0; i < stock.Length; i++)
        {
            int picked = random.Next(i, pool.Length);
            (pool[i], pool[picked]) = (pool[picked], pool[i]);
            var basis = pool[i];
            int itemLevel = random.Next(Math.Max(minimum, EquipmentBalance.Base(basis.Code).QualityLevel), maximum + 1);
            var variant = basis.Variants[random.Next(basis.Variants.Length)];
            stock[i] = new(basis.Code, variant, itemLevel, TrophyPrice(basis.Code));
        }
        return stock;
    }

    private static bool InCategory(GambleVariant variant, BountyGearCategory category) =>
        EquipmentBalance.TryGet(EquipmentCrafting.Resolve(variant.Type), out var gear) && (category switch
        {
            BountyGearCategory.Armour => gear.Slot is GearSlot.Shield or GearSlot.Head or GearSlot.Body
                or GearSlot.Hands or GearSlot.Feet or GearSlot.Belt,
            BountyGearCategory.Weapons => gear.Slot == GearSlot.Weapon,
            BountyGearCategory.Accessories => gear.Slot is GearSlot.Ring or GearSlot.Amulet or GearSlot.Quiver,
            _ => false
        });

    internal static int TrophyPrice(string code) => EquipmentBalance.Base(code).QualityLevel switch
    {
        <= 10 => 10,
        <= 20 => 15,
        _ => 20
    };

    private static bool FitsRegionalCap(NormalBase basis, int maximum) =>
        basis.QualityLevel <= maximum && basis.RequiredLevel <= maximum
        && EquipmentCrafting.EquipFloor(Enum.Parse<GearTier>(basis.Tier)) <= maximum;

    internal static GambleResult BuyRegional(PlayerMobile player, ConversationNpc npc, RegionalGambleShop shop,
        BountyGearCategory category, GambleOffer offer, int quotedLevel, BountyPayment payment, Random random)
    {
        if (!RegionalGambling.CanUse(player, npc, shop) || Level(player) != quotedLevel || offer == null
            || !_bases.TryGetValue(offer.Code, out var basis) || Array.IndexOf(basis.Variants, offer.Variant) < 0
            || !InCategory(offer.Variant, category) || offer.Price != TrophyPrice(offer.Code) || !Enum.IsDefined(payment))
            return GambleResult.Invalid;

        var (minimum, maximum) = shop.ItemLevels(quotedLevel);
        var baseItem = EquipmentBalance.Base(offer.Code);
        if (!FitsRegionalCap(baseItem, maximum) || offer.ItemLevel < Math.Max(minimum, baseItem.QualityLevel)
            || offer.ItemLevel > maximum)
            return GambleResult.Invalid;

        return payment == BountyPayment.Gold
            ? Purchase(player, offer, typeof(Gold), shop.GoldPrice(offer.Price), random, shop.MaximumLevel)
            : Purchase(player, offer, shop.TrophyType, offer.Price, random, shop.MaximumLevel);
    }
}
