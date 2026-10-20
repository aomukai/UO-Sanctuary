// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class JewelleryCrafting
{
    public static EquipmentRecipe[] Recipes { get; private set; } = [];
    public static bool IsOutput(Type type) => type == typeof(GoldRing) || type == typeof(SilverNecklace);
    public static void Load()
    {
        var rows = new List<EquipmentRecipe>();
        foreach (var band in TieredImbuing.Jewellery)
        {
            foreach (var type in new[] { typeof(GoldRing), typeof(SilverNecklace) })
            {
                EquipmentBalance.TryGet(type, out var definition);
                rows.Add(new("jewellery-" + band.Name + "-" + type.Name, type.Name,
                    band.Name + (type == typeof(GoldRing) ? " ring" : " amulet"), SkillName.Tinkering, GearTier.Normal,
                    definition.BaseCode, band.Skill, band.Skill + 50, [new("IronIngot", band.Ingots)]));
            }
        }
        Recipes = rows.ToArray();
    }
    public static bool Contains(EquipmentRecipe row) => Array.IndexOf(Recipes, row) >= 0;
    public static JewelleryBand Band(EquipmentRecipe row)
    {
        foreach (var band in TieredImbuing.Jewellery) { if (Contains(row) && row.RequiredSkill == band.Skill) { return band; } }
        return null;
    }
    internal static void Finish(Item item, EquipmentRecipe row, CraftQuality quality, Random random)
    {
        var band = Band(row); var basis = EquipmentBalance.Base(row.BaseCode);
        ((BaseJewel)item).Resource = CraftResource.Iron;
        item.Name = row.Name; item.Hue = 0; item.PlayerConstructed = true;
        var roll = new GearRoll(GearRarity.Normal, band.ItemLevel,
            AffixBalance.Level(band.ItemLevel, basis.QualityLevel, basis.MagicLevel), Math.Max(band.EquipLevel, basis.RequiredLevel), []);
        ClassGear.Profiles.Store(item, new(row.BaseCode, random.Next(basis.MinDefense, basis.MaxDefense + 1), false, roll)
        { CraftedEquipment = true, Quality = quality });
    }
    internal static bool Valid(Item item, GearState state)
    {
        if (!IsOutput(item.GetType()) || state.Tier != GearTier.Normal || state.NativeSockets != 0 || state.TinkerSockets != 0
            || state.MaterialRolls.Length != 0 || state.DurabilityPercent != 0) { return false; }
        // Craft-time levels are saved values, not a reference to today's tuning.
        return ((BaseJewel)item).Resource == CraftResource.Iron;
    }
}

public sealed class JewelleryGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    private readonly TinkerTools _tool;
    private readonly NetState _connection;
    private bool _used;
    public override bool Singleton => true;
    private JewelleryGump(PlayerMobile player, TinkerTools tool) : base(35, 25)
    { _owner = player; _tool = tool; _connection = player.NetState; }
    public static void DisplayTo(PlayerMobile player, TinkerTools tool)
    {
        if (player.NetState != null && MaterialProcessing.ToolValid(player, tool)
            && ClassProfessions.Request(player, SkillName.Tinkering, tool, true)) { player.SendGump(new JewelleryGump(player, tool)); }
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 565, 435, 5054);
        b.AddLabel(22, 18, 0x481, "Craft white jewellery");
        b.AddLabel(22, 48, 0x47E, "Iron ingots; one tool use per attempt. No sockets.");
        for (int i = 0; i < TieredImbuing.Jewellery.Length; i++)
        {
            var band = TieredImbuing.Jewellery[i]; int y = 90 + i * 60;
            b.AddButton(22, y, 4005, 4007, i * 2 + 1); b.AddLabel(57, y, 0x481, $"{band.Name} ring");
            b.AddButton(295, y, 4005, 4007, i * 2 + 2); b.AddLabel(330, y, 0x481, $"{band.Name} amulet");
            b.AddLabel(57, y + 25, 0x47E, $"Skill {band.Skill}; {band.Ingots} ingots; ilvl {band.ItemLevel}; equip {band.EquipLevel}");
        }
        b.AddButton(510, 18, 4017, 4019, 0);
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || !ReferenceEquals(sender, _connection) || sender.Mobile != _owner) { return; }
        _used = true;
        if (info.ButtonID < 1 || info.ButtonID > JewelleryCrafting.Recipes.Length || !MaterialProcessing.ToolValid(_owner, _tool)
            || !ClassProfessions.Active(_owner, SkillName.Tinkering)) { return; }
        EquipmentCraftGump.ChooseMaterial(_owner, _tool, JewelleryCrafting.Recipes[info.ButtonID - 1]);
    }
}
