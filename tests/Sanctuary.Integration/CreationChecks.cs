using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Engines.CharacterCreation;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class CreationChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CreationChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "creation-checks.json");

    public static void Configure()
    {
        Register("creation-check", Create);
        Register("creation-reload-check", Reload);
        Register("creation-ui-check", CheckUi);
        Register("creation-ui-level", AdvanceUi);
        Register("creation-migration-check", Migration);
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

    private static CharacterCreatedEventArgs Args(int id, Account account)
    {
        // Event-path checks need protocol capabilities, but must not register fake sockets.
        var state = (NetState)RuntimeHelpers.GetUninitializedObject(typeof(NetState));
        state.Version = new ClientVersion("7.0.117.1");
        return new CharacterCreatedEventArgs(
            state, account, $"Check{(DiabloClass)id}", true, 0x03EA,
            [255, 255, 255], new CityInfo("Britain", "Bank", 1438, 1696, 0, Map.Felucca),
            [(SkillName.Magery, 50), (SkillName.Swords, 50), (SkillName.Necromancy, 50), (SkillName.Tailoring, 50)],
            3, 5, 0x203B, 0x044E, 0, 0, id, Race.Human);
    }

    private static void Verify(Mobile mobile, DiabloClass characterClass)
    {
        Require(ClassCreation.Profiles.TryGet(mobile.Serial, out var profile), "Missing class profile");
        Require(profile == new CharacterProfile(characterClass, 1, 0), "Incorrect class, level, or XP");
        Require(mobile.Title == $"Level 1 {characterClass}", "Incorrect paperdoll title");
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
    }

    private static void Create()
    {
        FoundationChecks.Check();
        OriginChecks.Check();
        GearChecks.Check();
        var serials = new List<uint>();
        var account = new Account("creation-check", Guid.NewGuid().ToString("N")) { AccessLevel = AccessLevel.Owner };
        foreach (var invalid in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 100, 108, 255 })
        {
            var args = Args(invalid, account);
            CharacterCreation.CharacterCreatedEvent(args);
            Require(args.Rejected && args.Mobile == null && account.Count == 0, "Invalid/custom template created a character");
        }

        foreach (var characterClass in Enum.GetValues<DiabloClass>())
        {
            var owner = new Account($"check-{(byte)characterClass}", Guid.NewGuid().ToString("N")) { AccessLevel = AccessLevel.Owner };
            var args = Args((int)characterClass, owner);
            CharacterCreation.CharacterCreatedEvent(args);
            Require(!args.Rejected && args.Mobile != null, "Valid class creation failed");
            Require(owner.AccessLevel == AccessLevel.Owner, "Account privileges changed");
            Verify(args.Mobile, characterClass);
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
                (mobile.Map == Map.Felucca || mobile.LogoutMap == Map.Felucca), "Incorrect world entry");
    }

    private static void AdvanceUi()
    {
        var mobile = Accounts.GetAccount("classui")?[0];
        Require(mobile != null && ClassCreation.Profiles.AdvanceLevel(mobile, 2), "Could not award UI test level");
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
