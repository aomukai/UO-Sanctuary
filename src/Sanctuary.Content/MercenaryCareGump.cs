// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class MercenaryCareGump : DynamicGump
{
    private readonly MercenaryServiceRequest _request;
    public override bool Singleton => true;
    private MercenaryCareGump(MercenaryServiceRequest request) : base(60, 45) { _request = request; }
    public static void DisplayTo(PlayerMobile owner, BaseCreature mercenary, BaseCreature healer)
    {
        if (owner.NetState == null) { return; }
        if (MercenaryCare.Quote(owner, mercenary, healer) is not { } quote)
        { owner.SendMessage("Bring your mercenary to the healer outside combat. No supported treatment is needed or available here."); return; }
        owner.SendGump(new MercenaryCareGump(quote));
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 500, 240, 5054);
        b.AddLabel(22, 20, 0x481, _request.Resurrection ? "Mercenary resurrection" : "Mercenary healing");
        b.AddLabel(22, 55, 0x481, $"Mercenary: {_request.Mercenary.Name}");
        b.AddLabel(22, 90, 0x47E, $"Bank payment: {_request.Gold} gold.");
        b.AddLabel(22, 120, 0x47E, "Equipment and stored healing potions are retained.");
        b.AddLabel(22, 150, 0x47E, "Cancel or insufficient gold changes nothing.");
        b.AddButton(22, 195, 4005, 4007, 1); b.AddLabel(57, 195, 0x481, "Confirm");
        b.AddButton(335, 195, 4017, 4019, 0); b.AddLabel(370, 195, 0x481, "Cancel");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile != _request.Owner || info.ButtonID != 1) { return; }
        sender.Mobile.SendMessage(MercenaryCare.Commit(_request) ? "Your mercenary has been treated." : "The treatment could not proceed. No payment was taken.");
    }
}
