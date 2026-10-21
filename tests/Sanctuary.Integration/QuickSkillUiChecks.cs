// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class QuickSkillUiChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(QuickSkillUiChecks));
    private static PlayerMobile _owner;
    private static Corpse _corpse;
    private static int _mana;
    private static long _action;
    private static void Require(bool value, string why) => GearDropChecks.Require(value, why);
    public static void Configure()
    {
        Command("setup", () =>
        {
            _owner = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_owner); _owner.Target = null;
            _owner.Blessed = true; _owner.Frozen = _owner.Paralyzed = false; ClassCreation.Profiles.AdvanceLevel(_owner, 30);
            foreach (var item in _owner.Items.ToArray())
            { if (item.Layer is not (Layer.Backpack or Layer.Bank or Layer.Hair or Layer.FacialHair) && item is not PotionBeltStorage) { _owner.Backpack.DropItem(item); } }
            Require(ClassCreation.Profiles.TryTrain(_owner.Serial, "necromancer.teeth", out _)
                && ClassCreation.Profiles.TryTrain(_owner.Serial, "necromancer.raiseskeleton", out _), "Quick-cast skill training failed");
            _owner.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            _owner.Mana = _owner.ManaMax;
            SkillBindings.Instance.Assign(_owner, true, CombatAbility.BasicAttack); SkillBindings.Instance.Assign(_owner, false, CombatAbility.Teeth);
            MakeCorpse();
        });
        Command("assigned", () =>
        {
            Require(SkillBindings.Instance.GetQuick(_owner.Serial, DiabloClass.Necromancer, 0) == CombatAbility.Teeth
                && SkillBindings.Instance.GetQuick(_owner.Serial, DiabloClass.Necromancer, 1) == CombatAbility.RaiseSkeleton
                && SkillBindings.Instance.GetQuick(_owner.Serial, DiabloClass.Necromancer, 11) == CombatAbility.Teeth
                && SkillBindings.Instance.Get(_owner) == (CombatAbility.Teeth, CombatAbility.RaiseSkeleton), "Hover F1/F2/F12 or mouse assignment failed");
            _mana = _owner.Mana; _action = ClassCombat.For(_owner).NextAction;
        });
        Command("chat", () => Require(_owner.Mana >= _mana && ClassCombat.For(_owner).NextAction == _action,
            "Quick casting consumed chat input"));
        Command("teeth", () =>
        {
            Require(_owner.Mana < _mana && ClassCombat.For(_owner).NextAction > _action
                && SkillBindings.Instance.Get(_owner) == (CombatAbility.Teeth, CombatAbility.RaiseSkeleton), "Quick Teeth did not cast at cursor or changed mouse pair");
            _owner.Mana = _owner.ManaMax; _mana = _owner.Mana;
        });
        Command("invalid", () => Require(SkeletonPets.Count(_owner) == 0 && !_corpse.SanctuaryConsumed && _owner.Mana >= _mana,
            "Empty/UI cursor summoned nearest corpse or spent mana"));
        Command("raised", () =>
        {
            Require(SkeletonPets.Count(_owner) == 1 && _corpse.SanctuaryConsumed && _owner.Mana < _mana
                && SkillBindings.Instance.GetQuick(_owner.Serial, DiabloClass.Necromancer, 1) == CombatAbility.RaiseSkeleton, $"Quick Raise Skeleton failed: count={SkeletonPets.Count(_owner)} consumed={_corpse.SanctuaryConsumed} usable={SkeletonPets.Usable(_owner, _corpse)} pending={ClassCombat.For(_owner).PendingAbility} mana={_owner.Mana}/{_mana}");
            SkeletonPets.Dismiss(_owner); ClassCreation.Profiles.TryGet(_owner.Serial, out var profile);
            ClassCreation.Profiles.ResetAllocatedPoints(_owner, profile); _owner.Mana = _owner.ManaMax;
            _mana = _owner.Mana; _action = ClassCombat.For(_owner).NextAction;
        });
        Command("unavailable", () => Require(_owner.Mana >= _mana && ClassCombat.For(_owner).NextAction == _action
            && SkillBindings.Instance.GetQuick(_owner.Serial, DiabloClass.Necromancer, 0) == CombatAbility.Teeth, "Unavailable saved quick binding cast or disappeared"));
        Command("cleared", () => Require(SkillBindings.Instance.GetQuick(_owner.Serial, DiabloClass.Necromancer, 11) == CombatAbility.None,
            "Clear-binding hover did not clear F12"));
        Command("final", () => Require(SkillBindings.Instance.GetQuick(_owner.Serial, DiabloClass.Necromancer, 0) == CombatAbility.Teeth
            && SkillBindings.Instance.GetQuick(_owner.Serial, DiabloClass.Necromancer, 1) == CombatAbility.RaiseSkeleton
            && SkillBindings.Instance.GetQuick(_owner.Serial, DiabloClass.Necromancer, 11) == CombatAbility.None, "Final quick bar lost bindings"));
    }
    private static void MakeCorpse()
    {
        var enemy = new GraveyardSkeleton { Frozen = true, CantWalk = true, NoKillAwards = true }; enemy.AIObject?.AITimer.Stop();
        enemy.MoveToWorld(CombatPractice.FindFloor(_owner.Map, _owner.X + 2, _owner.Y), _owner.Map);
        _owner.DoHarmful(enemy); enemy.Damage(1000, _owner); if (enemy.Alive) { enemy.Kill(); }
        _corpse = (Corpse)enemy.Corpse;
        Require(SkeletonPets.CanRaise(_owner, _corpse), $"Invalid summoning fixture: usable={SkeletonPets.Usable(_owner, _corpse)} spawn={_owner.Map.CanSpawnMobile(_corpse.X, _corpse.Y, _corpse.Z)}");
    }
    private static object Position(IEntity entity) => new { x = entity.X, y = entity.Y, z = entity.Z, serial = entity.Serial.Value };
    private static void Command(string suffix, Action action)
    {
        string name = "quick-skill-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual S panel, hover quick bindings, mouse assignment and cursor casting", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"),
                    JsonSerializer.Serialize(new { player = Position(_owner), corpse = Position(_corpse), mana = _owner.Mana }));
                _log.Information("{Command}: passed", name);
            }
            catch (Exception error) { _log.Error(error, "{Command}: failed", name); }
        }));
    }
}
