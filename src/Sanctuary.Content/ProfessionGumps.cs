// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Engines.Craft;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class ProfessionChoiceGump : DynamicGump
{
    private readonly Serial _owner;
    private readonly ClassProfessions.Choice _choice;
    public override bool Singleton => true;
    private ProfessionChoiceGump(PlayerMobile owner, ClassProfessions.Choice choice) : base(100, 80)
    { _owner = owner.Serial; _choice = choice; }
    internal static void DisplayTo(PlayerMobile owner, ClassProfessions.Choice choice)
    {
        if (owner.NetState != null) { owner.SendGump(new ProfessionChoiceGump(owner, choice)); }
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 510, 290, 5054);
        b.AddLabel(22, 20, 0x481, "Choose your first profession");
        b.AddLabel(22, 58, 0x47E, $"Choose {ClassProfessions.Name(_choice.Skill)}?");
        b.AddHtml(22, 90, 465, 110,
            "<BASEFONT COLOR=#FFFFFF>Until you reach class level 80 or 100.0 in a crafting skill and earn town-hall wardrobe access, you can choose only one crafting profession.<BR>Gathering stays available. Cancel leaves your choice open and costs nothing.</BASEFONT>",
            background: false, scrollbar: false);
        b.AddButton(22, 235, 4005, 4007, 1); b.AddLabel(57, 235, 0x481, "Confirm profession");
        b.AddButton(290, 235, 4017, 4019, 0); b.AddLabel(325, 235, 0x481, "Cancel");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is PlayerMobile player && player.Serial == _owner)
        { ClassProfessions.Respond(player, _choice.Token, info.ButtonID == 1); }
    }
}

public sealed class ProfessionGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    public override bool Singleton => true;
    private ProfessionGump(PlayerMobile owner) : base(100, 80) { _owner = owner; }
    public static void DisplayTo(PlayerMobile owner)
    {
        if (owner?.NetState != null && ClassProfessions.Affected(owner)) { owner.SendGump(new ProfessionGump(owner)); }
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        var state = ClassProfessions.Profiles.Find(_owner);
        b.AddPage(); b.AddBackground(0, 0, 500, 435, 5054);
        b.AddLabel(22, 20, 0x481, $"Craft: {ClassProfessions.Name(state.Craft)}");
        b.AddLabel(22, 48, 0x47E, state.Confirmed ? "First profession confirmed" : "Choice open: use a crafting tool or trainer");
        int y = 85;
        foreach (byte index in TradeSkills.Crafting)
        {
            var skill = _owner.Skills[(SkillName)index];
            b.AddLabel(22, y, 0x481, $"{ClassProfessions.Name((SkillName)index)}: {skill.Base:F1} / {skill.Cap:F1}");
            y += 25;
        }
        b.AddLabel(22, 350, 0x47E, "Mining, Lumberjacking and Fishing stay active.");
        b.AddLabel(22, 375, 0x47E, "Use your tool for practice from 30.0 through 70.0.");
        b.AddButton(445, 20, 4017, 4019, 0);
    }
}

public sealed class ProfessionToolGump : DynamicGump
{
    private readonly Serial _owner;
    private readonly BaseTool _tool;
    private readonly SkillName _skill;
    private readonly int _band;
    private bool _used;
    public override bool Singleton => true;
    private ProfessionToolGump(PlayerMobile owner, BaseTool tool) : base(100, 80)
    {
        _owner = owner.Serial; _tool = tool; _skill = tool.CraftSystem.MainSkill;
        _band = ProfessionPractice.Band(owner.Skills[_skill].BaseFixedPoint);
    }
    public static void DisplayTo(PlayerMobile owner, BaseTool tool)
    {
        if (owner?.NetState != null && ClassProfessions.Ready(owner) && tool is { Deleted: false }
            && (tool.IsChildOf(owner.Backpack) || tool.Parent == owner)) { owner.SendGump(new ProfessionToolGump(owner, tool)); }
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 500, 400, 5054);
        b.AddLabel(22, 20, 0x481, $"{ClassProfessions.Name(_skill)} tool");
        b.AddLabel(22, 55, 0x47E, EquipmentRepairs.DedicatedTool(_tool)
            ? "Repairs need your craft and a public town workshop."
            : "Production and practice need your craft and town station.");
        if (!EquipmentRepairs.DedicatedTool(_tool))
        { b.AddButton(22, 95, 4005, 4007, 1); b.AddLabel(57, 95, 0x481, "Open tool actions"); }
        if (!EquipmentRepairs.DedicatedTool(_tool) && _band >= 0 && ProfessionPractice.Recipe(_skill) is { } recipe)
        {
            b.AddLabel(22, 140, 0x481, $"Practice: {30 + _band * 10}.0 to {40 + _band * 10}.0");
            b.AddLabel(22, 165, 0x47E, $"Cost: {recipe.Amounts[_band]} {ProfessionPractice.ResourceName(recipe.Resource)} and one tool use. No output.");
            b.AddButton(22, 205, 4005, 4007, 2); b.AddLabel(57, 205, 0x481, "Practice once");
        }
        else if (!EquipmentRepairs.DedicatedTool(_tool)) { b.AddLabel(22, 145, 0x47E, "Practice is available from 30.0 to below 70.0."); }
        if (EquipmentCrafting.Supports(_tool))
        {
            b.AddButton(22, 235, 4005, 4007, 4); b.AddLabel(57, 235, 0x481, "Craft white equipment");
        }
        if (MaterialProcessing.Supports(_tool))
        {
            b.AddButton(22, 270, 4005, 4007, 3); b.AddLabel(57, 270, 0x481, "Process materials into scraps");
            b.AddLabel(22, 305, 0x47E, "Portable; no profession choice or tool uses needed.");
        }
        if (EquipmentRepairs.RepairTool(_tool))
        {
            b.AddButton(22, 345, 4005, 4007, 5); b.AddLabel(57, 345, 0x481, "Repair / recharge equipment");
        }
        b.AddButton(445, 20, 4017, 4019, 0);
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || sender.Mobile is not PlayerMobile owner || owner.Serial != _owner || !ClassProfessions.Affected(owner)
            || !ClassProfessions.Ready(owner) || _tool is not { Deleted: false, UsesRemaining: > 0 }
            || !(_tool.IsChildOf(owner.Backpack) || _tool.Parent == owner) || _tool.CraftSystem.MainSkill != _skill) { return; }
        _used = true;
        if (EquipmentRepairs.DedicatedTool(_tool) && info.ButtonID != 5) { return; }
        if (info.ButtonID == 1)
        {
            if (_skill == SkillName.Imbuing)
            {
                if (ClassProfessions.Request(owner, _skill, _tool, true)) { EssenceGump.DisplayTo(owner); }
            }
            else if (ClassProfessions.Request(owner, _skill, _tool, true)) { CraftItem.ShowCraftMenu(owner, _tool.CraftSystem, _tool); }
        }
        else if (info.ButtonID == 5) { RepairGump.Target(owner, _tool, null); }
        else if (info.ButtonID == 4) { EquipmentCraftGump.DisplayTo(owner, _tool); }
        else if (info.ButtonID == 3) { ProcessingGump.Target(owner, _tool); }
        else if (info.ButtonID == 2)
        {
            if (!ProfessionPractice.Attempt(owner, _tool, _band, out _))
            { owner.SendMessage("Practice did not start. Check your active craft, skill band, ingredients, tool and current action."); }
        }
    }
}
