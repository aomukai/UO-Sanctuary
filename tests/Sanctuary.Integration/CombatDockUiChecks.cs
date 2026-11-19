using System;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CombatDockUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CombatDockUiChecks));

    public static void Configure()
    {
        Register("setup", player =>
        {
            Require(ClassCreation.Profiles.AwardExperience(player, 17500), "Could not award experience");
            Require(ClassCreation.Profiles.TryTrain(player.Serial, "paladin.holybolt", out _), "Could not learn Holy Bolt");
            Require(ClassCreation.Profiles.TryTrain(player.Serial, "paladin.sacrifice", out _), "Could not learn Sacrifice");
            Require(SkillBindings.Instance.Assign(player, false, CombatAbility.HolyBolt)
                && SkillBindings.Instance.AssignQuick(player, 0, CombatAbility.Sacrifice)
                && SkillBindings.Instance.AssignQuick(player, 1, CombatAbility.HolyBolt), "Could not set up the combat bindings");
            player.RawStr = 100;
            player.FindItemOnLayer(Layer.MiddleTorso)?.Delete();
            Require(player.EquipItem(new PotionPlatedBelt()), "Could not equip the test belt");
            player.Backpack.DropItem(new RestorationPotion(RestorationKind.Healing, 1) { Amount = 12 });
            player.Backpack.DropItem(new RestorationPotion(RestorationKind.Mana, 1) { Amount = 12 });
            player.Hits = player.HitsMax / 2;
            player.Mana = player.ManaMax / 3;
            player.Stam = player.StamMax * 3 / 4;
        });
        Register("belt", player => Require(PotionBelts.Rows(player) == 4 && PotionBelts.Entry(player, 0) != null
            && PotionBelts.Entry(player, 15) != null, "Fill did not populate all four rows"));
        Register("use", player => Require(PotionBelts.Entry(player, 12) == null && PotionBelts.Entry(player, 0) != null,
            "Column-one key did not consume and compact a potion"));
        Register("experience", player => Require(ClassCreation.Profiles.AwardExperience(player, 3500),
            "Could not advance the XP strip"));
        Register("human-m", player => Appearance(player, Race.Human, false, 8251, 0x03EA, 0x0455));
        Register("human-f", player => Appearance(player, Race.Human, true, 8262, 0x0422, 0x044E));
        Register("elf-m", player => Appearance(player, Race.Elf, false, 0x2FBF, 0x04DD, 0x0034));
        Register("elf-f", player => Appearance(player, Race.Elf, true, 0x2FC0, 0x024C, 0x059C));
        Register("gargoyle-m", player => Appearance(player, Race.Gargoyle, false, 0x4258, 0x06DA, 0x0708));
        Register("gargoyle-f", player => Appearance(player, Race.Gargoyle, true, 0x4261, 0x06E5, 0x0764));
    }

    private static void Appearance(PlayerMobile player, Race race, bool female, int hair, int skin, int hairHue)
    {
        player.Female = female;
        player.Race = race;
        player.Hue = skin | 0x8000;
        player.HairItemID = hair;
        player.HairHue = hairHue;
    }

    private static void Register(string suffix, Action<PlayerMobile> action)
    {
        string command = $"dock-ui-{suffix}";
        ConsoleInputHandler.RegisterCommand(command, "Combat dock fixture", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action((PlayerMobile)Accounts.GetAccount("classui")[0]);
                _logger.Information("{Command}: passed", command);
            }
            catch (Exception error)
            {
                _logger.Error(error, "{Command}: failed", command);
            }
        }));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
