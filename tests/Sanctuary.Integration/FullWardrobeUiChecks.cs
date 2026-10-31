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

public static class FullWardrobeUiChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(FullWardrobeUiChecks));
    private static PlayerMobile _owner;
    private static Item _wardrobe;
    private static Item[] _gear;
    private static SkeletonGuardian _pet;
    private static Item _retrieved;
    private static int _life;
    private static void Require(bool value, string why) => GearDropChecks.Require(value, why);
    public static void Configure()
    {
        Command("setup", () =>
        {
            _owner = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_owner);
            _owner.Target = null; _owner.Frozen = _owner.Paralyzed = false; _owner.Blessed = true;
            ClassCreation.Profiles.AdvanceLevel(_owner, 80);
            foreach (var item in _owner.Items.Where(WardrobeEquipment.Equipment).ToArray()) { _owner.Backpack.DropItem(item); }
            var dagger = new Dagger(); ClassGear.Profiles.MarkStarter(dagger); var robe = new Robe();
            Require(_owner.EquipItem(dagger) && _owner.EquipItem(robe), "UI equipment setup failed"); _gear = [dagger, robe];
            Require(ClassCreation.Profiles.TryTrain(_owner.Serial, "necromancer.raiseskeleton", out _)
                && ClassCreation.Profiles.TryTrain(_owner.Serial, "necromancer.teeth", out _), "UI training failed");
            SkillBindings.Instance.Assign(_owner, false, CombatAbility.RaiseSkeleton);
            SkillBindings.Instance.AssignQuick(_owner, 11, CombatAbility.Teeth);
            _pet = new SkeletonGuardian(); _pet.Initialize(_owner, 1, 0); _pet.SetControlMaster(_owner);
            _pet.Hits = _life = _pet.HitsMax / 2; PersistentMinions.Profiles.Track(_pet);
            ClassProfessions.Profiles.Confirm(_owner, SkillName.Alchemy);
            string site = RecognitionChecks.Place(_owner, Array.FindIndex(TownRecognition.Halls, h => h.Id == "Felucca/Britain"));
            Require(TownRecognition.Claim(_owner, site, RecognitionHonor.Hero), "UI recognition failed");
            _wardrobe = TownRecognition.PublicWardrobe(site); _owner.Hits = _owner.HitsMax;
            SkillBindingNetwork.Send(_owner); Open();
        });
        Command("sorceress", () =>
        {
            Require(Profile().Class == DiabloClass.Sorceress && Profile().Level == 1
                && ClassProfessions.Profiles.Find(_owner).Craft == SkillName.Tinkering
                && _gear.All(i => i.Parent == WardrobeEquipment.Instance.Find(_owner.Serial, DiabloClass.Necromancer))
                && _pet.Map == Map.Internal && _pet.Hits == _life && !PersistentMinions.CanThink(_pet)
                && SkillBindings.Instance.GetQuick(_owner.Serial, DiabloClass.Sorceress, 11) == CombatAbility.None,
                "Actual UI did not switch class/craft with real gear and summon parking");
        });
        Command("open", Open);
        Command("retrieved", () =>
        {
            _retrieved = _gear.Single(i => i.Parent == _owner.Backpack);
            Require(_gear.Count(i => i.Parent is WardrobeSet) == 1, "UI retrieval moved or lost multiple items");
        });
        Command("back", () =>
        {
            Require(Profile().Class == DiabloClass.Necromancer && Profile().Level == 80
                && ClassProfessions.Profiles.Find(_owner).Craft == SkillName.Alchemy
                && _retrieved.Parent == _owner.Backpack && _gear.Single(i => i != _retrieved).Parent == _owner
                && _pet.ControlMaster == _owner && _pet.Hits >= _life && !PersistentMinions.Profiles.Find(_pet).Parked
                && SkillBindings.Instance.Get(_owner).Right == CombatAbility.RaiseSkeleton
                && SkillBindings.Instance.GetQuick(_owner.Serial, DiabloClass.Necromancer, 11) == CombatAbility.Teeth,
                "Actual UI did not restore the remaining set, class bindings and summon");
        });
        Command("final", () => Require(Profile().Class == DiabloClass.Necromancer
            && _retrieved.Parent == _owner.Backpack && ClassProfessions.Profiles.Find(_owner).Craft == SkillName.Alchemy,
            "Final UI state changed unexpectedly"));
    }
    private static CharacterProfile Profile() { ClassCreation.Profiles.TryGet(_owner.Serial, out var p); return p; }
    private static void Open() => WardrobeGump.DisplayTo(_owner, _wardrobe);
    private static void Command(string suffix, Action action)
    {
        string name = "full-wardrobe-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual full wardrobe selector, retrieval and class panel refresh", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new { characterClass = Profile().Class }));
                _log.Information("{Command}: passed", name);
            }
            catch (Exception error) { _log.Error(error, "{Command}: failed", name); }
        }));
    }
}
