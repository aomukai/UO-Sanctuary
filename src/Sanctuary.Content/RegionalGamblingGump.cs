// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

internal sealed class RegionalGamblingGump : DynamicGump
{
    private const int Refresh = 1, FirstCategory = 10, FirstTrophy = 100, FirstGold = 200;

    private readonly PlayerMobile _player;
    private readonly ConversationNpc _npc;
    private readonly RegionalGambleShop _shop;
    private readonly NetState _connection;
    private readonly BountyGearCategory _category;
    private readonly int _level;
    private readonly GambleOffer[] _stock;
    private readonly long _session;

    public override bool Singleton => true;

    private RegionalGamblingGump(PlayerMobile player, ConversationNpc npc, RegionalGambleShop shop,
        BountyGearCategory category, int level, GambleOffer[] stock) : base(30, 30)
    {
        _player = player;
        _npc = npc;
        _shop = shop;
        _connection = player.NetState;
        _category = category;
        _level = level;
        _stock = stock;
        _session = GamblingGump.Start(player.Serial);
    }

    internal static void DisplayTo(PlayerMobile player, ConversationNpc npc, RegionalGambleShop shop,
        BountyGearCategory category = BountyGearCategory.Armour)
    {
        if (player.NetState == null || !RegionalGambling.CanUse(player, npc, shop))
            return;

        player.CloseGump<GamblingGump>();
        int level = Gambling.Level(player);
        var stock = Gambling.RegionalStock(shop, category, level, Random.Shared);
        player.SendGump(new RegionalGamblingGump(player, npc, shop, category, level, stock));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage();
        b.AddBackground(0, 0, 535, 450, 5054);
        b.AddLabel(20, 18, 0x481, _shop.Title);
        b.AddLabel(331, 18, 0x47E, $"Item levels {_shop.MinimumLevel}–{_shop.MaximumLevel}");
        b.AddButton(493, 18, 4017, 4019, 0);

        foreach (var category in Enum.GetValues<BountyGearCategory>())
        {
            int x = 20 + (int)category * 150;
            if (category != _category)
                b.AddButton(x, 51, 4005, 4007, FirstCategory + (int)category);
            b.AddLabel(x + 35, 51, category == _category ? 0x481 : 0x47E, category.ToString());
        }

        b.AddLabel(20, 84, 0x47E, "Choose a base and how to pay. Properties are revealed on purchase.");
        for (int i = 0; i < _stock.Length; i++)
        {
            if (_stock[i] is not { } offer)
                continue;
            int y = 112 + i * 41;
            b.AddItem(20, y, offer.Variant.Graphic);
            b.AddLabelCropped(75, y + 6, 230, 19, 0x481, offer.Variant.Name);
            b.AddButton(313, y + 6, 4005, 4007, FirstTrophy + i);
            b.AddLabel(347, y + 6, 0x47E, $"{offer.Price} {_shop.Trophies}");
            b.AddButton(419, y + 6, 4005, 4007, FirstGold + i);
            b.AddLabel(452, y + 6, 0x47E, $"{_shop.GoldPrice(offer.Price):N0} gp");
        }
        if (_stock.Length == 0)
            b.AddLabel(20, 120, 0x47E, "No bases are available in this category at your level.");
        b.AddButton(20, 420, 4005, 4007, Refresh);
        b.AddLabel(55, 420, 0x481, "Refresh stock");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (!ReferenceEquals(sender, _connection) || sender.Mobile != _player || _player.NetState != _connection
            || !GamblingGump.Take(_player.Serial, _session) || info.ButtonID == 0)
            return;
        if (!RegionalGambling.CanUse(_player, _npc, _shop) || Gambling.Level(_player) != _level)
        {
            _player.SendMessage("Your gambling quote has changed. Reopen the exchange.");
            return;
        }

        int button = info.ButtonID;
        if (button == Refresh)
        {
            DisplayTo(_player, _npc, _shop, _category);
            return;
        }
        if (button >= FirstCategory && Enum.IsDefined((BountyGearCategory)(button - FirstCategory)))
        {
            DisplayTo(_player, _npc, _shop, (BountyGearCategory)(button - FirstCategory));
            return;
        }

        bool gold = button >= FirstGold;
        int index = button - (gold ? FirstGold : FirstTrophy);
        if (index < 0 || index >= _stock.Length || _stock[index] is not { } offer)
            return;

        var result = Gambling.BuyRegional(_player, _npc, _shop, _category, offer, _level,
            gold ? BountyPayment.Gold : BountyPayment.Trophies, Random.Shared);
        _player.SendMessage(result switch
        {
            GambleResult.Purchased => "The revealed item is in your backpack.",
            GambleResult.Refunded => "That roll has no eligible item. Nothing was charged.",
            GambleResult.NoFunds => "You cannot afford that gamble.",
            GambleResult.NoCapacity => "Your backpack cannot hold that purchase.",
            _ => "That quote is no longer valid."
        });
        if (result is GambleResult.Purchased or GambleResult.Refunded)
            _stock[index] = null;
        _player.SendGump(new RegionalGamblingGump(_player, _npc, _shop, _category, _level, _stock));
    }
}
