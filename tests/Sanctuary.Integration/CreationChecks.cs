using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Engines.CharacterCreation;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class CreationChecks
{
    private const ushort UiTemplate = AppearanceTemplates.Count;
    private static Banker _dialogueBanker;
    private static Provisioner _dialogueVendor;
    private static int _dialoguePurchases;
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CreationChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "creation-checks.json");

    public static void Configure()
    {
        Register("creation-check", Create);
        Register("creation-reload-check", Reload);
        Register("creation-starter-check", () => CheckStarters(false));
        Register("creation-starter-reload", () => CheckStarters(true));
        Register("creation-ui-check", CheckUi);
        Register("portrait-dialogue-check", () =>
        {
            var player = (PlayerMobile)Accounts.GetAccount("classui")[0];
            Require(player.Map == Map.Trammel && player.Location == new Point3D(3503, 2574, 14), "Incorrect New Haven arrival location");
            CheckAppearanceCatalogue();
            CheckTemplatePlayer(player);
            var guard = World.Mobiles.Values.OfType<ArrivalGuard>().Single();
            Require(player.InRange(guard, 3) && player.CanSee(guard) && player.InLOS(guard),
                $"Arrival guard is outside talking range or line of sight at {guard.Location}, {guard.Map}");
        });
        Register("portrait-dialogue-reload-check", () =>
        {
            var player = (PlayerMobile)Accounts.GetAccount("classui")[0];
            CheckTemplatePlayer(player);
            Require(World.Mobiles.Values.OfType<ArrivalGuard>().Count() == 1, "The arrival guard was duplicated on restart");
        });
        Register("portrait-dialogue-banker", () =>
        {
            var guard = World.Mobiles.Values.OfType<ArrivalGuard>().Single();
            guard.Hidden = true;
            _dialogueBanker = new Banker { CantWalk = true };
            _dialogueBanker.MoveToWorld(guard.Location, guard.Map);
        });
        Register("portrait-dialogue-bank-opened", () =>
        {
            var player = (PlayerMobile)Accounts.GetAccount("classui")[0];
            Require(player.BankBox.Opened, "A single click did not open the player's bank box");
            var guard = World.Mobiles.Values.OfType<ArrivalGuard>().Single();
            _dialogueBanker.Delete();
            _dialogueVendor = new Provisioner { CantWalk = true };
            _dialogueVendor.MoveToWorld(guard.Location, guard.Map);
            ClassEconomyHooks.PurchaseOpened += (vendor, buyer) =>
            {
                if (vendor == _dialogueVendor && buyer == player)
                    ++_dialoguePurchases;
            };
        });
        Register("portrait-dialogue-vendor-opened", () =>
        {
            Require(_dialoguePurchases == 1, "A single click must open the vendor buy interface once");
            _dialogueVendor.Delete();
            World.Mobiles.Values.OfType<ArrivalGuard>().Single().Hidden = false;
        });
        Register("creation-ui-level", AdvanceUi);
        Register("skills-ui-setup", SetupSkillsUi);
        Register("skills-ui-choose", () => Require(ClassProfessions.Profiles.Confirm(SkillsUiPlayer, SkillName.Alchemy),
            "First craft did not confirm"));
        Register("skills-ui-unlock", UnlockSkillsUi);
        Register("skills-ui-switch", SwitchSkillsUi);
        Register("creation-migration-check", Migration);
    }

    private static void CheckAppearanceCatalogue()
    {
        for (ushort id = 1; id <= AppearanceTemplates.Count; id++)
        {
            Require(AppearanceTemplates.TryGet(id, out var look), $"Missing portrait template {id}");
            var race = Race.Races[look.Race - 1];
            Require(race.ClipSkinHue(look.Skin) == look.Skin, $"Unsupported skin for portrait {id}");
            Require(look.Hair == 0 || race.ValidateHair(look.Female, look.Hair)
                && race.ClipHairHue(look.HairHue) == look.HairHue, $"Unsupported hair or horns for portrait {id}");
            Require(look.Beard == 0 || race.ValidateFacialHair(look.Female, look.Beard)
                && race.ClipHairHue(look.BeardHue) == look.BeardHue, $"Unsupported facial appearance for portrait {id}");
        }
    }

    private static void CheckTemplatePlayer(PlayerMobile player)
    {
        Require(AppearanceTemplates.TryGet(UiTemplate, out var look), "Missing test portrait template");
        Require(ClassCreation.Profiles.AppearanceTemplate(player.Serial) == UiTemplate, "Saved template ID differs from the picker");
        Require(player.Race == Race.Gargoyle && player.Female, "Portrait race or sex differs from the player");
        Require((player.Hue & 0x3FFF) == look.Skin, $"Portrait and native skin hues differ for template {UiTemplate}");
        Require(player.HairItemID == look.Hair && player.HairHue == look.HairHue,
            $"Portrait and native hair or horns differ for template {UiTemplate}");
        Require(player.FacialHairItemID == look.Beard && player.FacialHairHue == look.BeardHue,
            $"Portrait and native facial appearance differ for template {UiTemplate}");
    }

    private static void Register(string command, Action check)
    {
        ConsoleInputHandler.RegisterCommand(command, "Isolated creation integration check", _ =>
            Core.LoopContext.Post(() =>
            {
                try
                {
                    check();
                    _logger.Information("{Command}: passed", command);
                }
                catch (Exception exception)
                {
                    _logger.Error(exception, "{Command}: failed", command);
                }
            }));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    internal static CharacterCreatedEventArgs Args(int id, Account account, bool female = true)
    {
        // Event-path checks need protocol capabilities, but must not register fake sockets.
        var state = (NetState)RuntimeHelpers.GetUninitializedObject(typeof(NetState));
        state.Version = new ClientVersion("7.0.117.1");
        string suffix = PortraitProtocol.TryDecodeCreation(id, out var characterClass, out _) ? characterClass.ToString() : "Invalid";
        return new CharacterCreatedEventArgs(
            state, account, $"Check{suffix}", female, 0x03EA,
            [255, 255, 255], new CityInfo("Britain", "Bank", 1438, 1696, 0, Map.Felucca),
            [(SkillName.Magery, 50), (SkillName.Swords, 50), (SkillName.Necromancy, 50), (SkillName.Tailoring, 50)],
            3, 5, 0x203B, 0x044E, 0, 0, id, Race.Human);
    }

    private static void Verify(Mobile mobile, DiabloClass characterClass)
    {
        Require(ClassCreation.Profiles.TryGet(mobile.Serial, out var profile), "Missing class profile");
        Require(profile == new CharacterProfile(characterClass, 1, 0), "Incorrect class, level, or XP");
        Require(mobile.Title == $"Level 1 {DiabloClasses.DisplayName(characterClass, mobile.Female)}", "Incorrect paperdoll title");
        Require(mobile.AccessLevel == AccessLevel.Player && !mobile.Blessed, "New character inherited staff powers");
        var basis = ClassStats.For(characterClass);
        Require(mobile.Str == basis.Strength && mobile.Dex == basis.Dexterity && mobile.Int == basis.Energy,
            "Class base attributes were not applied");
        Require(mobile.HitsMax == basis.Life && mobile.ManaMax == basis.Mana && mobile.StamMax == basis.Stamina,
            "Class starting resource pools are wrong");
        foreach (var skill in mobile.Skills)
        {
            Require(skill.BaseFixedPoint == 0, "Client-supplied skill allocation was trusted");
        }
        Require(mobile.Female && mobile.Race == Race.Human && mobile.HairItemID == 0x203B, "Appearance was lost");
        Require(mobile.HairHue == Race.Human.ClipHairHue(0x044E), "Hair colour was lost");
        Require(mobile.Backpack != null && mobile.Backpack.Items.Count > 0, "Missing starting backpack/items");
        foreach (var item in mobile.Backpack.FindItems(true))
        {
            Require(item is Server.Items.Gold || item.LootType == LootType.Blessed, "An actual starting item was not protected");
        }
        CheckStarterGear((PlayerMobile)mobile, characterClass);
    }

    private static void CheckStarters(bool reload)
    {
        foreach (var characterClass in Enum.GetValues<DiabloClass>())
        {
            string name = $"starter-{(byte)characterClass}";
            var account = reload ? (Account)Accounts.GetAccount(name) : new Account(name, Guid.NewGuid().ToString("N"));
            if (!reload)
                CharacterCreation.CharacterCreatedEvent(Args((byte)characterClass, account));
            Require(account[0] is PlayerMobile, "Starter fixture character was not created");
            var player = (PlayerMobile)account[0];
            FixtureSpace.Place(player, new Point3D(1450, 1696, 0), Map.Felucca);
            CheckStarterGear(player, characterClass);
        }
    }

    private static void CheckStarterGear(PlayerMobile player, DiabloClass characterClass)
    {
        var starter = player.Backpack.FindItemByType<Dagger>();
        Require(ClassEquipment.StarterClass(starter) == characterClass, "The starting dagger belongs to the wrong class");
        var ability = ClassEquipment.StarterAbility(characterClass);
        var talent = ClassAbilities.Find(ability)?.TalentId;
        Require(talent != null && TalentCatalog.TryGet(characterClass, talent, out var definition)
            && definition.RequiredLevel == 1 && ClassCreation.Profiles.GetRank(player.Serial, talent) == 0,
            "Starter dagger skill must be tier one and spend no hard points");
        Require(player.EquipItem(starter), $"The starting dagger could not be equipped for {characterClass}");
        Require(SkillMechanics.Rank(player, ability) == 1 && ClassCombat.Available(player, ability),
            $"The starting dagger did not grant a usable {ability} for {characterClass}");
        player.Backpack.DropItem(starter);
        Require(SkillMechanics.Rank(player, ability) == 0, "The dagger grant stayed active in the backpack");
    }

    private static void Create()
    {
        FoundationChecks.Check();
        OriginChecks.Check();
        GearChecks.Check();
        var serials = new List<uint>();
        var account = new Account("creation-check", Guid.NewGuid().ToString("N")) { AccessLevel = AccessLevel.Owner };
        foreach (var invalid in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 100, 159, 216, 255 })
        {
            var args = Args(invalid, account);
            CharacterCreation.CharacterCreatedEvent(args);
            Require(args.Rejected && args.Mobile == null && account.Count == 0, "Invalid/custom template created a character");
        }

        foreach (var characterClass in Enum.GetValues<DiabloClass>())
        {
            var owner = new Account($"check-{(byte)characterClass}", Guid.NewGuid().ToString("N")) { AccessLevel = AccessLevel.Owner };
            byte face = (byte)((int)characterClass - (int)DiabloClass.Amazon);
            var args = Args(PortraitProtocol.EncodeCreation(characterClass, face), owner);
            CharacterCreation.CharacterCreatedEvent(args);
            Require(!args.Rejected && args.Mobile != null, "Valid class creation failed");
            Require(owner.AccessLevel == AccessLevel.Owner, "Account privileges changed");
            Verify(args.Mobile, characterClass);
            Require(ClassCreation.Profiles.Face(args.Mobile.Serial) == face, "Portrait choice was lost");
            FixtureSpace.Place((PlayerMobile)args.Mobile,new Point3D(1450,1696,0),Map.Felucca);
            serials.Add(args.Mobile.Serial.Value);
        }

        var legacy = new PlayerMobile { Body = 0x190, Name = "LegacyGM", AccessLevel = AccessLevel.Owner, Title = "Legacy title" };
        serials.Add(legacy.Serial.Value);
        var deleted = new PlayerMobile();
        ClassCreation.Profiles.Assign(deleted, DiabloClass.Amazon);
        deleted.Delete();
        ClassCreation.Profiles.PruneDeleted();
        Require(!ClassCreation.Profiles.TryGet(deleted.Serial, out _), "Deleted character retained progression");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(serials));
        CheckTalents();
        StatChecks.Check();
    }

    private static void Reload()
    {
        OriginChecks.Reload();
        GearChecks.Reload();
        var serials = JsonSerializer.Deserialize<uint[]>(File.ReadAllText(Manifest));
        var classes = Enum.GetValues<DiabloClass>();
        for (var i = 0; i < classes.Length; i++)
        {
            var mobile = World.FindMobile((Serial)serials[i]);
            Require(mobile != null, "Character was lost on restart");
            Verify(mobile, classes[i]);
            Require(ClassCreation.Profiles.Face(mobile.Serial) == i, "Portrait choice did not survive restart");
        }

        StatChecks.Reload();
        var legacy = World.FindMobile((Serial)serials[^1]);
        Require(legacy?.AccessLevel == AccessLevel.Owner && legacy.Title == "Legacy title", "Existing GM changed");
        Require(!ClassCreation.Profiles.TryGet(legacy.Serial, out _), "Existing GM received an invented class");
        var talentUser = Accounts.GetAccount("talent-check")?[0];
        Require(talentUser != null, "Talent test character was lost");
        Require(ClassCreation.Profiles.TryGet(talentUser.Serial, out var profile) && profile.Level == 99, "Talent level did not persist");
        Require(ClassCreation.Profiles.GetRank(talentUser.Serial, "sorceress.firebolt") == 20, "Talent rank did not persist");
        Require(ClassCreation.Profiles.GetRank(talentUser.Serial, "sorceress.fireball") == 1, "Second talent rank did not persist");
        Require(ClassCreation.Profiles.AvailablePoints(talentUser.Serial) == 77, "Unspent points changed on restart");
    }

    private static void CheckUi()
    {
        var account = Accounts.GetAccount("classui");
        var mobile = account?[0];
        Require(mobile?.Name == "ClassUI", "Client did not submit the appearance/name screen");
        Require(ClassCreation.Profiles.TryGet(mobile.Serial, out var profile), "Client did not transmit a class");
        Require(profile == new CharacterProfile(DiabloClass.Paladin, 2, 0), "Client transmitted the wrong class");
        Require(ClassCreation.Profiles.GetRank(mobile.Serial, "paladin.prayer") == 1, "Client could not learn a talent");
        Require(ClassCreation.Profiles.AvailablePoints(mobile.Serial) == 0, "Client talent spending did not debit points");
        Require(mobile.AccessLevel == AccessLevel.Player &&
                (mobile.Map == Map.Trammel || mobile.LogoutMap == Map.Trammel), "Incorrect world entry");
    }

    private static void AdvanceUi()
    {
        var mobile = Accounts.GetAccount("classui")?[0];
        Require(mobile != null && ClassCreation.Profiles.AdvanceLevel(mobile, 2), "Could not award UI test level");
    }

    private static PlayerMobile SkillsUiPlayer => (PlayerMobile)Accounts.GetAccount("classui")[0];

    private static void SetupSkillsUi()
    {
        var player = SkillsUiPlayer;
        Require(ClassProfessions.Affected(player) && !TownRecognition.HasAccess(player)
            && !ClassProfessions.Profiles.Find(player).Confirmed, "New player did not start with a choice open");
        player.Skills.Alchemy.Base = 42.5;
        player.Skills.Tinkering.Base = 67.3;
        player.Skills.Mining.Base = 25.4;
    }

    private static void UnlockSkillsUi()
    {
        var player = SkillsUiPlayer;
        player.Skills.Cartography.Base = 100;
        string hall = RecognitionChecks.Place(player);
        Require(TownRecognition.Claim(player, hall, RecognitionHonor.Pillar) && TownRecognition.HasAccess(player),
            "Recognition did not unlock the wardrobe");
    }

    private static void SwitchSkillsUi()
    {
        var player = SkillsUiPlayer;
        var craft = ClassProfessions.Profiles.Find(player);
        ClassCreation.Profiles.TryGet(player.Serial, out var profile);
        long token = TownRecognition.BeginSession(player);
        Require(WardrobeSwitch.Commit(player, WardrobeAccess.NearbyWardrobe(player), token, profile,
                craft, profile.Class, SkillName.Tinkering)
            && ClassProfessions.Active(player, SkillName.Tinkering)
            && !ClassProfessions.Active(player, SkillName.Alchemy)
            && player.Skills.Alchemy.BaseFixedPoint == 425 && player.Skills.Tinkering.BaseFixedPoint == 673,
            "Switching did not preserve both craft skill values");
    }

    private static void CheckTalents()
    {
        foreach (var characterClass in Enum.GetValues<DiabloClass>())
        {
            var trees = TalentCatalog.ForClass(characterClass).Trees;
            Require(trees.Length == 3 && Array.TrueForAll(trees, tree => tree.Skills.Length == 10), "Incorrect talent roster");
        }

        var account = new Account("talent-check", Guid.NewGuid().ToString("N"));
        var args = Args((int)DiabloClass.Sorceress, account);
        CharacterCreation.CharacterCreatedEvent(args);
        var mobile = args.Mobile;
        FixtureSpace.Place((PlayerMobile)mobile,new Point3D(1450,1696,0),Map.Felucca);
        var profiles = ClassCreation.Profiles;
        Require(!profiles.TryTrain(mobile.Serial, "sorceress.firebolt", out _), "Level 1 has unearned points");
        Require(profiles.AdvanceLevel(mobile, 3) && profiles.AvailablePoints(mobile.Serial) == 2, "Level rewards are wrong");
        Require(!profiles.TryTrain(mobile.Serial, "necromancer.teeth", out _), "Cross-class talent was accepted");
        Require(!profiles.TryTrain(mobile.Serial, "unknown", out _), "Unknown talent was accepted");
        Require(!profiles.TryTrain(mobile.Serial, "sorceress.fireball", out _), "Level requirement was bypassed");
        Require(profiles.TryTrain(mobile.Serial, "sorceress.firebolt", out _), "Could not train first rank");
        Require(profiles.TryTrain(mobile.Serial, "sorceress.firebolt", out _), "Could not train second rank");
        Require(!profiles.TryTrain(mobile.Serial, "sorceress.warmth", out _) && profiles.AvailablePoints(mobile.Serial) == 0,
            "Repeated spending exceeded available points");
        Require(profiles.AdvanceLevel(mobile, 12), "Could not award level 12");
        Require(profiles.TryTrain(mobile.Serial, "sorceress.fireball", out _), "Prerequisite unlock did not work");
        Require(!profiles.TryTrain(mobile.Serial, "sorceress.fireball", out _), "Per-rank level requirement was bypassed");
        Require(!profiles.TryTrain(mobile.Serial, "sorceress.enchant", out _), "Missing prerequisite was bypassed");
        Require(!profiles.AdvanceLevel(mobile, 11) && !profiles.AdvanceLevel(mobile, 100), "Invalid level change was accepted");
        Require(profiles.AdvanceLevel(mobile, 99), "Could not award max test level");
        Require(!profiles.TryTrain(mobile.Serial, "sorceress.blizzard", out _), "Missing prerequisite was bypassed at high level");
        while (profiles.GetRank(mobile.Serial, "sorceress.firebolt") < 20)
        {
            Require(profiles.TryTrain(mobile.Serial, "sorceress.firebolt", out _), "Could not reach rank cap");
        }
        Require(!profiles.TryTrain(mobile.Serial, "sorceress.firebolt", out _), "Rank cap was bypassed");
        Require(profiles.AvailablePoints(mobile.Serial) == 77, "Talent accounting is wrong");
    }

    private static void Migration()
    {
        var serials = JsonSerializer.Deserialize<uint[]>(File.ReadAllText(Manifest));
        var classes = Enum.GetValues<DiabloClass>();
        for (var i = 0; i < classes.Length; i++)
        {
            var mobile = World.FindMobile((Serial)serials[i]);
            Verify(mobile, classes[i]);
            Require(ClassCreation.Profiles.AvailablePoints(mobile.Serial) == 0, "Legacy profile gained points");
        }
    }
}
