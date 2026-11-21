// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal static class AssassinLockpicking
{
    internal static bool CanPick(Mobile from, Item item) =>
        from is PlayerMobile { Alive: true, Deleted: false } player
        && ClassCreation.Profiles.TryGet(player.Serial, out var profile) && profile.Class == DiabloClass.Assassin
        && item.Map == player.Map && !item.QuestItem && !item.InSecureTrade && item.IsAccessibleTo(player)
        && player.InRange(item.GetWorldLocation(), 2) && player.CanSee(item)
        && (item.RootParent is not Mobile owner || owner == player)
        // The Doom healer's door uses its lock as a ghosts-only rule.
        && (item is not BaseDoor { Hue: 0x44E } || item.Map != Map.Malas);
}
