using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Engines.Craft.T2A;
using Server.Items;
using Server.Logging;
using Server.Misc;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class CraftingUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CraftingUiChecks));
    private static PlayerMobile _player;
    private static TinkerTools _tool;
    private static IronIngot _iron;
    private static readonly List<Item> _fillers = new();
    private static CraftTransactions.Request _stale;
    private static uint _output;
    private static Point3D _menuLocation;
    private sealed record Saved(uint Owner, uint Tool, uint Output, int Uses, int Iron, CraftToolState State);
    private static string Manifest => Path.Join(Core.BaseDirectory, "crafting-ui.json");
    public static void Configure()
    {
        Register("setup", () =>
        {
            _player = Accounts.GetAccount("combatui")?[0] as PlayerMobile;
            CraftingChecks.Require(_player?.NetState != null && ClassProfessions.Affected(_player), "Actual client missing");
            ClassCombat.Stop(_player);
            _player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            _player.Skills.Tinkering.Base = 100;
            _tool = new TinkerTools(50); _iron = new IronIngot(20);
            _player.Backpack.DropItem(_tool); _player.Backpack.DropItem(_iron);
            _tool.OnDoubleClick(_player);
        });
        Register("confirmed", () => CraftingChecks.Require(ClassProfessions.Profiles.Find(_player)
            == new ProfessionState(SkillName.Tinkering, true), "Client confirmation did not choose Tinkering"));
        Register("material", Material);
        Register("cancelled", () => CraftingChecks.Require(_tool.UsesRemaining == 50 && _iron.Amount == 20
            && !_player.Backpack.Items.OfType<SewingKit>().Any() && _player.Location == _menuLocation && CraftFacilities.Allows(_player, SkillName.Tinkering, false),
            "Native menu cancellation consumed or moved the character"));
        Register("crafted", () =>
        {
            var output = _player.Backpack.Items.OfType<SewingKit>().Single();
            _output = output.Serial.Value;
            CraftingChecks.Require(_player.Location == _menuLocation && _tool.UsesRemaining == 49 && _iron.Amount == 18 && output.Origin == ItemOrigin.Crafted
                && CraftTransactions.Profiles.Find(output) is { Crafted: true } state && output.UsesRemaining == state.Capacity,
                "Actual native menu response did not commit exactly one tool craft");
        });
        Register("full", () =>
        {
            while (_player.Backpack.TotalItems < _player.Backpack.MaxItems)
            { var filler = new Bag { Weight = 0 }; _player.Backpack.DropItem(filler); _fillers.Add(filler); }
            Material();
        });
        Register("rejected", () =>
        {
            CraftingChecks.Require(_player.Location == _menuLocation && _tool.UsesRemaining == 49 && _iron.Amount == 18
                && _player.Backpack.Items.OfType<SewingKit>().Count() == 1, "Full-pack native menu lost inputs or output");
            foreach (var filler in _fillers) { filler.Delete(); } _fillers.Clear();
            _stale = (CraftTransactions.Request)CraftTransactions.Begin(_player, DefTinkering.CraftSystem,
                DefTinkering.CraftSystem.CraftItems.SearchFor(typeof(SewingKit)), typeof(IronIngot), _tool, -1);
        });
        Register("town", () => Town(SkillName.Tinkering));
        Register("cauldron", () => Town(SkillName.Imbuing));
        Register("disconnected", () =>
        {
            CraftingChecks.Require(_player.NetState == null && CraftFacilities.Allows(_player, SkillName.Tinkering, false)
                && !CraftTransactions.Complete(_stale)
                && _tool.UsesRemaining == 49 && _iron.Amount == 18, "Disconnected craft ticket committed");
            Save();
        });
        Register("final", Save);
    }
    private static void Town(SkillName skill)
    {
        var site = BalanceCatalog.Read<CraftFacility>("craft-facilities").First(s => s.Map == "Felucca" && s.Skill == skill);
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                var point = new Point3D(site.X + dx, site.Y + dy, site.Z);
                if (!Map.Felucca.CanSpawnMobile(point)) { continue; }
                _player.MoveToWorld(point, Map.Felucca);
                if (CraftFacilities.Allows(_player, skill, false)) { return; }
            }
        }
        throw new InvalidOperationException("No usable public facility position");
    }
    private static void Material()
    {
        _player.Target = null;
        _menuLocation = _player.Location;
        TinkeringMenu.ResourceSelection(_player, _tool, _iron);
    }
    private static void Save()
    {
        CraftingChecks.Require(_output != 0, "Actual native tool output missing");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(_player.Serial.Value, _tool.Serial.Value,
            _output, _tool.UsesRemaining, _iron.Amount, CraftTransactions.Profiles.Find(World.FindItem((Serial)_output)))));
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand("crafting-ui-" + name,
        "Actual native tool menu crafting check", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { Uses = _tool?.UsesRemaining, Iron = _iron?.Amount, Output = _output,
                    X = _player?.X, Y = _player?.Y, Z = _player?.Z,
                    Facility = _player != null && CraftFacilities.Allows(_player, SkillName.Tinkering, false) }));
                _logger.Information("crafting-ui-{Name}: passed", name);
            }
            catch (Exception error) { _logger.Error(error, "crafting-ui-{Name}: failed", name); }
        }));
    public static void Reload()
    {
        if (!File.Exists(Manifest)) { return; }
        var row = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var player = World.FindMobile((Serial)row.Owner) as PlayerMobile;
        var tool = World.FindItem((Serial)row.Tool) as TinkerTools;
        var output = World.FindItem((Serial)row.Output) as SewingKit;
        CraftingChecks.Require(player != null && tool?.UsesRemaining == row.Uses && tool.IsChildOf(player.Backpack)
            && output != null && output.IsChildOf(player.Backpack) && CraftTransactions.Profiles.Find(output) == row.State
            && player.Backpack.GetAmount(typeof(IronIngot)) == row.Iron, "Actual craft UI state did not persist");
    }
}
