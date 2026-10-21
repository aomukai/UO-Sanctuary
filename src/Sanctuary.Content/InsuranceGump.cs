// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class InsuranceGump : DynamicGump
{
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static long _sequence;
    private readonly PlayerMobile _player;
    private readonly NetState _connection;
    private readonly long _session;
    private readonly InsuranceQuote[] _quotes;
    private readonly bool[] _selected;
    private readonly int _page;
    private readonly bool _confirm;
    private bool _used;
    public override bool Singleton => true;
    public static void Forget(Serial owner) => _sessions.Remove(owner);
    private InsuranceGump(PlayerMobile player, InsuranceQuote[] quotes, bool[] selected, int page, bool confirm) : base(30, 30)
    {
        _player = player; _connection = player.NetState; _quotes = quotes; _selected = selected;
        _page = page; _confirm = confirm; _session = ++_sequence; _sessions[player.Serial] = _session;
    }
    public static void DisplayTo(PlayerMobile player)
    {
        if (player is not { Deleted: false, Alive: true, NetState: not null }) { return; }
        EquipmentInsurance.AdoptCarried(player);
        var quotes = EquipmentInsurance.Carried(player).Where(i => EquipmentInsurance.Cost(i) > 0)
            .OrderBy(i => i.Serial.Value).Take(1024).Select(EquipmentInsurance.Quote).ToArray();
        Show(player, quotes, quotes.Select(q => q.Policy?.Active == true).ToArray(), 0, false);
    }
    private static void Show(PlayerMobile p, InsuranceQuote[] q, bool[] selected, int page, bool confirm)
        => p.SendGump(new InsuranceGump(p, q, selected, page, confirm));
    private int Charge()
    {
        int total = 0;
        for (int i = 0; i < _quotes.Length; i++)
        { if (_selected[i] && _quotes[i].Policy?.Paid != true) { total = checked(total + _quotes[i].Cost); } }
        return total;
    }
    private int Renewal()
    {
        int total = 0;
        for (int i = 0; i < _quotes.Length; i++) { if (_selected[i]) { total = checked(total + _quotes[i].Cost); } }
        return total;
    }
    private static void Button(ref DynamicGumpBuilder b, int x, int y, int id, string label)
    { b.AddButton(x, y, 4005, 4007, id); b.AddLabel(x + 35, y, 0x481, label); }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 555, 430, 5054);
        b.AddLabel(22, 18, 0x481, _confirm ? "Confirm equipment insurance" : "Equipment insurance");
        b.AddButton(510, 18, 4017, 4019, 0);
        b.AddLabel(22, 47, 0x47E, $"Bank: {Server.Mobiles.Banker.GetBalance(_player)} gold");
        if (_confirm)
        {
            b.AddLabel(22, 91, 0x481, $"Pay now: {Charge()} gold from your bank");
            b.AddLabel(22, 127, 0x481, "Each paid policy covers the item's next death, with no time expiry.");
            b.AddLabel(22, 163, 0x481, "Covered equipment stays in your backpack on death.");
            b.AddLabel(22, 199, 0x481, "Cancelling leaves unused credit; transferring ownership cancels it.");
            b.AddLabel(22, 235, 0x47E, _player.AutoRenewInsurance ? "Auto-renew is ON: pay again after protection is used." : "Auto-renew is OFF: protection ends after it is used.");
            b.AddLabel(22, 271, 0x481, "If renewal funds run out, this death is still covered.");
            Button(ref b, 22, 383, 5, "Back"); Button(ref b, 310, 383, 6, "Confirm changes");
            return;
        }
        Button(ref b, 315, 46, 1, _player.AutoRenewInsurance ? "Auto-renew: ON" : "Auto-renew: OFF");
        b.AddLabel(22, 76, 0x481, "White, Magic and Rare equipment; one paid death, no time expiry.");
        b.AddLabel(22, 96, 0x481, "Identify equipment first. Bags, gold and consumables are excluded.");
        if (_quotes.Length == 0) { b.AddLabel(22, 155, 0x47E, "No eligible equipment is currently worn or carried."); }
        for (int i = _page * 3, row = 0; i < _quotes.Length && row < 3; i++, row++)
        {
            var q = _quotes[i]; int y = 130 + row * 61;
            string name = q.Item.Name ?? ClassGear.Base(q.Item)?.Name ?? q.Item.GetType().Name;
            if (name.Length > 36) { name = name[..33] + "..."; }
            b.AddLabel(22, y, 0x481, name); b.AddItemProperty(q.Item.Serial);
            b.AddLabel(22, y + 22, 0x47E, q.Policy?.Paid == true ? "Prepaid credit available" : "No prepaid credit");
            b.AddLabel(365, y + 22, 0x481, $"{q.Cost} gold");
            b.AddButton(500, y + 10, _selected[i] ? 211 : 210, _selected[i] ? 210 : 211, 100 + i);
        }
        b.AddLabel(22, 319, 0x47E, $"Pay now: {Charge()} gold; full renewal: {Renewal()} gold");
        if (_page > 0) { Button(ref b, 22, 349, 3, "Previous"); }
        if ((_page + 1) * 3 < _quotes.Length) { Button(ref b, 365, 349, 4, "Next"); }
        Button(ref b, 22, 389, 7, "Refresh equipment"); Button(ref b, 310, 389, 2, "Review changes");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || !ReferenceEquals(sender, _connection) || sender.Mobile != _player || _player.Deleted
            || !_player.Alive || _player.NetState != _connection || _sessions.GetValueOrDefault(_player.Serial) != _session)
        { return; }
        _used = true;
        if (info.ButtonID == 0) { return; }
        if (info.ButtonID == 7) { DisplayTo(_player); return; }
        if (_quotes.Any(q => !EquipmentInsurance.Current(_player, q)))
        { _player.SendMessage("Your equipment or insurance changed. Review a fresh quote."); DisplayTo(_player); return; }
        if (_confirm)
        {
            if (info.ButtonID == 6) { EquipmentInsurance.Apply(_player, _quotes, _selected); DisplayTo(_player); }
            else if (info.ButtonID == 5) { Show(_player, _quotes, _selected, _page, false); }
            return;
        }
        int page = _page; bool confirm = false;
        switch (info.ButtonID)
        {
            case 1:
                _player.AutoRenewInsurance = !_player.AutoRenewInsurance;
                _player.SendMessage(_player.AutoRenewInsurance ? "Automatic insurance renewal enabled; renewal is paid from your bank." : "Automatic insurance renewal disabled.");
                break;
            case 2: confirm = true; break;
            case 3: if (page > 0) { page--; } break;
            case 4: if ((page + 1) * 3 < _quotes.Length) { page++; } break;
            default:
                int index = info.ButtonID - 100;
                if (index >= 0 && index < _selected.Length && index / 3 == page) { _selected[index] = !_selected[index]; }
                else { return; }
                break;
        }
        Show(_player, _quotes, _selected, page, confirm);
    }
}
