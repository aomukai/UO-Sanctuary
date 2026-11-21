// SPDX-License-Identifier: GPL-3.0-or-later
using ModernUO.Serialization;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class NewHavenHealer : BaseHealer
{
    internal static readonly Point3D House = new(3463, 2558, 35);

    [Constructible]
    public NewHavenHealer()
    {
        Name = "Merryn";
        Title = "the healer";
        CantWalk = true;
    }

    public override bool IsInvulnerable => true;
    public override bool HealsYoungPlayers => false;

    protected override ResurrectGump CreateFreeResurrectionGump(Mobile ghost) => new MerrynResurrectionGump(ghost);

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player || player.Map != Map || !InRange(player, 4) || !InLOS(player))
            return;
        if (!player.Alive)
        {
            if (Map.CanFit(player.Location, 16, false, false))
                OfferResurrection(player);
            return;
        }

        if (MercenaryCare.ActiveStoryCompanion(player) is { IsDeadPet: true } companion)
        {
            if (MercenaryCare.Quote(player, companion, this) is { } request)
                player.SendGump(new MerrynResurrectionGump(player, request));
            else
                player.SendMessage("Bring thy companion's spirit close, and let the dust of battle settle.");
        }
    }
}

internal sealed class MerrynResurrectionGump(Mobile player, MercenaryServiceRequest companion = null)
    : ResurrectGump(player, ResurrectMessage.Healer)
{
    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 400, 350, 2600);
        builder.AddHtml(0, 20, 400, 35, "<center>Merryn</center>");
        builder.AddHtml(50, 70, 300, 105,
            companion == null ? "Thy thread is not yet severed. Come closer, and I shall guide thee back to the living."
                : "Thy companion's spirit lingers here. If thou art ready, I shall guide them back to the living.", background: true);

        builder.AddButton(200, 227, 4005, 4007, 0);
        builder.AddHtml(235, 230, 110, 35, "Not yet");
        builder.AddButton(65, 227, 4005, 4007, 1);
        builder.AddHtml(100, 230, 110, 35, companion == null ? "Return to life" : "Call them back");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (companion == null)
            base.OnResponse(sender, info);
        else if (info.ButtonID == 1 && sender.Mobile == companion.Owner)
            sender.Mobile.SendMessage(MercenaryCare.Commit(companion) ? $"{companion.Mercenary.Name} has returned to the living."
                : "I could not reach thy companion's spirit. Bring them close and speak to me again.");
    }
}
