// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed class AlchemyGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    private readonly MortarPestle _tool;
    private readonly NetState _connection;
    private readonly AlchemyRequest _request;
    private readonly bool _combat;
    private bool _used;
    public override bool Singleton => true;
    private AlchemyGump(PlayerMobile owner, MortarPestle tool, AlchemyRequest request = null, bool combat = false) : base(60, 45)
    { _owner = owner; _tool = tool; _request = request; _combat = combat; _connection = owner.NetState; }
    public static void DisplayTo(PlayerMobile owner, MortarPestle tool)
    {
        if (owner.NetState != null && MaterialProcessing.ToolValid(owner, tool)
            && ClassProfessions.Request(owner, SkillName.Alchemy, tool, true))
        { AlchemyRestoratives.Invalidate(owner); owner.SendGump(new AlchemyGump(owner, tool)); }
    }
    private static string Ingredients(AlchemyRecipe row)
    {
        if (row.Reagents is { Length: > 0 })
        {
            var parts = new string[row.Reagents.Length];
            for (int i = 0; i < parts.Length; i++) { parts[i] = $"{row.Reagents[i].Amount} {row.Reagents[i].Name}"; }
            return string.Join(" + ", parts);
        }
        string life = row.Healing == 0 ? "" : $"{row.Healing} {PotionBeltCatalog.Potion(RestorationKind.Healing, row.HealingTier).Name}";
        string mana = row.Mana == 0 ? "" : $"{row.Mana} {PotionBeltCatalog.Potion(RestorationKind.Mana, row.ManaTier).Name}";
        return row.Healing > 0 && row.Mana > 0 ? $"{life} + {mana}" : life + mana;
    }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 590, 405, 5054);
        b.AddLabel(22, 18, 0x481, "Alchemy potions");
        if (_request == null)
        {
            b.AddButton(230, 18, 4005, 4007, 20); b.AddLabel(265, 18, 0x481, "Restoratives");
            b.AddButton(385, 18, 4005, 4007, 21); b.AddLabel(420, 18, 0x481, "Combat buffs");
        }
        b.AddButton(535, 18, 4017, 4019, 0);
        if (_request is { } q)
        {
            b.AddLabel(22, 65, 0x481, $"One {PotionBeltCatalog.Potion(q.Row.Kind, q.Row.Tier).Name}");
            b.AddLabel(22, 100, 0x47E, $"{Ingredients(q.Row)}");
            b.AddLabel(22, 130, 0x47E, "Plus one empty bottle; one mortar use with crafted saving.");
            b.AddLabel(22, 170, 0x481, $"Alchemy {q.Row.Skill:0.0}; success chance {q.Chance:P1}");
            b.AddLabel(22, 210, 0x47E, "Failure: half of each ingredient, rounded up; one tool use.");
            b.AddLabel(22, 245, 0x47E, q.Row.Kind == RestorationKind.CombatBuff
                ? "One combat buff; repeated doses extend it to 90 seconds."
                : "Potion ingredients are consumed without returning bottles.");
            b.AddButton(22, 320, 4005, 4007, 1); b.AddLabel(57, 320, 0x481, "Brew one potion");
        }
        else
        {
            b.AddButton(22, 365, 4005, 4007, 22); b.AddLabel(57, 365, 0x481, "Powder of Fortifying");
            b.AddLabel(22, 50, 0x47E, "Public Alchemy station; one output, bottle and mortar use.");
            int visible = 0;
            for (int i = 0; i < AlchemyRestoratives.Recipes.Length; i++)
            {
                var row = AlchemyRestoratives.Recipes[i];
                if ((row.Kind == RestorationKind.CombatBuff) != _combat) { continue; }
                int y = _combat ? 80 + visible++ * 55 : 90 + visible++ * 70;
                b.AddButton(22, y, 4005, 4007, i + 1);
                b.AddLabel(57, y, 0x481, $"{PotionBeltCatalog.Potion(row.Kind, row.Tier).Name} — Alchemy {row.Skill:0}");
                b.AddLabel(57, y + 25, 0x47E, $"{Ingredients(row)} + bottle");
            }
        }
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || !ReferenceEquals(sender, _connection) || sender.Mobile != _owner) { return; } _used = true;
        if (_request != null)
        {
            if (info.ButtonID == 1) { AlchemyRestoratives.Start(_request); }
            else { _request.Claimed = true; AlchemyRestoratives.End(_request); }
            return;
        }
        if (info.ButtonID == 22) { AlchemyRestoratives.Invalidate(_owner); FortificationCraftGump.DisplayTo(_owner, _tool); return; }
        if (info.ButtonID is 20 or 21)
        { AlchemyRestoratives.Invalidate(_owner); _owner.SendGump(new AlchemyGump(_owner, _tool, combat: info.ButtonID == 21)); return; }
        if (info.ButtonID < 1 || info.ButtonID > AlchemyRestoratives.Recipes.Length) { return; }
        var q = AlchemyRestoratives.Quote(_owner, _tool, AlchemyRestoratives.Recipes[info.ButtonID - 1]);
        if (q == null) { _owner.SendMessage("Check your active Alchemy, trained skill, ingredients, mortar, public station and current action."); return; }
        _owner.SendGump(new AlchemyGump(_owner, _tool, q));
    }
}
