// SPDX-License-Identifier: GPL-3.0-or-later
using ModernUO.Serialization;
using Server;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class GraveyardHealer : BaseHealer
{
    [Constructible]
    public GraveyardHealer()
    {
        Name = "Seren";
        Title = "the graveyard healer";
        CantWalk = true;
    }

    public override bool IsInvulnerable => true;
    public override bool HealsYoungPlayers => false;
}
