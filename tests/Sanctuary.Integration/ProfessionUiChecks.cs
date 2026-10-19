using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ProfessionUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(ProfessionUiChecks));
    private static PlayerMobile _player;
    private static SewingKit _tool;
    private static ProfessionInstructor _trainer;
    private static Gold _payment;
    private static int _gold;
    private static string Manifest => Path.Join(Core.BaseDirectory, "profession-ui.json");
    private sealed record Saved(uint Owner, uint Tool, uint Trainer, int Base, int Gold, int Cloth, int Uses);
    public static void Configure()
    {
        Register("setup", () =>
        {
            _player = Accounts.GetAccount("combatui")?[0] as PlayerMobile;
            Require(_player?.NetState != null && ClassProfessions.Affected(_player), "Actual class client missing");
            ClassCombat.Stop(_player);
            _player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            Require(!ClassProfessions.Profiles.Find(_player).Confirmed, "New client was already craft-locked");
            _tool = new SewingKit(50);
            _player.Backpack.DropItem(_tool);
            _player.Backpack.DropItem(new Cloth(40));
            _payment = ItemOrigins.Create(ItemOrigin.Crafted, () => new Gold(300));
            _player.Backpack.DropItem(_payment);
            _gold = _player.Backpack.GetAmount(typeof(Gold));
            _trainer = new ProfessionInstructor();
            _trainer.MoveToWorld(_player.Location, _player.Map);
            _trainer.AIObject.AITimer.Stop();
            ProfessionGump.DisplayTo(_player);
        });
        Register("tool", () => _tool.OnDoubleClick(_player));
        Register("cancelled", () => Require(!ClassProfessions.Profiles.Find(_player).Confirmed
            && _player.Skills.Tailoring.Base == 0 && _tool.UsesRemaining == 50 && _player.Backpack.GetAmount(typeof(Gold)) == _gold,
            "Actual cancel selected craft or consumed inputs"));
        Register("trainer", () => Require(!_trainer.Teach(SkillName.Tailoring, _player, 0, false),
            "Trainer skipped first-choice confirmation"));
        Register("confirmed", () => Require(ClassProfessions.Profiles.Find(_player) == new ProfessionState(SkillName.Tailoring, true)
            && _player.Skills.Tailoring.Base == 0 && _player.Backpack.GetAmount(typeof(Gold)) == _gold,
            "Actual first-choice confirmation changed skill or gold"));
        Register("pay", () => Require(_trainer.OnGoldGiven(_player, _payment) && _player.Skills.Tailoring.Base == 30
            && _player.Backpack.GetAmount(typeof(Gold)) == _gold - 300, "Native trainer payment failed"));
        Register("final", () =>
        {
            Require(ClassProfessions.Profiles.Find(_player) == new ProfessionState(SkillName.Tailoring, true)
                && _player.Skills.Tailoring.BaseFixedPoint == 301 && _tool.UsesRemaining == 49
                && _player.Backpack.GetAmount(typeof(Cloth)) == 38 && _player.Backpack.GetAmount(typeof(Gold)) == _gold - 300,
                "Actual practice response did not consume exact inputs for0.1 skill");
            File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(_player.Serial.Value, _tool.Serial.Value,
                _trainer.Serial.Value, 301, _gold - 300, 38, 49)));
        });
        Register("status", () => ProfessionGump.DisplayTo(_player));
        Register("reload", Reload);
    }
    private static void Require(bool value, string message)
    { if (!value) { throw new InvalidOperationException(message); } }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand("profession-ui-" + name,
        "Actual client profession prompt/practice check", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                if (_player != null)
                {
                    File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                    { Selection = ClassProfessions.Profiles.Find(_player), Base = _player.Skills.Tailoring.Base,
                        Gold = _player.Backpack.GetAmount(typeof(Gold)), Uses = _tool.UsesRemaining }));
                }
                _logger.Information("profession-ui-{Name}: passed", name);
            }
            catch (Exception error) { _logger.Error(error, "profession-ui-{Name}: failed", name); }
        }));
    public static void Reload()
    {
        if (!File.Exists(Manifest)) { return; }
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var owner = World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner != null && ClassProfessions.Profiles.Find(owner) == new ProfessionState(SkillName.Tailoring, true)
            && owner.Skills.Tailoring.BaseFixedPoint == saved.Base && owner.Backpack.GetAmount(typeof(Gold)) == saved.Gold
            && owner.Backpack.GetAmount(typeof(Cloth)) == saved.Cloth
            && World.FindItem((Serial)saved.Tool) is SewingKit { UsesRemaining: 49 }
            && World.FindMobile((Serial)saved.Trainer) is ProfessionInstructor, "GUI state did not persist");
    }
}
