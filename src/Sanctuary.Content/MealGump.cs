// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class MealGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    private readonly Skillet _tool;
    private readonly NetState _connection;
    private readonly MealRequest _request;
    private readonly int _tier;
    private bool _used;
    public override bool Singleton => true;
    private MealGump(PlayerMobile owner, Skillet tool, int tier = 1, MealRequest request = null) : base(40, 30)
    { _owner = owner; _tool = tool; _tier = tier; _request = request; _connection = owner.NetState; }
    public static void DisplayTo(PlayerMobile owner, Skillet tool)
    {
        if (owner.NetState != null && MaterialProcessing.ToolValid(owner, tool) && ClassProfessions.Request(owner, SkillName.Cooking, tool, true))
        { MealCooking.Invalidate(owner); owner.SendGump(new MealGump(owner, tool)); }
    }
    private static string Ingredients(MealRecipe r)
    {
        var parts = new string[r.Ingredients.Length];
        for (int i = 0; i < parts.Length; i++) { parts[i] = $"{r.Ingredients[i].Amount} {r.Ingredients[i].Name}"; }
        return string.Join(", ", parts);
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 600, 440, 5054); b.AddLabel(22, 16, 0x481, "The travelling cook's recipe book");
        b.AddButton(550, 16, 4017, 4019, 0);
        if (_request is { } q)
        {
            b.AddLabel(22, 54, 0x481, $"One {q.Row.Name}");
            b.AddHtml(22, 85, 550, 55, $"<BASEFONT COLOR=#FFFFFF>{Meals.Description(q.Row)}</BASEFONT>", background: false, scrollbar: false);
            b.AddHtml(22, 145, 550, 60, $"<BASEFONT COLOR=#AACCFF>{Ingredients(q.Row)}, {q.Row.Chips} Wooden Chips</BASEFONT>", background: false, scrollbar: false);
            b.AddLabel(22, 215, 0x481, $"Cooking {q.Row.Skill:0}; success chance {q.Chance:P1}");
            b.AddLabel(22, 250, 0x47E, "One meal, one skillet use; crafted-tool saving applies.");
            b.AddLabel(22, 280, 0x47E, "Failure loses half of each ingredient/fuel, rounded up.");
            b.AddLabel(22, 310, 0x47E, "One hour; one food effect, independent of your potion.");
            b.AddButton(22, 370, 4005, 4007, 1); b.AddLabel(57, 370, 0x481, "Cook one meal");
        }
        else
        {
            for (int t = 1; t <= 3; t++)
            { int x = 22 + (t - 1) * 175; b.AddButton(x, 47, 4005, 4007, 100 + t); b.AddLabel(x + 35, 47, 0x481, $"Tier {t}"); }
            int visible = 0;
            for (int i = 0; i < Meals.Recipes.Length; i++)
            {
                var row = Meals.Recipes[i]; if (row.Tier != _tier) { continue; }
                int y = 83 + visible++ * 55;
                b.AddButton(22, y, 4005, 4007, i + 1); b.AddLabel(57, y, 0x481, $"{row.Name} — Cooking {row.Skill:0}");
                b.AddHtml(57, y + 20, 515, 34, $"<BASEFONT COLOR=#AACCFF>{Ingredients(row)}; {row.Chips} chips</BASEFONT>", background: false, scrollbar: false);
            }
            b.AddLabel(22, 416, 0x47E, "Free public Cooking station; a flour portion is 1/20 of a sack.");
        }
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || !ReferenceEquals(sender, _connection) || sender.Mobile != _owner) { return; } _used = true;
        if (_request != null)
        {
            if (info.ButtonID == 1) { MealCooking.Start(_request); }
            else { _request.Claimed = true; MealCooking.End(_request); }
            return;
        }
        if (info.ButtonID is >= 101 and <= 103)
        { MealCooking.Invalidate(_owner); _owner.SendGump(new MealGump(_owner, _tool, info.ButtonID - 100)); return; }
        if (info.ButtonID < 1 || info.ButtonID > Meals.Recipes.Length) { return; }
        var q = MealCooking.Quote(_owner, _tool, Meals.Recipes[info.ButtonID - 1]);
        if (q == null) { _owner.SendMessage("Check active Cooking, trained skill, ingredients, Wooden Chips, skillet and public station."); return; }
        _owner.SendGump(new MealGump(_owner, _tool, _tier, q));
    }
}
