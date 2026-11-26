// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server;
using Server.Engines.Craft;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Sanctuary;

internal sealed record TreasureKeyRecipe(int Tier, string Name, string MaterialName, Type Material, double Skill,
    bool WorldLoot, string ChestName = null, bool RequiresRareRecipe = false);

public sealed class TreasureKeyRequest
{
    internal PlayerMobile Owner;
    internal TinkerTools Tool;
    internal TreasureKeyRecipe Row;
    internal CraftItem Recipe;
    internal AlchemyInput Input;
    internal object ToolParent;
    internal int Uses, Skill;
    internal ItemOrigin Origin;
    internal CraftToolState ToolState;
    internal NetState Connection;
    internal ProfessionState Profession;
    internal double Chance;
    internal bool Started, Claimed;
}
public static class TreasureKeys
{
    private static readonly Dictionary<CraftItem, TreasureKeyRequest> _starting = new();
    internal static bool Ready(PlayerMobile owner, TinkerTools tool) => MaterialProcessing.ToolValid(owner, tool)
        && ClassProfessions.Active(owner, SkillName.Tinkering) && CraftFacilities.Allows(owner, SkillName.Tinkering);
    private static readonly TreasureKeyRecipe[] _worldKeys =
    [
        new(1, "Iron key", "Iron ingot", typeof(IronIngot), 59, true),
        new(2, "Shadow Iron key", "Shadow Iron ingot", typeof(ShadowIronIngot), 69, true),
        new(3, "Copper key", "Copper ingot", typeof(CopperIngot), 79, true),
        new(4, "Bronze key", "Bronze ingot", typeof(BronzeIngot), 89, true),
        new(5, "Golden key", "Gold ingot", typeof(GoldIngot), 99, true)
    ];

    internal static bool IsOutput(Type type) => type == typeof(TreasureSkeletonKey) || type == typeof(WorldChestKey);

    internal static TreasureKeyRecipe Recipe(int tier, bool worldLoot)
    {
        if (worldLoot) { return _worldKeys[tier - 1]; }
        var row = TreasureHunts.Tier(tier);
        return new(tier, row.KeyName + " skeleton key", row.KeyMaterial, EquipmentCrafting.Resolve(row.KeyMaterial),
            row.KeySkill, false, row.Name, tier == 5);
    }

    internal static TreasureKeyRequest Quote(PlayerMobile owner, TinkerTools tool, int tier, bool worldLoot = false)
    {
        if (tier is < 1 or > 5) { return null; }
        var row = Recipe(tier, worldLoot);
        if (!Ready(owner, tool) || !owner.CanBeginAction<CraftSystem>() || owner.Skills.Tinkering.Base < row.Skill
            || row.RequiresRareRecipe && !RareRecipes.Allowed(owner, "key/splendid")) { return null; }
        Item input = null;
        foreach (var item in owner.Backpack.FindItemsByType<Item>(true))
        { if (item.GetType() == row.Material && RuneCrafting.Accessible(owner, item)) { input = item; break; } }
        if (input == null) { return null; }
        var recipe = new CraftItem(worldLoot ? typeof(WorldChestKey) : typeof(TreasureSkeletonKey),
            worldLoot ? "Dungeon keys" : "Skeleton keys", row.Name);
        recipe.AddSkill(SkillName.Tinkering, row.Skill, row.Skill + 50);
        double chance = row.RequiresRareRecipe ? 1 : recipe.GetSuccessChance(owner, null, DefTinkering.CraftSystem, false, out _);
        if (chance <= 0) { return null; }
        return new() { Owner = owner, Tool = tool, Row = row, Recipe = recipe, Input = new(input, 1, 0),
            ToolParent = tool.Parent, Uses = tool.UsesRemaining, Skill = owner.Skills.Tinkering.BaseFixedPoint,
            Origin = tool.Origin, ToolState = CraftTransactions.Profiles.Find(tool), Connection = owner.NetState,
            Profession = ClassProfessions.Profiles.Find(owner), Chance = Math.Clamp(chance, 0, 1) };
    }
    private static bool Valid(TreasureKeyRequest q) => q is { Claimed: false } && Ready(q.Owner, q.Tool)
        && q.Input.Valid(q.Owner) && q.Input.Item.GetType() == q.Row.Material
        && q.Owner.Skills.Tinkering.BaseFixedPoint == q.Skill && q.Owner.NetState == q.Connection
        && ReferenceEquals(q.Profession, ClassProfessions.Profiles.Find(q.Owner)) && q.Tool.Parent == q.ToolParent
        && q.Tool.UsesRemaining == q.Uses && q.Tool.Origin == q.Origin && q.ToolState == CraftTransactions.Profiles.Find(q.Tool)
        && (!q.Row.RequiresRareRecipe || RareRecipes.Allowed(q.Owner, "key/splendid"));
    internal static bool Start(TreasureKeyRequest q)
    {
        if (q == null || q.Started || !Valid(q) || !q.Owner.CanBeginAction<CraftSystem>()) { return false; }
        q.Started = true; _starting.Add(q.Recipe, q);
        try { q.Recipe.Craft(q.Owner, DefTinkering.CraftSystem, null, q.Tool, 0); }
        finally { _starting.Remove(q.Recipe); }
        return true;
    }
    internal static TreasureKeyRequest Attach(CraftTransactions.Request t) => _starting.GetValueOrDefault(t.Recipe) is { } q
        && q.Owner == t.Owner && q.Tool == t.Tool && q.Started ? q : null;
    internal static bool Complete(CraftTransactions.Request ticket, Random random)
    {
        if (ticket.Claimed) { return false; } ticket.Claimed = true; var q = ticket.TreasureKey;
        if (!Valid(q) || !CraftTransactions.Valid(ticket)) { if (q != null) { q.Claimed = true; } return false; }
        q.Claimed = true;
        var output = ItemOrigins.Create<Item>(ItemOrigin.Crafted, () => q.Row.WorldLoot
            ? new WorldChestKey(q.Row.Tier) { PlayerConstructed = true }
            : new TreasureSkeletonKey(q.Row.Tier) { PlayerConstructed = true });
        var delivery = MarketDelivery.Create(q.Owner, [(q.Input.Item, 1)], [output]);
        if (delivery == null) { output.Delete(); return false; }
        random ??= Random.Shared;
        bool success = random.NextDouble() < q.Chance;
        if (success) { delivery.Commit(); } else { q.Input.Item.Consume(1); output.Delete(); }
        CraftTransactions.SpendUse(q.Owner, q.Tool, random);
        if (q.Tool.UsesRemaining < 1 && q.Tool.BreakOnDepletion) { q.Tool.Delete(); }
        if (!q.Row.RequiresRareRecipe) { q.Recipe.GetSuccessChance(q.Owner, null, DefTinkering.CraftSystem, true, out _); }
        q.Owner.SendMessage(success
            ? q.Row.WorldLoot ? $"You crafted one {q.Row.Name}." : "You crafted one single-use skeleton key."
            : "The key failed: one ingot and one tool use were spent. Crafted-tool saving applies.");
        return true;
    }
}
[SerializationGenerator(0)]
public partial class TreasureSkeletonKey : Item
{
    [SerializableField(0, setter: "private")] private int _tier;
    [Constructible] public TreasureSkeletonKey() : this(1) { }
    public TreasureSkeletonKey(int tier) : base(0x1012)
    { _tier = tier; Name = TreasureHunts.Tier(tier).KeyName + " skeleton key"; Weight = 0.1; Stackable = true; }
    public override bool CanStackWith(Item item) => item is TreasureSkeletonKey other && other.Tier == _tier && base.CanStackWith(item);
    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile owner && ClassProfessions.Ready(owner) && RuneCrafting.Accessible(owner, this) && owner.Target == null)
        { owner.SendMessage("Choose your matching treasure chest. A key is spent only when it unlocks."); owner.Target = new ChestTarget(this, owner.NetState); }
    }
    private sealed class ChestTarget(TreasureSkeletonKey key, NetState connection) : Target(2, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object target)
        { if (from is PlayerMobile owner && owner.NetState == connection && target is ExpeditionChest chest) { TreasureHunts.Unlock(owner, key, chest); } }
    }
    public override void GetProperties(IPropertyList list)
    { base.GetProperties(list); list.Add($"{"One successful unlock of a matching "}{TreasureHunts.Tier(_tier).Name}{" chest. No Lockpicking or Remove Trap required."}"); }
}
[SerializationGenerator(0)]
public partial class WorldChestKey : Item
{
    [SerializableField(0, setter: "private")] private int _tier;

    [Constructible]
    public WorldChestKey() : this(1) { }

    [Constructible]
    public WorldChestKey(int tier) : base(0x1012)
    {
        _tier = Math.Clamp(tier, 1, 5);
        Name = TreasureKeys.Recipe(_tier, true).Name;
        Stackable = true;
        Weight = 0.1;
    }

    public override bool CanStackWith(Item item) =>
        item is WorldChestKey other && other.Tier == _tier && base.CanStackWith(item);
}

public sealed class TreasureKeyGump : DynamicGump
{
    private readonly PlayerMobile _owner;
    private readonly TinkerTools _tool;
    private readonly NetState _connection;
    private readonly TreasureKeyRequest _request;
    private readonly bool _worldLoot;
    private bool _answered;
    public override bool Singleton => true;
    private TreasureKeyGump(PlayerMobile owner, TinkerTools tool, bool worldLoot, TreasureKeyRequest request = null) : base(60, 45)
    { _owner = owner; _tool = tool; _connection = owner.NetState; _request = request; _worldLoot = worldLoot; }
    public static void DisplayTo(PlayerMobile owner, TinkerTools tool, bool worldLoot = false)
    { if (owner.NetState != null && TreasureKeys.Ready(owner, tool)) { owner.SendGump(new TreasureKeyGump(owner, tool, worldLoot)); } }
    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage(); b.AddBackground(0, 0, 560, 390, 5054); b.AddLabel(22, 20, 0x481, _worldLoot ? "Tinkering — Dungeon Keys" : "Tinkering — Skeleton Keys");
        if (_request is { } q)
        {
            b.AddLabel(22, 75, 0x481, q.Row.WorldLoot ? q.Row.Name : $"{q.Row.Name} for a {q.Row.ChestName} chest");
            b.AddLabel(22, 115, 0x47E, $"Tinkering {q.Row.Skill:F1}; chance {q.Chance:P1}");
            b.AddLabel(22, 155, 0x47E, $"One {q.Row.MaterialName}; one tool use with saving.");
            b.AddLabel(22, 195, 0x47E, "Failure loses the ingot and tool use. One successful unlock.");
            b.AddButton(22, 300, 4005, 4007, 1); b.AddLabel(57, 300, 0x481, "Craft key");
        }
        else
        {
            for (int i = 1; i <= 5; i++)
            {
                var r = TreasureKeys.Recipe(i, _worldLoot); int y = 70 + (i - 1) * 52;
                string label = _worldLoot ? r.Name : $"{r.Name} — {r.ChestName}";
                b.AddButton(22, y, 4005, 4007, i); b.AddLabel(57, y, 0x481, $"{label} — Tinkering {r.Skill:F0}");
                b.AddLabel(57, y + 22, 0x47E, $"One {r.MaterialName}{(r.RequiresRareRecipe && !RareRecipes.Allowed(_owner, "key/splendid", false) ? "; recipe not learned" : "")}");
            }
        }
        b.AddButton(440, 340, 4017, 4019, 0); b.AddLabel(475, 340, 0x481, "Cancel");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_answered || sender != _connection || sender.Mobile != _owner) { return; } _answered = true;
        if (_request != null) { if (info.ButtonID == 1) { TreasureKeys.Start(_request); } else { _request.Claimed = true; } return; }
        if (info.ButtonID is < 1 or > 5) { return; }
        var q = TreasureKeys.Quote(_owner, _tool, info.ButtonID, _worldLoot);
        if (q != null) { _owner.SendGump(new TreasureKeyGump(_owner, _tool, _worldLoot, q)); }
        else { _owner.SendMessage("Check your key recipe, Tinkering skill, exact ingot, tool and public workshop."); }
    }
}
