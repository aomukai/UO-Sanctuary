// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Sanctuary;

public sealed class RepairGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    private readonly Item _item;
    private readonly BaseTool _tool;
    private readonly BaseVendor _vendor;
    private readonly RepairRequest _request;
    private bool _answered;
    public override bool Singleton => true;
    private RepairGump(PlayerMobile owner, Item item, BaseTool tool, BaseVendor vendor, RepairRequest request) : base(35, 25)
    { _owner = owner; _item = item; _tool = tool; _vendor = vendor; _request = request; }
    public static void Initialize()
    {
        CommandSystem.Register("Repair", AccessLevel.Player, e =>
        {
            if (e.Mobile is PlayerMobile player)
            {
                if (!EquipmentRepairs.Ready(player)) { return; }
                foreach (var tool in player.Backpack.FindItemsByType<BaseTool>(true))
                {
                    if (EquipmentRepairs.RepairTool(tool) && ClassProfessions.Active(player, tool.CraftSystem.MainSkill))
                    { Target(player, tool, null); return; }
                }
                player.SendMessage("Use a repair tool at your public workshop, or choose Repair on the appropriate town merchant.");
            }
        });
    }
    public static void Target(PlayerMobile player, BaseTool tool, BaseVendor vendor)
    {
        if (!EquipmentRepairs.Ready(player) || player.Target != null
            || (vendor != null ? !EquipmentRepairs.ServiceReady(player, vendor)
                : !EquipmentRepairs.RepairTool(tool) || !ClassProfessions.Request(player, tool.CraftSystem.MainSkill, tool, true)))
        { return; }
        player.SendMessage("Select equipment in your backpack to repair or recharge. A cost confirmation follows.");
        player.Target = new ItemTarget(tool, vendor, player.NetState);
    }
    private sealed class ItemTarget(BaseTool tool, BaseVendor vendor, NetState connection) : Target(2, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object selected)
        {
            if (from is PlayerMobile player && ReferenceEquals(player.NetState, connection) && selected is Item item)
            { DisplayTo(player, item, tool, vendor); }
        }
    }
    public static void DisplayTo(PlayerMobile player, Item item, BaseTool tool = null, BaseVendor vendor = null)
    {
        if (player.NetState == null) { return; }
        var check = EquipmentRepairs.Quote(player, item, tool, vendor, true, -1);
        if (check == null)
        {
            player.SendMessage("That item needs no supported work, or the tool, active profession, workshop or NPC is unsuitable. Keep it in your backpack.");
            return;
        }
        player.SendGump(new RepairGump(player, item, tool, vendor, null));
    }
    private string Name => _item.Name ?? ClassGear.Base(_item)?.Name ?? _item.GetType().Name;
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 570, 440, 5054);
        b.AddLabel(22, 18, 0x481, _vendor == null ? "Repair / recharge equipment" : "Town repair / recharge service");
        b.AddLabel(22, 50, 0x481, $"{Name}");
        b.AddItem(510, 55, _item.ItemID, _item.Hue); b.AddItemProperty(_item.Serial);
        var native = GearDetails.Read(_item);
        b.AddLabel(22, 80, 0x47E, $"Durability: {native.Durability ?? 0} / {native.MaximumDurability ?? 0}");
        if (_request == null)
        {
            bool damaged = native.Durability < native.MaximumDurability && native.MaximumDurability > 0;
            if (damaged)
            {
                b.AddButton(22, 135, 4005, 4007, 1); b.AddLabel(57, 135, 0x481, "Repair durability");
            }
            if (_item is BaseWand wand)
            {
                b.AddLabel(22, 175, 0x481, $"Spell charges: {wand.Charges} / {EquipmentRepairs.Capacity(wand)}");
                b.AddLabel(22, 210, 0x47E, "Charges to restore:");
                b.AddBackground(205, 204, 100, 30, 3000); b.AddTextEntry(215, 210, 80, 22, 0, 0, "1");
                b.AddButton(22, 250, 4005, 4007, 2); b.AddLabel(57, 250, 0x481, "Recharge quantity");
                b.AddButton(300, 250, 4005, 4007, 3); b.AddLabel(335, 250, 0x481, "Fill charges");
                if (damaged)
                {
                    b.AddButton(22, 290, 4005, 4007, 4); b.AddLabel(57, 290, 0x481, "Repair + recharge quantity");
                    b.AddButton(300, 290, 4005, 4007, 5); b.AddLabel(335, 290, 0x481, "Repair + fill");
                }
            }
            b.AddLabel(22, 325, 0x47E, "Repairs restore durability to its remaining maximum.");
            b.AddLabel(22, 350, 0x47E, "Charge-only work does not lower maximum durability.");
            b.AddLabel(22, 375, 0x47E, "The next screen shows the cost before you commit.");
        }
        else
        {
            int y = 120;
            foreach (var cost in _request.Costs)
            {
                b.AddLabel(22, y, 0x481, $"{cost.Amount} x {ResourceName(cost.Type)}"); y += 26;
            }
            b.AddLabel(22, 235, 0x481, $"Gold fee: {_request.Gold}. Charges restored: {_request.RestoreCharges}.");
            var bracket = EquipmentRepairs.Bracket(_request.SkillValue);
            if (_request.Durability)
            {
                b.AddLabel(22, 275, 0x21, $"Maximum durability: {bracket.LossChance}% chance to lose {bracket.LossMin}-{bracket.LossMax}, floor 1.");
            }
            else { b.AddLabel(22, 275, 0x47E, "No durability loss or physical material cost."); }
            b.AddLabel(22, 310, 0x47E, $"{bracket.SavingChance}% chance to save all materials and essence.");
            b.AddLabel(22, 345, 0x47E, _vendor == null ? "One tool use; crafted tools may save the use." : "Resources come from your backpack. No personal tool needed.");
            b.AddLabel(22, 375, 0x47E, "Have the full quoted resources available before confirming.");
            b.AddButton(22, 400, 4005, 4007, 6); b.AddLabel(57, 400, 0x481, "Confirm");
        }
        b.AddButton(385, 400, 4017, 4019, 0); b.AddLabel(420, 400, 0x481, "Cancel");
    }
    public static string ResourceName(Type type) => type == typeof(MagicEssence) ? "Magic Essence"
        : type == typeof(MetalScraps) ? "Metal Scraps" : type == typeof(LeatherStrips) ? "Leather Strips"
        : type == typeof(ClothScraps) ? "Cloth Scraps"
        : System.Text.RegularExpressions.Regex.Replace(type.Name, "([a-z])([A-Z])", "$1 $2");
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_answered || sender.Mobile != _owner || !EquipmentRepairs.Ready(_owner)) { return; }
        _answered = true;
        if (_request != null)
        {
            if (info.ButtonID != 6) { return; }
            if (EquipmentRepairs.Commit(_request, out var result))
            {
                _owner.SendMessage($"Work complete. Maximum durability lost: {result.LostMaximum}; charges restored: {result.Charges}.");
                if (result.SavedResources) { _owner.SendMessage("Your materials and essence were saved."); }
            }
            else { _owner.SendMessage("Nothing was consumed. Check the item, resources, tool/workshop or merchant, then try again."); }
            return;
        }
        bool durability = info.ButtonID is 1 or 4 or 5;
        int charges;
        if (info.ButtonID == 1) { charges = 0; }
        else if (info.ButtonID is 3 or 5) { charges = -1; }
        else if (info.ButtonID is 2 or 4 && int.TryParse(info.GetTextEntry(0), out int quantity) && quantity > 0) { charges = quantity; }
        else { return; }
        var request = EquipmentRepairs.Quote(_owner, _item, _tool, _vendor, durability, charges);
        if (request != null) { _owner.SendGump(new RepairGump(_owner, _item, _tool, _vendor, request)); }
        else { _owner.SendMessage("The item or service has changed, or that charge quantity is invalid. Nothing was consumed."); }
    }
}
