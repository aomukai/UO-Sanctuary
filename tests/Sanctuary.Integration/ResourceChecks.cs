using System;
using Server;
using Server.Misc;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ResourceChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Check()
    {
        var player = new PlayerMobile { Body = 0x190, Name = "ResourceCheck", Player = true };
        var legacy = new PlayerMobile { Body = 0x190, Name = "LegacySkillCheck", Player = true };
        try
        {
            ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
            player.Str = player.Dex = player.Int = 100;
            player.Hits = 100;
            player.Stam = player.Mana = 10;
            player.MoveToWorld(CombatPractice.FindFloor(Map.Felucca, 1382, 1495), Map.Felucca);
            for (var i = 0; i < 100; i++)
            {
                Require(Mobile.StamRegenRateHandler(player) == TimeSpan.FromSeconds(5), "Class stamina regeneration changed");
                Require(Mobile.ManaRegenRateHandler(player) == TimeSpan.FromSeconds(2), "Class mana regeneration changed");
            }
            Require(player.Skills.Focus.BaseFixedPoint == 0 && player.Skills.Meditation.BaseFixedPoint == 0,
                "Passive regeneration trained Focus or Meditation");
            var stats = (player.RawStr, player.RawDex, player.RawInt);
            foreach (var skill in player.Skills)
            {
                if (TradeSkills.Includes((int)skill.SkillName))
                {
                    continue;
                }
                skill.SetLockNoRelay(SkillLock.Up);
                for (var i = 0; i < 5; i++)
                {
                    player.CheckSkill(skill.SkillName, 0.5);
                    SkillCheck.Gain(player, skill);
                }
                Require(skill.BaseFixedPoint == 0, $"Non-trade skill gained: {skill.SkillName}");
            }
            Require((player.RawStr, player.RawDex, player.RawInt) == stats, "Hidden combat checks awarded UO stats");
            foreach (var skill in player.Skills)
            {
                if (TradeSkills.Includes((int)skill.SkillName))
                {
                    skill.SetLockNoRelay(SkillLock.Up);
                    SkillCheck.Gain(player, skill);
                    Require(skill.BaseFixedPoint > 0, $"Trade skill stopped gaining: {skill.SkillName}");
                }
            }
            player.Skills.Focus.Base = 100;
            Require(Mobile.StamRegenRateHandler(player) == TimeSpan.FromSeconds(5),
                "Existing accidental Focus changed class stamina recovery");
            SkillCheck.Gain(legacy, legacy.Skills.Focus);
            Require(legacy.Skills.Focus.BaseFixedPoint > 0, "Legacy skill training was changed");
        }
        finally
        {
            player.Delete();
            legacy.Delete();
            ClassCreation.Profiles.PruneDeleted();
        }
    }
}
