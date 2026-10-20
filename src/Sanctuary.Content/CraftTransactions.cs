// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Network;

namespace Sanctuary;

public sealed record CraftToolSettings(int VendorPrice, int VendorMinUses, int VendorMaxUses, int AverageUses,
    int HighQualityUses, int SuperiorUses, int SaveChancePercent, double LadleRequiredSkill, int LadleIngots);

public static class CraftTransactions
{
    public static CraftToolProfiles Profiles { get; private set; }
    public static CraftToolSettings Settings { get; private set; }
    private static readonly HashSet<Type> _toolTypes = [typeof(MortarPestle), typeof(SmithHammer), typeof(FletcherTools),
        typeof(Saw), typeof(MapmakersPen), typeof(Skillet), typeof(ScribesPen), typeof(SewingKit), typeof(TinkerTools), typeof(ImbuingLadle),
        typeof(RepairHammer), typeof(TailoringRepairKit)];
    internal sealed class Request(Mobile owner, CraftSystem system, CraftItem recipe, Type material, BaseTool tool, int hue)
    {
        public Mobile Owner { get; } = owner;
        public CraftSystem System { get; } = system;
        public CraftItem Recipe { get; } = recipe;
        public Type Material { get; } = material;
        public BaseTool Tool { get; } = tool;
        public int Hue { get; } = hue;
        public NetState Connection { get; } = owner.NetState;
        public ProfessionState Profession { get; } = ClassProfessions.Profiles.Find(owner);
        public bool Claimed { get; set; }
        public EquipmentCrafting.Work Equipment { get; set; }
        public RuneRequest Rune { get; set; }
        public GemUpgradeRequest GemUpgrade { get; set; }
        public AlchemyRequest Alchemy { get; set; }
        public MealRequest Meal { get; set; }
        public CarpentryRequest Carpentry { get; set; }
        public SurveyRequest Survey { get; set; }
        public InscriptionRequest Scribing { get; set; }
    }
    public static bool ToolType(Type type) => _toolTypes.Contains(type);
    public static void Configure()
    {
        var rows = BalanceCatalog.Read<CraftToolSettings>("craft-tools");
        if (rows.Length != 1) { throw new InvalidDataException("One craft tool configuration is required."); }
        Settings = rows[0];
        if (Settings.VendorPrice is < 1 or > 60000 || Settings.VendorMinUses < 1
            || Settings.VendorMaxUses < Settings.VendorMinUses || Settings.VendorMaxUses > 10000
            || Settings.AverageUses < 1 || Settings.HighQualityUses < Settings.AverageUses
            || Settings.SuperiorUses < Settings.HighQualityUses || Settings.SuperiorUses > 10000
            || Settings.SaveChancePercent is < 0 or > 100 || Settings.LadleRequiredSkill is < 0 or > 120
            || Settings.LadleIngots is < 1 or > 100)
        { throw new InvalidDataException("Invalid craft tool configuration."); }
        Profiles = new CraftToolProfiles();
        CraftFacilities.Load();
        TradeSkillHooks.BeginCraft = Begin;
        TradeSkillHooks.CompleteCraft = Handle;
        TradeSkillHooks.ToolUse = (from, tool, output) =>
        {
            if (ClassProfessions.Affected(from) && CraftFacilities.Processing(output)) { return; }
            SpendUse(from, tool);
        };
        TradeSkillHooks.AdditionalTinkerTools = () => ClassProfessions.Enabled
            ? [typeof(FletcherTools), typeof(MapmakersPen), typeof(ScribesPen), typeof(Skillet), typeof(ImbuingLadle), typeof(RepairHammer), typeof(TailoringRepairKit)] : [];
        TradeSkillHooks.ToolPrice = type => ClassProfessions.Enabled && ToolType(type) ? Settings.VendorPrice : null;
        TradeSkillHooks.ToolSupplied = tool =>
        {
            if (ClassProfessions.Enabled && ToolType(tool.GetType())) { Supply(tool); }
        };
        TradeSkillHooks.ToolProperties = (tool, list) =>
        {
            if (Profiles.Find(tool) is { } state)
            {
                list.Add(1060658, $"{"Quality"}\t{QualityName(state.Quality)}");
                if (state.Crafted) { list.Add(1060659, $"{"Tool use saving"}\t{Settings.SaveChancePercent}%"); }
            }
            if (ClassProfessions.Enabled && tool is TinkerTools)
            { list.Add(1060660, $"{"Socket success bonus"}\t+{TinkeringSockets.ToolBonus(tool)} percentage points"); }
        };
        EventSink.WorldSave += Profiles.Prune;
    }
    [CallPriority(100)]
    public static void Initialize()
    {
        if (!ClassProfessions.Enabled) { return; }
        var system = DefTinkering.CraftSystem;
        var repair = EquipmentRepairs.Settings;
        if (system.CraftItems.SearchFor(typeof(RepairHammer)) == null)
        {
            int index = system.AddCraft(typeof(RepairHammer), 1044046, "repair hammer", repair.ToolRequiredSkill,
                repair.ToolRequiredSkill + 50, typeof(IronIngot), "Iron Ingots", repair.HammerIngots, "You need iron ingots.");
            system.AddRes(index, typeof(MetalScraps), "Metal Scraps", repair.HammerScraps, "You need metal scraps.");
        }
        if (system.CraftItems.SearchFor(typeof(TailoringRepairKit)) == null)
        {
            int index = system.AddCraft(typeof(TailoringRepairKit), 1044046, "tailoring repair kit", repair.ToolRequiredSkill,
                repair.ToolRequiredSkill + 50, typeof(IronIngot), "Iron Ingots", repair.KitIngots, "You need iron ingots.");
            system.AddRes(index, typeof(LeatherStrips), "Leather Strips", repair.KitStrips, "You need leather strips.");
        }
        if (system.CraftItems.SearchFor(typeof(ImbuingLadle)) == null)
        {
            system.AddCraft(typeof(ImbuingLadle), 1044046, "imbuing ladle", Settings.LadleRequiredSkill,
                Settings.LadleRequiredSkill + 50, typeof(IronIngot), 1044036, Settings.LadleIngots, 1044037);
        }
    }
    public static string QualityName(CraftQuality quality) => quality switch
    { CraftQuality.HighQuality => "High Quality", CraftQuality.Superior => "Superior", _ => "Average" };
    public static CraftQuality Quality(double skill, double required, int roll) =>
        skill < Math.Min(required + 10, 120) ? CraftQuality.Average
        : roll < 1 ? CraftQuality.Superior : roll < 11 ? CraftQuality.HighQuality : CraftQuality.Average;
    public static void Supply(BaseTool tool, Random random = null)
    {
        if (Profiles.Find(tool) != null || tool.Origin != ItemOrigin.Vendor || !ToolType(tool.GetType())) { return; }
        int uses = (random ?? Random.Shared).Next(Settings.VendorMinUses, Settings.VendorMaxUses + 1);
        tool.UsesRemaining = uses;
        Profiles.Store(tool, new(CraftQuality.Average, false, uses));
    }
    internal static void FinishTool(BaseTool output, Mobile maker, CraftQuality quality)
    {
        int capacity = quality switch
        {
            CraftQuality.HighQuality => Settings.HighQualityUses, CraftQuality.Superior => Settings.SuperiorUses,
            _ => Settings.AverageUses
        };
        output.Quality = ToolQuality.Regular;
        output.UsesRemaining = capacity;
        output.Crafter = maker.Skills[SkillName.Tinkering].Base >= 100 ? maker.RawName : null;
        output.PlayerConstructed = true;
        Profiles.Store(output, new(quality, true, capacity));
    }
    public static void SpendUse(Mobile from, BaseTool tool, Random random = null)
    {
        if (ClassProfessions.Affected(from) && tool.Origin == ItemOrigin.Crafted
            && Profiles.Find(tool) is { Crafted: true }
            && (random ?? Random.Shared).Next(100) < Settings.SaveChancePercent) { return; }
        tool.UsesRemaining--;
    }
    private static bool Managed(Mobile from, CraftItem recipe) => ClassProfessions.Affected(from) && (InscriptionCrafting.IsOutput(recipe.ItemType) || recipe.ItemType == typeof(SurveyMap) || CarpentryCrafting.IsOutput(recipe.ItemType) || ToolType(recipe.ItemType) || EquipmentCrafting.IsOutput(recipe.ItemType) || JewelleryCrafting.IsOutput(recipe.ItemType) || recipe.ItemType == typeof(IdentifyScroll) || recipe.ItemType == typeof(InscribedRune) || recipe.ItemType == typeof(SocketGem) || recipe.ItemType == typeof(RestorationPotion) || recipe.ItemType == typeof(PreparedMeal));
    internal static object Begin(Mobile from, CraftSystem system, CraftItem recipe, Type material, BaseTool tool, int hue)
    {
        if (!Managed(from, recipe)) { return null; }
        var ticket = new Request(from, system, recipe, material, tool, hue);
        if (EquipmentCrafting.IsOutput(recipe.ItemType) || JewelleryCrafting.IsOutput(recipe.ItemType)) { ticket.Equipment = EquipmentCrafting.Begin(ticket); }
        if (recipe.ItemType == typeof(InscribedRune)) { ticket.Rune = RuneCrafting.Attach(ticket); }
        if (recipe.ItemType == typeof(PreparedMeal)) { ticket.Meal = MealCooking.Attach(ticket); }
        if (InscriptionCrafting.IsOutput(recipe.ItemType)) { ticket.Scribing = InscriptionCrafting.Attach(ticket); }
        if (recipe.ItemType == typeof(SurveyMap)) { ticket.Survey = MapDrawing.Attach(ticket); }
        if (CarpentryCrafting.IsOutput(recipe.ItemType)) { ticket.Carpentry = CarpentryCrafting.Attach(ticket); }
        if (recipe.ItemType == typeof(RestorationPotion)) { ticket.Alchemy = AlchemyRestoratives.Attach(ticket); }
        if (recipe.ItemType == typeof(SocketGem)) { ticket.GemUpgrade = GemUpgrading.Attach(ticket); }
        return ticket;
    }
    private static bool Handle(Mobile from, CraftSystem system, CraftItem recipe, Type material, BaseTool tool, int hue, object ticket)
    {
        if (!Managed(from, recipe)) { return false; }
        if (ticket is Request request && ReferenceEquals(request.Owner, from) && ReferenceEquals(request.System, system)
            && ReferenceEquals(request.Recipe, recipe) && request.Material == material && ReferenceEquals(request.Tool, tool)
            && request.Hue == hue) { Complete(request); }
        return true;
    }
    internal static bool Valid(Request request)
    {
        var from = request.Owner; var tool = request.Tool; var recipe = request.Recipe; var system = request.System;
        return !(!ClassProfessions.Affected(from) || !ClassProfessions.Ready(from)
            || !ReferenceEquals(from.NetState, request.Connection)
            || !ReferenceEquals(ClassProfessions.Profiles.Find(from), request.Profession)
            || !ClassProfessions.Active(from, system.MainSkill)
            || tool is not { Deleted: false, UsesRemaining: > 0 } || tool.CraftSystem != system
            || !(tool.IsChildOf(from.Backpack) || tool.Parent == from)
            || !CraftFacilities.AllowsRecipe(from, system.MainSkill, recipe.ItemType)
            || system.CanCraft(from, tool, recipe.ItemType) != 0
            || recipe.Recipe != null && (from as Server.Mobiles.PlayerMobile)?.HasRecipe(recipe.Recipe) != true);
    }
    internal static bool Complete(Request request, Random random = null)
    {
        if (request.Scribing != null || InscriptionCrafting.IsOutput(request.Recipe.ItemType) && request.Recipe.ItemType != typeof(IdentifyScroll)) { return InscriptionCrafting.Complete(request, random); }
        if (request.Recipe.ItemType == typeof(SurveyMap)) { return MapDrawing.Complete(request, random); }
        if (CarpentryCrafting.IsOutput(request.Recipe.ItemType)) { return CarpentryCrafting.Complete(request, random); }
        if (request.Recipe.ItemType == typeof(PreparedMeal)) { return MealCooking.Complete(request, random); }
        if (request.Recipe.ItemType == typeof(RestorationPotion)) { return AlchemyRestoratives.Complete(request, random); }
        if (request.Recipe.ItemType == typeof(SocketGem)) { return GemUpgrading.Complete(request, random); }
        if (request.Recipe.ItemType == typeof(InscribedRune)) { return RuneCrafting.Complete(request, random); }
        if (EquipmentCrafting.IsOutput(request.Recipe.ItemType) || JewelleryCrafting.IsOutput(request.Recipe.ItemType)) { return EquipmentCrafting.Complete(request, random); }
        if (request.Claimed) { return false; }
        request.Claimed = true;
        var from = request.Owner;
        var tool = request.Tool;
        var recipe = request.Recipe;
        var system = request.System;
        if (!Valid(request)) { return false; }
        bool scroll = recipe.ItemType == typeof(IdentifyScroll);
        if (scroll && (!ReferenceEquals(recipe, Identification.Recipe) || system != DefInscription.CraftSystem || tool is not ScribesPen)) { return false; }
        double chance = recipe.GetSuccessChance(from, request.Material, system, false, out bool allSkills);
        if (!allSkills || chance <= 0) { return false; }
        int hue = 0, amount = 0;
        TextDefinition message = null;
        if (!recipe.ConsumeSelectedResources(from, request.Material, system, request.Hue, ref hue, ref amount,
                ConsumeType.None, ref message) || !recipe.ConsumeAttributes(from, ref message, false)) { return false; }
        var output = ItemOrigins.Create(ItemOrigin.Crafted, () => recipe.ItemType.CreateInstance<Item>());
        if (output == null) { return false; }
        if (from.TotalWeight + output.PileWeight > from.MaxWeight || !from.Backpack.CheckHold(from, output, false))
        {
            output.Delete();
            from.SendMessage("Make room in your backpack before crafting. Nothing was consumed.");
            return false;
        }
        random ??= Random.Shared;
        bool success = random.NextDouble() < chance;
        // Native resource plans are checked in full before the single-threaded commit.
        if (!recipe.ConsumeSelectedResources(from, request.Material, system, request.Hue, ref hue, ref amount,
                ConsumeType.All, ref message, !success))
        { output.Delete(); return false; }
        if (success) { recipe.ConsumeAttributes(from, ref message, true); }
        SpendUse(from, tool, random);
        bool broken = tool.UsesRemaining < 1 && tool.BreakOnDepletion;
        if (broken) { tool.Delete(); }
        int endQuality = 1;
        if (success)
        {
            double required = 0;
            foreach (var skill in recipe.Skills)
            { if (skill.SkillToMake == system.MainSkill) { required = skill.MinSkill; } }
            var quality = Quality(from.Skills[system.MainSkill].Base, required, random.Next(100));
            if (output is BaseTool craftedTool) { FinishTool(craftedTool, from, quality); }
            else { output.PlayerConstructed = true; }
            if (!scroll) { output.Hue = hue; }
            from.Backpack.DropItem(output);
            if (scroll) { from.SendMessage("You crafted an Identify scroll."); }
            else { from.SendMessage($"You crafted {QualityName(quality)} {output.DefaultName ?? output.GetType().Name}."); }
            endQuality = scroll || quality == CraftQuality.Average ? 1 : 2;
        }
        else { output.Delete(); }
        recipe.GetSuccessChance(from, request.Material, system, true, out _);
        int ending = system.PlayEndingEffect(from, !success, true, broken, endQuality, false, recipe);
        if (ending > 0) { from.SendLocalizedMessage(ending); }
        return true;
    }
}
