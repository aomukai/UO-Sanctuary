using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ProcessingUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(ProcessingUiChecks));
    private static PlayerMobile _player;
    private static SewingKit _tool;
    private static SpinedLeather _source;
    private static MaterialProcessing.Request _stale;
    private static string Manifest => Path.Join(Core.BaseDirectory, "processing-ui.json");
    private sealed record Saved(uint Owner, uint Tool, uint Source, int Amount, int Output, int Uses);
    public static void Configure()
    {
        Register("setup", () =>
        {
            _player = Accounts.GetAccount("combatui")?[0] as PlayerMobile;
            CraftingChecks.Require(_player?.NetState != null && ClassProfessions.Affected(_player), "Actual client missing");
            ClassCombat.Stop(_player);
            _player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            _tool = new SewingKit(50); _source = new SpinedLeather(20);
            _player.Backpack.DropItem(_tool); _player.Backpack.DropItem(_source);
            _tool.OnDoubleClick(_player);
        });
        Register("tool", () => _tool.OnDoubleClick(_player));
        Register("target", () =>
        {
            CraftingChecks.Require(_player.Target != null, "Tool processing button did not start native target");
            _player.Target.Invoke(_player, _source);
        });
        Register("cancelled", () => Check(20, 0));
        Register("partial", () => Check(19, 10));
        Register("full", () =>
        {
            foreach (var item in _player.Backpack.Items.OfType<LeatherStrips>().ToArray()) { item.Delete(); }
            while (_player.Backpack.TotalItems < _player.Backpack.MaxItems)
            { _player.Backpack.DropItem(new Bag { Weight = 0, Name = "processing ui filler" }); }
            _tool.OnDoubleClick(_player);
        });
        Register("rejected", () => Check(19, 0));
        Register("relief", () =>
        {
            foreach (var item in _player.Backpack.Items.Where(i => i.Name == "processing ui filler").ToArray()) { item.Delete(); }
            _player.Backpack.DropItem(new Bag { Weight = 500 });
            _tool.OnDoubleClick(_player);
        });
        Register("completed", () =>
        {
            Check(0, 190);
            _source = new SpinedLeather(3); _player.Backpack.DropItem(_source);
            _stale = MaterialProcessing.Quote(_player, _tool, _source);
        });
        Register("disconnected", () =>
        {
            CraftingChecks.Require(_player.NetState == null && !MaterialProcessing.Complete(_stale, 1)
                && _source.Amount == 3 && _tool.UsesRemaining == 50, "Disconnected UI request committed");
        });
        Register("final", () => File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(_player.Serial.Value,
            _tool.Serial.Value, _source.Serial.Value, 3, 190, 50))));
    }
    private static void Check(int amount, int output) => CraftingChecks.Require(
        (amount == 0 ? _source.Deleted : !_source.Deleted && _source.Amount == amount)
        && _player.Backpack.GetAmount(typeof(LeatherStrips)) == output && _tool.UsesRemaining == 50
        && !ClassProfessions.Profiles.Find(_player).Confirmed && _player.Skills.Tailoring.Base == 0,
        "Actual UI changed wrong quantity, profession, uses or skill");
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand("processing-ui-" + name,
        "Actual client processing checks", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { Amount = _source?.Deleted == false ? _source.Amount : 0, Output = _player.Backpack.GetAmount(typeof(LeatherStrips)), Uses = _tool.UsesRemaining }));
                _logger.Information("processing-ui-{Name}: passed", name);
            }
            catch (Exception error) { _logger.Error(error, "processing-ui-{Name}: failed", name); }
        }));
    public static void Reload()
    {
        if (!File.Exists(Manifest)) { return; }
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var owner = World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        CraftingChecks.Require(owner != null && World.FindItem((Serial)saved.Tool) is SewingKit { UsesRemaining: 50 }
            && World.FindItem((Serial)saved.Source) is SpinedLeather { Amount: 3 }
            && owner.Backpack.GetAmount(typeof(LeatherStrips)) == saved.Output
            && !ClassProfessions.Profiles.Find(owner).Confirmed, "Actual processing output/choice changed across restart");
    }
}
