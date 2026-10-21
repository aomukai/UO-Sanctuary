// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class EarnedTitleGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    private readonly NetState _connection;
    private readonly EarnedTitleState _state;
    private bool _used;
    private readonly long _token;
    public override bool Singleton => true;
    internal EarnedTitleGump(PlayerMobile player) : base(35, 35)
    { _owner = player; _connection = player.NetState; _state = EarnedTitles.Profiles.Find(player.Serial); _token = EarnedTitles.Begin(player); }
    public static void DisplayTo(PlayerMobile player)
    {
        if (!EarnedTitles.Applies(player) || !ClassProfessions.Ready(player) || player.NetState == null) { return; }
        EarnedTitles.Refresh(player);
        player.CloseGump<WardrobeGump>(); player.CloseGump<WardrobeGearGump>();
        player.SendGump(new EarnedTitleGump(player));
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 555, 435, 5054);
        b.AddLabel(22, 18, 0x481, "Earned titles — choose or hide each slot");
        b.AddLabel(22, 48, 0x47E, "Titles stay earned and can represent any of your classes or skills.");
        b.AddGroup(1);
        b.AddRadio(22, 78, 210, 211, _state.Prefix == 0, 100); b.AddLabel(48, 78, 0x481, "Hide class prefix");
        for (int i = 0; i < 7; i++)
        {
            var c = (DiabloClass)(101 + i); int y = 103 + 25 * i;
            bool earned = (_state.Classes & 1 << i) != 0;
            if (earned) { b.AddRadio(22, y, 210, 211, _state.Prefix == c, 101 + i); }
            b.AddLabel(48, y, earned ? 0x481 : 0x3B2, $"{EarnedTitles.Prefix(c)} ({DiabloClasses.DisplayName(c, _owner.Female)})");
        }
        b.AddLabel(22, 285, 0x47E, "Class level 99 unlocks its prefix.");
        b.AddLabel(22, 310, 0x47E, "Gathering titles grant no civic rights.");
        b.AddGroup(2);
        b.AddRadio(285, 78, 210, 211, _state.Skill == byte.MaxValue, 1255); b.AddLabel(311, 78, 0x481, "Hide skill suffix");
        for (int i = 0; i < EarnedTitles.Skills.Length; i++)
        {
            var skill = EarnedTitles.Skills[i]; byte rank = _state.Ranks[i]; int y = 101 + i * 18;
            if (rank > 0) { b.AddRadio(285, y, 210, 211, _state.Skill == (byte)skill, 1000 + (byte)skill); }
            string levels = rank switch { 1 => "100", 2 => "100/110", 3 => "100/110/120", _ => "locked" };
            b.AddLabel(311, y, rank > 0 ? 0x481 : 0x3B2, $"{EarnedTitles.Profession(skill)} ({levels})");
        }
        b.AddGroup(3);
        for (byte tier = 1; tier <= 3; tier++)
        {
            int x = 22 + (tier - 1) * 175;
            b.AddRadio(x, 355, 210, 211, (_state.Tier == 0 ? 1 : _state.Tier) == tier, 2000 + tier);
            b.AddLabel(x + 26, 355, 0x481, $"{EarnedTitles.Rank(tier)}");
        }
        b.AddLabel(22, 378, 0x47E, "Select an unlocked skill rank; either title may be hidden.");
        b.AddButton(22, 400, 4005, 4007, 1); b.AddLabel(57, 400, 0x481, "Apply titles");
        b.AddButton(285, 400, 4005, 4007, 2); b.AddLabel(320, 400, 0x481, "View paperdoll");
        b.AddButton(510, 18, 4017, 4019, 0);
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || !EarnedTitles.Session(_owner, _token) || sender != _connection || sender.Mobile != _owner || sender.Running != true
            || !ReferenceEquals(EarnedTitles.Profiles.Find(_owner.Serial), _state)) { return; }
        _used = true; EarnedTitles.End(_owner);
        if (info.ButtonID == 2)
        { if (ClassProfessions.Ready(_owner)) { EventSink.InvokePaperdollRequest(_owner, _owner); } return; }
        if (info.ButtonID != 1) { return; }
        int prefix = -1, skill = -1, tier = -1;
        foreach (int id in info.Switches)
        {
            if (id is >= 100 and <= 107 && prefix < 0) { prefix = id == 100 ? 0 : id; }
            else if (id >= 1000 && id <= 1255 && (id == 1255 || TradeSkills.Includes(id - 1000)) && skill < 0) { skill = id - 1000; }
            else if (id is >= 2001 and <= 2003 && tier < 0) { tier = id - 2000; }
            else { prefix = -1; break; }
        }
        if (prefix < 0 || skill < 0 || tier < 0 || !EarnedTitles.Choose(_owner, _state, (DiabloClass)prefix, (byte)skill, skill == 255 ? (byte)0 : (byte)tier))
        { _owner.SendMessage("Choose one earned prefix (or hide), one earned skill (or hide), and an unlocked rank. Reopen Titles if your choices changed."); }
        else { _owner.SendMessage("Your title choices are saved. Open your paperdoll to view them."); }
        DisplayTo(_owner);
    }
}
