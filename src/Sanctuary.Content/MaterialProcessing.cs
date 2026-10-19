// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class MaterialProcessing
{
    private static Mobile _gaining;
    private static SkillName _gainSkill;
    public static void Configure()
    {
        MaterialProcessHooks.Weight = Weight;
        MaterialProcessHooks.Normalize = Normalize;
        MaterialProcessHooks.Cut = Cut;
        MaterialProcessHooks.Smelt = Smelt;
        MaterialProcessHooks.Combine = Combine;
        MaterialProcessHooks.Spin = Spin;
        MaterialProcessHooks.Weave = Weave;
        MaterialProcessHooks.StockAllowed = type => !ClassProfessions.Enabled || StockAllowed(type);
        MaterialProcessHooks.ResaleAllowed = item => !ClassProfessions.Enabled || ResaleAllowed(item);
        MaterialProcessHooks.GainCap = (from, skill) => ReferenceEquals(from, _gaining) && skill == _gainSkill ? 300 : null;
    }
    public static double? Weight(Item item) => !ClassProfessions.Enabled ? null : item switch
    {
        BaseOre ore => ore.ItemID switch { 0x19B7 => 1, 0x19B9 => 4, _ => 2 },
        BaseIngot => 1, BaseHides => 2, BaseLeather => 1, Log => 2, Board => 1,
        Cloth or UncutCloth => .1, BoltOfCloth => 5, Wool => 4, BaseClothMaterial => 1,
        MetalScraps or WoodenChips or LeatherStrips => .1, ClothScraps => .01, _ => null
    };
    public static void Normalize(Item item)
    {
        if (Weight(item).HasValue) { item.Weight = -1; }
    }
    public static bool StockAllowed(Type type) => !typeof(BaseOre).IsAssignableFrom(type)
        && !typeof(BaseIngot).IsAssignableFrom(type) && !typeof(Log).IsAssignableFrom(type)
        && !typeof(Board).IsAssignableFrom(type) && !typeof(BaseHides).IsAssignableFrom(type)
        && !typeof(BaseLeather).IsAssignableFrom(type) && !typeof(Bandage).IsAssignableFrom(type)
        && type != typeof(MetalScraps) && type != typeof(WoodenChips) && type != typeof(LeatherStrips);
    public static bool ResaleAllowed(Item item)
    {
        if (!StockAllowed(item.GetType())) { return false; }
        if (item is CommodityDeed { Commodity: { } commodity } && !ResaleAllowed(commodity)) { return false; }
        if (item is Container container)
        {
            foreach (var child in container.Items) { if (!ResaleAllowed(child)) { return false; } }
        }
        return true;
    }
    public static bool Accessible(Mobile from, Item source) => ClassProfessions.Ready(from)
        && source is { Deleted: false, Movable: true, Amount: > 0 } && source.IsChildOf(from.Backpack);
    public static bool ToolValid(Mobile from, BaseTool tool) => ClassProfessions.Ready(from)
        && tool is { Deleted: false, UsesRemaining: > 0, Movable: true }
        && (tool.IsChildOf(from.Backpack) || tool.Parent == from);
    public static bool Supports(BaseTool tool) => tool is SmithHammer or TinkerTools or FletcherTools or Saw or SewingKit;
    private sealed record Snapshot(Item Item, object Parent, int Amount, int Hue, int Art, ItemOrigin Origin, CraftResource? Resource, double Weight)
    {
        public static Snapshot Capture(Item item) => new(item, item.Parent, item.Amount, item.Hue, item.ItemID, item.Origin, MaterialResource(item), item.Weight);
        private static CraftResource? MaterialResource(Item item) => item switch
        { BaseOre ore => ore.Resource, BaseIngot ingot => ingot.Resource, Log log => log.Resource,
            Board board => board.Resource, BaseHides hides => hides.Resource, BaseLeather leather => leather.Resource, _ => null };
        public bool Valid => !Item.Deleted && ReferenceEquals(Item.Parent, Parent) && Item.Amount == Amount
            && Item.Hue == Hue && Item.ItemID == Art && Item.Origin == Origin && MaterialResource(Item) == Resource && Item.Weight == Weight;
    }
    public sealed class Request
    {
        internal readonly Mobile Owner;
        internal readonly BaseTool Tool;
        internal readonly Item Source;
        internal readonly NetState Connection;
        private readonly Snapshot _source;
        private readonly object _toolParent;
        private readonly int _uses;
        internal readonly int Ratio;
        internal readonly Func<Item> Create;
        internal bool Claimed;
        internal Request(Mobile owner, BaseTool tool, Item source, int ratio, Func<Item> create)
        {
            Owner = owner; Tool = tool; Source = source; Ratio = ratio; Create = create;
            Connection = owner.NetState; _source = Snapshot.Capture(source); _toolParent = tool.Parent; _uses = tool.UsesRemaining;
        }
        internal bool Valid => !Claimed && ReferenceEquals(Connection, Owner.NetState) && _source.Valid
            && Accessible(Owner, Source) && ToolValid(Owner, Tool) && ReferenceEquals(Tool.Parent, _toolParent)
            && Tool.UsesRemaining == _uses;
        public int Available => _source.Amount;
        public int OutputPerUnit => Ratio;
        public string OutputName => Source switch
        { BaseOre or BaseIngot => "Metal Scraps", Log or Board => "Wooden Chips", BaseHides or BaseLeather => "Leather Strips", _ => "Cloth Scraps" };
    }
    public static Request Quote(Mobile owner, BaseTool tool, Item source)
    {
        if (!ClassProfessions.Enabled || !ToolValid(owner, tool) || !Accessible(owner, source)) { return null; }
        (int ratio, Func<Item> create) recipe = (tool, source) switch
        {
            (SmithHammer or TinkerTools, BaseOre ore) => (ore.ItemID switch { 0x19B7 => 5, 0x19B9 => 20, _ => 10 }, () => new MetalScraps()),
            (SmithHammer or TinkerTools, BaseIngot) => (10, () => new MetalScraps()),
            (FletcherTools or Saw, Log or Board) => (10, () => new WoodenChips()),
            (SewingKit, BaseHides or BaseLeather) => (10, () => new LeatherStrips()),
            (SewingKit, Cloth or UncutCloth) => (10, () => new ClothScraps()),
            (SewingKit, BoltOfCloth) => (500, () => new ClothScraps()),
            _ => (0, null)
        };
        return recipe.create == null ? null : new(owner, tool, source, recipe.ratio, recipe.create);
    }
    public static bool Complete(Request request, int quantity)
    {
        if (request == null || !request.Valid || quantity <= 0 || quantity > request.Available || quantity > int.MaxValue / request.Ratio) { return false; }
        request.Claimed = true;
        using var plan = Delivery.Create(request.Owner, request.Source, quantity, request.Create,
            checked(quantity * request.Ratio));
        if (plan == null) { request.Owner.SendMessage("Your backpack cannot hold that output. Nothing was consumed."); return false; }
        plan.Commit();
        Gain(request.Owner, request.Tool.CraftSystem.MainSkill);
        request.Owner.SendMessage("You process the materials into generic scraps. No tool uses were spent.");
        return true;
    }
    private static void Gain(Mobile from, SkillName skill)
    {
        var state = ClassProfessions.Profiles.Find(from);
        var trained = from.Skills[skill];
        if (!ClassProfessions.Affected(from) || !state.Confirmed || state.Craft != skill || trained.Lock != SkillLock.Up
            || trained.BaseFixedPoint >= Math.Min(300, trained.CapFixedPoint)) { return; }
        _gaining = from; _gainSkill = skill;
        try { from.CheckSkill(skill, 0, 30); }
        finally { _gaining = null; }
    }
    // Capacity is computed from integer pile weights after consumption and merging, including slot reuse.
    internal sealed class Delivery : IDisposable
    {
        private readonly Mobile _owner;
        private readonly Item _source;
        private readonly int _consume;
        private readonly List<(Item Target, Item Output)> _merges = new();
        private readonly List<Item> _stacks = new();
        private bool _committed;
        private Delivery(Mobile owner, Item source, int consume) { _owner = owner; _source = source; _consume = consume; }
        public static Delivery Create(Mobile owner, Item source, int consume, Func<Item> factory, int outputAmount)
        {
            if (!Accessible(owner, source) || consume <= 0 || consume > source.Amount || outputAmount <= 0) { return null; }
            var plan = new Delivery(owner, source, consume);
            int remaining = outputAmount;
            int weight = (int)Math.Ceiling(source.Weight * (source.Amount - consume)) - source.PileWeight;
            foreach (var target in owner.Backpack.Items)
            {
                if (remaining == 0) { break; }
                if (target == source || target.Amount >= 60000 || !target.Stackable) { continue; }
                var output = ItemOrigins.Create(ItemOrigin.Crafted, factory);
                output.Amount = Math.Min(remaining, 60000 - target.Amount);
                if (!target.CanStackWith(output)) { output.Delete(); continue; }
                weight += (int)Math.Ceiling(target.Weight * (target.Amount + output.Amount)) - target.PileWeight;
                remaining -= output.Amount;
                plan._merges.Add((target, output));
            }
            int newStacks = remaining / 60000 + (remaining % 60000 == 0 ? 0 : 1);
            int slots = newStacks - (consume == source.Amount ? 1 : 0);
            if (slots > 0 && owner.Backpack.MaxItems != 0 && owner.Backpack.TotalItems + slots > owner.Backpack.MaxItems)
            { plan.Dispose(); return null; }
            while (remaining > 0)
            {
                var output = ItemOrigins.Create(ItemOrigin.Crafted, factory);
                output.Amount = Math.Min(60000, remaining);
                remaining -= output.Amount; weight += output.PileWeight; plan._stacks.Add(output);
            }
            if (weight > 0 && (owner.Backpack.MaxWeight != 0 && owner.Backpack.TotalWeight + weight > owner.Backpack.MaxWeight
                || owner.TotalWeight + weight > owner.MaxWeight)) { plan.Dispose(); return null; }
            return plan;
        }
        public void Commit()
        {
            if (_committed) { throw new InvalidOperationException("Processing delivery already committed."); }
            _committed = true;
            _source.Consume(_consume);
            foreach (var (target, output) in _merges)
            {
                if (!target.StackWith(_owner, output)) { throw new InvalidOperationException("Processing merge changed during commit."); }
            }
            foreach (var output in _stacks) { _owner.Backpack.DropItem(output); }
        }
        public void Dispose()
        {
            if (_committed) { return; }
            foreach (var (_, output) in _merges) { output.Delete(); }
            foreach (var output in _stacks) { output.Delete(); }
        }
    }
    public static bool? Cut(Mobile from, Item source, Item output, int ratio, Item tool)
    {
        if (!ClassProfessions.Enabled) { return null; }
        try
        {
            if (output is Bandage) { from.SendMessage("Cloth can be processed into scraps with a tailoring kit."); return false; }
            if (!Accessible(from, source) || tool != null && (tool.Deleted || !tool.Movable
                || !(tool.IsChildOf(from.Backpack) || tool.Parent == from))) { return false; }
            if (source is not Log) { output.Hue = source.Hue; }
            int amount = Math.Min(source.Amount, 60000 / ratio);
            // Native handlers choose the subtype before entering this callback.
            using var plan = Delivery.Create(from, source, amount, () =>
            {
                var copy = output.GetType().CreateInstance<Item>(); copy.Hue = output.Hue;
                if (Item.ScissorCopyLootType) { copy.LootType = source.LootType; }
                return copy;
            }, amount * ratio);
            if (plan == null) { from.SendMessage("Your backpack cannot hold the output. Nothing was consumed."); return false; }
            plan.Commit(); return true;
        }
        finally { output.Delete(); }
    }
    public static bool Smelt(Mobile from, BaseOre source, object forge, double min, double max)
    {
        if (!ClassProfessions.Enabled) { return false; }
        if (!Accessible(from, source)) { return true; }
        int consume = Math.Min(source.Amount, 30000);
        if (source.ItemID == 0x19B7) { consume -= consume % 2; }
        int amount = source.ItemID switch { 0x19B7 => consume / 2, 0x19B9 => consume * 2, _ => consume };
        using var plan = Delivery.Create(from, source, consume, source.GetIngot, amount);
        if (plan == null) { from.SendMessage("Your backpack cannot hold the ingots. Nothing was consumed."); return true; }
        if (from.CheckTargetSkill(SkillName.Mining, forge, min, max)) { plan.Commit(); from.SendLocalizedMessage(501988); }
        else
        {
            if (source.Amount < 2) { source.ItemID = source.ItemID == 0x19B9 ? 0x19B8 : 0x19B7; }
            else { source.Amount /= 2; }
            from.SendLocalizedMessage(501990);
        }
        return true;
    }
    public static bool Combine(Mobile from, BaseOre source, BaseOre target)
    {
        if (!ClassProfessions.Enabled) { return false; }
        if (!Accessible(from, source) || !Accessible(from, target) || source == target || source.Resource != target.Resource
            || source.Origin != target.Origin || source.Hue != target.Hue) { return true; }
        int unit = Math.Min(OreUnits(source), OreUnits(target));
        int worth = source.Amount * OreUnits(source) + target.Amount * OreUnits(target);
        if (worth / unit > 60000) { from.SendMessage("There is too much ore to combine."); return true; }
        for (var parent = target.Parent as Container; parent != null && parent != from.Backpack; parent = parent.Parent as Container)
        {
            if (!source.IsChildOf(parent) && parent.MaxWeight != 0 && parent.TotalWeight + source.PileWeight > parent.MaxWeight)
            { from.SendMessage("The container cannot hold the combined ore."); return true; }
        }
        target.ItemID = unit switch { 1 => 0x19B7, 4 => 0x19B9, _ => 0x19B8 };
        target.Amount = worth / unit; source.Delete(); return true;
    }
    private static int OreUnits(BaseOre ore) => ore.ItemID switch { 0x19B7 => 1, 0x19B9 => 4, _ => 2 };
    private static bool Station(Mobile from, Item station) => station is { Deleted: false } && station.Map == from.Map
        && from.InRange(station.Location, 3) && from.InLOS(station);
    public static bool Spin(Mobile from, Item source, ISpinningWheel wheel)
    {
        if (!ClassProfessions.Enabled) { return false; }
        if (wheel is not Item station || wheel.Spinning || !Station(from, station) || !Accessible(from, source)) { return true; }
        var snapshot = Snapshot.Capture(source); var connection = from.NetState;
        int amount = source switch { TaintedWool => 1, Wool => 3, Cotton or Flax => 6, _ => 0 };
        if (amount == 0) { return true; }
        int hue = source.Hue;
        Func<Item> create = source is Wool ? () => new DarkYarn { Hue = hue } : () => new SpoolOfThread { Hue = hue };
        using (var check = Delivery.Create(from, source, 1, create, amount))
        { if (check == null) { from.SendMessage("Your backpack cannot hold the spun material."); return true; } }
        wheel.BeginSpin((_, owner, _) =>
        {
            if (!ReferenceEquals(connection, owner.NetState) || !snapshot.Valid || !Station(owner, station)) { return; }
            using var plan = Delivery.Create(owner, source, 1, create, amount);
            if (plan == null) { owner.SendMessage("Your backpack cannot hold the spun material. Nothing was consumed."); return; }
            plan.Commit(); owner.SendLocalizedMessage(source is not Wool ? 1010577 : amount == 1 ? 1010574 : 1010576);
        }, from, source.Hue);
        return true;
    }
    public static bool Weave(Mobile from, BaseClothMaterial source, ILoom loom, Item station)
    {
        if (!ClassProfessions.Enabled) { return false; }
        if (!Accessible(from, source) || !Station(from, station)) { return true; }
        if (loom.Phase < 4) { source.Consume(); station.SendLocalizedMessageTo(from, 1010001 + loom.Phase++); return true; }
        using var plan = Delivery.Create(from, source, 1, () => new BoltOfCloth { Hue = source.Hue }, 1);
        if (plan == null) { from.SendMessage("Your backpack cannot hold the bolt. Nothing was consumed."); return true; }
        plan.Commit(); loom.Phase = 0; from.SendLocalizedMessage(500368); return true;
    }
}
