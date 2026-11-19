// SPDX-License-Identifier: GPL-3.0-or-later
using ModernUO.Serialization;
using Server;
using Server.Gumps;
using Server.Mobiles;

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
        if (from is PlayerMobile { Alive: false } ghost && ghost.Map == Map && InRange(ghost, 4) && InLOS(ghost)
            && Map.CanFit(ghost.Location, 16, false, false))
            OfferResurrection(ghost);
    }
}

internal sealed class MerrynResurrectionGump(Mobile ghost) : ResurrectGump(ghost, ResurrectMessage.Healer)
{
    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 400, 350, 2600);
        builder.AddHtml(0, 20, 400, 35, "<center>Merryn</center>");
        builder.AddHtml(50, 70, 300, 105,
            "Thy thread is not yet severed. Come closer, and I shall guide thee back to the living.", background: true);

        builder.AddButton(200, 227, 4005, 4007, 0);
        builder.AddHtml(235, 230, 110, 35, "Not yet");
        builder.AddButton(65, 227, 4005, 4007, 1);
        builder.AddHtml(100, 230, 110, 35, "Return to life");
    }
}
