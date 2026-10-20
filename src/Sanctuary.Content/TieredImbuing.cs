// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record ImbuingBand(GearTier Tier, int Skill, int Gold, int Essence);
public sealed record JewelleryBand(string Name, int Skill, int ItemLevel, int EquipLevel, int Ingots, int Gold, int Essence);

public static class TieredImbuing
{
    public static ImbuingBand[] Equipment { get; private set; } = [];
    public static JewelleryBand[] Jewellery { get; private set; } = [];
    public static void Load()
    {
        Equipment = BalanceCatalog.Read<ImbuingBand>("imbuing-tiers");
        Jewellery = BalanceCatalog.Read<JewelleryBand>("jewellery-grades");
        if (Equipment.Length != 3 || Jewellery.Length != 5) { throw new InvalidDataException("Incomplete Imbuing progression."); }
        for (int i = 0; i < Equipment.Length; i++)
        {
            var row = Equipment[i];
            if ((int)row.Tier != i || row.Skill is < 0 or > 120 || row.Gold is < 0 or > 60000 || row.Essence is < 1 or > 60000)
            { throw new InvalidDataException("Invalid Imbuing equipment band."); }
        }
        int previousLevel = 0, previousSkill = 0;
        foreach (var row in Jewellery)
        {
            if (string.IsNullOrWhiteSpace(row.Name) || row.Skill <= previousSkill || row.Skill > 120
                || row.ItemLevel <= previousLevel || row.ItemLevel > 99 || row.EquipLevel is < 0 or > 99
                || row.Ingots is < 1 or > 60000 || row.Gold is < 0 or > 60000 || row.Essence is < 1 or > 60000)
            { throw new InvalidDataException("Invalid jewellery progression."); }
            previousLevel = row.ItemLevel; previousSkill = row.Skill;
        }
    }
    public static JewelleryBand JewelleryFor(int itemLevel)
    {
        foreach (var row in Jewellery) { if (itemLevel <= row.ItemLevel) { return row; } }
        return Jewellery[^1];
    }
    public static ImbuingBand Cost(Item item)
    {
        var state = ClassGear.State(item);
        if (item is BaseJewel)
        {
            var band = JewelleryFor(state?.Roll.ItemLevel ?? 1);
            return new(GearTier.Normal, band.Skill, band.Gold, band.Essence);
        }
        return Equipment[(int)(state?.Tier ?? GearTier.Normal)];
    }
    public static int RareChance(PlayerMobile player) => player.Skills.Imbuing.BaseFixedPoint >= 1200 ? 30 : 25;
    public static bool ToolValid(PlayerMobile player, ImbuingLadle tool) => EquipmentEconomy.Ready(player)
        && tool is { Deleted: false, Movable: true, UsesRemaining: > 0 }
        && (tool.Parent == player || tool.IsChildOf(player.Backpack)) && tool.IsAccessibleTo(player) && tool.VerifyMove(player);
    public static bool Ready(PlayerMobile player, Item item, ImbuingLadle ladle)
    {
        if (!ToolValid(player, ladle) || !ClassProfessions.Active(player, SkillName.Imbuing)) { return false; }
        int required = Cost(item).Skill;
        if (player.Skills.Imbuing.BaseFixedPoint < required * 10)
        { player.SendMessage($"This item requires {required} Imbuing."); return false; }
        return CraftFacilities.Allows(player, SkillName.Imbuing);
    }
}
