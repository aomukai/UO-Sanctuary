using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class RepairUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(RepairUiChecks));
    private static PlayerMobile _player;
    private static RepairHammer _tool;
    private static Longsword _sword;
    private static HealWand _wand;
    private static int _capacity, _gold, _maximum;
    private static BaseVendor _vendor;
    public static void Configure()
    {
        Register("setup", Setup);
        Register("target", () =>
        {
            Require(_player.Target?.GetType().DeclaringType == typeof(RepairGump), "Repair tool button did not create repair target");
            _player.Target.Invoke(_player, _sword);
        });
        Register("cancelled", () =>
        {
            Require(_sword.HitPoints == 0 && _tool.UsesRemaining == 25
                && _player.Backpack.GetAmount(typeof(CopperIngot)) == 20, "Cancel consumed resources or repaired");
            RepairGump.DisplayTo(_player, _sword, _tool);
        });
        Register("repaired", () => Require(_sword.HitPoints == _sword.MaxHitPoints && _sword.MaxHitPoints is >= 18 and <= 20
            && _tool.UsesRemaining == 24, $"Actual repair confirmation failed: hits={_sword.HitPoints}/{_sword.MaxHitPoints}, uses={_tool.UsesRemaining}, position={_player.Location}/{_player.Map}, ready={EquipmentRepairs.Ready(_player)}, skill={_player.Skills[SkillName.Blacksmith].Base}, craft={ClassProfessions.Profiles.Find(_player)}, copper={_player.Backpack.GetAmount(typeof(CopperIngot))}, scraps={_player.Backpack.GetAmount(typeof(MetalScraps))}"));
        Register("npc", () =>
        {
            _vendor = TownMarkets.Profiles.At("Felucca/Britain/Bowyer");
            Approach(_vendor);
            _wand = new HealWand(); _capacity = EquipmentRepairs.Capacity(_wand); _wand.Charges = 0;
            _wand.MaxHitPoints = 20; _wand.HitPoints = 5; _maximum = _wand.MaxHitPoints;
            _player.Backpack.DropItem(_wand);
            _player.Backpack.DropItem(new MagicEssence(500)); _player.Backpack.DropItem(new Board(20));
            _gold = _player.Backpack.GetAmount(typeof(Gold));
            var entries = PooledRefList<ContextMenuEntry>.Create();
            ContextMenuEntry service = null;
            try
            {
                _vendor.AddCustomContextEntries(_player, ref entries);
                foreach (var entry in entries) { if (entry.Number == 1044260) { service = entry; break; } }
            }
            finally { entries.Dispose(); }
            Require(service != null, "Town context menu has no repair service");
            service.OnClick(_player, _vendor);
            Require(_player.Target != null, "NPC service did not start target");
            _player.Target.Invoke(_player, _wand);
        });
        Register("quantity", () =>
        {
            Require(_wand.Charges == 2 && _wand.MaxHitPoints == _maximum && _wand.HitPoints == 5
                && _player.Backpack.GetAmount(typeof(Gold)) == _gold - 100, "NPC partial recharge damaged/repaired/billed incorrectly");
            RepairGump.DisplayTo(_player, _wand, vendor: _vendor);
        });
        Register("final", () => Require(_wand.Charges == _capacity && _wand.HitPoints == _wand.MaxHitPoints
            && _wand.MaxHitPoints is >= 18 and <= 20 && _player.Backpack.GetAmount(typeof(Gold)) == _gold - 200,
            "NPC combined repair/fill failed or charged twice"));
    }
    private static void Approach(BaseVendor vendor)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                var point = new Point3D(vendor.X + x, vendor.Y + y, vendor.Z);
                if (vendor.Map.CanFit(point, 16, false, false, true) && vendor.Map.LineOfSight(point, vendor.Location))
                { _player.MoveToWorld(point, vendor.Map); return; }
            }
        }
        throw new InvalidOperationException("No UI service approach");
    }
    private static void Setup()
    {
        _player = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_player);
        ClassCreation.Profiles.AdvanceLevel(_player, 99); _player.RawStr = 200; _player.RawDex = 200;
        ClassProfessions.Profiles.Confirm(_player, SkillName.Blacksmith);
        _player.Skills[SkillName.Blacksmith].Base = 80;
        _player.MoveToWorld(new Point3D(5450, 1161, 0), Map.Trammel);
        new Static(0xFAF).MoveToWorld(new Point3D(5451, 1160, 0), Map.Trammel);
        new Static(0xFB1).MoveToWorld(new Point3D(5451, 1161, 0), Map.Trammel);
        var removed = _player.Backpack.Items.ToArray(); foreach (var item in removed) { item.Delete(); }
        _tool = new RepairHammer(); _player.Backpack.DropItem(_tool);
        _sword = ItemOrigins.Create(ItemOrigin.Vendor, () => new Longsword { Resource = CraftResource.Copper, MaxHitPoints = 20, HitPoints = 0 });
        ClassGear.Profiles.Ensure(_sword); _player.Backpack.DropItem(_sword);
        _player.Backpack.DropItem(new CopperIngot(20)); _player.Backpack.DropItem(new MetalScraps(20));
        _player.Backpack.DropItem(new Gold(1000));
        _tool.OnDoubleClick(_player);
    }
    private static void Register(string command, Action action)
    {
        string name = "repair-ui-" + command;
        ConsoleInputHandler.RegisterCommand(name, "Disposable actual repair UI assertions", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { player = _player.Serial.Value, item = _sword.Serial.Value, wand = _wand?.Serial.Value, capacity = _capacity }));
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    }
    private static void Require(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
}
