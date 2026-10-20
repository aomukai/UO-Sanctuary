using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class EquipmentEconomyUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(EquipmentEconomyUiChecks));
    private static PlayerMobile _player;
    private static Dagger _salvage, _white, _vendor;
    private static int _gold, _essence;
    public static void Configure()
    {
        Register("setup", () =>
        {
            _player = Accounts.GetAccount("combatui")?[0] as PlayerMobile;
            Require(_player?.NetState != null, "Actual class player missing");
            ClassCombat.Stop(_player);
            _player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            _player.RawStr = 200;
            _player.Backpack.DropItem(new SmithHammer());
            _player.Backpack.DropItem(new ImbuingLadle());
            _player.Skills.Imbuing.Cap = 120; _player.Skills.Imbuing.Base = 120;
            _salvage = Make(ItemOrigin.Drop, GearRarity.Magic);
            _white = Make(ItemOrigin.Crafted, GearRarity.Normal);
            ClassGear.Profiles.Store(_white, ClassGear.State(_white) with
            { Quality = CraftQuality.HighQuality, NativeSockets = 1, IdentifiedSaleOffer = 321 });
            _vendor = Make(ItemOrigin.Vendor, GearRarity.Magic);
            _player.Backpack.DropItem(ItemOrigins.Create(ItemOrigin.Crafted, () => new MagicEssence(20)));
            _player.Backpack.DropItem(new Gold(1000));
            _gold = _player.Backpack.GetAmount(typeof(Gold));
            _essence = _player.Backpack.GetAmount(typeof(MagicEssence));
        });
        Register("salvage-target", () => Target(_salvage));
        Register("white-target", () => Target(_white));
        Register("vendor-target", () => Target(_vendor));
        Register("cancelled", () => Require(!_salvage.Deleted && _player.Backpack.GetAmount(typeof(Gold)) == _gold
            && _player.Backpack.GetAmount(typeof(MagicEssence)) == _essence, "Cancel consumed inputs"));
        Register("salvaged", () =>
        {
            Require(_salvage.Deleted && _player.Backpack.GetAmount(typeof(Gold)) == _gold,
                "Confirmed salvage failed or charged the wrong fee");
            _essence = _player.Backpack.GetAmount(typeof(MagicEssence));
        });
        Register("final", () => Require(!_vendor.Deleted && ClassGear.State(_white).Roll.Rarity is GearRarity.Magic or GearRarity.Rare
            && _white.Origin == ItemOrigin.Crafted && _player.Backpack.GetAmount(typeof(Gold)) == _gold - EquipmentEconomy.Settings.ImbueGoldCost
            && ClassGear.State(_white) is { Quality: CraftQuality.HighQuality, NativeSockets: 1, TinkerSockets: 0, IdentifiedSaleOffer: 321 }
            && _player.Backpack.GetAmount(typeof(MagicEssence)) == _essence - EquipmentEconomy.Settings.ImbueEssenceCost,
            "Vendor restriction, actual confirmation or imbuing costs failed"));
    }
    private static Dagger Make(ItemOrigin origin, GearRarity rarity)
    {
        var item = ItemOrigins.Create(origin, () => new Dagger());
        ClassGear.Profiles.Convert(item, 30, rarity, new Random(42));
        _player.Backpack.DropItem(item);
        return item;
    }
    private static void Target(Item item)
    {
        Require(_player.Target != null, "Actual gump button did not request an item target");
        _player.Target.Invoke(_player, item);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand("economy-ui-" + name,
        "Actual native gump salvage and imbuing check", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                {
                    SalvageDeleted = _salvage.Deleted, VendorDeleted = _vendor.Deleted,
                    WhiteRarity = ClassGear.State(_white).Roll.Rarity.ToString(),
                    Gold = _player.Backpack.GetAmount(typeof(Gold)), Essence = _player.Backpack.GetAmount(typeof(MagicEssence))
                }));
                _logger.Information("economy-ui-{Name}: passed", name);
            }
            catch (Exception error) { _logger.Error(error, "economy-ui-{Name}: failed", name); }
        }));
}
