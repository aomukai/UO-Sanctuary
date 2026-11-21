// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class ShrineResurrection
{
    public static void Configure() => Ankhs.ResurrectionOffer = Offer;

    internal static bool Near(Mobile mobile, Item shrine, int range) =>
        shrine is { Deleted: false, Parent: null, Map: { } map } && map != Map.Internal && mobile.Map == map
        && mobile.InRange(shrine, range) && Math.Abs(mobile.Z - shrine.Z) < 16 && mobile.CanSee(shrine) && mobile.InLOS(shrine);

    private static bool Offer(Mobile mobile, Item shrine)
    {
        if (mobile is not PlayerMobile player)
            return false;
        if (player.Alive)
            return OfferCompanion(player, shrine, showFailure: true);

        if (Near(player, shrine, Ankhs.ResurrectRange))
            player.SendGump(new ShrineResurrectionGump(player, shrine));
        return true;
    }

    internal static bool OfferCompanion(PlayerMobile player, Item shrine, bool showFailure = false)
    {
        if (MercenaryCare.ActiveStoryCompanion(player) is not { IsDeadPet: true } companion)
            return false;

        if (MercenaryCare.CanRaiseAtShrine(player, companion, shrine))
            player.SendGump(new ShrineResurrectionGump(player, shrine, companion));
        else if (showFailure)
            player.SendMessage("Bring thy companion's spirit close to the shrine once the fighting has passed.");
        return true;
    }
}

internal sealed class ShrineResurrectionGump(PlayerMobile player, Item shrine, BaseCreature companion = null)
    : ResurrectGump(player, ResurrectMessage.VirtueShrine)
{
    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 400, 350, 2600);
        builder.AddHtml(0, 20, 400, 35, "<center>Shrine</center>");
        builder.AddHtml(50, 70, 300, 105, companion == null
            ? "The shrine's light reaches beyond the veil. Wilt thou return to the living?"
            : "Thy companion's spirit lingers in the light of the shrine. Wilt thou call them back?", background: true);
        builder.AddButton(200, 227, 4005, 4007, 0);
        builder.AddHtml(235, 230, 110, 35, "Not yet");
        builder.AddButton(65, 227, 4005, 4007, 1);
        builder.AddHtml(100, 230, 110, 35, companion == null ? "Return to life" : "Call companion back");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID != 1 || sender.Mobile != player)
            return;

        if (companion != null)
        {
            if (!MercenaryCare.RaiseAtShrine(player, companion, shrine))
                player.SendMessage("The shrine can no longer reach thy companion's spirit.");
        }
        else if (!player.Alive && ShrineResurrection.Near(player, shrine, Ankhs.ResurrectRange))
        {
            base.OnResponse(sender, info);
            if (player.Alive)
                ShrineResurrection.OfferCompanion(player, shrine);
        }
    }
}
