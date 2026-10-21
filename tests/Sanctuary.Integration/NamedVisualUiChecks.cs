// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Gumps;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class NamedVisualUiChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(NamedVisualUiChecks));
    private static PlayerMobile _owner;
    private static Item _piece;
    public static void Configure()
    {
        Command("setup", () =>
        {
            var viewer = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(viewer);
            viewer.CloseGump<GamblingGump>(); viewer.Target = null;
            _owner = new PlayerMobile { Body = 0x190, Player = true, Name = "NatalyaVisual", Hue = 1005 };
            new Account("named-visual-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"))[0] = _owner;
            _owner.AddItem(new Backpack()); ClassCreation.Profiles.Assign(_owner, DiabloClass.Assassin);
            ClassCreation.Profiles.AdvanceLevel(_owner, 99); _owner.RawStr = _owner.RawDex = 1000;
            _owner.MoveToWorld(CombatPractice.FindFloor(viewer.Map, viewer.X + 5, viewer.Y - 1), viewer.Map);
            for (int i = 62; i <= 65; i++)
            {
                var item = NamedGear.Create("piece/" + i, 99, new Random(3)); _owner.Backpack.DropItem(item);
                RuneChecks.Require(_owner.EquipItem(item), "Visual scene cannot equip Natalya piece"); _piece = item;
            }
            ClassGear.Refresh(_owner);
            RuneChecks.Require(NamedSetVisuals.Faded(_owner) && !_owner.Hidden && viewer.CanSee(_owner), "Fade scene is not visible");
            Write();
        });
        Command("golden", () =>
        {
            var viewer = (PlayerMobile)Accounts.GetAccount("combatui")[0]; var point = _owner.Location; _owner.Delete();
            _owner = new PlayerMobile { Body = 0x190, Player = true, Name = "AldurVisual", Hue = 1005 };
            new Account("named-gold-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"))[0] = _owner;
            _owner.AddItem(new Backpack()); ClassCreation.Profiles.Assign(_owner, DiabloClass.Druid);
            ClassCreation.Profiles.AdvanceLevel(_owner, 99); _owner.RawStr = _owner.RawDex = 1000;
            _owner.MoveToWorld(point, viewer.Map);
            for (int i = 66; i <= 69; i++)
            {
                var item = NamedGear.Create("piece/" + i, 99, new Random(3)); _owner.Backpack.DropItem(item);
                RuneChecks.Require(_owner.EquipItem(item), "Cannot equip golden set in actual client");
            }
            ClassGear.Refresh(_owner); RuneChecks.Require(NamedSetVisuals.Active(_owner), "Golden set aura is inactive"); Write();
        });
        Command("blood", () =>
        {
            if (_owner.FindItemOnLayer(Layer.OneHanded) is BaseWeapon one) { _owner.Backpack.DropItem(one); }
            if (_owner.FindItemOnLayer(Layer.TwoHanded) is BaseWeapon two) { _owner.Backpack.DropItem(two); }
            var dagger = new Dagger(); _owner.Backpack.DropItem(dagger); RuneChecks.Require(_owner.EquipItem(dagger), "Cannot equip blood-scene dagger");
            var shield = NamedGear.Create("unique/98", 99, new Random(3)); _owner.Backpack.DropItem(shield);
            RuneChecks.Require(_owner.EquipItem(shield), "Cannot equip blood shield in actual client"); ClassGear.Refresh(_owner);
            var target = new Zombie { Frozen = true }; target.SetHits(10000);
            target.MoveToWorld(CombatPractice.FindFloor(_owner.Map, _owner.X + 1, _owner.Y), _owner.Map);
            GearEffects.Strike(_owner, target, 0); Write();
        });
        Command("opaque", () => { _owner.Backpack.DropItem(_piece); ClassGear.Refresh(_owner); Write(); });
        Command("faded", () =>
        {
            RuneChecks.Require(_owner.EquipItem(_piece), "Cannot restore fade in actual client scene"); ClassGear.Refresh(_owner); Write();
        });
    }
    private static void Write() => File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_NAMED_VISUAL_SERVER_STATE"),
        JsonSerializer.Serialize(new { Serial = _owner.Serial.Value, Opacity = NamedSetVisuals.Opacity(_owner), Hidden = _owner.Hidden }));
    private static void Command(string suffix, Action action)
    {
        string name = "named-visual-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Disposable named visual client proof", _ => Core.LoopContext.Post(() =>
        {
            try { action(); _log.Information("{Name}: passed", name); }
            catch (Exception e) { _log.Error(e, "{Name}: failed", name); }
        }));
    }
}
