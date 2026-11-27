// SPDX-License-Identifier: GPL-3.0-or-later
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class CoveGateRune : RecallRune
{
    internal static readonly Point3D Gates = new(2285, 1210, 0);
    internal const string Label = "Cove Gates";

    public CoveGateRune()
    {
        Description = Label;
        Target = Gates;
        TargetMap = Map.Trammel;
        Marked = true;
        MountedTravel = RuneMountTravel.Allowed;
        LootType = LootType.Blessed;
    }

    internal static void Award(PlayerMobile player) =>
        DungeonEntranceRunes.Award(player, DungeonEntranceRunes.Cove, announceRepeat: false, announceMissingBook: false);
}
