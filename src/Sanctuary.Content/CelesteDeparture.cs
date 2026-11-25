// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public partial class Celeste
{
    internal static bool LeavingCovetous(PlayerMobile player, Map map, Point3D destination) =>
        player.Map == Map.Trammel && player.Region.IsPartOf("Covetous")
        && (map != Map.Trammel || !Region.Find(destination, map).IsPartOf("Covetous"));

    internal static bool StopDeparture(PlayerMobile player, Func<bool> atExit, Action continueTravel = null)
    {
        if (!player.Alive || player.NetState?.Running != true
            || MercenaryCare.ActiveStoryCompanion(player) is not Celeste { GearRecovered: false } celeste
            || !celeste.OwnedBy(player) || !atExit())
            return false;

        if (!StatNetwork.SupportsCurrentProtocol(player) || player.HasGump<CelesteDepartureConfirmation>()
            || ScriptedDialogue.HasScopedConversation(player, celeste))
            return true;

        bool CanDecide(PlayerMobile owner, Mobile _) => owner.NetState?.Running == true && owner.Alive
            && !celeste.GearRecovered && celeste.OwnedBy(owner) && atExit();

        ScriptedDialogue.Show(player, celeste,
        [
            new("Wait. We still haven't found my armour or weapons. Wilt thou come back with me for them?",
                [new("I'll Go Back with Thee", 0, (_, _) => { }), new("Where Is Thy Gear?", 1), new("We Have to Leave", 2)]),
            new("Most likely in the torture chamber next to my cell. That's where I last saw the gaoler take my things.",
                [new("I'll Go Back with Thee", 0, (_, _) => { }), new("We Have to Leave", 2)]),
            new("My father gave me that Order Shield. I will not leave without it. If thou must go, I'll have to get it back alone.",
                [new("I'll Help Thee Find It", 0, (_, _) => { }), new("Leave Without Celeste", 0, (owner, speaker) =>
                {
                    owner.SendGump(new CelesteDepartureConfirmation(owner, () =>
                    {
                        if (CanDecide(owner, speaker) && MercenaryCare.AbandonCeleste(owner, celeste))
                            continueTravel?.Invoke();
                    }));
                })])
        ], canReply: CanDecide);
        return true;
    }
}

internal sealed class CelesteDepartureConfirmation(PlayerMobile owner, Action abandon) : DynamicGump(100, 75)
{
    public override bool Singleton => true;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 475, 166, 5054);
        builder.AddHtml(22, 17, 430, 27, "<BASEFONT COLOR=#FFFFFF><B>Leave Celeste behind?</B></BASEFONT>");
        builder.AddHtml(22, 46, 430, 57,
            "<BASEFONT COLOR=#FFFFFF>She will return to recover her father’s shield alone. You will permanently lose the opportunity to recruit her on this character.</BASEFONT>");
        builder.AddButton(22, 118, 4005, 4007, 1);
        builder.AddLabel(58, 118, 0x481, "Leave Celeste Behind");
        builder.AddButton(339, 118, 4017, 4019, 0);
        builder.AddLabel(375, 118, 0x481, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile == owner && info.ButtonID == 1)
            abandon();
    }
}
