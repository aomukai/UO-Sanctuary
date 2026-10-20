// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class CarpentryGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    private readonly BaseTool _tool;
    private readonly NetState _connection;
    private readonly CarpentryRequest _request;
    private readonly int _category, _page;
    private bool _used;
    public override bool Singleton => true;
    private CarpentryGump(PlayerMobile owner, BaseTool tool, int category = 0, int page = 0, CarpentryRequest request = null) : base(40, 30)
    { _owner = owner; _tool = tool; _category = category; _page = page; _request = request; _connection = owner.NetState; }
    public static void DisplayTo(PlayerMobile owner, BaseTool tool)
    {
        if (owner.NetState != null && MaterialProcessing.ToolValid(owner, tool) && CarpentryCrafting.Supports(tool)
            && ClassProfessions.Request(owner, SkillName.Carpentry, tool, true))
        { CarpentryCrafting.Invalidate(owner); owner.SendGump(new CarpentryGump(owner, tool)); }
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 600, 450, 5054); b.AddLabel(22, 16, 0x481, "The Carpenter's book"); b.AddButton(550, 16, 4017, 4019, 0);
        if (_request is { } q)
        {
            b.AddLabel(22, 58, 0x481, $"One {q.Row.Name}");
            b.AddHtml(22, 95, 550, 70, $"<BASEFONT COLOR=#AACCFF>{CarpentryCrafting.Cost(q.Row)}</BASEFONT>", background: false, scrollbar: false);
            b.AddLabel(22, 180, 0x481, $"Carpentry {q.Row.Skill:0.0}; success {q.Chance:P1}");
            b.AddLabel(22, 220, 0x47E, "One output and tool use; crafted-tool saving applies.");
            b.AddLabel(22, 255, 0x47E, "Failure loses half of each material, rounded up.");
            b.AddLabel(22, 290, 0x47E, "Station furniture does not unlock private crafting.");
            b.AddButton(22, 410, 4005, 4007, 1); b.AddLabel(57, 410, 0x481, "Craft one object");
            return;
        }
        for (int c = 0; c < CarpentryCrafting.Categories.Length; c++)
        { int x = 22 + c % 3 * 175, y = 50 + c / 3 * 30; b.AddButton(x, y, 4005, 4007, 1000 + c); b.AddLabel(x + 35, y, 0x481, $"{CarpentryCrafting.Categories[c]}"); }
        int visible = 0;
        for (int i = 0; i < CarpentryCrafting.Recipes.Length; i++)
        {
            var r = CarpentryCrafting.Recipes[i]; if (r.Category != CarpentryCrafting.Categories[_category]) { continue; }
            int index = visible++ - _page * 6; if (index is < 0 or >= 6) { continue; }
            int y = 120 + index * 45;
            b.AddButton(22, y, 4005, 4007, i + 1); b.AddLabel(57, y, 0x481, $"{r.Name} — {r.Skill:0.0}");
            b.AddHtml(57, y + 20, 540, 25, $"<BASEFONT COLOR=#AACCFF>{CarpentryCrafting.Cost(r)}</BASEFONT>", background: false, scrollbar: false);
        }
        if (_page > 0) { b.AddButton(22, 395, 4014, 4016, 2000); b.AddLabel(57, 395, 0x481, "Previous"); }
        if (visible > (_page + 1) * 6) { b.AddButton(440, 395, 4005, 4007, 2001); b.AddLabel(475, 395, 0x481, "Next"); }
        b.AddLabel(22, 430, 0x47E, "Free public workshop. Deeds can be placed in a house you own.");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || !ReferenceEquals(sender, _connection) || sender.Mobile != _owner) { return; } _used = true;
        if (_request != null)
        {
            if (info.ButtonID == 1) { CarpentryCrafting.Start(_request); }
            else { _request.Claimed = true; CarpentryCrafting.End(_request); }
            return;
        }
        if (!MaterialProcessing.ToolValid(_owner, _tool) || !ClassProfessions.Active(_owner, SkillName.Carpentry)) { return; }
        if (info.ButtonID is >= 1000 and < 1006)
        { CarpentryCrafting.Invalidate(_owner); _owner.SendGump(new CarpentryGump(_owner, _tool, info.ButtonID - 1000)); return; }
        if (info.ButtonID is 2000 or 2001)
        {
            int page = _page + (info.ButtonID == 2000 ? -1 : 1); if (page is < 0 or > 3) { return; }
            CarpentryCrafting.Invalidate(_owner); _owner.SendGump(new CarpentryGump(_owner, _tool, _category, page)); return;
        }
        if (info.ButtonID < 1 || info.ButtonID > CarpentryCrafting.Recipes.Length) { return; }
        var q = CarpentryCrafting.Quote(_owner, _tool, CarpentryCrafting.Recipes[info.ButtonID - 1]);
        if (q == null) { _owner.SendMessage("Check your active craft, trained skill, materials, tool and public Carpentry workshop. Keg components must be empty."); return; }
        _owner.SendGump(new CarpentryGump(_owner, _tool, _category, _page, q));
    }
}
