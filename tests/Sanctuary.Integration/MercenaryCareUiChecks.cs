using System;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Gumps;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class MercenaryCareUiChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(MercenaryCareUiChecks));
    private static PlayerMobile _owner;
    private static BaseCreature _merc, _healer;
    private static Item _gear;
    private static void Require(bool value, string message) => CraftingChecks.Require(value, message);
    public static void Configure()
    {
        Command("setup", () =>
        {
            _owner = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_owner);
            _owner.Target = null; _owner.Blessed = true; _owner.Frozen = _owner.Paralyzed = false;
            _owner.CloseGump<TalentGump>(); _owner.CloseGump<WardrobeGump>();
            _owner.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
            _merc = MercenaryCareChecks.Companion(_owner); Require(MercenaryCare.Register(_owner, _merc, 12), "UI registration failed");
            _gear = new Longsword(); _merc.Backpack.DropItem(_gear); _merc.EquipItem(_gear);
            var pots = new RestorationPotion(RestorationKind.Healing, 1) { Amount = 37 }; _owner.Backpack.DropItem(pots);
            Require(MercenaryCare.Deposit(_owner, _merc, pots), "UI potion stock failed");
            MercenaryCare.States[_merc.Serial] = MercenaryCare.States[_merc.Serial] with { NextDrinkUtc = Core.Now.AddHours(1) }; _merc.Kill();
            _healer = new WanderingHealer { CantWalk = true }; _healer.MoveToWorld(CombatPractice.FindFloor(_owner.Map, _owner.X, _owner.Y + 1), _owner.Map); _healer.AIObject.AITimer.Stop();
            int bank = Banker.GetBalance(_owner); if (bank > 0) { Banker.Withdraw(_owner, bank); }
            MercenaryCareGump.DisplayTo(_owner, _merc, _healer); Require(_owner.FindGump<MercenaryCareGump>() != null, "Care gump missing");
        });
        Command("insufficient", () =>
        {
            Require(_merc.IsDeadPet && Banker.GetBalance(_owner) == 0 && _owner.FindGump<MercenaryCareGump>() == null, "Insufficient funds changed mercenary or bank");
            MercenaryCareGump.DisplayTo(_owner, _merc, _healer);
        });
        Command("cancelled", () =>
        {
            Require(_merc.IsDeadPet && Banker.GetBalance(_owner) == 0 && _owner.FindGump<MercenaryCareGump>() == null, "Cancellation changed mercenary or bank");
            _owner.BankBox.DropItem(new Gold(13000)); MercenaryCareGump.DisplayTo(_owner, _merc, _healer);
        });
        Command("paid", () => Require(!_merc.IsDeadPet && Banker.GetBalance(_owner) == 1000 && _gear.IsChildOf(_merc.Backpack)
            && MercenaryCare.Stock(_merc) == 37 && _owner.FindGump<MercenaryCareGump>() == null, "Client payment lost gear/stock or charged wrong fee"));
        Command("auto-start", () =>
        {
            MercenaryCare.States[_merc.Serial] = MercenaryCare.States[_merc.Serial] with { NextDrinkUtc = Core.Now };
            _merc.Hits = _merc.HitsMax / 2;
        });
        Command("auto-first", () =>
        {
            Require(MercenaryCare.Stock(_merc) == 36 && _merc.Hits > _merc.HitsMax / 2, "Connected-owner timer did not consume and tick a healing dose");
            _merc.Hits = 400;
        });
        Command("auto-cooldown", () => Require(MercenaryCare.Stock(_merc) == 36, "Automatic timer drank before ten seconds"));
        Command("final", () => Require(MercenaryCare.Stock(_merc) == 35 && _merc.Hits > 400 && Banker.GetBalance(_owner) == 1000
            && _gear.IsChildOf(_merc.Backpack), "Automatic timer did not resume after cooldown or lost gear/payment"));
    }
    private static void Command(string suffix, Action action)
    {
        string name = "mercenary-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Disposable mercenary care client check", _ => Core.LoopContext.Post(() =>
        { try { action(); _log.Information("{Name}: passed", name); } catch (Exception e) { _log.Error(e, "{Name}: failed", name); } }));
    }
}
