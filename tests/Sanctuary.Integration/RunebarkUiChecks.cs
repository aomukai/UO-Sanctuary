using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Engines.Harvest;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

// Only the rare byproduct ticket is seeded. The resource extraction and sale use native paths.
public static class RunebarkUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(RunebarkUiChecks));
    private static PlayerMobile _player;
    private static Runebark _bark;
    private static Carpenter _vendor;
    private static int _gold;
    private sealed class Find : Random { public override int Next(int max) => max == 100 ? 0 : 7; }
    public static void Configure()
    {
        Register("setup", () =>
        {
            _player = Accounts.GetAccount("combatui")?[0] as PlayerMobile;
            Require(_player?.NetState != null, "Actual class player missing"); ClassCombat.Stop(_player);
            foreach (var item in _player.Backpack.Items.ToArray()) { item.Delete(); }
            _player.RawStr = 200; _player.Skills.Lumberjacking.Cap = 120; _player.Skills.Lumberjacking.Base = 120;
            _player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            var tool = new Hatchet(); _player.FindItemOnLayer(tool.Layer)?.Delete(); _player.AddItem(tool);
            var tree = new Static(0xCCA); tree.MoveToWorld(new Point3D(_player.X + 1, _player.Y, _player.Z), _player.Map);
            int calls = 0; var original = ClassHarvestHooks.Successful;
            try
            {
                ClassHarvestHooks.Successful = (from, def, trained, resourceType) =>
                { Require(def.Skill == SkillName.Lumberjacking, "Wrong harvest skill"); calls++; RunebarkHarvesting.Award(from, trained, new Find()); };
                for (int i = 0; i < 20 && calls == 0; i++)
                { Lumberjacking.System.FinishHarvesting(_player, tool, Lumberjacking.System.Definitions[0], tree, Lumberjacking.System); }
            }
            finally { ClassHarvestHooks.Successful = original; tree.Delete(); tool.Delete(); }
            Require(calls == 1 && _player.Backpack.Items.OfType<Log>().Any(), "Native wood harvest did not succeed");
            _bark = _player.Backpack.Items.OfType<Runebark>().Single();
            Require(_bark.Tier == 8 && _bark.Amount == 1, "Native success did not deliver expected bark");
            foreach (var item in _player.Backpack.Items.Where(i => i != _bark).ToArray()) { item.Delete(); }
            _bark.Location = new Point3D(45, 65, 0);
            _player.Backpack.OnDoubleClick(_player);
            _vendor = new Carpenter(); _vendor.MoveToWorld(_player.Location, _player.Map);
            _gold = _player.Backpack.GetAmount(typeof(Gold));
        });
        Register("sell", () => _vendor.VendorSell(_player));
        Register("sold", () => Require(_bark.Deleted && _player.Backpack.GetAmount(typeof(Gold)) == _gold + 80,
            "Actual client sale did not pay 80 gold"));
        Register("final", () =>
        {
            Require(_bark.Deleted, "Sold bark survived");
            Require(_vendor.GetBuyInfo().All(i => i is not GenericBuyInfo g || g.Type != typeof(Runebark)), "Bark was restocked");
        });
    }
    private static void Require(bool value, string message) => MouldChecks.Require(value, message);
    private static void Register(string suffix, Action action)
    {
        string name = "runebark-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Disposable native harvest and actual client bark resale", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { bark = _bark.Serial.Value, tier = _bark.Tier, gold = _player.Backpack.GetAmount(typeof(Gold)) }));
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    }
}
