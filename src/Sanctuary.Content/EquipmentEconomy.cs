// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record SalvageRange(int Min, int Max, int ChancePercent, int LevelsPerStep = 1, int BonusPerStep = 0)
{
    public (int Min, int Max) Bounds(int level)
    {
        int bonus = (Math.Clamp(level, 1, 99) - 1) / LevelsPerStep * BonusPerStep;
        return (Min + bonus, Max + bonus);
    }
    public int Roll(int level, Random random)
    {
        if (random.Next(100) >= ChancePercent)
        {
            return 0;
        }
        var (min, max) = Bounds(level);
        return random.Next(min, max + 1);
    }
}
public sealed record EconomySettings(SalvageRange MagicEssence, SalvageRange RareEssence, SalvageRange ReservedUniqueEssence,
    SalvageRange MetalScraps, SalvageRange WoodenChips, SalvageRange LeatherStrips, SalvageRange ClothScraps, int SalvageGoldCost,
    int ImbueEssenceCost, int ImbueGoldCost, int RareChancePercent);
public enum EssenceAction { Salvage, Imbue }
public enum SalvageMaterial { None, Metal, Wood, Leather, Cloth }
public sealed record SalvageResult(int Essence, int Material);

public static class EquipmentEconomy
{
    public static EconomySettings Settings { get; private set; }
    public static void Load()
    {
        var rows = BalanceCatalog.Read<EconomySettings>("economy");
        if (rows.Length != 1)
        {
            throw new InvalidDataException("Exactly one equipment economy configuration is required.");
        }
        var row = rows[0];
        if (row.SalvageGoldCost != 0 || row.ImbueEssenceCost is < 1 or > 60000
            || row.ImbueGoldCost is < 0 or > 60000 || row.RareChancePercent is < 0 or > 100)
        {
            throw new InvalidDataException("Invalid equipment economy configuration.");
        }
        foreach (var range in new[] { row.MagicEssence, row.RareEssence, row.ReservedUniqueEssence,
                     row.MetalScraps, row.WoodenChips, row.LeatherStrips, row.ClothScraps })
        {
            if (range == null || range.Min < 1 || range.Max <= range.Min || range.ChancePercent is < 0 or > 100
                || range.LevelsPerStep is < 1 or > 99 || range.BonusPerStep is < 0 or > 100
                || range.Bounds(99).Max > 60000)
            {
                throw new InvalidDataException("Invalid salvage yield range.");
            }
        }
        Settings = row;
    }
    public static void Configure()
    {
        ClassEconomyHooks.SellPrice = SellPrice;
        ClassEconomyHooks.LegacySalvageAllowed = (from, item) =>
        {
            if (!ClassGear.Converted(item))
            {
                return true;
            }
            from.SendMessage("Use Salvage gear on the matching crafting tool. Vendor and protected equipment cannot be salvaged.");
            return false;
        };
    }
    public static int SellPrice(Item item, int nativePrice) => TradePrices.Quote(item, nativePrice);
    public static SalvageMaterial Material(Item item)
    {
        if (item is BaseArmor armor)
        {
            return armor.MaterialType switch
            {
                ArmorMaterialType.Wood => SalvageMaterial.Wood,
                ArmorMaterialType.Plate or ArmorMaterialType.Chainmail or ArmorMaterialType.Ringmail => SalvageMaterial.Metal,
                ArmorMaterialType.Cloth => SalvageMaterial.Cloth,
                ArmorMaterialType.Leather or ArmorMaterialType.Studded or ArmorMaterialType.Spined
                    or ArmorMaterialType.Horned or ArmorMaterialType.Barbed => SalvageMaterial.Leather,
                _ => SalvageMaterial.None
            };
        }
        if (item is BaseJewel)
        {
            return SalvageMaterial.Metal;
        }
        if (item is BaseClothing clothing)
        {
            return CraftResources.GetType(clothing.Resource) == CraftResourceType.Leather
                ? SalvageMaterial.Leather : SalvageMaterial.Cloth;
        }
        if (item is not BaseWeapon)
        {
            return SalvageMaterial.None;
        }
        if (item is GlassStaff or SerpentstoneStaff or GlassSword or BoneMachete or BoneHarvester or StoneSlithClaw or RaptorClaw)
        {
            return SalvageMaterial.None;
        }
        // Native weapon Resource defaults to iron even for several wooden bases.
        // Use the physical weapon family, with the wooden melee exceptions explicit.
        return item is BaseRanged or BaseStaff or BaseWand or Club or Bokuto or Nunchaku or Tetsubo or Boomerang
            ? SalvageMaterial.Wood : SalvageMaterial.Metal;
    }
    public static SalvageRange EssenceRange(GearRarity rarity) => rarity switch
    {
        GearRarity.Magic => Settings.MagicEssence,
        GearRarity.Rare => Settings.RareEssence,
        _ => null
    };
    public static SalvageRange MaterialRange(SalvageMaterial material) => material switch
    {
        SalvageMaterial.Metal => Settings.MetalScraps,
        SalvageMaterial.Wood => Settings.WoodenChips,
        SalvageMaterial.Leather => Settings.LeatherStrips,
        SalvageMaterial.Cloth => Settings.ClothScraps,
        _ => null
    };
    public static string MaterialName(SalvageMaterial material) => material switch
    {
        SalvageMaterial.Metal => "Metal Scraps",
        SalvageMaterial.Wood => "Wooden Chips",
        SalvageMaterial.Leather => "Leather Strips",
        SalvageMaterial.Cloth => "Cloth Scraps",
        _ => "material"
    };
    private static Item CreateMaterial(SalvageMaterial material, int amount) => material switch
    {
        SalvageMaterial.Metal => new MetalScraps(amount),
        SalvageMaterial.Wood => new WoodenChips(amount),
        SalvageMaterial.Leather => new LeatherStrips(amount),
        SalvageMaterial.Cloth => new ClothScraps(amount),
        _ => throw new InvalidOperationException("Unknown salvage material.")
    };
    private static void StoreResource(PlayerMobile player, Item resource)
    {
        foreach (var existing in player.Backpack.Items)
        {
            if (existing.CanStackWith(resource) && existing.StackWith(player, resource, false))
            {
                return;
            }
        }
        player.Backpack.DropItem(resource);
    }
    public static bool Ready(PlayerMobile player) => player is { Deleted: false, Alive: true, Frozen: false, Paralyzed: false }
        && player.Backpack != null && player.Holding == null && ClassCombat.Enabled(player);
    private static bool Eligible(PlayerMobile player, Item item) => Ready(player)
        && item is { Deleted: false, Movable: true, Amount: 1, Insured: false, Nontransferable: false }
        && item.Origin is ItemOrigin.Drop or ItemOrigin.Crafted && item.IsChildOf(player.Backpack)
        && item.IsStandardLoot() && item.BlessedFor == null && item.IsAccessibleTo(player) && item.VerifyMove(player)
        && !item.IsLockedDown && !item.IsSecure
        && ClassGear.Converted(item) && ClassGear.State(item) is not { StarterTeeth: true };
    public static EssenceQuote Quote(PlayerMobile player, Item item, EssenceAction action, BaseTool salvageTool = null) =>
        BuildQuote(player, item, action, salvageTool, true);
    private static EssenceQuote BuildQuote(PlayerMobile player, Item item, EssenceAction action, BaseTool salvageTool, bool track)
    {
        if (!Enum.IsDefined(action) || !Eligible(player, item))
        {
            return null;
        }
        if (action == EssenceAction.Imbue && !ClassProfessions.Active(player, SkillName.Imbuing)) { return null; }
        BaseTool sourceTool = null;
        if (action == EssenceAction.Salvage)
        {
            sourceTool = salvageTool ?? EquipmentSalvage.FindTool(player, item);
            if (!EquipmentSalvage.Matches(player, item, sourceTool)) { return null; }
        }
        ImbuingLadle ladle = null;
        if (action == EssenceAction.Imbue)
        {
            ladle = salvageTool == null ? ImbuingTool(player) : salvageTool as ImbuingLadle;
            if (ladle == null)
            {
                player.SendMessage("Imbuing needs an accessible ladle with uses remaining.");
                return null;
            }
            if (!TieredImbuing.Ready(player, item, ladle)) { return null; }
        }
        var state = ClassGear.State(item);
        var rarity = state?.Roll.Rarity ?? GearRarity.Normal;
        var material = Material(item);
        if (action == EssenceAction.Salvage ? EssenceRange(rarity) == null && material == SalvageMaterial.None
            : rarity != GearRarity.Normal || state is { TinkerSockets: > 0 } || ClassGear.Definition(item) is not { AllowsAffixes: true }
                || ClassGear.Slot(item) == GearSlot.Cosmetic)
        {
            return null;
        }
        return new(player.Serial, item.Serial, item.Origin, state, action, rarity, material, state?.Roll.ItemLevel ?? 1,
            action == EssenceAction.Salvage ? 0 : TieredImbuing.Cost(item).Essence,
            action == EssenceAction.Salvage ? 0 : TieredImbuing.Cost(item).Gold)
        {
            Tool = sourceTool?.Serial ?? ladle?.Serial ?? Serial.Zero, Connection = player.NetState,
            ToolParent = (sourceTool ?? ladle)?.Parent, ToolUses = (sourceTool ?? ladle)?.UsesRemaining ?? 0,
            Snapshot = new EquipmentSalvage.Snapshot(item),
            Profession = ClassProfessions.Profiles.Find(player), ImbuingSkill = player.Skills.Imbuing.BaseFixedPoint,
            Session = track ? EquipmentSalvage.Begin(player) : 0
        };
    }
    private static ImbuingLadle ImbuingTool(PlayerMobile player)
    {
        foreach (var ladle in player.Backpack.FindItemsByType<ImbuingLadle>(true))
        {
            if (ladle is { Deleted: false, UsesRemaining: > 0 } && ladle.IsAccessibleTo(player)) { return ladle; }
        }
        foreach (var item in player.Items)
        {
            if (item is ImbuingLadle { Deleted: false, UsesRemaining: > 0 } ladle) { return ladle; }
        }
        return null;
    }
    internal static bool Commit(PlayerMobile player, EssenceQuote quote, out SalvageResult result, Random random = null)
    {
        result = null;
        if (!Ready(player) || quote == null || !quote.Claim() || player.Serial != quote.Owner
            || World.FindItem(quote.Item) is not { } item || !Eligible(player, item)
            || item.Origin != quote.Origin || !ReferenceEquals(ClassGear.State(item), quote.State))
        {
            return false;
        }
        {
            if (!EquipmentSalvage.Current(player, quote.Session)) { return false; }
            EquipmentSalvage.End(player, quote.Session);
        }
        if (!ReferenceEquals(player.NetState, quote.Connection)
            || quote.Snapshot?.Valid != true || World.FindItem(quote.Tool) is not BaseTool quotedTool
            || !ReferenceEquals(quotedTool.Parent, quote.ToolParent) || quotedTool.UsesRemaining != quote.ToolUses) { return false; }
        if (quote.Action == EssenceAction.Imbue && (player.Skills.Imbuing.BaseFixedPoint != quote.ImbuingSkill
            || !ReferenceEquals(ClassProfessions.Profiles.Find(player), quote.Profession))) { return false; }
        var fresh = BuildQuote(player, item, quote.Action, World.FindItem(quote.Tool) as BaseTool, false);
        if (fresh == null || ClassProfessions.Affected(player) && quote.Action == EssenceAction.Imbue
                && (fresh.Tool != quote.Tool || !ReferenceEquals(player.NetState, quote.Connection))
            || fresh.Essence != quote.Essence || fresh.Gold != quote.Gold || fresh.Material != quote.Material
            || fresh.Rarity != quote.Rarity || fresh.Level != quote.Level)
        {
            return false;
        }
        random ??= Random.Shared;
        if (quote.Action == EssenceAction.Salvage)
        {
            // Outcomes are drawn only after confirmation. A successful attempt can
            // recover nothing; the selected gear is still consumed in that case.
            int essenceCount = EssenceRange(quote.Rarity)?.Roll(1, random) ?? 0;
            int materialCount = MaterialRange(quote.Material)?.Roll(quote.Level, random) ?? 0;
            Item essence = essenceCount == 0 ? null : ItemOrigins.Create(ItemOrigin.Crafted, () => new MagicEssence(essenceCount));
            Item material = materialCount == 0 ? null : ItemOrigins.Create(ItemOrigin.Crafted, () => CreateMaterial(quote.Material, materialCount));
            int count = (essence == null ? 0 : 1) + (material == null ? 0 : 1);
            int weight = (essence?.PileWeight ?? 0) + (material?.PileWeight ?? 0);
            // Reserve space for every output before consuming anything. The gear's
            // freed space is deliberately not counted, including at full capacity.
            var probe = essence ?? material;
            if (probe != null && (player.TotalWeight + weight > player.MaxWeight
                || !player.Backpack.CheckHold(player, probe, false, true, count - 1, weight - probe.PileWeight)))
            {
                essence?.Delete();
                material?.Delete();
                return false;
            }
            if (essence != null)
            {
                StoreResource(player, essence);
            }
            if (material != null)
            {
                StoreResource(player, material);
            }
            item.Delete();
            result = new(essenceCount, materialCount);
            return true;
        }
        var rolled = ClassGear.Profiles.PrepareImbue(item, random.Next(100) < TieredImbuing.RareChance(player)
            ? GearRarity.Rare : GearRarity.Magic, random);
        if (rolled == null || player.Backpack.ConsumeTotal([typeof(MagicEssence), typeof(Gold)],
                [quote.Essence, quote.Gold]) != -1)
        {
            return false;
        }
        ClassGear.Profiles.ApplyImbue(item, rolled);
        if (World.FindItem(quote.Tool) is ImbuingLadle ladle)
        {
            CraftTransactions.SpendUse(player, ladle, random);
            if (ladle.UsesRemaining < 1 && ladle.BreakOnDepletion) { ladle.Delete(); }
        }
        if (ClassProfessions.Affected(player))
        {
            int required = TieredImbuing.Cost(item).Skill;
            player.CheckSkill(SkillName.Imbuing, required, required + 50);
        }
        return true;
    }
}

public sealed class EssenceQuote(Serial owner, Serial item, ItemOrigin origin, GearState state,
    EssenceAction action, GearRarity rarity, SalvageMaterial material, int level, int essence, int gold)
{
    public Serial Owner { get; } = owner;
    public Serial Item { get; } = item;
    public ItemOrigin Origin { get; } = origin;
    public GearState State { get; } = state;
    public EssenceAction Action { get; } = action;
    public GearRarity Rarity { get; } = rarity;
    public SalvageMaterial Material { get; } = material;
    public int Level { get; } = level;
    public int Essence { get; } = essence;
    public int Gold { get; } = gold;
    public Serial Tool { get; init; }
    public Server.Network.NetState Connection { get; init; }
    internal ProfessionState Profession { get; init; }
    internal int ImbuingSkill { get; init; }
    internal long Session { get; init; }
    internal object ToolParent { get; init; }
    internal int ToolUses { get; init; }
    internal EquipmentSalvage.Snapshot Snapshot { get; init; }
    private bool _claimed;
    internal bool Claim()
    {
        if (_claimed)
        {
            return false;
        }
        _claimed = true;
        return true;
    }
}

[SerializationGenerator(0)]
public partial class MagicEssence : Item
{
    [Constructible]
    public MagicEssence(int amount = 1) : base(0x3191)
    {
        Name = "Magic Essence";
        Hue = 0x48D;
        Weight = 0.1;
        Stackable = true;
        Amount = Math.Clamp(amount, 1, 60000);
    }
}
[SerializationGenerator(0)]
public partial class MetalScraps : Item
{
    public override double DefaultWeight => MaterialProcessing.Weight(this) ?? 0.1;
    [AfterDeserialization]
    private void NormalizeWeight() => MaterialProcessing.Normalize(this);
    [Constructible]
    public MetalScraps(int amount = 1) : base(0x1BF2)
    {
        Name = "Metal Scraps";
        Hue = 0x835;
        Weight = -1;
        Stackable = true;
        Amount = Math.Clamp(amount, 1, 60000);
    }
}
[SerializationGenerator(0)]
public partial class WoodenChips : Item
{
    public override double DefaultWeight => MaterialProcessing.Weight(this) ?? 0.1;
    [AfterDeserialization]
    private void NormalizeWeight() => MaterialProcessing.Normalize(this);
    [Constructible]
    public WoodenChips(int amount = 1) : base(0x1BD7)
    {
        Name = "Wooden Chips";
        Weight = -1;
        Stackable = true;
        Amount = Math.Clamp(amount, 1, 60000);
    }
}

[SerializationGenerator(0)]
public partial class LeatherStrips : Item
{
    public override double DefaultWeight => MaterialProcessing.Weight(this) ?? 0.1;
    [AfterDeserialization]
    private void NormalizeWeight() => MaterialProcessing.Normalize(this);
    [Constructible]
    public LeatherStrips(int amount = 1) : base(0x1081)
    {
        Name = "Leather Strips";
        Hue = 0x973;
        Weight = -1;
        Stackable = true;
        Amount = Math.Clamp(amount, 1, 60000);
    }
}
[SerializationGenerator(0)]
public partial class ClothScraps : Item
{
    public override double DefaultWeight => MaterialProcessing.Weight(this) ?? 0.1;
    [AfterDeserialization]
    private void NormalizeWeight() => MaterialProcessing.Normalize(this);
    [Constructible]
    public ClothScraps(int amount = 1) : base(0x1766)
    {
        Name = "Cloth Scraps";
        Hue = 0x47E;
        Weight = -1;
        Stackable = true;
        Amount = Math.Clamp(amount, 1, 60000);
    }
}
