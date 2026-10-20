// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Sanctuary;

public sealed class EssenceGump : DynamicGump
{
    private readonly Serial _owner;
    private readonly EssenceQuote _quote;
    private readonly NetState _connection;
    private readonly int _rareChance;
    private bool _used;
    public override bool Singleton => true;
    private EssenceGump(PlayerMobile player, EssenceQuote quote) : base(100, 80)
    {
        _owner = player.Serial;
        _quote = quote; _connection = player.NetState; _rareChance = TieredImbuing.RareChance(player);
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
            builder.AddLabel(22, 221, 0x481, "Salvage: matching tool; no skill, tool use or gold cost.");
            builder.AddLabel(22, 246, 0x481, "Imbuing: tier/grade skill and costs; ladle + town cauldron.");
            builder.AddLabel(22, 271, 0x481, $"Imbue result: {100 - _rareChance}% magic / {_rareChance}% rare.");
            builder.AddButton(22, 324, 4005, 4007, 1);
            builder.AddLabel(57, 324, 0x481, "Salvage with tool");
            builder.AddButton(255, 324, 4005, 4007, 2);
            builder.AddLabel(290, 324, 0x481, "Imbue white base");
            return;
        }
        var item = World.FindItem(_quote.Item);
        builder.AddLabel(22, 48, 0x481, $"{item?.Name ?? (item == null ? null : ClassGear.Base(item)?.Name) ?? "Equipment"}");
        bool hidden = _quote.State is { Identified: false };
        if (hidden) { builder.AddLabel(22, 72, 0x47E, "Unidentified — properties hidden"); }
        else { builder.AddLabel(22, 72, 0x47E, $"{_quote.Rarity}, item level {_quote.Level}"); }
        if (item != null)
        {
            builder.AddItem(430, 55, item.ItemID, item.Hue);
            builder.AddItemProperty(item.Serial);
        }
        if (_quote.Action == EssenceAction.Salvage)
        {
            builder.AddLabel(22, 95, 0x21, "This permanently destroys the selected item.");
            var essence = EquipmentEconomy.EssenceRange(_quote.Rarity);
            if (hidden) { builder.AddLabel(22, 132, 0x481, "Hidden properties determine recovery after confirmation."); }
            if (!hidden && essence != null)
            {
                builder.AddLabel(22, 132, 0x481, $"Essence: {essence.Min}-{essence.Max}, {essence.ChancePercent}% chance.");
            }
            var material = EquipmentEconomy.MaterialRange(_quote.Material);
            if (!hidden && material != null)
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
            var itemCost = EquipmentEconomy.Ready(World.FindMobile(_owner) as PlayerMobile) && item != null ? TieredImbuing.Cost(item) : null;
            if (itemCost != null) { builder.AddLabel(22, 205, 0x481, $"Imbuing {itemCost.Skill}; {_rareChance}% Rare, otherwise Magic."); }
            builder.AddLabel(22, 230, 0x47E, "One ladle use; town cauldron. No destructive failure.");
        }
        if (_quote.Action == EssenceAction.Salvage)
        {
            builder.AddLabel(22, 255, 0x21, "Filled contents are lost and add no recovery yield.");
            builder.AddLabel(22, 280, 0x481, "No skill, tool use or gold cost.");
        }
        else { builder.AddLabel(22, 267, 0x481, $"Fee: {_quote.Gold} gold from your backpack."); }
        builder.AddButton(22, 324, 4005, 4007, 3);
        builder.AddLabel(57, 324, 0x481, "Confirm");
        builder.AddButton(255, 324, 4017, 4019, 0);
        builder.AddLabel(290, 324, 0x481, "Cancel");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_used || !ReferenceEquals(sender, _connection) || sender.Mobile is not PlayerMobile player
            || player.Serial != _owner || !EquipmentEconomy.Ready(player))
        {
            return;
        }
        _used = true;
        if (_quote != null)
        {
            if (info.ButtonID != 3)
            {
                _quote.Claim();
                EquipmentSalvage.End(player, _quote.Session);
            }
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
            player.Target = new EssenceTarget(info.ButtonID == 1 ? EssenceAction.Salvage : EssenceAction.Imbue, null, player.NetState);
        }
    }
    public static void TargetSalvage(PlayerMobile player, BaseTool tool)
    {
        if (player.NetState == null || !EquipmentSalvage.ToolValid(player, tool) || player.Target != null) { return; }
        player.SendMessage("Select eligible backpack equipment for this tool. Salvage costs no skill, uses or gold; confirmation follows.");
        player.Target = new EssenceTarget(EssenceAction.Salvage, tool, player.NetState);
    }
    private sealed class EssenceTarget(EssenceAction action, BaseTool tool, NetState connection) : Target(1, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object selected)
        {
            if (from is not PlayerMobile player || !ReferenceEquals(player.NetState, connection))
            {
                return;
            }
            if (action == EssenceAction.Imbue && !ClassProfessions.Request(player, SkillName.Imbuing, selected as Item, true))
            { return; }
            var quote = EquipmentEconomy.Quote(player, selected as Item, action, tool);
            if (quote == null)
            {
                if (action == EssenceAction.Salvage && selected is Item item)
                { player.SendMessage($"Salvage needs an accessible {EquipmentSalvage.ToolName(item)} and unprotected dropped, crafted or gambled backpack gear."); }
                else { player.SendMessage("Use unprotected dropped or crafted white equipment in your backpack."); }
                return;
            }
            DisplayTo(player, quote);
        }
    }
}
