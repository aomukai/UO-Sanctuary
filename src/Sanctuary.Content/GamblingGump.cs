// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class GamblingGump : DynamicGump
{
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static long _sequence;
    private readonly PlayerMobile _player;
    private readonly TavernGambler _npc;
    private readonly NetState _connection;
    private readonly GambleOffer[] _stock;
    private readonly int _level, _page;
    private readonly long _session;
    private readonly Random _random = Random.Shared;
    private bool _used;
    public override bool Singleton => true;
    public static void Forget(Serial owner) => _sessions.Remove(owner);
    public static void Configure()
    {
        EventSink.Disconnected += from => _sessions.Remove(from.Serial);
    }
    private GamblingGump(PlayerMobile player, TavernGambler npc, GambleOffer[] stock, int level, int page = 0) : base(30, 30)
    {
        _player = player; _npc = npc; _stock = stock; _level = level; _page = page; _connection = player.NetState;
        _session = ++_sequence; _sessions[player.Serial] = _session;
    }
    public static void DisplayTo(PlayerMobile player, TavernGambler npc)
    {
        if (player.NetState == null || !Gambling.CanUse(player, npc))
        { player.SendMessage("Approach the gambler while alive and permitted to trade."); return; }
        int level = Gambling.Level(player);
        player.SendGump(new GamblingGump(player, npc, Gambling.Stock(level, Random.Shared), level));
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 555, 450, 5054);
        b.AddLabel(20, 18, 0x481, "Tavern gambling"); b.AddButton(510, 18, 4017, 4019, 0);
        b.AddLabel(340, 18, 0x47E, $"Stock page {_page + 1}/2");
        b.AddLabel(20, 48, 0x47E, "Choose a base. Reveal its tier and properties after purchase.");
        b.AddLabel(20, 72, 0x47E, "An unavailable named item refunds the stake, without a new roll.");
        for (int i = _page * 7, row = 0; i < _stock.Length && row < 7; i++, row++)
        {
            int y = 105 + row * 40;
            var offer = _stock[i];
            if (offer == null) { continue; }
            string name = offer.Variant.Name;
            if (name.Length > 32) { name = name[..29] + "..."; }
            b.AddItem(20, y, offer.Variant.Graphic);
            b.AddLabel(80, y + 6, 0x481, name);
            b.AddLabel(340, y + 6, 0x47E, $"{offer.Price} gold");
            b.AddButton(490, y + 6, 4005, 4007, 100 + i);
        }
        b.AddLabel(20, 389, 0x47E, "Backpack first. Bank fallback on purchases of 2,000+ gold.");
        b.AddButton(20, 420, 4005, 4007, 1); b.AddLabel(55, 420, 0x481, "Refresh stock");
        if (_page > 0) { b.AddButton(170, 420, 4005, 4007, 2); b.AddLabel(205, 420, 0x481, "Previous"); }
        if (_page < 1) { b.AddButton(305, 420, 4005, 4007, 3); b.AddLabel(340, 420, 0x481, "Next"); }
        b.AddButton(450, 420, 4005, 4007, 0); b.AddLabel(485, 420, 0x481, "Leave");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || !ReferenceEquals(sender, _connection) || sender.Mobile != _player || _player.NetState != _connection
            || !_sessions.TryGetValue(_player.Serial, out var current) || current != _session) { return; }
        _used = true; _sessions.Remove(_player.Serial);
        if (info.ButtonID == 0) { return; }
        if (!Gambling.CanUse(_player, _npc) || Gambling.Level(_player) != _level)
        { _player.SendMessage("Your gambling quote has changed. Reopen the shop. Nothing was charged."); return; }
        if (info.ButtonID == 1) { DisplayTo(_player, _npc); return; }
        if (info.ButtonID is 2 or 3)
        {
            int page = _page + (info.ButtonID == 2 ? -1 : 1);
            if (page is >= 0 and <= 1) { _player.SendGump(new GamblingGump(_player, _npc, _stock, _level, page)); }
            return;
        }
        int index = info.ButtonID - 100;
        if (index < 0 || index >= _stock.Length || index / 7 != _page || _stock[index] == null) { return; }
        var result = Gambling.Buy(_player, _npc, _stock[index], _level, _random);
        _player.SendMessage(result switch
        {
            GambleResult.Purchased => "Purchase complete. The revealed item is in your backpack.",
            GambleResult.Refunded => "That named item is awaiting its remaining effects. Your stake is refunded; no replacement was rolled.",
            GambleResult.NoFunds => "You cannot afford that gamble. Nothing was charged.",
            GambleResult.NoCapacity => "Your backpack cannot hold that purchase. Nothing was charged.",
            _ => "That quote is no longer valid. Nothing was charged."
        });
        if (result is GambleResult.Purchased or GambleResult.Refunded) { _stock[index] = null; }
        if (Gambling.CanUse(_player, _npc)) { _player.SendGump(new GamblingGump(_player, _npc, _stock, _level, _page)); }
    }
}
