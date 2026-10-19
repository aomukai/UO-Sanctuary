// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Commands;
using Server.Engines.CharacterCreation;
using Server.Gumps;

namespace Sanctuary;

public static class ClassCreation
{
    public static CharacterProfiles Profiles { get; private set; }

    public static void Configure()
    {
        TalentCatalog.Load();
        Profiles = new CharacterProfiles();
        CharacterCreation.Creating += Validate;
        CharacterCreation.Created += Assign;
        EventSink.WorldSave += Profiles.PruneDeleted;
        CommandSystem.Register("Class", AccessLevel.Player, ShowClass);
    }

    private static void Validate(CharacterCreatedEventArgs args)
    {
        if (!DiabloClasses.TryFromWire(args.RequestedProfession, out _))
        {
            args.Rejected = true;
            return;
        }

        args.AccessLevelOverride = AccessLevel.Player;
        Profiles.PruneDeleted();
        args.Profession = 0;
        args.City = new CityInfo("Britain", "West Britain Bank", 1438, 1696, 0, Map.Felucca);
        args.Stats[0] = 30;
        args.Stats[1] = 30;
        args.Stats[2] = args.State.NewCharacterCreation ? (byte)30 : (byte)20;
        Array.Clear(args.Skills);
    }

    private static void Assign(CharacterCreatedEventArgs args)
    {
        if (args.Mobile == null || !DiabloClasses.TryFromWire(args.RequestedProfession, out var characterClass))
        {
            return;
        }

        Profiles.Assign(args.Mobile, characterClass);
        ClassEquipment.ProtectNewbieGear((Server.Mobiles.PlayerMobile)args.Mobile);
        ClassEquipment.Prepare(args.Mobile as Server.Mobiles.PlayerMobile, true);
        if (ClassProfessions.Affected(args.Mobile))
        {
            args.Mobile.SendMessage("Alchemy is displayed initially; your first profession is still unchosen. Use [Profession, a crafting tool or a trainer.");
        }
    }

    private static void ShowClass(CommandEventArgs args)
    {
        if (Profiles.TryGet(args.Mobile.Serial, out var profile))
        {
            TalentGump.DisplayTo(args.Mobile);
        }
        else
        {
            args.Mobile.SendMessage("This existing character has no Diablo class. Create a new character to select one.");
        }
    }
}
