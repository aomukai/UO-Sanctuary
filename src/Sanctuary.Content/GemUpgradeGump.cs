// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Engines.Craft;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Sanctuary;

public sealed class GemUpgradeGump : DynamicGump
{
    private readonly GemUpgradeRequest _request;
    private bool _used;
    public override bool Singleton => true;
    private GemUpgradeGump(GemUpgradeRequest request) : base(70, 55) { _request = request; }
    public static void Target(PlayerMobile owner, TinkerTools tool)
    {
        if (owner?.NetState == null || !TinkeringSockets.ToolValid(owner, tool) || !owner.CanBeginAction<CraftSystem>()
            || !ClassProfessions.Request(owner, SkillName.Tinkering, tool, true) || !CraftFacilities.Allows(owner, SkillName.Tinkering)) { return; }
        if (owner.Target != null) { owner.SendMessage("Finish your current target first."); return; }
        owner.CloseGump<GemUpgradeGump>(); long session = GemUpgrading.Begin(owner);
        owner.Target = new GemTarget(owner, tool, owner.NetState, session);
        owner.SendMessage("Choose a socket gem in your backpack to upgrade matching gems. Legacy gems are decorative.");
    }
    private sealed class GemTarget(PlayerMobile owner, TinkerTools tool, NetState connection, long session) : Target(2, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from != owner || !ReferenceEquals(from.NetState, connection) || !GemUpgrading.Current(owner, session)) { return; }
            GemUpgrading.End(owner, session);
            var q = targeted is SocketGem gem ? GemUpgrading.Quote(owner, tool, gem) : null;
            if (q == null) { owner.SendMessage("No gems were used. Check matching family/quality, ingredients, trained Tinkering, tool and public bench."); return; }
            owner.SendGump(new GemUpgradeGump(q));
        }
        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType) => GemUpgrading.End(owner, session);
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        var q = _request; string name = SocketGems.Family(q.Family).Name;
        b.AddPage(); b.AddBackground(0, 0, 550, 315, 5054); b.AddLabel(22, 18, 0x481, "Upgrade matching socket gems");
        b.AddLabel(22, 60, 0x481, $"{SocketGems.Settings.UpgradeRatio} {q.Quality} {name} → 1 {q.Quality + 1} {name}");
        b.AddLabel(22, 95, 0x47E, $"Requires Tinkering {q.RequiredSkill:F0}. Success is guaranteed.");
        b.AddLabel(22, 135, 0x47E, "Cost: matching gems and one tool use. No gold fee.");
        b.AddLabel(22, 170, 0x47E, "Crafted-tool charge saving applies. Family stays unchanged.");
        b.AddButton(22, 250, 4005, 4007, 1); b.AddLabel(57, 250, 0x481, "Upgrade gems");
        b.AddButton(285, 250, 4017, 4019, 0); b.AddLabel(320, 250, 0x481, "Cancel");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || sender.Mobile != _request.Owner || !ReferenceEquals(sender, _request.Connection)) { return; } _used = true;
        if (info.ButtonID != 1) { GemUpgrading.End(_request.Owner, _request.Session); return; }
        if (!GemUpgrading.Start(_request)) { sender.Mobile.SendMessage("The upgrade inputs or conditions changed. Nothing was used."); }
    }
}
