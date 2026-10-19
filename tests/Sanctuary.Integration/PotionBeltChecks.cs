using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class PotionBeltChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(PotionBeltChecks));
    private static PlayerMobile _player;
    private static int _life, _mana;
    private static long _action, _recovery;
    private static ClassCombat.Session _session;
    private static string Manifest => Path.Join(Core.BaseDirectory, "belt-checks.json");
    private record Saved(uint Player, uint[] Cells, uint Tabard);
    public static void Configure()
    {
        Register("belt-check", Setup);
        Register("belt-reload-check", Reload);
    }
    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable potion belt inventory/restoration check", _ => Core.LoopContext.Post(() => Run(command, action)));
    private static void Require(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
    private static void Run(string command, Action action)
    {
        try { action(); }
        catch (Exception error) { _logger.Error(error, "{Command}: failed", command); }
    }
    private static int Count(PlayerMobile player)
    {
        int count = PotionBelts.Storage(player)?.Items?.Sum(p => p.Amount) ?? 0;
        foreach (var potion in player.Backpack.FindItemsByType<RestorationPotion>(true)) { count += potion.Amount; }
        return count;
    }
    private static void Setup()
    {
        _player = new PlayerMobile { Player = true, Body = 0x190, Name = "BeltCheck" };
        ClassCreation.Profiles.Assign(_player, DiabloClass.Necromancer);
        _player.RawStr = 100;
        _player.AddItem(new Backpack());
        _player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5465, 1165), Map.Trammel);
        var foreign = new RestorationPotion();
        Require(!PotionBelts.Load(_player, 0, foreign), "World potion loaded without ownership");
        foreign.Delete();
        var life = ItemOrigins.Create(ItemOrigin.Crafted, () => new RestorationPotion(RestorationKind.Healing, 1) { Amount = 30 });
        var mana = ItemOrigins.Create(ItemOrigin.Vendor, () => new RestorationPotion(RestorationKind.Mana, 2) { Amount = 30 });
        _player.Backpack.DropItem(life);
        _player.Backpack.DropItem(mana);
        PotionBelts.Fill(_player);
        Require(PotionBelts.Rows(_player) == 1 && PotionBelts.Storage(_player).Items.Count == 4 && Count(_player) == 60,
            "Unbelted capacity or split conservation failed");
        Require(PotionBelts.Cell(_player, 0).Origin == ItemOrigin.Crafted && PotionBelts.Cell(_player, 2).Origin == ItemOrigin.Vendor,
            "Loading lost item origins");
        foreach (var (tabard, rows) in new (PotionTabard, int)[] { (new PotionSash(), 2), (new PotionLightBelt(), 2),
            (new PotionBelt(), 3), (new PotionHeavyBelt(), 3), (new PotionPlatedBelt(), 4) })
        {
            Require(_player.EquipItem(tabard) && PotionBelts.Rows(_player) == rows && tabard.ItemID == 0x1FFD
                && tabard.Layer == Layer.MiddleTorso, "Tabard equipment/appearance/capacity failed");
            _player.Backpack.DropItem(tabard);
            tabard.Delete();
        }
        var large = new PotionPlatedBelt();
        Require(_player.EquipItem(large), "Could not equip four-row test tabard");
        PotionBelts.Fill(_player);
        Require(PotionBelts.Storage(_player).Items.Count == 16 && Count(_player) == 60, "Four rows did not contain 16 real units");
        var old = new HalfApron(); // Existing native Waist belt definition remains saved as-is.
        _player.AddItem(old);
        Require(ClassGear.Active(_player, large) && !ClassGear.Active(_player, old), "Two belt slots contributed stats together");
        old.Delete();
        _player.Backpack.DropItem(large);
        _player.Backpack.MaxItems = 1;
        life.Stackable = mana.Stackable = false; // Force a genuinely full bag, including stack destinations.
        PotionBelts.Reconcile(_player);
        Require(PotionBelts.Rows(_player) == 1 && PotionBelts.Storage(_player).Items.Count == 16 && Count(_player) == 60,
            $"Shrinking a full belt lost or dropped overflow: rows={PotionBelts.Rows(_player)} cells={PotionBelts.Storage(_player).Items.Count} count={Count(_player)}");
        _player.Mana = 0;
        Require(!PotionBelts.Use(_player, 6), "Inactive overflow potion remained usable");
        _player.Backpack.MaxItems = 125;
        life.Stackable = mana.Stackable = true;
        PotionBelts.Reconcile(_player);
        Require(PotionBelts.Storage(_player).Items.Count == 4 && Count(_player) == 60, "Overflow did not return safely to backpack");
        Require(_player.EquipItem(large), "Could not re-equip larger belt");
        PotionBelts.Fill(_player);
        Require(PotionBelts.Remove(_player, 0) && Count(_player) == 60 && PotionBelts.Cell(_player, 12) == null,
            "Explicit return lost stock or failed to compact its column");
        PotionBelts.Target(_player, 12);
        var target = _player.Target;
        Require(target != null, "Empty cell did not create a load target");
        PotionBelts.Target(_player, 13);
        Require(_player.Target == target, "Belt load replaced an existing target");
        target.Invoke(_player, life);
        Require(PotionBelts.Cell(_player, 12) != null && Count(_player) == 60, "Backpack load target failed");
        var reject = Server.Network.LRReason.Inspecific;
        Require(!PotionBelts.Storage(_player).CheckLift(_player, PotionBelts.Cell(_player, 0), ref reject),
            "Native lift bypassed belt inventory validation");
        var potion = PotionBelts.Cell(_player, 0);
        _player.Hits = _player.HitsMax;
        Require(!PotionBelts.Use(_player, 0) && !potion.Deleted, "Full-health drink consumed a potion");
        _player.Hits = 1;
        _player.Mana = 0;
        _life = _player.Hits;
        _mana = _player.Mana;
        var session = _session = ClassCombat.For(_player);
        session.NextAction = _action = Core.TickCount + 5000;
        session.RecoveryUntil = _recovery = Core.TickCount + 500;
        session.PendingAbility = CombatAbility.Teeth;
        using (new CombatRandom(0.19))
        {
            Require(PotionBelts.Use(_player, 0) && potion.Deleted, "Hit recovery blocked belt drinking");
        }
        Require(_player.Backpack.Items.OfType<Bottle>().Sum(p => p.Amount) == 1, "20% successful roll did not return a bottle");
        using (new CombatRandom(0.20))
        {
            Require(PotionBelts.Use(_player, 2) && PotionBelts.Use(_player, 0), "Hit recovery blocked mana drinking or repeated health use");
        }
        Require(_player.Backpack.Items.OfType<Bottle>().Sum(p => p.Amount) == 1, "20% failed roll returned a bottle");
        Require(_player.Hits == _life && _player.Mana == _mana && session.NextAction == _action
            && session.RecoveryUntil == _recovery && session.PendingAbility == CombatAbility.Teeth,
            "Potion was instant or disturbed the pending combat action/recovery deadline");
        session.PendingAbility = CombatAbility.None;
        Require(PotionRestoration.Amount(_player, PotionBeltCatalog.Potion(RestorationKind.Mana, 1)) == 40,
            "Necromancer mana scalar failed");
        Timer.StartTimer(TimeSpan.FromMilliseconds(1100), () => Run("belt-check", Restored));
    }
    private static void Restored()
    {
        Require(_player.Hits >= _life + 3 && _player.Hits <= _life + 7 && _player.Mana >= _mana + 12
            && _session.NextAction == _action, $"Restoration cadence/deadline: hp={_player.Hits - _life}, mana={_player.Mana - _mana}, action={_session.NextAction}/{_action}");
        _player.Mana = _player.ManaMax - 1;
        Timer.StartTimer(TimeSpan.FromMilliseconds(250), () => Run("belt-check", () =>
        {
            Require(_player.Mana == _player.ManaMax, "Active restoration did not reach full mana");
            _player.Mana = 0;
            Timer.StartTimer(TimeSpan.FromMilliseconds(350), () => Run("belt-check", () =>
            {
                Require(_player.Mana <= 1, "Overheal stored restoration after reaching full");
                Cleared();
            }));
        }));
    }
    private static void Cleared()
    {
        PotionRestoration.Clear(_player);
        int after = _player.Hits;
        Timer.StartTimer(TimeSpan.FromMilliseconds(700), () => Run("belt-check", () =>
        {
            Require(_player.Hits <= after + 1, "Cleared/offline restoration continued");
            _player.Hits = 1;
            var direct = new RestorationPotion();
            _player.Backpack.DropItem(direct);
            _player.Backpack.MaxItems = 1;
            foreach (var bottle in _player.Backpack.Items.OfType<Bottle>().ToArray()) { bottle.Delete(); }
            using (new CombatRandom(0)) { _player.Use(direct); }
            Require(direct.Deleted && !_player.Backpack.Items.OfType<Bottle>().Any(),
                "Backpack drinking failed or a recovered bottle escaped a full backpack");
            _player.Backpack.MaxItems = 125;
            PotionRestoration.Clear(_player);
            // Exercise the real corpse hook with a separate class character.
            var dead = new PlayerMobile { Player = true, Body = 0x190, Name = "BeltDeath" };
            ClassCreation.Profiles.Assign(dead, DiabloClass.Necromancer);
            dead.AddItem(new Backpack());
            dead.MoveToWorld(_player.Location, _player.Map);
            var potion = new RestorationPotion { Amount = 5 };
            dead.Backpack.DropItem(potion);
            PotionBelts.Fill(dead);
            dead.Kill();
            Require(!dead.Alive && PotionBelts.Storage(dead).Items.Count == 0 && dead.Corpse is Container corpse
                && corpse.Items.OfType<RestorationPotion>().Sum(p => p.Amount) == 5, "Death did not place all belt/backpack potions on the corpse");
            dead.Corpse.Delete();
            dead.Delete();
            PotionBelts.Fill(_player);
            var saved = new Saved(_player.Serial.Value, Enumerable.Range(0, 16).Select(i => PotionBelts.Cell(_player, i).Serial.Value).ToArray(),
                _player.FindItemOnLayer(Layer.MiddleTorso).Serial.Value);
            File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
            _logger.Information("belt-check: passed");
        }));
    }
    private static void Reload()
    {
        var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var player = World.FindMobile((Serial)saved.Player) as PlayerMobile;
        Require(player != null && PotionBelts.Rows(player) == 4 && player.FindItemOnLayer(Layer.MiddleTorso)?.Serial.Value == saved.Tabard,
            "Persisted tabard lost its capacity or equipment identity");
        for (int i = 0; i < saved.Cells.Length; i++)
        {
            var potion = PotionBelts.Cell(player, i);
            Require(potion != null && potion.Serial.Value == saved.Cells[i] && potion.Amount == 1 && !potion.Stackable
                && potion.Origin == (i % 4 < 2 ? ItemOrigin.Crafted : ItemOrigin.Vendor), "Saved belt cell/type/origin changed");
        }
        _logger.Information("belt-reload-check: passed");
    }
}
