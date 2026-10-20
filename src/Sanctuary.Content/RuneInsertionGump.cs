// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Sanctuary;

public sealed class RuneInsertionGump : DynamicGump
{
    private readonly RuneInsertion _request;
    private bool _used;
    public override bool Singleton => true;
    private RuneInsertionGump(RuneInsertion request) : base(70, 45) { _request = request; }
    public static void Target(PlayerMobile owner, InscribedRune rune)
    {
        if (owner?.NetState == null || !RuneSockets.Ready(owner) || !RuneSockets.Accessible(owner, rune)) { return; }
        if (owner.Target != null) { owner.SendMessage("Finish your current target first."); return; }
        owner.CloseGump<RuneInsertionGump>(); owner.CloseGump<GemInsertionGump>(); long session = RuneSockets.Begin(owner);
        owner.Target = new HostTarget(owner, rune, owner.NetState, session);
        owner.SendMessage("Choose identified equipment in your backpack with an empty socket. Installation is permanent.");
    }
    private sealed class HostTarget(PlayerMobile owner, InscribedRune rune, NetState connection, long session) : Target(2, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from != owner || !ReferenceEquals(from.NetState, connection) || !RuneSockets.Current(owner, session)) { return; }
            RuneSockets.End(owner, session);
            var q = targeted is Item item ? RuneSockets.Quote(owner, rune, item) : null;
            if (q == null) { owner.SendMessage("No rune was used. Choose identified, non-vendor equipment with an empty socket in your backpack."); return; }
            owner.SendGump(new RuneInsertionGump(q));
        }
        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType) => RuneSockets.End(owner, session);
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        var q = _request; int count = RuneSockets.Filled(q.State) + 1;
        b.AddPage(); b.AddBackground(0, 0, 550, 375, 5054);
        b.AddLabel(22, 18, 0x481, "Permanently install this rune");
        b.AddLabel(22, 55, 0x481, $"{q.Content.Pattern.Name}");
        b.AddLabel(22, 82, 0x47E, $"{q.Content.Pattern.StatLabel}: +{q.Content.Percentage}% — {CraftTransactions.QualityName(q.Content.Quality)}");
        b.AddLabel(22, 115, 0x481, $"Into: {q.Host.Name ?? ClassGear.Base(q.Host)?.Name ?? "Equipment"}");
        b.AddLabel(22, 145, 0x47E, $"Filled sockets: {RuneSockets.Filled(q.State)} → {count} of {q.State.SocketCount}");
        b.AddLabel(22, 175, 0x481, $"Result requires level {Math.Max(ClassGear.RequiredLevel(q.Host), RuneSockets.RequiredLevel(count))} plus its existing attribute requirements.");
        b.AddLabel(22, 210, 0x47E, "Cost: this rune only. No gold, tools or crafting skill needed.");
        b.AddLabel(22, 245, 0x21, "Permanent: no extraction or replacement. Salvage destroys it.");
        b.AddButton(22, 315, 4005, 4007, 1); b.AddLabel(57, 315, 0x481, "Install rune");
        b.AddButton(285, 315, 4017, 4019, 0); b.AddLabel(320, 315, 0x481, "Cancel");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || sender.Mobile != _request.Owner || !ReferenceEquals(sender, _request.Connection)) { return; } _used = true;
        if (info.ButtonID != 1) { RuneSockets.End(_request.Owner, _request.Session); return; }
        if (!RuneSockets.Commit(_request)) { sender.Mobile.SendMessage("The rune or equipment changed. Nothing was used."); }
    }
}
