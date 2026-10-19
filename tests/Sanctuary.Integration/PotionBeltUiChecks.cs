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

public static class PotionBeltUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(PotionBeltUiChecks));
    private static PlayerMobile _player;
    private static string _speech;
    private static string Manifest => Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE");
    public static void Configure()
    {
        EventSink.Speech += args => { if (args.Mobile == _player) { _speech = args.Speech; } };
        Register("setup", () =>
        {
            _player = Accounts.GetAccount("combatui")?[0] as PlayerMobile;
            Require(_player != null && ClassCombat.Enabled(_player), "Actual class player missing");
            ClassCombat.Stop(_player);
            _player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            _player.RawStr = 100;
            _player.FindItemOnLayer(Layer.MiddleTorso)?.Delete();
            Require(_player.EquipItem(new PotionPlatedBelt()), "Could not equip comparison tabard");
            _player.Backpack.DropItem(new RestorationPotion(RestorationKind.Healing, 1) { Amount = 30 });
            _player.Backpack.DropItem(new RestorationPotion(RestorationKind.Mana, 1) { Amount = 30 });
            _player.Hits = 1;
            _player.Mana = 0;
        });
        Register("state", () => { });
        Register("hurt", () => { _player.Hits = 1; _player.Mana = 0; PotionRestoration.Clear(_player); });
        Register("recovery", () =>
        {
            _player.Hits = 1;
            PotionRestoration.Clear(_player);
            ClassCombat.For(_player).RecoveryUntil = Core.TickCount + 3000;
            ClassCombat.For(_player).NextAction = Core.TickCount + 5000;
        });
        Register("chat", () => Require(_speech == "1234", "Enter-opened chat did not send digits as speech"));
        Register("death", () => _player.Kill());
        Register("final", () => Require(_speech == "1234" && PotionBelts.Rows(_player) == 4 && _player.Alive,
            "Client belt/chat final state failed"));
    }
    private static void Require(bool value, string message) { if (!value) { throw new InvalidOperationException(message); } }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand("belt-ui-" + name,
        "Actual client belt test", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Manifest, JsonSerializer.Serialize(new
                {
                    Rows = PotionBelts.Rows(_player), Cells = Enumerable.Range(0, 16).Select(i => PotionBelts.Cell(_player, i)?.Serial.Value ?? 0).ToArray(),
                    _player.Hits, _player.Mana, _player.X, _player.Y,
                    NextAction = ClassCombat.For(_player).NextAction, Recovery = ClassCombat.For(_player).RecoveryUntil,
                    Speech = _speech
                }));
                _logger.Information("belt-ui-{Name}: passed", name);
            }
            catch (Exception error) { _logger.Error(error, "belt-ui-{Name}: failed", name); }
        }));
}
