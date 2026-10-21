// SPDX-License-Identifier: GPL-3.0-or-later
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class PowerScrollGump : DynamicGump
{
    private readonly PowerScrollRequest _request;
    private bool _answered;
    public override bool Singleton => true;
    private PowerScrollGump(PowerScrollRequest request) : base(60, 120) { _request = request; }
    internal static void DisplayTo(PlayerMobile owner, PowerScroll scroll)
    {
        var request = ProfessionPowerScrolls.Quote(owner, scroll);
        if (request != null && owner.NetState != null) { owner.SendGump(new PowerScrollGump(request)); }
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        var q = _request;
        b.AddPage(); b.AddBackground(0, 0, 560, 310, 5054);
        b.AddLabel(22, 20, 0x481, "Increase skill cap");
        b.AddLabel(22, 65, 0x481, $"{ClassProfessions.Name(q.Skill)}");
        b.AddLabel(22, 105, 0x47E, $"Skill cap: {q.Cap / 10.0:F1} → {q.Target / 10.0:F1}");
        b.AddLabel(22, 140, 0x47E, $"Trained skill remains {q.Base / 10.0:F1}; your skill lock stays unchanged.");
        b.AddLabel(22, 175, 0x47E, "Permanent for this character, including inactive crafts.");
        b.AddLabel(22, 210, 0x47E, "Consumes this scroll. Other skills keep their existing caps.");
        b.AddButton(22, 245, 4005, 4007, 1); b.AddLabel(57, 245, 0x481, "Raise cap");
        b.AddButton(370, 245, 4017, 4019, 0); b.AddLabel(405, 245, 0x481, "Cancel");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_answered || sender != _request.Connection || sender.Mobile != _request.Owner) { return; }
        _answered = true;
        if (info.ButtonID == 1) { ProfessionPowerScrolls.Commit(_request); }
        else { _request.Claimed = true; }
    }
}
