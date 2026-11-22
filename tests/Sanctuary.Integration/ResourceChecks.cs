using System;
using Server;
using Server.Items;
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

    private static void StaminaRecovery(PlayerMobile player, PlayerMobile legacy)
    {
        int maximum = BattleOrders.StaminaMaximum256(player);
        BattleOrders.SetStamina256(player, 0);
        Require(Mobile.StaminaRegenOverride(player) && BattleOrders.Stamina256(player) == maximum / 256,
            "Standing must regain 1/256 of maximum stamina per frame");

        var session = ClassCombat.For(player);
        session.StepMap = player.Map;
        session.StepPosition = player.Location;
        session.RunningUntil = Core.TickCount + 1000;
        BattleOrders.SetStamina256(player, 0);
        Require(Mobile.StaminaRegenOverride(player) && BattleOrders.Stamina256(player) == maximum / 512,
            "Walking must regain half the standing rate, including from empty");

        session.RunningStep = true;
        int balance = BattleOrders.Stamina256(player);
        Require(Mobile.StaminaRegenOverride(player) && BattleOrders.Stamina256(player) == balance,
            "Running must pause ordinary stamina recovery");
        session.RunningUntil = Core.TickCount;
        session.RunningStep = false;

        var heading = Direction.North | Direction.Running;
        legacy.MoveToWorld(player.Location, player.Map);
        legacy.Dex = 100;
        legacy.Stam = 3;
        var threshold = StaminaSystem.AdditionalLossWhenBelow;
        try
        {
            StaminaSystem.AdditionalLossWhenBelow = 0.10;
            player.AddItem(new Backpack());
            var load = new Item(1) { Weight = player.MaxWeight + 25 };
            player.Backpack.DropItem(load);
            BattleOrders.SetStamina256(player, 3 * 256);
            var heavyStep = new MovementEventArgs(player, heading);
            StaminaSystem.EventSink_Movement(heavyStep);
            Require(heavyStep.Blocked && player.Stam == 0, "Encumbrance no longer drains stamina");
            load.Delete();
            BattleOrders.SetStamina256(player, 3 * 256);
            var lightStep = new MovementEventArgs(player, heading);
            StaminaSystem.EventSink_Movement(lightStep);
            Require(!lightStep.Blocked && player.Stam == 3, "Low-stamina running still drains extra stamina after unloading");

            StaminaSystem.EventSink_Movement(new MovementEventArgs(legacy, heading));
            Require(legacy.Stam == 2 && !Mobile.StaminaRegenOverride(legacy), "Legacy stamina handling changed");
        }
        finally
        {
            StaminaSystem.AdditionalLossWhenBelow = threshold;
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
                Require(Mobile.StamRegenRateHandler(player) == TimeSpan.FromMilliseconds(40), "Class stamina regeneration changed");
                Require(Mobile.ManaRegenRateHandler(player) == TimeSpan.FromSeconds(2), "Class mana regeneration changed");
            }
            StaminaRecovery(player, legacy);
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
            Require(Mobile.StamRegenRateHandler(player) == TimeSpan.FromMilliseconds(40),
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
