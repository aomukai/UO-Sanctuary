// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Factions;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public static class RunebookTravel
{
    public static void Configure() => ClassMapHooks.RunebookTravel = Travel;

    private static void Travel(Mobile from, RunebookEntry entry)
    {
        if (from is not PlayerMobile player || !player.CheckAlive() || MountTravel.BlocksTravel(player))
            return;

        var map = entry.Map;
        var destination = entry.Location;
        if (player.Map == null || player.Map == Map.Internal || map == null || map == Map.Internal
            || destination.X < 0 || destination.X >= map.Width || destination.Y < 0 || destination.Y >= map.Height)
        {
            player.SendMessage("That destination is unavailable.");
            return;
        }

        if (player.Frozen || player.Paralyzed || player.Spell != null || HitRecovery.Active(player, Core.TickCount))
        {
            player.SendMessage("Thou canst not travel right now.");
            return;
        }
        if (Sigil.ExistsOn(player) || (map == Map.Felucca && player.Young) || (player.Murderer && map != Map.Felucca)
            || player.Criminal || StaminaSystem.IsOverloaded(player) || SpellHelper.CheckCombat(player))
        {
            player.SendMessage("Thou canst not travel under these circumstances.");
            return;
        }
        if (!SpellHelper.CheckTravel(player, TravelCheckType.RecallFrom, out _)
            || !SpellHelper.CheckTravel(player, map, destination, TravelCheckType.RecallTo, out _))
        {
            player.SendMessage("Thou canst not travel between these places.");
            return;
        }

        if (player.Map == map && player.Location == destination)
            return;

        if (!map.CanSpawnMobile(destination) || SpellHelper.CheckMulti(destination, map))
        {
            player.SendMessage("That destination is blocked.");
            return;
        }

        ClassCombat.Stop(player);
        player.RevealingAction();
        BaseCreature.TeleportPets(player, destination, map);
        player.PlaySound(0x1FC);
        player.MoveToWorld(destination, map);
        player.PlaySound(0x1FC);
    }
}
