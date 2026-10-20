// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class SurveyGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    private readonly MapmakersPen _tool;
    private readonly NetState _connection;
    private readonly SurveyRequest _request;
    private bool _used;
    public override bool Singleton => true;
    private SurveyGump(PlayerMobile owner, MapmakersPen tool, SurveyRequest request = null) : base(40, 30)
    { _owner = owner; _tool = tool; _request = request; _connection = owner.NetState; }
    public static void DisplayTo(PlayerMobile owner, MapmakersPen tool)
    {
        if (owner.NetState != null && MaterialProcessing.ToolValid(owner, tool) && ClassProfessions.Request(owner, SkillName.Cartography, tool, true))
        { MapDrawing.Invalidate(owner); owner.SendGump(new SurveyGump(owner, tool)); }
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 600, 440, 5054); b.AddLabel(22, 16, 0x481, "The Cartographer's field book"); b.AddButton(550, 16, 4017, 4019, 0);
        if (_request is { } q)
        {
            b.AddLabel(22, 58, 0x481, $"One {q.Row.Name}");
            b.AddHtml(22, 95, 550, 65, $"<BASEFONT COLOR=#AACCFF>{MapSurveys.Description(q.Row)}</BASEFONT>", background: false, scrollbar: false);
            b.AddLabel(22, 180, 0x481, $"Cartography {q.Row.Skill:0}–{q.Row.Maximum:0}; success {q.Chance:P1}");
            b.AddLabel(22, 220, 0x47E, "Cost: one Blank Map and one pen use; crafted-tool saving.");
            b.AddLabel(22, 255, 0x47E, "Failure spends both. Cancel spends nothing.");
            b.AddLabel(22, 290, 0x47E, "Consume to use: 30 minutes, one independent map effect.");
            b.AddButton(22, 370, 4005, 4007, 1); b.AddLabel(57, 370, 0x481, "Draw one map"); return;
        }
        for (int i = 0; i < MapSurveys.Recipes.Length; i++)
        {
            var r = MapSurveys.Recipes[i]; int y = 65 + i * 64;
            b.AddButton(22, y, 4005, 4007, i + 1); b.AddLabel(57, y, 0x481, $"{r.Name} — Cartography {r.Skill:0}–{r.Maximum:0}");
            b.AddHtml(57, y + 21, 515, 40, $"<BASEFONT COLOR=#AACCFF>{MapSurveys.Description(r)}</BASEFONT>", background: false, scrollbar: false);
        }
        b.AddLabel(22, 413, 0x47E, "One Blank Map, one pen use; free public Cartography desk.");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || sender.Mobile != _owner || !ReferenceEquals(sender, _connection)) { return; } _used = true;
        if (_request != null)
        {
            if (info.ButtonID == 1) { MapDrawing.Start(_request); }
            else { _request.Claimed = true; MapDrawing.End(_request); }
            return;
        }
        if (info.ButtonID < 1 || info.ButtonID > MapSurveys.Recipes.Length) { return; }
        var q = MapDrawing.Quote(_owner, _tool, MapSurveys.Recipes[info.ButtonID - 1]);
        if (q == null) { _owner.SendMessage("Check active Cartography, trained skill, a Blank Map, your pen and public desk. You need a positive success chance."); return; }
        _owner.SendGump(new SurveyGump(_owner, _tool, q));
    }
}
