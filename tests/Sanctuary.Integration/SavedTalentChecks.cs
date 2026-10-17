using System;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class SavedTalentChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(SavedTalentChecks));
    private static PlayerMobile _player;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _player = World.FindMobile((Serial)8) as PlayerMobile ?? throw new InvalidOperationException("Missing copied character");
            if (Environment.GetEnvironmentVariable("SANCTUARY_SAVED_ORIGINAL_ACCOUNT") == "1")
            {
                ((Account)_player.Account).SetPassword(Environment.GetEnvironmentVariable("SANCTUARY_SAVED_TEST_PASSWORD"));
                return;
            }
            var account = new Account("savedtalentui", Environment.GetEnvironmentVariable("SANCTUARY_SAVED_TEST_PASSWORD"));
            if (_player.Account is Account old)
            {
                for (int i = 0; i < 7; i++)
                {
                    if (old[i] == _player)
                    {
                        old[i] = null;
                    }
                }
            }
            account[0] = _player;
        });
        Register("open", () => TalentGump.DisplayTo(_player));
        Register("state", () =>
        {
            var gump = _player.FindGump<TalentGump>();
            _logger.Information("Copied talent state: serial={Serial}, type={Type}, points={Points}, rank={Rank}, requirement={Requirement}",
                gump?.Serial, gump?.TypeID, ClassCreation.Profiles.AvailablePoints(_player.Serial),
                ClassCreation.Profiles.GetRank(_player.Serial, "necromancer.raiseskeleton"),
                ClassCreation.Profiles.TrainingRequirement(_player.Serial, "necromancer.raiseskeleton"));
        });
        Register("learned", () =>
        {
            if (ClassCreation.Profiles.GetRank(_player.Serial, "necromancer.raiseskeleton") != 1
                || ClassCreation.Profiles.AvailablePoints(_player.Serial) != 0)
            {
                throw new InvalidOperationException("Copied character did not learn Raise Skeleton");
            }
        });
    }
    private static void Register(string suffix, Action action)
    {
        string name = "saved-talent-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Copied-world talent checks", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception error)
            {
                _logger.Error(error, "{Command}: failed", name);
            }
        }));
    }
}
