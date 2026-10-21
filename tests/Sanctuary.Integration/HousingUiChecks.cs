using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Multis;

namespace Sanctuary.Integration;

public static class HousingUiChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(HousingUiChecks));
    private static PlayerMobile _owner;
    private static HouseMeasurement _measure;
    private static TownHouse _house;
    private static Item _belonging;
    private static int _funds;
    private static Point3D _exit;
    private static string Manifest => Path.Join(Core.BaseDirectory, "housing-ui-checks.json");
    private sealed record Saved(uint House, uint Player, uint Wardrobe, int Balance);
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    public static void Configure()
    {
        Command("setup", () =>
        {
            _owner = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_owner);
            _owner.Target = null; _owner.Frozen = _owner.Paralyzed = false; _owner.Blessed = true;
            _owner.AccessLevel = AccessLevel.GameMaster;
            _owner.CloseGump<TalentGump>(); _owner.CloseGump<WardrobeGump>();
            _exit = CombatPractice.FindFloor(Map.Felucca, 5700, 1410);
            Move(5700, 1400, _exit.Z);
            HouseMeasureGump.Start(_owner);
            _measure = (HouseMeasurement)typeof(HouseMeasureGump).GetField("_measure", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_owner.FindGump<HouseMeasureGump>());
        });
        for (int floor = 0; floor < 3; floor += 2)
        {
            int f = floor;
            for (int corner = 0; corner < 4; corner++)
            {
                int c = corner;
                Command($"floor-{f}-corner-{c}", () =>
                {
                    int side = f == 0 ? 4 : 2;
                    Move(5700 + (c is 1 or 2 ? side : 0), 1400 + (c >= 2 ? side : 0), _exit.Z + f * 20);
                });
            }
        }
        for (int corner = 0; corner < 4; corner++)
        {
            int c = corner;
            Command($"garden-corner-{c}", () => Move(5699 + (c is 1 or 2 ? 6 : 0), 1399 + (c >= 2 ? 8 : 0), _exit.Z));
        }
        Command("exit", () => _owner.MoveToWorld(_exit, Map.Felucca));
        Command("measured", () =>
        {
            var definition = _measure.Build("Town home");
            Require(definition.LivingTiles == 59 && definition.GardenTiles == 38 && definition.Floors[1].Inherited
                && _owner.FindGump<HouseAuthorSaleGump>() != null, "Actual corner/floor/garden controls did not reach sale setup");
        });
        Command("empty", () =>
        {
            var gump = _owner.FindGump<HouseAuthorSaleGump>();
            Require(gump != null && !(bool)typeof(HouseAuthorSaleGump).GetField("_furnished", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(gump), "Furnished/empty checkbox failed");
        });
        Command("author-open", () => Require(_owner.FindGump<HouseAuthorSaleGump>() != null, "Cancelling a target lost the GM sale draft"));
        Command("listed", () =>
        {
            _house = BaseHouse.FindHouseAt(new Point3D(5701, 1401, _exit.Z), Map.Felucca, 16) as TownHouse;
            Require(_house != null && _house.Listed && _house.Owner == null && _house.ListPrice == 68500 && !_house.Furnished, "Actual GM sale setup failed to publish correct property");
            _owner.AccessLevel = AccessLevel.Player; Housing.Grant(_owner);
            Require(Banker.Deposit(_owner, 150000), "UI bank funding failed"); _funds = Banker.GetBalance(_owner);
            _owner.MoveToWorld(new Point3D(_house.Sign.X + 1, _house.Sign.Y, _house.Sign.Z), _house.Map);
            TownHouseGump.DisplayTo(_owner, _house);
        });
        Command("bought", () =>
        {
            Require(_house.Owner == _owner && Banker.GetBalance(_owner) == _funds - 68500, "Actual player purchase did not commit once");
            TownHouseGump.DisplayTo(_owner, _house);
        });
        Command("manage", () => Require(_owner.FindGump<HouseGumpAOS>() != null, "Native house management did not open"));
        Command("blocked-setup", () =>
        {
            _owner.CloseGump<HouseGumpAOS>();
            _belonging = new Gold(5); _belonging.MoveToWorld(new Point3D(5699, 1400, _exit.Z), _house.Map);
            TownHouseGump.DisplayTo(_owner, _house);
        });
        Command("blocked", () =>
        {
            Require(_house.Owner == _owner && !_belonging.Deleted && Banker.GetBalance(_owner) == _funds - 68500, "UI buyback lost belongings or refunded a blocked sale");
            _belonging.Delete(); TownHouseGump.DisplayTo(_owner, _house);
        });
        Command("sold", () => Require(_house.Owner == null && _house.Listed && Banker.GetBalance(_owner) == _funds - 34250, "Actual UI buyback refund/relisting differs"));
        Command("replacement", () =>
        {
            TownRecognition.Profiles.Store(_owner, new(RecognitionHonor.Hero, RecognitionHonor.Hero, RecognitionHonor.Hero, true));
            string site = RecognitionChecks.Place(_owner); _funds = Banker.GetBalance(_owner);
            TownRecognition.Open(_owner, site);
        });
        Command("final", () =>
        {
            var wardrobe = _owner.Backpack.FindItemByType<EarnedWardrobe>();
            Require(Banker.GetBalance(_owner) == _funds - 1000 && wardrobe != null && wardrobe.Recipient == _owner.Serial.Value,
                "Actual town-hall wardrobe replacement did not charge/deliver correctly");
            File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(_house.Serial.Value, _owner.Serial.Value, wardrobe.Serial.Value, Banker.GetBalance(_owner))));
        });
        Command("reload", () =>
        {
            var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
            var house = World.FindItem((Serial)saved.House) as TownHouse;
            var player = World.FindMobile((Serial)saved.Player) as PlayerMobile;
            Require(house is { Owner: null, Listed: true, Furnished: false, Price: 68500 }
                && house.Definition.LivingTiles == 59 && house.Definition.GardenTiles == 38 && house.Definition.Floors[1].Inherited
                && player != null && Housing.HasRights(player) && !BaseHouse.HasAccountHouse(player)
                && Banker.GetBalance(player) == saved.Balance
                && World.FindItem((Serial)saved.Wardrobe) is EarnedWardrobe wardrobe && wardrobe.Recipient == player.Serial.Value,
                "Actual UI purchase/buyback/replacement state changed on reload");
        });
    }
    private static void Move(int x, int y, int z) => _owner.MoveToWorld(new Point3D(x, y, z), Map.Felucca);
    private static void Command(string suffix, Action action)
    {
        string command = "housing-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(command, "Actual GM authoring and player town-house UI", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new { ok = true }));
                _log.Information("{Command}: passed", command);
            }
            catch (Exception e) { _log.Error(e, "{Command}: failed", command); }
        }));
    }
}
