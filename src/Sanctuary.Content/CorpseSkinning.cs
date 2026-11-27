// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record CorpseSkin(int Amount, HideType Type);

internal static class CorpseSkinning
{
    public static void Configure() => ClassFoodHooks.Skinning = Resolve;

    private static (int Amount, HideType Type)? Resolve(Mobile from, BaseCreature creature, Corpse corpse)
    {
        if (from is not PlayerMobile)
            return null;

        var saved = CorpseSkillRules.Profiles.Find(corpse);
        if (corpse is not { OwnerWasBaseCreature: true, OwnerWasSummoned: false, OwnerWasAnimatedDead: false,
                Animated: false, SanctuaryRestInPeace: false, IsBones: false }
            || saved is { Owned: true } or { Shattered: true })
            return (0, HideType.Regular);

        var skin = saved?.Skinning;
        if (skin == null && creature != null)
        {
            if (!CorpseSkillRules.TryMonsterLevel(corpse, out int level))
                level = CreatureProfiles.Level(creature);
            skin = Capture(creature, (Body)corpse.Amount, level);
        }
        return skin == null ? (0, HideType.Regular) : (skin.Amount, skin.Type);
    }

    internal static CorpseSkin Capture(BaseCreature creature, Body body, int level)
    {
        if (creature is { Controlled: true } or { Summoned: true } or { IsBonded: true } or { IsAnimatedDead: true }
            || creature is BaseVendor || body.IsHuman || body.IsGhost || body.IsGargoyle
            || GemCombat.Undead(creature) || creature is SkeletalDragon
            || SlayerGroup.GetEntryByName(SlayerName.ElementalBan)?.Slays(creature) == true
            || creature is EncounterCreature && body.BodyID is >= 13 and <= 16 or 790)
            return new(0, HideType.Regular);

        bool ogre = SlayerGroup.GetEntryByName(SlayerName.OgreTrashing)?.Slays(creature) == true
            || creature is EncounterCreature && body.BodyID == 1;
        if (!ogre && (SlayerGroup.GetEntryByName(SlayerName.Repond)?.Slays(creature) == true
            || SlayerGroup.GetEntryByName(SlayerName.LizardmanSlaughter)?.Slays(creature) == true
            || body.BodyID is 4 or 7 or 17 or 18 or 35 or 36 or 42 or 53 or 54 or 138 or 139 or 140 or 142 or 143
                or 182 or 189 or 0x2F2 or 0x2F3 or 0x2F6))
            return new(0, HideType.Regular);

        bool dragonkin = SlayerGroup.GetEntryByName(SlayerName.DragonSlaying)?.Slays(creature) == true
            || body.BodyID is 12 or 49 or >= 59 and <= 62 or 206;
        if (body.IsAnimal && !dragonkin && !ogre && !NamedCombat.Demon(creature))
            return new(Math.Max(1, creature.Hides), HideType.Regular);

        int tier = Math.Clamp(level / 25, 0, 3);
        return new(Utility.RandomMinMax(4 + tier * 2, 6 + tier * 2), (HideType)tier);
    }
}
