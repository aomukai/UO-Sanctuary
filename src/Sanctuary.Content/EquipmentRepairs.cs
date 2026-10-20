// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public sealed record RepairCost(Type Type, int Amount);
public sealed record RepairResult(bool SavedResources, int LostMaximum, int Charges);

public sealed class RepairRequest
{
    internal bool Claimed;
    public PlayerMobile Owner { get; init; }
    public Item Item { get; init; }
    public BaseTool Tool { get; init; }
    public BaseVendor Vendor { get; init; }
    public SkillName Skill { get; init; }
    public double SkillValue { get; init; }
    public bool Durability { get; init; }
    public int RestoreCharges { get; init; }
    public int Charges { get; init; }
    public int Capacity { get; init; }
    public int Gold { get; init; }
    public GearTier Tier { get; init; }
    public GearRarity Rarity { get; init; }
    public NativeGearDetails Native { get; init; }
    public RepairCost[] Costs { get; init; }
    internal object Parent { get; init; }
    internal object ToolParent { get; init; }
    internal int Uses { get; init; }
    internal ItemOrigin Origin { get; init; }
    internal GearState State { get; init; }
    internal NetState Connection { get; init; }
}

public static class EquipmentRepairs
{
    public static RepairSettings Settings { get; private set; }
    public static RepairProfiles Profiles { get; private set; }
    public static bool RepairTool(BaseTool tool) => tool is RepairHammer or TailoringRepairKit or FletcherTools or TinkerTools;
    public static bool DedicatedTool(BaseTool tool) => tool is RepairHammer or TailoringRepairKit;
    public static RepairSkill Bracket(double skill) => Settings.Skills.Last(row => skill >= row.Minimum);
    public static RepairTier Tier(GearTier tier) => Settings.Tiers.Single(row => row.Tier == tier);
    public static void Load()
    {
        var rows = BalanceCatalog.Read<RepairSettings>("repairs");
        if (rows.Length != 1) { throw new InvalidDataException("One repair settings row is required."); }
        Settings = rows[0];
        if (Settings.Tiers.Length != 3 || Settings.Skills.Length != 4
            || !Settings.Tiers.Select(t => t.Tier).Order().SequenceEqual(Enum.GetValues<GearTier>())
            || !Settings.Skills.Select(s => s.Minimum).SequenceEqual(new double[] { 0, 80, 100, 120 })
            || Settings.RareEssenceMultiplier is < 1 or > 10 || Settings.UniqueEssenceMultiplier < Settings.RareEssenceMultiplier
            || Settings.UniqueEssenceMultiplier > 10 || Settings.MinimumMaximumDurability != 1
            || Settings.EssencePerCharge is < 1 or > 100 || Settings.NpcSkill is < 0 or > 120
            || Settings.ToolRequiredSkill is < 0 or > 120
            || new[] { Settings.HammerIngots, Settings.HammerScraps, Settings.KitIngots, Settings.KitStrips }.Any(n => n is < 1 or > 100))
        { throw new InvalidDataException("Invalid repair settings."); }
        foreach (var tier in Settings.Tiers)
        {
            if (tier.Min < 1 || tier.Max < tier.Min || tier.Max > 100 || tier.Scraps is < 1 or > 100 || tier.Gold is < 0 or > 60000)
            { throw new InvalidDataException("Invalid repair tier."); }
        }
        foreach (var skill in Settings.Skills)
        {
            if (skill.LossChance is < 0 or > 100 || skill.SavingChance is < 0 or > 100
                || skill.LossMin < 1 || skill.LossMax < skill.LossMin || skill.LossMax > 100)
            { throw new InvalidDataException("Invalid repair skill bracket."); }
        }
    }
    public static void Configure()
    {
        Profiles = new RepairProfiles();
        TradeSkillHooks.RepairUsed = (from, system, tool, deed) =>
        {
            if (!ClassProfessions.Affected(from)) { return false; }
            if (tool == null || !RepairTool(tool))
            { from.SendMessage("Use your profession's repair tool. Repair deeds cannot replace repair resources."); }
            else { RepairGump.Target((PlayerMobile)from, tool, null); }
            return true;
        };
        TradeSkillHooks.WandCreated = (wand, charges) => Profiles.Capture(wand, charges);
        ClassEconomyHooks.ServiceEntries = AddEntries;
        EventSink.WorldSave += Profiles.Prune;
    }
    private static void AddEntries(BaseVendor vendor, Mobile from, ref PooledRefList<ContextMenuEntry> entries)
    {
        if (ClassProfessions.Enabled && from is PlayerMobile && ServiceSkill(vendor) != null)
        { entries.Add(new ServiceEntry(vendor)); }
    }
    private sealed class ServiceEntry(BaseVendor vendor) : ContextMenuEntry(1044260, 3)
    {
        public override void OnClick(Mobile from, IEntity target)
        {
            if (from is PlayerMobile player) { RepairGump.Target(player, null, vendor); }
        }
    }
    public static SkillName? ServiceSkill(BaseVendor vendor) => vendor == null ? null : TownMarkets.Role(vendor) switch
    {
        MarketRole.Smith => SkillName.Blacksmith, MarketRole.Tailor => SkillName.Tailoring,
        MarketRole.Bowyer or MarketRole.Carpenter => SkillName.Fletching,
        MarketRole.Tinker or MarketRole.Jeweller => SkillName.Tinkering, _ => null
    };
    public static SkillName? Skill(Item item) => item is BaseJewel ? SkillName.Tinkering : GearDetails.Read(item).Material switch
    {
        GearMaterial.Metal => SkillName.Blacksmith, GearMaterial.Wood => SkillName.Fletching,
        GearMaterial.Leather or GearMaterial.Cloth or GearMaterial.Bone => SkillName.Tailoring, _ => null
    };
    public static bool Ready(PlayerMobile player) => ClassProfessions.Enabled && EquipmentEconomy.Ready(player)
        && player.CanBeginAction<CraftSystem>();
    public static bool ServiceReady(PlayerMobile player, BaseVendor vendor) => Ready(player)
        && vendor is { Deleted: false, Alive: true, IsActiveVendor: true } && ServiceSkill(vendor) != null
        && player.Map == vendor.Map && player.InRange(vendor, 3) && player.InLOS(vendor) && vendor.CheckVendorAccess(player);
    private static bool ItemReady(PlayerMobile player, Item item) => Ready(player)
        && item is { Deleted: false, Movable: true, Amount: 1, IsLockedDown: false, IsSecure: false }
        && item.IsChildOf(player.Backpack) && item.IsAccessibleTo(player) && item.VerifyMove(player)
        && (item.BlessedFor == null || item.BlessedFor == player)
        && (ClassGear.Converted(item) || item is BaseWand) && !ClassGear.CosmeticClothing(item)
        && ClassGear.State(item) is not { Identified: false };
    private static bool ToolReady(PlayerMobile player, BaseTool tool, SkillName skill)
    {
        if (!RepairTool(tool) || tool is not { Deleted: false, UsesRemaining: > 0 } || tool.CraftSystem.MainSkill != skill
            || !(tool.Parent == player || tool.IsChildOf(player.Backpack)) || !tool.IsAccessibleTo(player)
            || !ClassProfessions.Active(player, skill) || !CraftFacilities.Allows(player, skill, false)) { return false; }
        if (skill == SkillName.Blacksmith)
        {
            DefBlacksmithy.CheckAnvilAndForge(player, 2, out bool anvil, out bool forge);
            return anvil && forge;
        }
        return true;
    }
    // Legacy wands lack an original capacity. Preserve current charges and use
    // the native type's constructor ceiling; newly created wands save their roll.
    public static int Capacity(BaseWand wand) => Profiles.Capacity(wand) ?? Math.Max(wand.Charges, wand.WandEffect switch
    {
        WandEffect.Identification => 175,
        WandEffect.Clumsiness or WandEffect.Feeblemindedness or WandEffect.Weakness or WandEffect.ManaDraining => 30,
        _ when Core.ML => 109,
        WandEffect.GreaterHealing => 5, WandEffect.Fireball => 15, WandEffect.Healing => 25,
        WandEffect.Lightning => 20, _ => 30
    });
    public static RepairRequest Quote(PlayerMobile player, Item item, BaseTool tool = null, BaseVendor vendor = null,
        bool durability = true, int charges = 0, Random random = null)
    {
        if (!ItemReady(player, item) || Skill(item) is not { } skill || (tool == null) == (vendor == null)
            || (vendor != null ? !ServiceReady(player, vendor) || ServiceSkill(vendor) != skill : !ToolReady(player, tool, skill)))
        { return null; }
        var native = GearDetails.Read(item);
        bool damaged = native.MaximumDurability > 0 && native.Durability >= 0 && native.Durability < native.MaximumDurability;
        durability &= damaged;
        int capacity = item is BaseWand wand ? Capacity(wand) : 0;
        int current = item is BaseWand charged ? charged.Charges : 0;
        if (charges < -1 || capacity > 60000 || current < 0 || current > capacity) { return null; }
        int restore = charges == -1 ? capacity - current : charges;
        if (restore > capacity - current || !durability && restore == 0) { return null; }
        var state = ClassGear.State(item);
        var tier = state?.Tier ?? GearTier.Normal;
        var rarity = state?.Roll.Rarity ?? GearRarity.Normal;
        var band = Tier(tier);
        var costs = new List<RepairCost>();
        int essence = restore * Settings.EssencePerCharge;
        if (durability)
        {
            Type processed = Processed(native);
            if (processed == null) { return null; }
            var saved = Profiles.Price(item);
            if (saved == null || saved.Hits != native.Durability || saved.Maximum != native.MaximumDurability
                || saved.Material != native.Material || saved.Resource != native.Resource || saved.Tier != tier || saved.Rarity != rarity)
            {
                random ??= Random.Shared;
                int material = random.Next(band.Min, band.Max + 1);
                int magic = rarity == GearRarity.Normal ? 0 : random.Next(band.Min, band.Max + 1)
                    * (rarity == GearRarity.Rare ? Settings.RareEssenceMultiplier : 1);
                saved = new(native.Durability.Value, native.MaximumDurability.Value, native.Material, native.Resource,
                    tier, rarity, material, magic);
                Profiles.Store(item, saved);
            }
            costs.Add(new(processed, saved.Processed)); essence += saved.Essence;
            Type scrap = native.Material switch
            {
                GearMaterial.Metal => typeof(MetalScraps), GearMaterial.Leather => typeof(LeatherStrips),
                GearMaterial.Cloth => typeof(ClothScraps), _ => null
            };
            if (scrap != null) { costs.Add(new(scrap, band.Scraps)); }
        }
        if (essence > 60000) { return null; }
        if (essence > 0) { costs.Add(new(typeof(MagicEssence), essence)); }
        if (item is BaseWand capture && Profiles.Capacity(item) == null) { Profiles.Capture(capture, capacity); }
        return new()
        {
            Owner = player, Item = item, Tool = tool, Vendor = vendor, Skill = skill,
            SkillValue = vendor == null ? player.Skills[skill].Base : Settings.NpcSkill,
            Durability = durability, RestoreCharges = restore, Charges = current, Capacity = capacity,
            Gold = vendor == null ? 0 : band.Gold, Native = native, Tier = tier, Rarity = rarity,
            Costs = costs.ToArray(), Parent = item.Parent, ToolParent = tool?.Parent, Uses = tool?.UsesRemaining ?? 0,
            Origin = item.Origin, State = state, Connection = player.NetState
        };
    }
    public static Type Processed(NativeGearDetails native) => native.Material switch
    {
        GearMaterial.Cloth => typeof(Cloth), GearMaterial.Bone => typeof(Bone),
        GearMaterial.Metal => CraftResources.GetInfo(native.Resource)?.ResourceTypes.FirstOrDefault(t => typeof(BaseIngot).IsAssignableFrom(t)),
        GearMaterial.Leather => CraftResources.GetInfo(native.Resource)?.ResourceTypes.FirstOrDefault(t => typeof(BaseLeather).IsAssignableFrom(t)),
        GearMaterial.Wood => CraftResources.GetInfo(native.Resource)?.ResourceTypes.FirstOrDefault(t => typeof(Board).IsAssignableFrom(t)),
        _ => null
    };
    public static bool Commit(RepairRequest request, out RepairResult result, Random random = null)
    {
        result = null;
        if (request == null || request.Claimed) { return false; }
        request.Claimed = true;
        var player = request.Owner; var item = request.Item;
        if (!ItemReady(player, item) || !ReferenceEquals(player.NetState, request.Connection)
            || !ReferenceEquals(item.Parent, request.Parent) || item.Origin != request.Origin
            || !ReferenceEquals(ClassGear.State(item), request.State) || GearDetails.Read(item) != request.Native
            || item is BaseWand wand && (wand.Charges != request.Charges || Capacity(wand) != request.Capacity)
            || (request.Vendor != null ? !ServiceReady(player, request.Vendor) || ServiceSkill(request.Vendor) != request.Skill
                : !ToolReady(player, request.Tool, request.Skill) || request.Tool.UsesRemaining != request.Uses
                    || !ReferenceEquals(request.Tool.Parent, request.ToolParent) || player.Skills[request.Skill].Base != request.SkillValue))
        { return false; }
        var consumes = new List<(Item Item, int Amount)>();
        foreach (var cost in request.Costs.Append(new(typeof(Gold), request.Gold)))
        {
            int remaining = cost.Amount;
            foreach (var input in player.Backpack.FindItemsByType(cost.Type, true))
            {
                // Coloured types and resource properties must both match.
                if (input.GetType() != cost.Type || !input.IsAccessibleTo(player) || !input.VerifyMove(player)
                    || input.IsLockedDown || input.IsSecure || input is BaseIngot or Board or BaseLeather
                        && EquipmentCrafting.Resource(input) != request.Native.Resource) { continue; }
                int take = Math.Min(remaining, input.Amount);
                if (take > 0) { consumes.Add((input, take)); remaining -= take; }
                if (remaining == 0) { break; }
            }
            if (remaining != 0) { return false; }
        }
        random ??= Random.Shared;
        var bracket = Bracket(request.SkillValue);
        bool saved = random.Next(100) < bracket.SavingChance;
        int loss = request.Durability && random.Next(100) < bracket.LossChance ? random.Next(bracket.LossMin, bracket.LossMax + 1) : 0;
        int maximum = request.Native.MaximumDurability ?? 0;
        loss = Math.Min(loss, Math.Max(0, maximum - Settings.MinimumMaximumDurability));
        foreach (var (input, count) in consumes)
        {
            if (!saved || input is Gold) { input.Consume(count); }
        }
        if (request.Tool != null)
        {
            CraftTransactions.SpendUse(player, request.Tool, random);
            if (request.Tool.UsesRemaining < 1 && request.Tool.BreakOnDepletion) { request.Tool.Delete(); }
        }
        if (request.Durability)
        {
            SetDurability(item, maximum - loss);
            Profiles.Forget(item);
        }
        if (request.RestoreCharges > 0 && item is BaseWand recharge) { recharge.Charges += request.RestoreCharges; }
        item.InvalidateProperties();
        ClassGear.Changed(player);
        result = new(saved, loss, request.RestoreCharges);
        return true;
    }
    private static void SetDurability(Item item, int maximum)
    {
        switch (item)
        {
            case BaseWeapon weapon: weapon.MaxHitPoints = maximum; weapon.HitPoints = maximum; break;
            case BaseArmor armor: armor.MaxHitPoints = maximum; armor.HitPoints = maximum; break;
            case BaseClothing clothing: clothing.MaxHitPoints = maximum; clothing.HitPoints = maximum; break;
            case BaseJewel jewel: jewel.MaxHitPoints = maximum; jewel.HitPoints = maximum; break;
        }
    }
}
