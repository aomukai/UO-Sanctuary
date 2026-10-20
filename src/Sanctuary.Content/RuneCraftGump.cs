// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Gumps;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Sanctuary;

public sealed class RuneCraftGump : DynamicGump
{
    private readonly RuneRequest _request;
    private bool _used;
    public override bool Singleton => true;
    private RuneCraftGump(RuneRequest request) : base(70, 20) { _request = request; }
    internal static void Display(RuneRequest request)
    {
        if (request is { Claimed: false, Started: false } && request.Owner.NetState != null
            && ReferenceEquals(request.Owner.NetState, request.Connection) && RuneCrafting.Current(request.Owner, request.Session))
        { request.Owner.SendGump(new RuneCraftGump(request)); }
    }
    public static void Target(PlayerMobile owner, ScribesPen tool)
    {
        if (owner?.NetState == null || !RuneCrafting.ToolValid(owner, tool)
            || !ClassProfessions.Request(owner, SkillName.Inscribe, tool, true)
            || !CraftFacilities.Allows(owner, SkillName.Inscribe)) { return; }
        if (!owner.CanBeginAction<CraftSystem>()) { owner.SendLocalizedMessage(500119); return; }
        if (owner.Target != null) { owner.SendMessage("Finish your current target first."); return; }
        owner.CloseGump<RuneCraftGump>(); long session = RuneCrafting.Begin(owner);
        owner.Target = new MouldTarget(owner, tool, owner.NetState, session);
        owner.SendMessage("Choose a rune mould in your backpack. Lowest suitable bark tiers are used first; review the costs before crafting.");
    }
    private sealed class MouldTarget(PlayerMobile owner, ScribesPen tool, NetState connection, long session) : Target(2, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from != owner || !ReferenceEquals(from.NetState, connection) || !RuneCrafting.Current(owner, session)) { return; }
            RuneCrafting.End(owner, session);
            var quote = targeted is RuneMould mould ? RuneCrafting.Quote(owner, tool, mould) : null;
            if (quote == null)
            {
                owner.SendMessage("Cannot craft that rune. Check the mould, suitable bark, pen, public desk and trained Inscription. The success chance must be above zero.");
                return;
            }
            Display(quote);
        }
        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType) => RuneCrafting.End(owner, session);
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        var q = _request; var p = q.Pattern;
        b.AddPage(); b.AddBackground(0, 0, 550, 440, 5054);
        b.AddLabel(22, 18, 0x481, "Craft the declared rune");
        b.AddLabel(22, 50, 0x481, $"{p.Name}");
        b.AddLabel(22, 77, 0x47E, $"{p.StatLabel} — the mould fixes this stat");
        b.AddLabel(22, 104, 0x481, $"Tier {p.Tier}: required Inscription {q.Row.RequiredSkill:F0}. Success: {q.Chance:P1}.");
        b.AddLabel(22, 131, 0x47E, "Cost: this mould, the bark below and one pen use.");
        int y = 163;
        for (int tier = p.Tier; tier <= 8; tier++)
        {
            int amount = 0;
            foreach (var input in q.Bark) { if (input.Tier == tier) { amount += input.Take; } }
            if (amount > 0)
            { b.AddLabel(22, y, 0x481, $"{amount} {RunebarkHarvesting.Tiers[tier - 1].Name} Runebark"); y += 22; }
        }
        if (q.Skill < (q.Row.RequiredSkill + 10) * 10) { b.AddLabel(22, 267, 0x47E, "On success: Average 100%."); }
        else if (q.Skill < (q.Row.RequiredSkill + 20) * 10) { b.AddLabel(22, 267, 0x47E, "On success: Average 90%, High Quality 10%."); }
        else { b.AddLabel(22, 267, 0x47E, "On success: Average 89%, High Quality 10%, Superior 1%."); }
        b.AddLabel(22, 293, 0x481, $"Rune values: {p.Baseline + p.Tier - 1}% Average / {p.Baseline + p.Tier}% High / {p.Baseline + p.Tier + 1}% Superior");
        b.AddLabel(22, 326, 0x21, "Failure loses the mould and half the bark, rounded up.");
        b.AddLabel(22, 351, 0x47E, "A pen use is spent on either result; crafted-tool saving applies.");
        b.AddButton(22, 393, 4005, 4007, 1); b.AddLabel(57, 393, 0x481, "Craft rune");
        b.AddButton(285, 393, 4017, 4019, 0); b.AddLabel(320, 393, 0x481, "Cancel");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || sender.Mobile != _request.Owner || !ReferenceEquals(sender, _request.Connection)) { return; }
        _used = true;
        if (info.ButtonID != 1) { RuneCrafting.End(_request.Owner, _request.Session); return; }
        if (!RuneCrafting.Start(_request)) { sender.Mobile.SendMessage("Crafting did not start. Recheck the inputs, pen, active craft and public desk."); }
    }
}
