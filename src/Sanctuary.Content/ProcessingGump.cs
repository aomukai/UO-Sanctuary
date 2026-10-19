// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Network;
using Server.Targeting;

namespace Sanctuary;

public sealed class ProcessingGump : DynamicGump
{
    private readonly MaterialProcessing.Request _request;
    private bool _answered;
    public override bool Singleton => true;
    private ProcessingGump(MaterialProcessing.Request request) : base(100, 80) { _request = request; }
    public static void Target(Mobile owner, BaseTool tool)
    {
        if (!ClassProfessions.Enabled || !MaterialProcessing.Supports(tool) || !MaterialProcessing.ToolValid(owner, tool)) { return; }
        owner.SendMessage("Select a material stack in your backpack to process into generic scraps.");
        var connection = owner.NetState;
        owner.Target = new MaterialTarget(owner, tool, connection);
    }
    private sealed class MaterialTarget(Mobile owner, BaseTool tool, NetState connection) : Target(2, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from != owner || !ReferenceEquals(from.NetState, connection)) { return; }
            var request = targeted is Item item ? MaterialProcessing.Quote(owner, tool, item) : null;
            if (request == null) { from.SendMessage("That material cannot be processed with this tool."); return; }
            if (from.NetState != null) { from.SendGump(new ProcessingGump(request)); }
        }
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 510, 325, 5054);
        b.AddLabel(22, 20, 0x481, "Process materials");
        b.AddLabel(22, 55, 0x481, $"Available: {_request.Available}. Each gives {_request.OutputPerUnit} {_request.OutputName}.");
        b.AddHtml(22, 90, 465, 75,
            "<BASEFONT COLOR=#FFFFFF>Output is generic scraps. All material colour and special subtype identity will be lost.<BR>No tool uses. Cancel costs nothing.</BASEFONT>", background: false, scrollbar: false);
        b.AddLabel(22, 180, 0x481, "Input quantity:");
        b.AddBackground(165, 176, 150, 30, 3000);
        b.AddTextEntry(175, 180, 125, 22, 0, 0, "1");
        b.AddButton(22, 235, 4005, 4007, 1); b.AddLabel(57, 235, 0x481, "Process quantity");
        b.AddButton(275, 235, 4005, 4007, 2); b.AddLabel(310, 235, 0x481, "Process all");
        b.AddButton(22, 280, 4017, 4019, 0); b.AddLabel(57, 280, 0x481, "Cancel");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_answered || sender.Mobile != _request.Owner) { return; }
        _answered = true;
        int quantity = info.ButtonID == 2 ? _request.Available
            : info.ButtonID == 1 && int.TryParse(info.GetTextEntry(0), out int parsed) ? parsed : 0;
        if (quantity > 0 && !MaterialProcessing.Complete(_request, quantity))
        { sender.Mobile.SendMessage("Processing did not complete. Check the materials, tool and free backpack space."); }
    }
}
