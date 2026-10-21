// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class CorpseEscrowGump : DynamicGump
{
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static long _sequence;
    private readonly PlayerMobile _player;
    private readonly Banker _banker;
    private readonly NetState _connection;
    private readonly long _session;
    private readonly CorpseClaim[] _claims;
    private readonly EscrowQuote _quote;
    private readonly int _page;
    private readonly bool _forfeit;
    private bool _used;
    public override bool Singleton => true;
    public static void Forget(Serial owner) => _sessions.Remove(owner);
    public static void Configure() => EventSink.Disconnected += from => Forget(from.Serial);
    private CorpseEscrowGump(PlayerMobile player, Banker banker, CorpseClaim[] claims, EscrowQuote quote, int page, bool forfeit) : base(30, 30)
    {
        _player = player; _banker = banker; _claims = claims; _quote = quote; _page = page; _forfeit = forfeit;
        _connection = player.NetState; _session = ++_sequence; _sessions[player.Serial] = _session;
    }
    public static void DisplayTo(PlayerMobile player, Banker banker)
    {
        if (player.NetState == null || !CorpseEscrow.CanUse(player, banker))
        { player.SendMessage("Speak to a nearby banker while alive and permitted to bank."); return; }
        player.SendGump(new CorpseEscrowGump(player, banker, CorpseEscrow.Claims(player), null, 0, false));
    }
    private void Show(EscrowQuote quote, int page, bool forfeit = false)
        => _player.SendGump(new CorpseEscrowGump(_player, _banker, _claims, quote, page, forfeit));
    private static void Button(ref DynamicGumpBuilder b, int x, int y, int id, string label)
    { b.AddButton(x, y, 4005, 4007, id); b.AddLabel(x + 35, y, 0x481, label); }
    private static string Place(CorpseClaim c) => $"{Map.Maps[c.Facet]?.Name ?? "Unknown"} {c.Location.X}, {c.Location.Y}, {c.Location.Z}";
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 555, 450, 5054);
        b.AddLabel(22, 18, 0x481, _forfeit ? "Confirm permanent forfeiture" : "Banker — corpse escrow");
        b.AddButton(510, 18, 4017, 4019, 0);
        b.AddLabel(22, 48, 0x47E, $"Bank: {Banker.GetBalance(_player)} gold");
        if (_forfeit)
        {
            b.AddLabel(22, 100, 0x481, "This permanently deletes ALL belongings in this claim.");
            b.AddLabel(22, 140, 0x481, $"Death: {_quote.Claim.Death:u}");
            b.AddLabel(22, 175, 0x481, Place(_quote.Claim));
            b.AddLabel(22, 215, 0x481, $"Contents: {_quote.Items.Length} item stacks/objects, including nested contents.");
            b.AddLabel(22, 265, 0x47E, "No refund. Other claims and your bank remain untouched.");
            b.AddLabel(22, 300, 0x47E, "Closing or going back keeps everything in escrow.");
            Button(ref b, 22, 395, 1, "Keep belongings"); Button(ref b, 285, 395, 4, "Confirm forfeiture");
            return;
        }
        if (_quote == null)
        {
            b.AddLabel(22, 80, 0x481, "Unrecovered corpses enter escrow after 24 real hours.");
            if (_claims.Length == 0) { b.AddLabel(22, 140, 0x47E, "No escrowed belongings. Closing never forfeits anything."); }
            for (int i = _page * 3, row = 0; i < _claims.Length && row < 3; i++, row++)
            {
                var claim = _claims[i]; int y = 120 + row * 80;
                b.AddLabel(22, y, 0x481, $"Death: {claim.Death:u}");
                b.AddLabel(22, y + 24, 0x47E, Place(claim));
                Button(ref b, 370, y + 12, 100 + i, "Review");
            }
            if (_page > 0) { Button(ref b, 22, 367, 5, "Previous"); }
            if ((_page + 1) * 3 < _claims.Length) { Button(ref b, 365, 367, 6, "Next"); }
            Button(ref b, 22, 407, 0, "Leave for later"); Button(ref b, 300, 407, 7, "Refresh claims");
            return;
        }
        b.AddLabel(22, 78, 0x481, $"Death: {_quote.Claim.Death:u}");
        b.AddLabel(22, 103, 0x47E, Place(_quote.Claim));
        b.AddLabel(22, 132, 0x481, $"Recover entire claim: {_quote.Fee} gold from bank");
        for (int i = _page * 3, row = 0; i < _quote.Items.Length && row < 3; i++, row++)
        {
            var entry = _quote.Items[i]; var item = World.FindItem(entry.Item);
            string name = item?.Name ?? (item == null ? "Missing item" : ClassGear.Base(item)?.Name ?? item.GetType().Name);
            if (name.Length > 38) { name = name[..35] + "..."; }
            b.AddLabel(22, 170 + row * 45, 0x481, $"{name} x{entry.Amount}");
            if (item != null) { b.AddItemProperty(item.Serial); }
        }
        if (_page > 0) { Button(ref b, 22, 309, 5, "Previous items"); }
        if ((_page + 1) * 3 < _quote.Items.Length) { Button(ref b, 340, 309, 6, "Next items"); }
        b.AddLabel(22, 350, 0x47E, "No funds or bank space? Nothing is charged or forfeited.");
        Button(ref b, 22, 380, 1, "Back to claims"); Button(ref b, 290, 380, 2, "Confirm recovery");
        Button(ref b, 22, 413, 0, "Leave for later"); Button(ref b, 290, 413, 3, "Surrender belongings...");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || !ReferenceEquals(sender, _connection) || sender.Mobile != _player || _player.NetState != _connection
            || _sessions.GetValueOrDefault(_player.Serial) != _session || !CorpseEscrow.CanUse(_player, _banker)) { return; }
        _used = true;
        int id = info.ButtonID;
        if (id == 0) { return; }
        if (_quote != null && !CorpseEscrow.Current(_player, _quote))
        { _player.SendMessage("These contents changed. Review a fresh claim."); DisplayTo(_player, _banker); return; }
        if (_forfeit)
        {
            if (id == 4) { CorpseEscrow.Forfeit(_player, _banker, _quote); DisplayTo(_player, _banker); }
            else if (id == 1) { Show(_quote, 0); }
            return;
        }
        if (id == 7 || id == 1) { DisplayTo(_player, _banker); return; }
        if (_quote != null)
        {
            if (id == 2) { CorpseEscrow.Recover(_player, _banker, _quote); DisplayTo(_player, _banker); }
            else if (id == 3) { Show(_quote, 0, true); }
            else if (id == 5 && _page > 0) { Show(_quote, _page - 1); }
            else if (id == 6 && (_page + 1) * 3 < _quote.Items.Length) { Show(_quote, _page + 1); }
            return;
        }
        if (id == 5 && _page > 0) { Show(null, _page - 1); }
        else if (id == 6 && (_page + 1) * 3 < _claims.Length) { Show(null, _page + 1); }
        else if (id >= 100 && id - 100 < _claims.Length && (id - 100) / 3 == _page)
        {
            var quote = CorpseEscrow.Quote(_player, _claims[id - 100]);
            if (quote != null) { Show(quote, 0); }
            else { DisplayTo(_player, _banker); }
        }
    }
}
