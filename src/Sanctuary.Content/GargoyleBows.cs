// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal static class GargoyleBows
{
    // Named Sanctuary items keep the weapon families from their own definitions.
    internal static bool Matches(Item item) => item is Boomerang or Cyclone or SoulGlaive
        && ClassGear.State(item)?.NamedItemId == null;

    internal static bool CanUse(Item item, PlayerMobile player) =>
        !Matches(item) || item.CheckRace(player.Race);

    internal static NormalBase Stats(Item item, NormalBase basis)
    {
        if (basis == null || !Matches(item))
            return basis;

        var (normal, exceptional, elite) = item switch
        {
            Boomerang => ("lbw", "8lb", "6lb"),
            Cyclone => ("cbw", "8cb", "6cb"),
            _ => ("lwb", "8lw", "6lw")
        };
        return EquipmentBalance.Base(basis.Tier switch
        {
            "Exceptional" => exceptional,
            "Elite" => elite,
            _ => normal
        });
    }
}
