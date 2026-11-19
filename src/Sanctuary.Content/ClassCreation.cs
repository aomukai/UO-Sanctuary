// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Commands;
using Server.Engines.CharacterCreation;
using Server.Gumps;

namespace Sanctuary;

public static class ClassCreation
{
    internal static readonly Point3D Arrival = new(3503, 2574, 14);
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
        if (!PortraitProtocol.TryDecodeCreation(args.RequestedProfession, out _, out _))
        {
            args.Rejected = true;
            return;
        }

        if (args.AppearanceTemplateId != 0
            && (!AppearanceTemplates.TryGet(args.AppearanceTemplateId, out var look)
                || Core.Expansion < args.Race.RequiredExpansion
                || args.Race.RaceID + 1 != look.Race || args.Female != look.Female
                || args.Hue != look.Skin || args.HairID != look.Hair || args.HairHue != look.HairHue
                || args.BeardID != look.Beard || args.BeardHue != look.BeardHue))
        {
            args.Rejected = true;
            return;
        }

        args.AccessLevelOverride = AccessLevel.Player;
        Profiles.PruneDeleted();
        args.Profession = 0;
        args.City = new CityInfo("New Haven", "The Bountiful Harvest Inn", Arrival.X, Arrival.Y, Arrival.Z, Map.Trammel);
        args.Stats[0] = 30;
        args.Stats[1] = 30;
        args.Stats[2] = args.State.NewCharacterCreation ? (byte)30 : (byte)20;
        Array.Clear(args.Skills);
    }

    private static void Assign(CharacterCreatedEventArgs args)
    {
        if (args.Mobile == null || !PortraitProtocol.TryDecodeCreation(args.RequestedProfession, out var characterClass, out byte face))
        {
            return;
        }

        // ModernUO initially clips skin before setting the character's race.
        if (AppearanceTemplates.TryGet(args.AppearanceTemplateId, out var look))
            args.Mobile.Hue = look.Skin | 0x8000;
        Profiles.Assign(args.Mobile, characterClass, face, args.AppearanceTemplateId);
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
