// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Sanctuary;

public sealed class EssenceGump : DynamicGump
{
    private readonly Serial _owner;
    private readonly EssenceQuote _quote;
    public override bool Singleton => true;
    private EssenceGump(PlayerMobile player, EssenceQuote quote) : base(100, 80)
    {
        _owner = player.Serial;
        _quote = quote;
    }
    public static void Initialize()
    {
        SkillInfo.Table[(int)SkillName.Imbuing].Callback = OnUse;
        CommandSystem.Register("Essence", AccessLevel.Player, e => OnUse(e.Mobile));
    }
    private static TimeSpan OnUse(Mobile from)
    {
        if (from is PlayerMobile player)
        {
            DisplayTo(player);
        }
        return TimeSpan.FromSeconds(1);
    }
    public static void DisplayTo(PlayerMobile player, EssenceQuote quote = null)
    {
        if (player?.NetState == null || !EquipmentEconomy.Ready(player))
        {
            return;
        }
        player.SendGump(new EssenceGump(player, quote));
    }
    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 490, 380, 5054);
        builder.AddLabel(22, 16, 0x481, "Imbuing and Salvage");
        builder.AddButton(445, 16, 4017, 4019, 0);
        var row = EquipmentEconomy.Settings;
        if (_quote == null)
        {
            builder.AddLabel(22, 55, 0x47E, "Salvage gear for essence and its primary material.");
            builder.AddLabel(22, 80, 0x47E, "Dropped, crafted and gambled gear only.");
            builder.AddLabel(22, 113, 0x481, $"Magic: {row.MagicEssence.Min}-{row.MagicEssence.Max} essence, {row.MagicEssence.ChancePercent}% chance.");
            builder.AddLabel(22, 138, 0x481, $"Rare: {row.RareEssence.Min}-{row.RareEssence.Max} essence, {row.RareEssence.ChancePercent}% chance.");
            builder.AddLabel(22, 163, 0x47E, "Scraps, chips and strips scale with item level.");
            builder.AddLabel(22, 188, 0x47E, "White gear yields no essence. Any roll may yield nothing.");
            builder.AddLabel(22, 221, 0x481, $"Salvage fee: {row.SalvageGoldCost} backpack gold.");
            builder.AddLabel(22, 246, 0x481, $"Imbue: {row.ImbueEssenceCost} essence + {row.ImbueGoldCost} backpack gold.");
            builder.AddLabel(22, 271, 0x481, $"Imbue result: {100 - row.RareChancePercent}% magic / {row.RareChancePercent}% rare.");
            builder.AddButton(22, 324, 4005, 4007, 1);
            builder.AddLabel(57, 324, 0x481, "Salvage gear");
            builder.AddButton(255, 324, 4005, 4007, 2);
            builder.AddLabel(290, 324, 0x481, "Imbue white base");
            return;
        }
        var item = World.FindItem(_quote.Item);
        builder.AddLabel(22, 48, 0x481, $"{item?.Name ?? (item == null ? null : ClassGear.Base(item)?.Name) ?? "Equipment"}");
        builder.AddLabel(22, 72, 0x47E, $"{_quote.Rarity}, item level {_quote.Level}");
        if (item != null)
        {
            builder.AddItem(430, 55, item.ItemID, item.Hue);
            builder.AddItemProperty(item.Serial);
        }
        if (_quote.Action == EssenceAction.Salvage)
        {
            builder.AddLabel(22, 95, 0x21, "This permanently destroys the selected item.");
            var essence = EquipmentEconomy.EssenceRange(_quote.Rarity);
            if (essence != null)
            {
                builder.AddLabel(22, 132, 0x481, $"Essence: {essence.Min}-{essence.Max}, {essence.ChancePercent}% chance.");
            }
            var material = EquipmentEconomy.MaterialRange(_quote.Material);
            if (material != null)
            {
                var (min, max) = material.Bounds(_quote.Level);
                builder.AddLabel(22, 162, 0x481, $"{EquipmentEconomy.MaterialName(_quote.Material)}: {min}-{max}, {material.ChancePercent}% chance.");
            }
            builder.AddLabel(22, 202, 0x47E, "Each output rolls independently after confirmation.");
            builder.AddLabel(22, 227, 0x21, "The item is lost even if both rolls return nothing.");
        }
        else
        {
            builder.AddLabel(22, 95, 0x47E, "The same white base gains random magic or rare affixes.");
            builder.AddLabel(22, 132, 0x481, $"Cost: {_quote.Essence} Magic Essence.");
            builder.AddLabel(22, 172, 0x47E, "Base, material, item level and durability are retained.");
        }
        builder.AddLabel(22, 267, 0x481, $"Fee: {_quote.Gold} gold from your backpack.");
        builder.AddButton(22, 324, 4005, 4007, 3);
        builder.AddLabel(57, 324, 0x481, "Confirm");
        builder.AddButton(255, 324, 4017, 4019, 0);
        builder.AddLabel(290, 324, 0x481, "Cancel");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile player || player.Serial != _owner || !EquipmentEconomy.Ready(player))
        {
            return;
        }
        if (_quote != null)
        {
            if (info.ButtonID == 3)
            {
                if (EquipmentEconomy.Commit(player, _quote, out var result))
                {
                    if (result != null)
                    {
                        if (_quote.Material == SalvageMaterial.None)
                        {
                            player.SendMessage($"Salvaged: {result.Essence} Magic Essence.");
                        }
                        else
                        {
                            player.SendMessage($"Salvaged: {result.Essence} Magic Essence, {result.Material} {EquipmentEconomy.MaterialName(_quote.Material)}.");
                        }
                    }
                    else
                    {
                        player.SendMessage("Your item has been imbued.");
                    }
                }
                else
                {
                    player.SendMessage("Nothing was consumed. Check the item, materials, gold and free pack space, then try again.");
                }
            }
            return;
        }
        if (info.ButtonID is 1 or 2)
        {
            if (player.Target != null)
            {
                player.SendMessage("Finish your current target first.");
                return;
            }
            player.SendMessage("Select one eligible item in your backpack. A confirmation follows.");
            player.Target = new EssenceTarget(info.ButtonID == 1 ? EssenceAction.Salvage : EssenceAction.Imbue);
        }
    }
    private sealed class EssenceTarget(EssenceAction action) : Target(1, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object selected)
        {
            if (from is not PlayerMobile player)
            {
                return;
            }
            var quote = EquipmentEconomy.Quote(player, selected as Item, action);
            if (quote == null)
            {
                player.SendMessage("Use unprotected dropped or crafted gear in your backpack. Salvage material-bearing or magic/rare gear; imbue white bases.");
                return;
            }
            DisplayTo(player, quote);
        }
    }
}
