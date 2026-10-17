using System;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class StatUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(StatUiChecks));
    public static void Configure()
    {
        Register("setup", player =>
        {
            if (!ClassCreation.Profiles.AwardExperience(player, 500))
            {
                throw new InvalidOperationException("Could not award test level");
            }
            var weapon = CombatTiming.Equipped(player) ?? player.Backpack.FindItemByType<Dagger>();
            if (weapon != null)
            {
                player.EquipItem(weapon);
            }
        });
        Register("talents", player =>
        {
            if (player.FindGump<TalentGump>() == null)
            {
                throw new InvalidOperationException("Talent notification button did not open the talents window");
            }
        });
        Register("spent", player =>
        {
            var snapshot = ClassStats.Snapshot(player);
            if (snapshot[(int)StatField.Points] != 0 || snapshot[(int)StatField.TalentPoints] != 0
                || ClassCreation.Profiles.GetRank(player.Serial, "necromancer.raiseskeleton") != 1)
            {
                throw new InvalidOperationException("Point buttons did not open working allocation windows or reflect spent points");
            }
        });
        Register("four", player =>
        {
            if (ClassCreation.Profiles.Stats(player.Serial) != new StatAllocation(1, 1, 1, 1)
                || ClassCreation.Profiles.AvailableStatPoints(player.Serial) != 1)
            {
                throw new InvalidOperationException("The four native status rows did not each spend exactly one point");
            }
        });
        Register("final", player =>
        {
            if (ClassCreation.Profiles.Stats(player.Serial) != new StatAllocation(1, 1, 1, 2)
                || ClassCreation.Profiles.AvailableStatPoints(player.Serial) != 0
                || player.Str != 16 || player.Dex != 26 || player.Int != 26 || ClassStats.Vitality(player) != 17
                || player.HitsMax != 50 || player.ManaMax != 29 || player.StamMax != 82)
            {
                throw new InvalidOperationException("Native status spending did not apply real class stats or overspent its budget");
            }
        });
    }
    private static void Register(string suffix, Action<PlayerMobile> check)
    {
        var name = "stat-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Status UI fixture", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                check((PlayerMobile)Accounts.GetAccount("combatui")[0]);
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception error)
            {
                _logger.Error(error, "{Command}: failed", name);
            }
        }));
    }
}
