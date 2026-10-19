// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Network;
using Server.Targeting;

namespace Sanctuary;

public sealed class EquipmentCraftGump : DynamicGump
{
    private readonly Mobile _owner;
    private readonly BaseTool _tool;
    private readonly EquipmentRecipe[] _rows;
    private readonly GearTier _tier;
    private readonly int _page;
    private readonly int _pages;
    private const int PageSize = 3;
    private bool _answered;
    public override bool Singleton => true;
    private EquipmentCraftGump(Mobile owner, BaseTool tool, EquipmentRecipe[] rows, GearTier tier, int page, int pages) : base(100, 80)
    { _owner = owner; _tool = tool; _rows = rows; _tier = tier; _page = page; _pages = pages; }
    public static void DisplayTo(Mobile owner, BaseTool tool, GearTier tier = GearTier.Normal, int page = 0)
    {
        if (owner?.NetState == null || !ClassProfessions.Affected(owner) || !MaterialProcessing.ToolValid(owner, tool)
            || !EquipmentCrafting.Supports(tool) || !ClassProfessions.Request(owner, tool.CraftSystem.MainSkill, tool, true)) { return; }
        var rows = new List<EquipmentRecipe>();
        foreach (var row in EquipmentCrafting.Recipes) { if (row.Skill == tool.CraftSystem.MainSkill && row.Tier == tier) { rows.Add(row); } }
        if (rows.Count == 0) { return; }
        int pages = (rows.Count + PageSize - 1) / PageSize;
        page = Math.Clamp(page, 0, pages - 1);
        var visible = rows.GetRange(page * PageSize, Math.Min(PageSize, rows.Count - page * PageSize));
        owner.SendGump(new EquipmentCraftGump(owner, tool, visible.ToArray(), tier, page, pages));
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 540, 165 + _rows.Length * 65, 5054);
        b.AddLabel(22, 20, 0x481, "Craft white equipment");
        b.AddLabel(22, 36, 0x47E, "One charge per attempt; eligible crafted tools may save it.");
        for (int tier = 0; tier < 3; tier++)
        {
            b.AddButton(22 + tier * 160, 55, 4005, 4007, 101 + tier);
            b.AddLabel(57 + tier * 160, 55, (GearTier)tier == _tier ? 0x481 : 0x47E, $"{(GearTier)tier}");
        }
        for (int i = 0; i < _rows.Length; i++)
        {
            var row = _rows[i]; int y = 105 + i * 65;
            b.AddButton(22, y, 4005, 4007, i + 1);
            b.AddLabel(57, y, 0x481, $"{row.Tier} {row.Name} — skill {row.RequiredSkill:F0}");
            b.AddLabel(57, y + 20, 0x47E, $"{EquipmentCrafting.Cost(row, 0)}");
            if (row.Ingredients.Length > 2) { b.AddLabel(57, y + 37, 0x47E, $"{EquipmentCrafting.Cost(row, 2)}"); }
        }
        int footer = 120 + _rows.Length * 65;
        if (_page > 0) { b.AddButton(22, footer, 4014, 4016, 201); b.AddLabel(57, footer, 0x481, "Previous"); }
        b.AddLabel(220, footer, 0x47E, $"Page {_page + 1} of {_pages}");
        if (_page + 1 < _pages) { b.AddButton(420, footer, 4005, 4007, 202); b.AddLabel(455, footer, 0x481, "Next"); }
        b.AddButton(495, 20, 4017, 4019, 0);
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_answered || sender.Mobile != _owner) { return; }
        _answered = true;
        if (info.ButtonID is >= 101 and <= 103)
        { DisplayTo(_owner, _tool, (GearTier)(info.ButtonID - 101)); return; }
        if (info.ButtonID is 201 or 202)
        { DisplayTo(_owner, _tool, _tier, _page + (info.ButtonID == 201 ? -1 : 1)); return; }
        if (info.ButtonID is < 1 || info.ButtonID > _rows.Length || !MaterialProcessing.ToolValid(_owner, _tool)
            || !ClassProfessions.Active(_owner, _tool.CraftSystem.MainSkill)) { return; }
        _owner.SendMessage("Target the first ingredient. Its material subtype and colour will be used.");
        _owner.Target = new MaterialTarget(_rows[info.ButtonID - 1], _tool, sender);
    }
    private sealed class MaterialTarget(EquipmentRecipe row, BaseTool tool, NetState connection) : Target(2, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (!ReferenceEquals(connection, from.NetState) || !ClassProfessions.Affected(from)
                || !MaterialProcessing.ToolValid(from, tool) || targeted is not Item item || !MaterialProcessing.Accessible(from, item)) { return; }
            var recipe = EquipmentCrafting.Prepare(row, item);
            if (recipe == null) { from.SendMessage("That is not a suitable processed ingredient for this recipe."); return; }
            recipe.Craft(from, tool.CraftSystem, null, tool, item.Hue);
        }
    }
}
