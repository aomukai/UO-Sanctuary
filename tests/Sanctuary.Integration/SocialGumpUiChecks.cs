using System;
using Server;
using Server.Accounting;
using Server.Engines.PartySystem;
using Server.Guilds;
using Server.Gumps;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class SocialGumpUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(SocialGumpUiChecks));

    public static void Configure()
    {
        Register("invite", player =>
        {
            Require(player.Target != null, "Invite did not open the native party target");
            player.Target.Cancel(player, Server.Targeting.TargetCancelType.Canceled);
        });
        Register("party", player =>
        {
            var member = new PlayerMobile { Name = "Rowan", Body = 0x190, Player = true };
            member.MoveToWorld(new Point3D(player.X + 1, player.Y, player.Z), player.Map);

            var party = new Party(player);
            player.Party = party;
            party.Add(member);
            party[player].CanLoot = false;
        });
        Register("no-loot", player => Require(Party.Get(player)?[player]?.CanLoot == false,
            "Cancel changed the native loot permission"));
        Register("loot", player => Require(Party.Get(player)?[player]?.CanLoot == true,
            "Apply did not update the native loot permission"));
        Register("left", player => Require(Party.Get(player) == null, "Native party removal did not run"));
        Register("fund", player => Require(Banker.Deposit(player, Guild.RegistrationFee), "Could not fund guild registration"));
        Register("created", player => Require(player.Guild is Guild { Name: "Lanternwatch", Abbreviation: "LW" }
            && Banker.GetBalance(player) == 0, $"Unexpected registration result: guild={player.Guild?.Name}, abbreviation={player.Guild?.Abbreviation}, balance={Banker.GetBalance(player)}"));
        Register("guild", player => Require(player.HasGump<GuildInfoGump>(), "My Guild did not open"));
        Register("roster", player => Require(player.HasGump<Server.Guilds.GuildRosterGump>(), "Roster tab did not open"));
        Register("diplomacy", player => Require(player.HasGump<GuildDiplomacyGump>(), "Diplomacy tab did not open"));
        Register("search", player => Require(player.HasGump<GuildAdvancedSearchGump>(), "Advanced Search did not open"));
        Register("closed", player => Require(!player.HasGump<GuildDiplomacyGump>(), "Close did not dismiss Diplomacy"));
    }

    private static void Register(string suffix, Action<PlayerMobile> check)
    {
        string name = $"social-ui-{suffix}";
        ConsoleInputHandler.RegisterCommand(name, "Social window fixture", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                check((PlayerMobile)Accounts.GetAccount("classui")[0]);
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception error)
            {
                _logger.Error(error, "{Command}: failed", name);
            }
        }));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
