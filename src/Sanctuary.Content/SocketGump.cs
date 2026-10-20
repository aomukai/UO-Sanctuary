// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Sanctuary;

public sealed class SocketGump : DynamicGump
{
    private readonly SocketRequest _request;
    private bool _used;
    public override bool Singleton => true;
    private SocketGump(SocketRequest request) : base(70, 20) { _request = request; }
    internal static void Display(SocketRequest request)
    {
        if (request is { Claimed: false } && request.Owner.NetState != null
            && ReferenceEquals(request.Owner.NetState, request.Connection)
            && TinkeringSockets.Current(request.Owner, request.Session))
        { request.Owner.SendGump(new SocketGump(request)); }
    }
    public static void Target(PlayerMobile player, TinkerTools tool)
    {
        if (player?.NetState == null || !TinkeringSockets.ToolValid(player, tool)
            || !ClassProfessions.Request(player, SkillName.Tinkering, tool, true)
            || !CraftFacilities.Allows(player, SkillName.Tinkering)) { return; }
        player.CloseGump<SocketGump>();
        long session = TinkeringSockets.Begin(player);
        player.Target = new SocketTarget(player, tool, player.NetState, session);
        player.SendMessage("Select identified socketable gear in your backpack. Failure destroys it.");
    }
    private sealed class SocketTarget(PlayerMobile owner, TinkerTools tool, NetState connection, long session)
        : Target(2, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from != owner || !ReferenceEquals(from.NetState, connection) || !TinkeringSockets.Current(owner, session)) { return; }
            TinkeringSockets.End(owner, session);
            var quote = targeted is Item item ? TinkeringSockets.Quote(owner, tool, item) : null;
            if (quote == null)
            {
                from.SendMessage("Cannot socket that item. Check its slot, origin, identification, five-socket cap and your tier/socket skill gates.");
                return;
            }
            Display(quote);
        }
        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType) => TinkeringSockets.End(owner, session);
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        var q = _request;
        b.AddPage(); b.AddBackground(0, 0, 550, 445, 5054);
        b.AddLabel(22, 18, 0x481, q.Retry ? "Intuition — retry or keep your item" : "Add one socket");
        b.AddLabel(22, 55, 0x481, $"{q.Item.Name ?? ClassGear.Base(q.Item)?.Name ?? "Equipment"}");
        b.AddItem(475, 55, q.Item.ItemID, q.Item.Hue); b.AddItemProperty(q.Item.Serial);
        b.AddLabel(22, 82, 0x47E, $"{q.State.Tier} gear — adding socket {q.Socket} of 5");
        b.AddLabel(22, 110, 0x481, $"Required Tinkering: {q.RequiredSkill}. Success: {q.Chance}%.");
        b.AddLabel(22, 138, 0x47E, $"Current sockets: {q.State.NativeSockets} native + {q.State.TinkerSockets} added.");
        b.AddLabel(22, 166, 0x481, "Cost: one tool use; crafted-tool charge saving applies.");
        b.AddLabel(22, 194, 0x21, "Failure destroys this item and all installed contents.");
        if (q.State.Roll.Rarity == GearRarity.Normal)
        {
            b.AddHtml(22, 225, 505, 50,
                "<BASEFONT COLOR=#FFB060>Socketing this white item permanently prevents it from being imbued. Continue anyway?</BASEFONT>", background: false, scrollbar: false);
        }
        else { b.AddLabel(22, 235, 0x47E, "Existing properties stay unchanged if socketing succeeds."); }
        if (q.Retry)
        {
            b.AddLabel(22, 290, 0x481, "No charge used. Your item is unchanged.");
            b.AddLabel(22, 318, 0x47E, "Retry once at the same odds, or abort and keep it safely.");
        }
        else if (q.Skill >= 1200)
        {
            b.AddLabel(22, 290, 0x47E, $"A failed roll has {TinkeringSockets.Settings.IntuitionChance}% intuition chance.");
            b.AddLabel(22, 318, 0x47E, "Intuition stops before acting: retry once or keep the item.");
        }
        b.AddButton(22, 380, 4005, 4007, 1);
        b.AddLabel(57, 380, 0x481, q.Retry ? "Retry once" : "Attempt socketing");
        b.AddButton(285, 380, 4017, 4019, 0);
        b.AddLabel(320, 380, 0x481, q.Retry ? "Abort — keep item" : "Cancel");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || sender.Mobile != _request.Owner || !ReferenceEquals(sender, _request.Connection)) { return; }
        _used = true;
        if (info.ButtonID != 1) { TinkeringSockets.Cancel(_request.Owner, _request); return; }
        if (!TinkeringSockets.Commit(_request.Owner, _request, out var result))
        { sender.Mobile.SendMessage("Socketing did not start. Check the item, tool, active craft, skill and public workbench."); return; }
        if (result.Outcome == SocketOutcome.Intuition && result.Retry != null)
        { Display(result.Retry); }
    }
}
