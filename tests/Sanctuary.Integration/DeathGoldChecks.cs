using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class DeathGoldChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(DeathGoldChecks));
    private static string Manifest => Path.Join(Core.BaseDirectory, "death-gold-checks.json");
    private sealed record Saved(uint Player, uint Corpse, CharacterProfile Profile, int Gold, bool Recovered, bool Claimed = true);

    public static void Configure()
    {
        Register("death-gold-check", Check);
        Register("death-gold-reload-check", Reload);
    }

    private static void Register(string command, Action action) => ConsoleInputHandler.RegisterCommand(command,
        "Disposable flat death-gold and replay checks", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action();
                _logger.Information("{Command}: passed", command);
            }
            catch (Exception error)
            {
                _logger.Error(error, "{Command}: failed", command);
            }
        }));

    private static void Require(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }

    private static CharacterProfile Profile(PlayerMobile player)
    {
        Require(ClassCreation.Profiles.TryGet(player.Serial, out var profile), "Missing class profile");
        return profile;
    }

    private static PlayerMobile Player(int level = 1)
    {
        var player = new PlayerMobile { Player = true, Body = 0x190, Name = "DeathGoldCheck" };
        ClassCreation.Profiles.Assign(player, DiabloClass.Necromancer);
        if (level > 1) { ClassCreation.Profiles.AdvanceLevel(player, level); }
        player.AddItem(new Backpack());
        var bankBag = new Bag();
        bankBag.DropItem(new Gold(10000));
        player.BankBox.DropItem(bankBag);
        player.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 1367, 1504), Map.Trammel);
        return player;
    }

    private static int Gold(Container container) => container?.GetAmount(typeof(Gold)) ?? 0;

    private static void Replay(PlayerMobile player, Corpse corpse)
    {
        ClassCombatHooks.PlayerCorpseCreated(player, corpse);
        ClassCombatHooks.PlayerDied(player, corpse);
        ClassCombatHooks.PlayerDied(player, corpse);
        player.Kill();
    }

    private static void Check()
    {
        CorpseRecoveryChecks.Check();
        foreach (int level in new[] { 1, 10, 20, 99 })
        {
            foreach (int total in new[] { 0, 1, 4, 5, 6, 9, 10, 99, 1001, 120001 })
            {
                var player = Player(level);
                Corpse corpse = null;
                try
                {
                    int remaining = total;
                    // Separate one-coin stacks prove rounding happens once across nested containers.
                    for (int i = 0; remaining > 0; i++)
                    {
                        var bag = new Bag();
                        var inner = new Bag();
                        int amount = total <= 10 ? 1 : Math.Min(remaining, 60000);
                        var gold = new Gold(amount) { Weight = 0, LootType = i % 2 == 0 ? LootType.Blessed : LootType.Regular };
                        inner.DropItem(gold);
                        bag.DropItem(inner);
                        player.Backpack.DropItem(bag);
                        remaining -= amount;
                    }
                    var profile = Profile(player);
                    player.Kill();
                    corpse = player.Corpse as Corpse;
                    Require(!player.Alive && corpse is { SanctuaryDeathCharged: true }, "Death was not claimed");
                    int expected = total - total / 5;
                    Require(Gold(player.Backpack) + Gold(corpse) == expected && Gold(player.BankBox) == 10000,
                        $"Wrong total/bank at level {level}, carried {total}");
                    Replay(player, corpse);
                    Require(Gold(player.Backpack) + Gold(corpse) == expected && Profile(player) == profile,
                        "Replay charged twice or changed progression");
                    player.Resurrect();
                    ClassCombatHooks.PlayerDied(player, corpse);
                    Require(ClassDeath.Recover(player, corpse) && Gold(player.Backpack) == expected,
                        $"Recovery lost remaining gold at level {level}, carried {total}: backpack={Gold(player.Backpack)}, corpse={Gold(corpse)}");
                    Replay(player, corpse);
                    // A fresh Kill is a distinct death; its charge must apply even before recovering another corpse.
                    var second = player.Corpse as Corpse;
                    Require(second != corpse && second.SanctuaryDeathCharged
                        && Gold(player.Backpack) + Gold(second) == expected - expected / 5,
                        "New life did not receive its own flat penalty");
                    int previous = Gold(corpse);
                    ClassCombatHooks.PlayerDied(player, corpse);
                    ClassCombatHooks.PlayerCorpseCreated(player, corpse);
                    Require(Gold(corpse) == previous && Gold(player.BankBox) == 10000 && Profile(player) == profile,
                        "Stale corpse callback touched another life or bank/progression");
                    second.Delete();
                }
                finally
                {
                    player.Corpse?.Delete();
                    corpse?.Delete();
                    player.Delete();
                    ClassCreation.Profiles.PruneDeleted();
                }
            }
        }
        var legacy = new PlayerMobile { Player = true, Body = 0x190, Name = "LegacyDeathGold" };
        legacy.AddItem(new Backpack());
        legacy.Backpack.DropItem(new Gold(1000));
        legacy.Kill();
        Require(Gold(legacy.Backpack) + Gold(legacy.Corpse) == 1000
            && legacy.Corpse is Corpse { SanctuaryDeathCharged: false }, "Non-class player received class death penalty");
        legacy.Corpse?.Delete();
        legacy.Delete();

        var twice = Player();
        twice.Backpack.DropItem(new Gold(100));
        twice.Kill();
        var firstCorpse = (Corpse)twice.Corpse;
        twice.Resurrect();
        twice.Backpack.DropItem(new Gold(10));
        twice.Kill();
        var nextCorpse = (Corpse)twice.Corpse;
        Require(firstCorpse != nextCorpse && Gold(firstCorpse) == 80 && Gold(nextCorpse) == 8,
            "Second death charged an unrecovered earlier corpse");
        firstCorpse.Delete();
        nextCorpse.Delete();
        twice.Delete();
        ClassCreation.Profiles.PruneDeleted();

        var saved = new List<Saved>();
        foreach (bool recovered in new[] { false, true })
        {
            var player = Player(10);
            player.Backpack.DropItem(new Gold(1001));
            var profile = Profile(player);
            player.Kill();
            var corpse = (Corpse)player.Corpse;
            if (recovered)
            {
                player.Resurrect();
                Require(ClassDeath.Recover(player, corpse), "Fixture recovery failed");
            }
            saved.Add(new Saved(player.Serial.Value, corpse.Serial.Value, profile, 801, recovered));
        }
        var empty = Player();
        empty.Kill();
        // Gold arriving after a zero-gold death is never part of that death's bill.
        empty.Backpack.DropItem(new Gold(100));
        Replay(empty, (Corpse)empty.Corpse);
        Require(Gold(empty.Backpack) == 100, "Zero-gold replay charged subsequently acquired gold");
        saved.Add(new Saved(empty.Serial.Value, empty.Corpse.Serial.Value, Profile(empty), 100, false));
        var older = Player();
        older.Backpack.DropItem(new Gold(100));
        older.Kill();
        var olderCorpse = (Corpse)older.Corpse;
        olderCorpse.SanctuaryDeathCharged = false;
        saved.Add(new Saved(older.Serial.Value, olderCorpse.Serial.Value, Profile(older), 80, false, false));
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved));
    }

    private static void Reload()
    {
        foreach (var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var player = World.FindMobile((Serial)saved.Player) as PlayerMobile;
            var corpse = World.FindItem((Serial)saved.Corpse) as Corpse;
            Require(player != null && corpse is { Deleted: false } && corpse.SanctuaryDeathCharged == saved.Claimed && corpse.Owner == player
                && player.Alive == saved.Recovered && Profile(player) == saved.Profile,
                "Reload lost death claim, ghost/recovered state or progression");
            int total = Gold(player.Backpack) + Gold(corpse);
            EventSink.InvokeDisconnected(player);
            EventSink.InvokeDisconnected(player);
            EventSink.InvokeLogout(player);
            EventSink.InvokeConnected(player);
            player.ClaimAutoStabledPets();
            if (saved.Claimed) { ClassCombatHooks.PlayerCorpseCreated(player, corpse); }
            ClassCombatHooks.PlayerDied(player, corpse);
            ClassCombatHooks.PlayerDied(player, corpse);
            Require(total == saved.Gold && Gold(player.Backpack) + Gold(corpse) == total
                && Gold(player.BankBox) == 10000 && Profile(player) == saved.Profile
                && corpse.SanctuaryDeathCharged == saved.Claimed,
                "Reload/replayed callbacks charged or changed bank/progression");
        }
    }
}
