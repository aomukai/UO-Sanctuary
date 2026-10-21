// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class WardrobeGearGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    private readonly NetState _connection;
    private readonly Item _wardrobe;
    private readonly long _token;
    private readonly int _page;
    private readonly (WardrobeSet Set, Item Item)[] _items;
    private bool _used;
    public override bool Singleton => true;
    private WardrobeGearGump(PlayerMobile owner, Item wardrobe, int page) : base(35, 35)
    {
        _owner = owner; _wardrobe = wardrobe; _connection = owner.NetState;
        _token = TownRecognition.BeginSession(owner); _page = page;
        _items = Enum.GetValues<DiabloClass>().Select(c => WardrobeEquipment.Instance.Find(owner.Serial, c))
            .Where(s => s != null).SelectMany(s => s.Items.Select(i => (s, i))).ToArray();
    }
    internal static void DisplayTo(PlayerMobile player, Item wardrobe, int page = 0)
    {
        if (!WardrobeAccess.Ready(player, wardrobe) || player.NetState == null) { return; }
        player.CloseGump<WardrobeGump>(); player.CloseGump<RespecGump>(); player.CloseGump<RecognitionGump>();
        player.SendGump(new WardrobeGearGump(player, wardrobe, page));
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 555, 435, 5054);
        b.AddLabel(22, 18, 0x481, "Stored class equipment — retrieve to backpack");
        b.AddLabel(22, 48, 0x47E, "Retrieve gear to share it. Stored gear adds no carried weight.");
        int start = _page * 10;
        for (int i = start; i < Math.Min(start + 10, _items.Length); i++)
        {
            var (set, item) = _items[i]; int y = 85 + (i - start) * 27;
            b.AddButton(22, y, 4005, 4007, 100 + i);
            b.AddLabel(57, y, 0x481, $"{DiabloClasses.DisplayName(set.CharacterClass, _owner.Female)}: {item.Name ?? item.GetType().Name}");
        }
        if (_items.Length == 0) { b.AddLabel(22, 85, 0x47E, "No stored equipment."); }
        if (_page > 0) { b.AddButton(22, 375, 4014, 4016, 1); b.AddLabel(57, 375, 0x481, "Previous"); }
        if (start + 10 < _items.Length) { b.AddButton(190, 375, 4005, 4007, 2); b.AddLabel(225, 375, 0x481, "Next"); }
        b.AddButton(400, 375, 4005, 4007, 3); b.AddLabel(435, 375, 0x481, "Wardrobe");
        b.AddButton(510, 18, 4017, 4019, 0);
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || sender != _connection || sender.Mobile != _owner || !TownRecognition.SessionValid(_owner, _token)
            || !WardrobeAccess.Ready(_owner, _wardrobe)) { return; }
        _used = true;
        int index = info.ButtonID - 100;
        if (index >= _page * 10 && index < Math.Min(_page * 10 + 10, _items.Length))
        {
            var (set, item) = _items[index];
            if (!WardrobeTransaction.Retrieve(_owner, _wardrobe, _token, set, item))
            { _owner.SendMessage("The item could not be retrieved. Check backpack capacity and reopen the wardrobe."); }
            DisplayTo(_owner, _wardrobe); return;
        }
        if (info.ButtonID == 1 && _page > 0) { DisplayTo(_owner, _wardrobe, _page - 1); return; }
        if (info.ButtonID == 2 && (_page + 1) * 10 < _items.Length) { DisplayTo(_owner, _wardrobe, _page + 1); return; }
        if (info.ButtonID == 3) { WardrobeGump.DisplayTo(_owner, _wardrobe); return; }
        TownRecognition.EndSession(_owner);
    }
}
