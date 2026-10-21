// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class RecognitionGump : DynamicGump
{
    private readonly Serial _owner;
    private readonly string _site;
    private readonly NetState _connection;
    private readonly RecognitionState _state;
    private readonly Item _wardrobe;
    private bool _used;
    private readonly long _token;
    public override bool Singleton => true;
    private RecognitionGump(PlayerMobile player, string site, Item wardrobe) : base(35, 35)
    {
        _owner = player.Serial; _site = site; _connection = player.NetState; _wardrobe = wardrobe;
        _state = TownRecognition.Profiles.Find(player); _token = TownRecognition.BeginSession(player);
    }
    public static void DisplayTo(PlayerMobile player, string site, Item wardrobe)
    {
        if (player is { Deleted: false, NetState: not null } && ClassProfessions.Affected(player))
        { player.SendGump(new RecognitionGump(player, site, wardrobe)); }
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 555, 435, 5054);
        b.AddLabel(22, 18, 0x481, "Civic recognition");
        if (_site == null) { b.AddLabel(22, 47, 0x47E, "Visit a town-hall office to receive recognition"); }
        else { b.AddLabel(22, 47, 0x47E, $"{TownRecognition.Hall(_site).Town} town-hall office"); }
        Row(ref b, RecognitionHonor.Hero, 95);
        Row(ref b, RecognitionHonor.Pillar, 155);
        b.AddHtml(22, 220, 510, 72,
            "<BASEFONT COLOR=#FFFFFF>Class level 80 earns Hero of the People. Any crafting skill at 100.0 earns Pillar of Society; gathering does not qualify. Cartography is a craft.</BASEFONT>", background: false, scrollbar: false);
        if (_state.Earned != 0 && _wardrobe != null)
        { b.AddButton(22, 300, 4005, 4007, 5); b.AddLabel(57, 300, 0x481, "Open wardrobe — free craft switching"); }
        else { b.AddLabel(22, 305, 0x47E, _state.Earned != 0 ? "Permanent wardrobe and housing rights earned" : "Recognition unlocks wardrobe and housing rights"); }
        if (_site != null && TownRecognition.Pending(World.FindMobile(_owner)))
        { b.AddButton(22, 345, 4005, 4007, 3); b.AddLabel(57, 345, 0x481, "Collect pending awards"); }
        else
        { b.AddLabel(22, 345, 0x47E, "First wardrobe awarded free."); }
        if (_state.Earned == RecognitionHonor.Both && _wardrobe != null)
        { b.AddButton(22, 390, 4005, 4007, 4); b.AddLabel(57, 390, 0x481, "Reset active class points — 5,000 gold"); }
        else if (_state.Earned == RecognitionHonor.Both)
        { b.AddLabel(22, 390, 0x47E, "Use a nearby placed or public wardrobe for point resets."); }
        else { b.AddLabel(22, 390, 0x47E, "Both ceremonies unlock active-class point resets."); }
        if (_site != null && _state.Earned != 0)
        { b.AddButton(300, 345, 4005, 4007, 6); b.AddLabel(335, 345, 0x481, $"Wardrobe: {Housing.WardrobePrice} gold"); }
        b.AddButton(510, 18, 4017, 4019, 0);
    }
    private void Row(ref DynamicGumpBuilder b, RecognitionHonor honor, int y)
    {
        b.AddLabel(22, y, 0x481, $"{TownRecognition.Title(honor)}");
        if ((_state.Earned & honor) != 0) { b.AddLabel(22, y + 25, 0x47E, "Honor earned"); }
        else if ((_state.Eligible & honor) == 0) { b.AddLabel(22, y + 25, 0x47E, "Not yet eligible"); }
        else if (_site == null) { b.AddLabel(22, y + 25, 0x47E, "Eligible — visit any supported town hall"); }
        else
        { b.AddButton(300, y, 4005, 4007, (int)honor); b.AddLabel(335, y, 0x481, "Receive recognition"); }
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || !ReferenceEquals(sender, _connection) || sender.Mobile is not PlayerMobile player || player.Serial != _owner
            || !TownRecognition.SessionValid(player, _token))
        { return; }
        _used = true;
        if (info.ButtonID == 0) { return; }
        if (info.ButtonID == 6) { Housing.ReplaceWardrobe(player, _site); return; }
        if (info.ButtonID == 5) { WardrobeGump.DisplayTo(player, _wardrobe); return; }
        if (info.ButtonID == 4) { ClassRespec.DisplayTo(player, _wardrobe); return; }
        if (info.ButtonID == 3) { TownRecognition.Collect(player, _site); }
        else if (info.ButtonID is 1 or 2) { TownRecognition.Claim(player, _site, (RecognitionHonor)info.ButtonID); }
        TownRecognition.Open(player, TownRecognition.AtOffice(player, _site) ? _site : null);
    }
}
