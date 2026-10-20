// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Sanctuary;

public sealed class FortificationGump : DynamicGump
{
    private readonly FortifyRequest _request;
    private bool _answered;
    public override bool Singleton => true;
    private FortificationGump(FortifyRequest request) : base(60, 45) { _request = request; }
    internal static void Target(PlayerMobile owner, Item powder)
    {
        if (!EquipmentRepairs.Ready(owner) || !RuneCrafting.Accessible(owner, powder) || owner.Target != null) { return; }
        owner.SendMessage("Choose identified equipment in your backpack to fortify. A confirmation follows.");
        owner.Target = new EquipmentTarget(powder, owner.NetState);
    }
    private sealed class EquipmentTarget(Item powder, NetState connection) : Target(2, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object selected)
        {
            if (from is not PlayerMobile owner || owner.NetState != connection || selected is not Item item) { return; }
            var request = Fortification.Quote(owner, powder, item);
            if (request == null) { owner.SendMessage("Choose identified, naturally durable equipment below its ceiling, and keep both items in your backpack."); return; }
            if (owner.NetState != null) { owner.SendGump(new FortificationGump(request)); }
        }
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        var q = _request;
        b.AddPage(); b.AddBackground(0, 0, 500, 310, 5054);
        b.AddLabel(22, 20, 0x481, "Fortify equipment");
        b.AddLabel(22, 65, 0x481, $"{q.Item.Name ?? q.Item.GetType().Name}");
        b.AddLabel(22, 105, 0x47E, $"Maximum durability: {q.Native.MaximumDurability} → {q.Native.MaximumDurability + q.Added}");
        b.AddLabel(22, 140, 0x47E, $"Natural ceiling: {q.Ceiling}; current durability stays {q.Native.Durability}.");
        b.AddLabel(22, 180, 0x47E, "Consumes one powder use. Ordinary repair remains separate.");
        b.AddButton(22, 245, 4005, 4007, 1); b.AddLabel(57, 245, 0x481, "Fortify");
        b.AddButton(320, 245, 4017, 4019, 0); b.AddLabel(355, 245, 0x481, "Cancel");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_answered || sender != _request.Connection || sender.Mobile != _request.Owner) { return; }
        _answered = true;
        if (info.ButtonID == 1) { Fortification.Commit(_request); }
        else { _request.Claimed = true; }
    }
}

public sealed class FortificationCraftGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    private readonly MortarPestle _tool;
    private readonly FortificationCraftRequest _request;
    private readonly NetState _connection;
    private bool _answered;
    public override bool Singleton => true;
    private FortificationCraftGump(PlayerMobile owner, MortarPestle tool, FortificationCraftRequest request) : base(60, 45)
    { _owner = owner; _tool = tool; _request = request; _connection = owner.NetState; }
    internal static void DisplayTo(PlayerMobile owner, MortarPestle tool)
    {
        if (owner.NetState == null || !FortificationCrafting.Ready(owner, tool)) { return; }
        var request = FortificationCrafting.Quote(owner, tool);
        if (request == null) { owner.SendMessage("Check your Alchemy skill, chitin, mortar and public workshop."); return; }
        owner.SendGump(new FortificationCraftGump(owner, tool, request));
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 500, 310, 5054);
        b.AddLabel(22, 20, 0x481, "Alchemy — Powder of Fortifying");
        b.AddLabel(22, 65, 0x47E, $"{Fortification.Settings.ChitinCost} chitin → one single-use powder.");
        b.AddLabel(22, 105, 0x47E, $"Alchemy {Fortification.Settings.RequiredSkill:0}; success {_request.Chance:P1}.");
        b.AddLabel(22, 140, 0x47E, "One mortar use; crafted-tool saving applies. No bottle.");
        b.AddLabel(22, 180, 0x47E, $"Failure consumes {(Fortification.Settings.ChitinCost + 1) / 2} chitin and one tool use.");
        b.AddButton(22, 245, 4005, 4007, 1); b.AddLabel(57, 245, 0x481, "Craft powder");
        b.AddButton(320, 245, 4017, 4019, 0); b.AddLabel(355, 245, 0x481, "Cancel");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_answered || sender != _connection || sender.Mobile != _owner) { return; } _answered = true;
        if (info.ButtonID == 1) { FortificationCrafting.Start(_request); }
        else { _request.Claimed = true; }
    }
}
