using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using NativeInsurance = Server.Engines.Insurance.Insurance;

namespace Sanctuary.Integration;

public static class InsuranceUiChecks
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(InsuranceUiChecks));
    private static PlayerMobile _player;
    private static Item[] _gear;
    private static int _bank;
    private static string Manifest => Path.Join(Core.BaseDirectory, "insurance-ui-checks.json");
    private sealed record Saved(uint Player, uint[] Gear, int Balance);
    private static void Require(bool value, string why) => RuneChecks.Require(value, why);
    public static void Configure()
    {
        Command("setup", () =>
        {
            _player = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_player);
            _player.Target = null; _player.Frozen = _player.Paralyzed = false; _player.Blessed = true;
            _player.CloseGump<TalentGump>(); _player.CloseGump<WardrobeGump>();
            foreach (var item in EquipmentInsurance.Carried(_player).ToArray()) { item.Delete(); }
            _gear = [InsuranceChecks.Gear(_player), InsuranceChecks.Gear(_player, GearRarity.Magic, GearTier.Exceptional),
                InsuranceChecks.Gear(_player, GearRarity.Rare, GearTier.Elite)];
            Require(Banker.Deposit(_player, 5000), "Could not fund UI insurance"); _bank = Banker.GetBalance(_player);
            _player.AutoRenewInsurance = false;
            NativeInsurance.OpenItemInsuranceMenu(_player);
            Require(_player.FindGump<InsuranceGump>() != null && !_gear.Any(i => i.Insured), "Native insurance entry did not open project menu");
        });
        Command("quoted", () =>
        {
            var menu = _player.FindGump<InsuranceGump>();
            Require(menu != null && (bool)typeof(InsuranceGump).GetField("_confirm", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu)
                && Banker.GetBalance(_player) == _bank && !_gear.Any(i => i.Insured), "Preview charged gold or failed to show confirmation");
        });
        Command("renew-on", () => Require(_player.AutoRenewInsurance && Banker.GetBalance(_player) == _bank, "Renewal control failed or charged upfront"));
        Command("bought", () => Require(_gear.All(i => i.Insured && i.PaidInsurance) && Banker.GetBalance(_player) == _bank - 2100,
            "Actual UI did not buy correct policies for exactly2100gold"));
        Command("death", () =>
        {
            _player.Blessed = false; _player.Kill();
            Require(!_player.Alive && _gear.All(i => i.Parent == _player.Backpack && i.Insured && i.PaidInsurance)
                && Banker.GetBalance(_player) == _bank - 4200, "Actual UI policies did not protect and renew through real death");
            _player.Resurrect(); NativeInsurance.OpenItemInsuranceMenu(_player);
        });
        Command("final", () =>
        {
            Require(_player.Alive && _player.AutoRenewInsurance && _gear.All(i => i.Parent == _player.Backpack && i.Insured)
                && Banker.GetBalance(_player) == _bank - 4200, "UI final state differs");
            File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(_player.Serial.Value, _gear.Select(i => i.Serial.Value).ToArray(), Banker.GetBalance(_player))));
        });
        Command("reload", () =>
        {
            var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
            var p = World.FindMobile((Serial)saved.Player) as PlayerMobile;
            Require(p is { Alive: true, AutoRenewInsurance: true } && Banker.GetBalance(p) == saved.Balance
                && saved.Gear.All(s => World.FindItem((Serial)s) is { Insured: true, PaidInsurance: true } item
                    && item.Parent == p.Backpack && EquipmentInsurance.Profiles.Find(item.Serial) is { Active: true, Paid: true } policy
                    && policy.Owner == p.Serial), "UI policies, actual equipment, bank or renewal setting changed after reload");
        });
    }
    private static void Command(string suffix, Action action)
    {
        string name = "insurance-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Disposable actual-client insurance checks", _ => Core.LoopContext.Post(() =>
        { try { action(); _log.Information("{Command}: passed", name); } catch (Exception e) { _log.Error(e, "{Command}: failed", name); } }));
    }
}
