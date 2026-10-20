// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class EquipmentSalvage
{
    private static readonly Dictionary<Serial, long> _sessions = new();
    private static long _nextSession;
    public static void Configure()
    {
        EventSink.Disconnected += from => _sessions.Remove(from.Serial);
        EventSink.WorldSave += () =>
        {
            var deleted = new List<Serial>();
            foreach (var serial in _sessions.Keys)
            { if (World.FindMobile(serial) is not { Deleted: false }) { deleted.Add(serial); } }
            foreach (var serial in deleted) { _sessions.Remove(serial); }
        };
    }
    internal static long Begin(Mobile player) => _sessions[player.Serial] = ++_nextSession;
    internal static bool Current(Mobile player, long token) => token != 0 && _sessions.GetValueOrDefault(player.Serial) == token;
    internal static void End(Mobile player, long token)
    { if (Current(player, token)) { _sessions.Remove(player.Serial); } }
    public static bool Supports(BaseTool tool) => tool is SmithHammer or FletcherTools or SewingKit or TinkerTools or ScribesPen;
    public static SkillName? Craft(Item item)
    {
        if (!ClassGear.Converted(item)) { return null; }
        if (item is Spellbook) { return SkillName.Inscribe; }
        if (item is BaseJewel) { return SkillName.Tinkering; }
        if (item is BaseClothing or BaseArmor { MaterialType: ArmorMaterialType.Bone }
            || EquipmentCrafting.PhysicalMaterial(item.GetType()) == GearMaterial.Bone) { return SkillName.Tailoring; }
        return EquipmentEconomy.Material(item) switch
        {
            SalvageMaterial.Metal => SkillName.Blacksmith,
            SalvageMaterial.Wood => SkillName.Fletching,
            SalvageMaterial.Leather or SalvageMaterial.Cloth => SkillName.Tailoring,
            // Exotic bases retain essence-only recovery until their physical recipes exist.
            _ => item is BaseStaff or BaseRanged or BaseWand ? SkillName.Fletching : SkillName.Blacksmith
        };
    }
    public static string ToolName(Item item) => Craft(item) switch
    {
        SkillName.Blacksmith => "smithing hammer", SkillName.Fletching => "fletching tools",
        SkillName.Tailoring => "tailoring kit", SkillName.Tinkering => "tinker's tools",
        SkillName.Inscribe => "scribe's pen", _ => "matching crafting tool"
    };
    public static bool ToolValid(PlayerMobile player, BaseTool tool) => EquipmentEconomy.Ready(player)
        && Supports(tool) && tool is { Deleted: false, Movable: true, UsesRemaining: > 0 }
        && (tool.Parent == player || tool.IsChildOf(player.Backpack)) && tool.IsAccessibleTo(player) && tool.VerifyMove(player);
    public static bool Matches(PlayerMobile player, Item item, BaseTool tool) => ToolValid(player, tool)
        && Craft(item) == tool.CraftSystem.MainSkill;
    public static BaseTool FindTool(PlayerMobile player, Item item)
    {
        if (!EquipmentEconomy.Ready(player)) { return null; }
        foreach (var tool in player.Backpack.FindItemsByType<BaseTool>(true))
        { if (Matches(player, item, tool)) { return tool; } }
        foreach (var equipped in player.Items)
        { if (equipped is BaseTool tool && Matches(player, item, tool)) { return tool; } }
        return null;
    }
    internal sealed class Snapshot
    {
        private readonly Item _item;
        private readonly object _parent;
        private readonly int _amount, _art, _hue, _charges;
        private readonly double _weight;
        private readonly ulong _spells;
        private readonly ItemOrigin _origin;
        private readonly NativeGearDetails _native;
        private readonly GearState _state;
        private readonly Snapshot[] _contents;
        public Snapshot(Item item)
        {
            _item = item; _parent = item.Parent; _amount = item.Amount; _art = item.ItemID; _hue = item.Hue;
            _charges = item is BaseWand wand ? wand.Charges : 0; _weight = item.Weight; _origin = item.Origin;
            _spells = item is Spellbook book ? book.Content : 0;
            _native = GearDetails.Read(item); _state = ClassGear.State(item);
            _contents = new Snapshot[item.Items?.Count ?? 0];
            for (int i = 0; i < _contents.Length; i++) { _contents[i] = new Snapshot(item.Items[i]); }
        }
        public bool Valid
        {
            get
            {
                if (_item.Deleted || !ReferenceEquals(_item.Parent, _parent) || _item.Amount != _amount || _item.ItemID != _art
                    || !ReferenceEquals(ClassGear.State(_item), _state) || _item.Hue != _hue || _item.Weight != _weight || _item.Origin != _origin || GearDetails.Read(_item) != _native
                    || (_item is Spellbook book ? book.Content : 0) != _spells
                    || (_item is BaseWand wand ? wand.Charges : 0) != _charges || (_item.Items?.Count ?? 0) != _contents.Length)
                { return false; }
                foreach (var child in _contents) { if (!child.Valid) { return false; } }
                return true;
            }
        }
    }
}
