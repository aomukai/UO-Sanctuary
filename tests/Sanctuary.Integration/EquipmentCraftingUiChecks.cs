using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Engines.Craft;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class EquipmentCraftingUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(EquipmentCraftingUiChecks));
    private static Func<Mobile, CraftSystem, CraftItem, Type, BaseTool, int, object, bool> _complete;
    private sealed class Success : Random
    {
        public override double NextDouble() => 0;
        public override int Next(int max) => 0;
        public override int Next(int min, int max) => min;
    }
    private static PlayerMobile _owner;
    private static SewingKit _tool;
    private static Leather _source;
    private static Item _output;
    private static Cloth _cloth;
    private static Item _pageOutput;
    private static CraftTransactions.Request _stale;
    private static string Manifest => Path.Join(Core.BaseDirectory, "equipment-crafting-ui.json");
    private sealed record Saved(uint Owner, uint Item, string State, NativeGearDetails Native, uint PageItem, string PageState, NativeGearDetails PageNative);
    public static void Configure()
    {
        Register("setup", () =>
        {
            _owner = Accounts.GetAccount("combatui")?[0] as PlayerMobile;
            CraftingChecks.Require(_owner?.NetState != null && ClassProfessions.Affected(_owner), "Actual equipment client missing");
            ClassCombat.Stop(_owner); _owner.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            ClassProfessions.Profiles.Confirm(_owner, SkillName.Tailoring);
            _owner.Skills.Tailoring.Cap = 120; _owner.Skills.Tailoring.Base = 120;
            _tool = new SewingKit(50); _source = new Leather(13);
            _owner.Backpack.DropItem(_tool); _owner.Backpack.DropItem(_source);
            _complete = TradeSkillHooks.CompleteCraft;
            TradeSkillHooks.CompleteCraft = (from, system, recipe, material, tool, hue, ticket) =>
            {
                if (from == _owner && EquipmentCrafting.IsOutput(recipe.ItemType))
                { if (ticket is CraftTransactions.Request request) { CraftTransactions.Complete(request, new Success()); } return true; }
                return _complete(from, system, recipe, material, tool, hue, ticket);
            };
            _tool.OnDoubleClick(_owner);
        });
        Register("target", () =>
        {
            CraftingChecks.Require(_owner.Target != null, "Equipment recipe button did not start target");
            _owner.Target.Invoke(_owner, _source);
        });
        Register("crafted", () =>
        {
            _output = _owner.Backpack.Items.OfType<LeatherCap>().Single();
            var state = ClassGear.State(_output);
            CraftingChecks.Require(state is { Tier: GearTier.Elite, CraftedEquipment: true }
                && state.Roll.ItemLevel == 99 && ClassGear.RequiredLevel(_output) == 80
                && _source.Amount == 7 && _tool.UsesRemaining == 49, "Actual Elite recipe did not commit exact output/cost");
            CraftingChecks.Require(ClassGear.CanEquip(_output, _owner) == false, "Low-level client could equip Elite");
        });
        Register("full", () =>
        {
            while (_owner.Backpack.TotalItems < _owner.Backpack.MaxItems)
            { _owner.Backpack.DropItem(new Bag { Name = "equipment ui filler", Weight = 0 }); }
            EquipmentCraftGump.DisplayTo(_owner, _tool);
        });
        Register("rejected", () => CraftingChecks.Require(_source.Amount == 7 && _tool.UsesRemaining == 49
            && _owner.Backpack.Items.OfType<LeatherCap>().Count() == 1, "Rejected actual gear craft charged or duplicated"));
        Register("equip", () =>
        {
            foreach (var item in _owner.Backpack.Items.Where(i => i.Name == "equipment ui filler").ToArray()) { item.Delete(); }
            ClassCreation.Profiles.AdvanceLevel(_owner, 80);
            _owner.RawStr = 100; _owner.RawDex = 100;
            foreach (var item in _owner.Items.Where(i => i.Layer == _output.Layer).ToArray()) { _owner.Backpack.DropItem(item); }
            CraftingChecks.Require(_owner.EquipItem(_output) && _output.Parent == _owner && ClassGear.Active(_owner, _output),
                "Native equip rejected legal Elite gear");
            _owner.DisplayPaperdollTo(_owner); _owner.Use(_owner.Backpack);
        });
        Register("pagination", () =>
        {
            _cloth = new Cloth(7); _owner.Backpack.DropItem(_cloth);
            EquipmentCraftGump.DisplayTo(_owner, _tool);
        });
        Register("page-target", () =>
        {
            CraftingChecks.Require(_owner.Target != null, "Next-page recipe did not start target");
            _owner.Target.Invoke(_owner, _cloth);
        });
        Register("page-crafted", () =>
        {
            _pageOutput = _owner.Backpack.Items.OfType<Bandana>().Single();
            CraftingChecks.Require(ClassGear.State(_pageOutput) is { Tier: GearTier.Normal, Quality: CraftQuality.Superior }
                && _cloth.Amount == 5 && _source.Amount == 7 && _tool.UsesRemaining == 48,
                "Next/previous-page navigation crafted the wrong recipe or charged incorrectly");
            _stale = EquipmentCraftingChecks.Ticket(_owner, _tool, EquipmentCraftingChecks.Row("LeatherCap", GearTier.Elite), _source);
            CraftingChecks.Require(_stale.Equipment != null && CraftTransactions.Valid(_stale), "Disconnected fixture was already invalid");
        });
        Register("disconnected", () =>
        {
            CraftingChecks.Require(_owner.NetState == null && !CraftTransactions.Complete(_stale)
                && _source.Amount == 7 && _tool.UsesRemaining == 48, "Disconnected equipment craft committed");
            TradeSkillHooks.CompleteCraft = _complete;
        });
        Register("final", () => File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(_owner.Serial.Value,
            _output.Serial.Value, JsonSerializer.Serialize(ClassGear.State(_output)), GearDetails.Read(_output), _pageOutput.Serial.Value,
            JsonSerializer.Serialize(ClassGear.State(_pageOutput)), GearDetails.Read(_pageOutput)))));
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand("equipment-crafting-ui-" + name,
        "Actual white equipment client checks", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { Material = _source.Amount, Uses = _tool.UsesRemaining, Item = _output?.Serial.Value, Cloth = _cloth?.Amount, PageItem = _pageOutput?.Serial.Value }));
                _logger.Information("equipment-crafting-ui-{Name}: passed", name);
            }
            catch (Exception error) { _logger.Error(error, "equipment-crafting-ui-{Name}: failed", name); }
        }));
    public static void Reload()
    {
        if (!File.Exists(Manifest)) { return; }
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var item = World.FindItem((Serial)saved.Item);
        CraftingChecks.Require(item?.Parent == World.FindMobile((Serial)saved.Owner)
            && saved.State == JsonSerializer.Serialize(ClassGear.State(item)) && saved.Native == GearDetails.Read(item), "Client-crafted equipped gear changed on restart");
        var second = World.FindItem((Serial)saved.PageItem);
        CraftingChecks.Require(second is Bandana && second.IsChildOf(World.FindMobile((Serial)saved.Owner).Backpack)
            && saved.PageState == JsonSerializer.Serialize(ClassGear.State(second)) && saved.PageNative == GearDetails.Read(second),
            "Next-page crafted gear changed on restart");
    }
}
